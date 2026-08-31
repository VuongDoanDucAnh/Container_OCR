import json
import csv
import os
import re
from PIL import Image
from paddleocr import PaddleOCR

# ============================================================
# COPY checksum tu test.py de dung chung tieu chuan
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


# ============================================================
# Phan loai nguyen nhan that bai -- day la phan MOI, chua co trong test.py
# ============================================================
def phan_loai_loi(texts: list[str]) -> tuple[str, str]:
    """Tra ve (ma_loi, mo_ta) de biet chinh xac ly do that bai."""
    joined = " ".join(texts).upper().replace(" ", "")

    if not texts or not joined:
        return "KHONG_PHAT_HIEN_CHU", (
            "PaddleOCR khong detect duoc chu nao trong anh -- "
            "thuong do anh mo, do phan giai thap, chu qua nho, hoac bi che khuat hoan toan."
        )

    ung_vien_11_ky_tu = re.findall(r"[A-Z0-9]{9,13}", joined)
    if not ung_vien_11_ky_tu:
        return "DO_DAI_SAI", (
            f"OCR co doc duoc chu nhung khong ra du 11 ky tu lien tuc "
            f"(doc duoc: '{joined[:30]}'). Thuong do: bbox crop bi cat hut mot phan ma, "
            f"hoac ky tu bi dut quang qua nhieu (ri set, bay mau son)."
        )

    for c in re.findall(r"[A-Z]{4}\d{7}", joined):
        if not validate_container_code(c) and not try_sua_loi_1_ky_tu(c):
            return "SAI_TREN_1_KY_TU", (
                f"OCR doc du 11 ky tu dung dinh dang nhung checksum sai, "
                f"va sai o NHIEU HON 1 ky tu (doc duoc: '{c}') nen co che sua loi hien tai "
                f"khong xu ly duoc. Can xem anh that de biet AI doc nham cho nao."
            )

    return "KHAC", f"Truong hop chua phan loai duoc, doc duoc: '{joined[:30]}'"


def main():
    with open("ket_qua_cache.json", encoding="utf-8") as f:
        cache = json.load(f)

    danh_sach_that_bai = [
        (ten_file, thong_tin) for ten_file, thong_tin in cache.items()
        if thong_tin.get("trang_thai") == "CHUA_DOC_DUOC"
    ]

    if not danh_sach_that_bai:
        print("Khong co anh nao that bai trong cache -- chua chay test.py hoac tat ca da thanh cong.")
        return

    print(f"Tim thay {len(danh_sach_that_bai)} anh that bai. Dang phan tich...\n")

    with open("Container-OCR-3/train/_annotations.coco.json", encoding="utf-8") as f:
        coco = json.load(f)

    anh_theo_ten = {img["file_name"]: img for img in coco["images"]}

    os.makedirs("anh_loi_can_xem", exist_ok=True)

    ocr = PaddleOCR(
        lang="en",
        enable_mkldnn=False,
        use_doc_orientation_classify=False,
        use_doc_unwarping=False,
        use_textline_orientation=False,
    )

    thong_ke_loai_loi = {}
    ket_qua_phan_tich = []

    for idx, (ten_file, _) in enumerate(danh_sach_that_bai, start=1):
        img_info = anh_theo_ten.get(ten_file)
        if not img_info:
            continue

        image_id = img_info["id"]
        all_anns = [a for a in coco["annotations"] if a["image_id"] == image_id]
        if not all_anns:
            continue

        min_x = min(a["bbox"][0] for a in all_anns)
        min_y = min(a["bbox"][1] for a in all_anns)
        max_x = max(a["bbox"][0] + a["bbox"][2] for a in all_anns)
        max_y = max(a["bbox"][1] + a["bbox"][3] for a in all_anns)
        padding = 20
        crop_box = (max(0, min_x - padding), max(0, min_y - padding), max_x + padding, max_y + padding)

        img = Image.open(f"Container-OCR-3/train/{ten_file}").convert("RGB")
        cropped = img.crop(crop_box)

        # Luu anh crop lai de xem bang mat -- day la buoc quan trong nhat de hieu loi that
        duong_dan_luu = f"anh_loi_can_xem/{ten_file}"
        cropped.save(duong_dan_luu)

        cropped.save("temp_phan_tich.jpg")
        result = ocr.predict("temp_phan_tich.jpg")
        texts = []
        for page in result:
            texts.extend(page["rec_texts"])

        ma_loi, mo_ta = phan_loai_loi(texts)
        thong_ke_loai_loi[ma_loi] = thong_ke_loai_loi.get(ma_loi, 0) + 1

        ket_qua_phan_tich.append({
            "ten_file": ten_file,
            "raw_ocr_text": " | ".join(texts),
            "ma_loi": ma_loi,
            "mo_ta": mo_ta,
            "duong_dan_anh": duong_dan_luu,
        })

        if idx % 20 == 0:
            print(f"Da phan tich {idx}/{len(danh_sach_that_bai)} anh...")

    with open("phan_tich_loi.csv", "w", newline="", encoding="utf-8-sig") as f_csv:
        writer = csv.DictWriter(f_csv, fieldnames=["ten_file", "raw_ocr_text", "ma_loi", "mo_ta", "duong_dan_anh"])
        writer.writeheader()
        writer.writerows(ket_qua_phan_tich)

    print()
    print("===== THONG KE NGUYEN NHAN THAT BAI =====")
    for ma_loi, so_luong in sorted(thong_ke_loai_loi.items(), key=lambda x: -x[1]):
        ty_le = so_luong / len(danh_sach_that_bai) * 100
        print(f"  {ma_loi:20s}: {so_luong:3d} anh ({ty_le:.1f}%)")
    print()
    print("Chi tiet: phan_tich_loi.csv")
    print("Anh crop de xem bang mat: thu muc anh_loi_can_xem/")


if __name__ == "__main__":
    main()
