import json
import csv
import time
import re
import os
import random
import base64
import requests
from dotenv import load_dotenv

load_dotenv()

# ============================================================
# Checksum ISO 6346 -- COPY Y HET tu test.py de dam bao cac script
# dung chung 1 tieu chuan danh gia, khong lech nhau vi logic khac nhau
# ============================================================
def build_letter_values() -> dict:
    values = {}
    n = 10
    for ch in "ABCDEFGHIJKLMNOPQRSTUVWXYZ":
        while n % 11 == 0:
            n += 1
        values[ch] = n
        n += 1
    return values


LETTER_VALUES = build_letter_values()


def iso6346_check_digit(code_10: str) -> int:
    weights = [2 ** i for i in range(10)]
    total = sum((int(ch) if ch.isdigit() else LETTER_VALUES[ch]) * w
                for ch, w in zip(code_10, weights))
    return (total % 11) % 10


def validate_container_code(code: str) -> bool:
    code = code.strip().upper()
    if len(code) != 11 or not code[:4].isalpha() or not code[4:].isdigit():
        return False
    return iso6346_check_digit(code[:10]) == int(code[10])


def find_valid_code(text: str):
    joined = text.upper().strip()
    for c in re.findall(r"[A-Z]{4}\d{7}", joined):
        if validate_container_code(c):
            return c
    return None


PROMPT = (
    "Doc chinh xac ma so container trong anh theo chuan ISO 6346 "
    "(gom 4 chu cai + 7 chu so, vi du: MSKU1234567). "
    "Chi tra ve dung chuoi 11 ky tu do, khong kem chu giai thich nao khac. "
    "Neu khong doc duoc, tra ve dung chu: KHONG_DOC_DUOC"
)


class QuotaNgayHetHan(Exception):
    pass


# ============================================================
# NGUON 1: Gemini (uu tien goi truoc, 20 luot/ngay free)
# ============================================================
GEMINI_API_KEY = os.getenv("GEMINI_API_KEY")
GEMINI_MODEL = "gemini-3.6-flash"
GEMINI_URL = f"https://generativelanguage.googleapis.com/v1beta/models/{GEMINI_MODEL}:generateContent"


def goi_gemini(image_bytes: bytes, so_lan_thu_lai=3):
    b64 = base64.b64encode(image_bytes).decode("utf-8")
    payload = {
        "contents": [{
            "parts": [
                {"text": PROMPT},
                {"inline_data": {"mime_type": "image/jpeg", "data": b64}}
            ]
        }],
        "generationConfig": {
            "temperature": 0,
            "maxOutputTokens": 1024,
            "thinkingConfig": {"thinkingLevel": "low"},
            "responseMimeType": "application/json",
            "responseSchema": {
                "type": "OBJECT",
                "properties": {
                    "ma_container": {
                        "type": "STRING",
                        "description": "Chuoi 11 ky tu ISO 6346 (4 chu + 7 so), hoac KHONG_DOC_DUOC neu khong doc duoc"
                    }
                },
                "required": ["ma_container"]
            }
        }
    }

    for lan in range(so_lan_thu_lai):
        try:
            headers = {"x-goog-api-key": GEMINI_API_KEY, "Content-Type": "application/json"}
            resp = requests.post(GEMINI_URL, json=payload, headers=headers, timeout=30)
        except requests.RequestException:
            time.sleep(2 ** lan)
            continue

        if resp.status_code == 429:
            try:
                loi_chi_tiet = resp.json().get("error", {}).get("message", "").lower()
            except Exception:
                loi_chi_tiet = ""
            if "quota" in loi_chi_tiet or "daily" in loi_chi_tiet or "per day" in loi_chi_tiet:
                raise QuotaNgayHetHan()
            time.sleep(5 * (lan + 1))
            continue

        if resp.status_code != 200:
            return None

        data = resp.json()
        try:
            text = data["candidates"][0]["content"]["parts"][0]["text"]
        except (KeyError, IndexError):
            return None

        # text gio la 1 chuoi JSON dang '{"ma_container": "..."}' nho structured output
        try:
            parsed = json.loads(text)
            ma = parsed.get("ma_container", "")
        except (json.JSONDecodeError, AttributeError):
            return None

        if not ma or "KHONG_DOC_DUOC" in ma.upper():
            return None
        return ma

    return None


