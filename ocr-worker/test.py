import json
import csv
import time
import re
import os
import random
import cv2
import numpy as np
from PIL import Image
from paddleocr import PaddleOCR
from deskew import determine_skew

def build_letter_values() -> dict:
    # Bang gia tri chu cai: A=10, tang dan, bo qua boi so cua 11 (11, 22, 33)
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
    # Tinh check digit tu 10 ky tu dau (4 chu + 6 so dau cua serial)
    weights = [2 ** i for i in range(10)]
    total = sum((int(ch) if ch.isdigit() else LETTER_VALUES[ch]) * w
                for ch, w in zip(code_10, weights))
    return (total % 11) % 10


def validate_container_code(code: str) -> bool:
    # Kiem tra dung dinh dang (4 chu + 7 so) VA dung check digit
    code = code.strip().upper()
    if len(code) != 11 or not code[:4].isalpha() or not code[4:].isdigit():
        return False
    return iso6346_check_digit(code[:10]) == int(code[10])


# ============================================================
# Tu dong sua loi doc nham 1 ky tu (vi du OCR doc O thanh 0, S thanh 5...)
# ============================================================
CAC_KY_TU_DE_NHAM = {
    "0": ["O", "Q", "D"], "O": ["0", "Q", "D"],
    "1": ["I", "L", "7"], "I": ["1", "L"], "L": ["1", "I"],
    "5": ["S"], "S": ["5"],
    "8": ["B"], "B": ["8"],
    "2": ["Z"], "Z": ["2"],
    "6": ["G"], "G": ["6"],
    "9": ["4", "P"], "4": ["9"],
    "7": ["1"],
}


def try_sua_loi_1_ky_tu(ma_11_ky_tu: str):
    # Thu thay tung vi tri bang ky tu de nham, giu dung loai (chu/so) theo vi tri.
    # Chi nhan ket qua neu CHI DUNG 1 cach thay the cho ra ma hop le.
    ung_vien_hop_le = []
    for vi_tri in range(11):
        ky_tu_hien_tai = ma_11_ky_tu[vi_tri]
        for ky_tu_thay_the in CAC_KY_TU_DE_NHAM.get(ky_tu_hien_tai, []):
            if vi_tri < 4 and not ky_tu_thay_the.isalpha():
                continue
            if vi_tri >= 4 and not ky_tu_thay_the.isdigit():
                continue
            ma_moi = ma_11_ky_tu[:vi_tri] + ky_tu_thay_the + ma_11_ky_tu[vi_tri + 1:]
            if validate_container_code(ma_moi):
                ung_vien_hop_le.append(ma_moi)
    return ung_vien_hop_le[0] if len(ung_vien_hop_le) == 1 else None


def find_valid_code(texts):
    # Loc chuoi dung dinh dang tu ket qua OCR tho, validate checksum,
    # neu sai thi thu sua 1 ky tu truoc khi bo cuoc
    joined = " ".join(texts).upper().replace(" ", "")
    for c in re.findall(r"[A-Z]{4}\d{7}", joined):
        if validate_container_code(c):
            return c, False  # (ma, co_sua_loi_khong)
        da_sua = try_sua_loi_1_ky_tu(c)
        if da_sua:
            return da_sua, True
    return None, False


# ============================================================
# Tien xu ly anh: CLAHE (tang tuong phan) + auto-deskew (tu dong sua nghieng)
# ============================================================
def apply_clahe(pil_img: Image.Image) -> Image.Image:
    img_np = np.array(pil_img.convert("L"))
    clahe = cv2.createCLAHE(clipLimit=2.0, tileGridSize=(8, 8))
    enhanced = clahe.apply(img_np)
    return Image.fromarray(enhanced).convert("RGB")


def auto_deskew(pil_img: Image.Image) -> Image.Image:
    img_np = np.array(pil_img.convert("L"))
    try:
        angle = determine_skew(img_np)
    except Exception:
        angle = None
    if angle is None or abs(angle) < 1.0:
        return pil_img
    return pil_img.rotate(angle, expand=True, fillcolor="white")


def preprocess(pil_img: Image.Image) -> Image.Image:
    return apply_clahe(auto_deskew(pil_img))


# ============================================================
# 8 phep xoay/lat (nhom doi xung D4) -- bao phu moi huong chup vuong goc
# ============================================================
TRANSFORMS = {
    "goc":                lambda im: im,
    "xoay_180":           lambda im: im.rotate(180, fillcolor="white"),
    "lat_ngang":          lambda im: im.transpose(Image.FLIP_LEFT_RIGHT),
    "lat_ngang_xoay_180": lambda im: im.transpose(Image.FLIP_LEFT_RIGHT).rotate(180, fillcolor="white"),
    "xoay_90":            lambda im: im.rotate(90, expand=True, fillcolor="white"),
    "xoay_270":           lambda im: im.rotate(270, expand=True, fillcolor="white"),
    "lat_ngang_xoay_90":  lambda im: im.transpose(Image.FLIP_LEFT_RIGHT).rotate(90, expand=True, fillcolor="white"),
    "lat_ngang_xoay_270": lambda im: im.transpose(Image.FLIP_LEFT_RIGHT).rotate(270, expand=True, fillcolor="white"),
}

QUIET_MODE = True       # True = khong in tung dong OCR, chi in tien do tong quat
EARLY_STOP = True       # True = dung ngay khi tim duoc ma hop le dau tien
BAT_VONG_2_DU_PHONG = True  # True = neu vong 1 (anh goc) that bai, thu tiep vong 2 (anh da tien xu ly)