# ============================================================
# NGUON 2: OpenRouter (Qwen2.5-VL free) -- du phong khi Gemini het han ngach ngay
# Dang ky mien phi tai openrouter.ai, KHONG can the, lay key tai openrouter.ai/keys
# ============================================================
OPENROUTER_API_KEY = os.getenv("OPENROUTER_API_KEY")
OPENROUTER_MODEL = "openrouter/free"
OPENROUTER_URL = "https://openrouter.ai/api/v1/chat/completions"


def goi_openrouter(image_bytes: bytes, so_lan_thu_lai=5):
    if not OPENROUTER_API_KEY:
        return None

    b64 = base64.b64encode(image_bytes).decode("utf-8")
    data_url = f"data:image/jpeg;base64,{b64}"

    payload = {
        "model": OPENROUTER_MODEL,
        "messages": [{
            "role": "user",
            "content": [
                {"type": "text", "text": PROMPT},
                {"type": "image_url", "image_url": {"url": data_url}}
            ]
        }],
        "max_tokens": 50,
        "temperature": 0
    }
    headers = {"Authorization": f"Bearer {OPENROUTER_API_KEY}"}

    for lan in range(so_lan_thu_lai):
        try:
            resp = requests.post(OPENROUTER_URL, json=payload, headers=headers, timeout=30)
        except requests.RequestException:
            time.sleep(2 ** lan)
            continue

        if resp.status_code == 429:
            try:
                loi_chi_tiet = resp.json().get("error", {}).get("metadata", {}).get("raw", "").lower()
            except Exception:
                loi_chi_tiet = ""

            # Phan biet 2 loai 429:
            # (a) pool free bi qua tai TAM THOI (nhieu nguoi dung cung goi model free chung)
            #     -- day la loai gap phai, chi can doi va thu lai, KHONG phai het han ngach
            # (b) that su het han ngach ngay cua tai khoan minh -- moi dung han script
            if "temporarily" in loi_chi_tiet or "retry shortly" in loi_chi_tiet or "rate-limited upstream" in loi_chi_tiet:
                cho_giay = 8 * (lan + 1)
                print(f"    (OpenRouter dang qua tai tam thoi, doi {cho_giay}s roi thu lai...)")
                time.sleep(cho_giay)
                continue

            # Khong ro nguyen nhan / co dau hieu la han ngach that -- coi la het han ngach ngay
            raise QuotaNgayHetHan()

        if resp.status_code != 200:
            return None

        data = resp.json()
        try:
            text = data["choices"][0]["message"]["content"]
        except (KeyError, IndexError):
            return None

        if not text:
            return None

        if "KHONG_DOC_DUOC" in text.upper():
            return None
        return text

    # Het so lan thu lai voi loi 429 tam thoi -- bo qua anh nay, KHONG dung han ca script
    return None


# ============================================================
# Cache
# ============================================================
FILE_CACHE_KET_QUA = "ket_qua_cache_gemini.json"


def doc_cache():
    if os.path.exists(FILE_CACHE_KET_QUA):
        with open(FILE_CACHE_KET_QUA, encoding="utf-8") as f:
            return json.load(f)
    return {}


def ghi_cache(cache: dict):
    with open(FILE_CACHE_KET_QUA, "w", encoding="utf-8") as f:
        json.dump(cache, f, ensure_ascii=False, indent=2)


def main():
    with open("Container-OCR-3/train/_annotations.coco.json", encoding="utf-8") as f:
        coco = json.load(f)

    SO_LUONG_ANH_CHAY = 300

    danh_sach_anh_da_xao = coco["images"][:]
    random.seed(42)
    random.shuffle(danh_sach_anh_da_xao)
    sample_images = danh_sach_anh_da_xao[:SO_LUONG_ANH_CHAY] if SO_LUONG_ANH_CHAY else danh_sach_anh_da_xao

    cache = doc_cache()
    so_bo_qua_vi_da_dung = sum(
        1 for img in sample_images
        if cache.get(img["file_name"], {}).get("trang_thai") == "THANH_CONG"
    )
    if so_bo_qua_vi_da_dung:
        print(f"Cache: {so_bo_qua_vi_da_dung} anh da doc dung tu lan truoc -> bo qua.\n")

    tong_so = len(sample_images)
    so_dung = 0
    so_xu_ly_moi = 0
    t_bat_dau = time.time()

    gemini_con_han_ngach = True
    openrouter_con_han_ngach = OPENROUTER_API_KEY is not None

    if not openrouter_con_han_ngach:
        print("Luu y: chua co OPENROUTER_API_KEY trong .env -- se CHI dung Gemini, "
              "khong co du phong khi het han ngach.\n")

    idx = 0
    for idx, img_info in enumerate(sample_images, start=1):
        filename = img_info["file_name"]

        muc_cache = cache.get(filename)
        if muc_cache and muc_cache.get("trang_thai") == "THANH_CONG":
            so_dung += 1
            if idx % 20 == 0 or idx == tong_so:
                print(f"[{idx}/{tong_so}] (bo qua anh cache...) Da tim duoc: {so_dung} anh ({so_dung/idx*100:.1f}%)")
            continue

        if not gemini_con_han_ngach and not openrouter_con_han_ngach:
            print()
            print("===== CA 2 NGUON DEU HET HAN NGACH HOM NAY =====")
            print("Chay lai 'python test_gemini.py' vao NGAY MAI -- cache da luu, se tiep tuc dung cho.")
            break

        so_xu_ly_moi += 1
        image_id = img_info["id"]
        all_anns = [a for a in coco["annotations"] if a["image_id"] == image_id]

        if not all_anns:
            cache[filename] = {"ma_container": None, "trang_thai": "KHONG_CO_ANNOTATION"}
            ghi_cache(cache)
            continue

        min_x = min(a["bbox"][0] for a in all_anns)
        min_y = min(a["bbox"][1] for a in all_anns)
        max_x = max(a["bbox"][0] + a["bbox"][2] for a in all_anns)
        max_y = max(a["bbox"][1] + a["bbox"][3] for a in all_anns)
        padding = 20
        crop_box = (max(0, min_x - padding), max(0, min_y - padding), max_x + padding, max_y + padding)

        from PIL import Image
        img = Image.open(f"Container-OCR-3/train/{filename}").convert("RGB")
        cropped = img.crop(crop_box)
        cropped.save("temp_gemini_crop.jpg")
        with open("temp_gemini_crop.jpg", "rb") as f_img:
            image_bytes = f_img.read()

        raw_text = None
        nguon_dung = None

        if gemini_con_han_ngach:
            try:
                raw_text = goi_gemini(image_bytes)
                nguon_dung = "gemini"
            except QuotaNgayHetHan:
                gemini_con_han_ngach = False
                print(f"[{idx}/{tong_so}] Gemini het han ngach hom nay -- chuyen sang OpenRouter...")

        if nguon_dung is None and openrouter_con_han_ngach:
            try:
                raw_text = goi_openrouter(image_bytes)
                nguon_dung = "openrouter"
            except QuotaNgayHetHan:
                openrouter_con_han_ngach = False
                print(f"[{idx}/{tong_so}] OpenRouter het han ngach hom nay.")

        if nguon_dung is None:
            continue

        ma_code = find_valid_code(raw_text) if raw_text else None

        cache[filename] = {
            "ma_container": ma_code,
            "raw_response": raw_text,
            "nguon": nguon_dung,
            "trang_thai": "THANH_CONG" if ma_code else "CHUA_DOC_DUOC",
        }
        ghi_cache(cache)

        if ma_code:
            so_dung += 1

        time.sleep(0.3)

        if idx % 20 == 0 or idx == tong_so:
            ty_le = so_dung / idx * 100
            da_troi_qua = time.time() - t_bat_dau
            print(f"[{idx}/{tong_so}] Da tim duoc: {so_dung} anh ({ty_le:.1f}%) "
                  f"| Moi xu ly: {so_xu_ly_moi} | Thoi gian: {da_troi_qua:.0f}s")

    with open("ket_qua_gemini.csv", "w", newline="", encoding="utf-8-sig") as f_csv:
        writer = csv.writer(f_csv)
        writer.writerow(["ten_file", "ma_container", "nguon", "trang_thai"])
        for img_info in sample_images:
            filename = img_info["file_name"]
            muc = cache.get(filename, {})
            writer.writerow([filename, muc.get("ma_container") or "",
                              muc.get("nguon") or "", muc.get("trang_thai") or ""])

    print()
    if idx > 0:
        print(f"===== TONG KET: {so_dung}/{idx} anh da xet, tim duoc ma hop le "
              f"({so_dung / idx * 100:.1f}%) =====")
    if idx < tong_so:
        print(f"(Con {tong_so - idx} anh chua xet vi het han ngach -- chay lai vao ngay mai)")
    print(f"(Trong do {so_bo_qua_vi_da_dung} anh lay tu cache, {so_xu_ly_moi} anh moi xu ly lan nay)")
    print(f"Ket qua: ket_qua_gemini.csv va {FILE_CACHE_KET_QUA}")


if __name__ == "__main__":
    main()  