def try_all_transforms(ocr: PaddleOCR, cropped: Image.Image, tien_to_ten: str = ""):
    # Thu lan luot 8 phep D4 tren 1 anh, dung ngay khi doc ra ma hop le
    for ten_phep, transform_fn in TRANSFORMS.items():
        variant = transform_fn(cropped)
        variant.save("temp_variant.jpg")

        result = ocr.predict("temp_variant.jpg")
        texts = []
        for page in result:
            texts.extend(page["rec_texts"])

        if not QUIET_MODE:
            print(f"  [{tien_to_ten}{ten_phep:15s}] doc: {texts}")

        valid_code, da_sua_loi = find_valid_code(texts)
        if valid_code:
            hau_to = "_DA_SUA_1_KY_TU" if da_sua_loi else ""
            return valid_code, f"{tien_to_ten}{ten_phep}{hau_to}"

    return None, None


def process_image(ocr: PaddleOCR, img_path: str, crop_box):
    # Vong 1: thu tren anh goc. Vong 2 (neu bat): thu tren anh da qua CLAHE+deskew,
    # chi chay khi vong 1 that bai -- khong bao gio lam ket qua te hon vong 1.
    img = Image.open(img_path).convert("RGB")
    cropped = img.crop(crop_box)

    ma_code, ten_phep = try_all_transforms(ocr, cropped)
    if ma_code:
        return ma_code, ten_phep

    if not BAT_VONG_2_DU_PHONG:
        return None, None

    cropped_da_xu_ly = preprocess(cropped)
    return try_all_transforms(ocr, cropped_da_xu_ly, tien_to_ten="xla_")


# ============================================================
# Cache ket qua giua cac lan chay -- anh da doc DUNG se duoc bo qua,
# chi xu ly lai anh chua thanh cong
# ============================================================
FILE_CACHE_KET_QUA = "ket_qua_cache.json"


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

    # Tat cac model phu (doc orientation, doc unwarping, textline orientation)
    # de tang toc do -- khong can vi da tu xoay bang 8 phep D4
    ocr = PaddleOCR(
        lang="en",
        enable_mkldnn=False,
        use_doc_orientation_classify=False,
        use_doc_unwarping=False,
        use_textline_orientation=False,
    )

    tong_so = len(sample_images)
    so_dung = 0
    so_xu_ly_moi = 0
    t_bat_dau = time.time()

    for idx, img_info in enumerate(sample_images, start=1):
        filename = img_info["file_name"]

        muc_cache = cache.get(filename)
        if muc_cache and muc_cache.get("trang_thai") == "THANH_CONG":
            so_dung += 1
            if idx % 20 == 0 or idx == tong_so:
                print(f"[{idx}/{tong_so}] (bo qua anh cache...) Da tim duoc: {so_dung} anh ({so_dung/idx*100:.1f}%)")
            continue

        so_xu_ly_moi += 1
        image_id = img_info["id"]
        all_anns = [a for a in coco["annotations"] if a["image_id"] == image_id]

        if not all_anns:
            cache[filename] = {"ma_container": None, "phep": None, "trang_thai": "KHONG_CO_ANNOTATION"}
            ghi_cache(cache)
            continue

        min_x = min(a["bbox"][0] for a in all_anns)
        min_y = min(a["bbox"][1] for a in all_anns)
        max_x = max(a["bbox"][0] + a["bbox"][2] for a in all_anns)
        max_y = max(a["bbox"][1] + a["bbox"][3] for a in all_anns)
        padding = 20
        crop_box = (max(0, min_x - padding), max(0, min_y - padding), max_x + padding, max_y + padding)

        ma_code, ten_phep = process_image(ocr, f"Container-OCR-3/train/{filename}", crop_box)

        cache[filename] = {
            "ma_container": ma_code,
            "phep": ten_phep,
            "trang_thai": "THANH_CONG" if ma_code else "CHUA_DOC_DUOC",
        }
        ghi_cache(cache)

        if ma_code:
            so_dung += 1

        if idx % 20 == 0 or idx == tong_so:
            ty_le = so_dung / idx * 100
            da_troi_qua = time.time() - t_bat_dau
            print(f"[{idx}/{tong_so}] Da tim duoc: {so_dung} anh ({ty_le:.1f}%) "
                  f"| Moi xu ly: {so_xu_ly_moi} | Thoi gian: {da_troi_qua:.0f}s")

    # Xuat ket qua ra CSV (xem nhanh bang Excel)
    with open("ket_qua_baseline.csv", "w", newline="", encoding="utf-8-sig") as f_csv:
        writer = csv.writer(f_csv)
        writer.writerow(["ten_file", "ma_container", "phep_bien_doi_thanh_cong", "trang_thai"])
        for img_info in sample_images:
            filename = img_info["file_name"]
            muc = cache.get(filename, {})
            writer.writerow([filename, muc.get("ma_container") or "",
                              muc.get("phep") or "", muc.get("trang_thai") or ""])

    print()
    print(f"===== TONG KET: {so_dung}/{tong_so} anh tim duoc ma hop le ({so_dung / tong_so * 100:.1f}%) =====")
    print(f"(Trong do {so_bo_qua_vi_da_dung} anh lay tu cache, {so_xu_ly_moi} anh moi xu ly lan nay)")
    print(f"Ket qua: ket_qua_baseline.csv va {FILE_CACHE_KET_QUA}")


if __name__ == "__main__":
    main()