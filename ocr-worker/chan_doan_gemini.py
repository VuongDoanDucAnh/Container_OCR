import os
import base64
import requests
from dotenv import load_dotenv

load_dotenv()

GEMINI_API_KEY = os.getenv("GEMINI_API_KEY")
print(f"1. API key doc duoc tu .env: {'CO' if GEMINI_API_KEY else 'KHONG - day la nguyen nhan!'}")
if GEMINI_API_KEY:
    print(f"   Do dai key: {len(GEMINI_API_KEY)} ky tu, bat dau bang: {GEMINI_API_KEY[:8]}...")

MODEL = "gemini-3.6-flash"
API_URL = f"https://generativelanguage.googleapis.com/v1beta/models/{MODEL}:generateContent"

# Tim 1 anh bat ky trong dataset de test
import glob
danh_sach_anh = glob.glob("Container-OCR-3/train/*.jpg")
if not danh_sach_anh:
    print("2. KHONG TIM THAY anh nao trong Container-OCR-3/train/ -- kiem tra dang chay dung thu muc chua")
else:
    duong_dan_anh = danh_sach_anh[0]
    print(f"2. Dung anh test: {duong_dan_anh}")

    with open(duong_dan_anh, "rb") as f:
        image_bytes = f.read()

    b64 = base64.b64encode(image_bytes).decode("utf-8")
    payload = {
        "contents": [{
            "parts": [
                {"text": "Doc chinh xac ma so container trong anh theo chuan ISO 6346."},
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

    print("3. Dang goi Gemini API...")
    headers = {"x-goog-api-key": GEMINI_API_KEY, "Content-Type": "application/json"}
    resp = requests.post(API_URL, json=payload, headers=headers, timeout=30)

    print(f"4. HTTP Status Code: {resp.status_code}")
    print(f"5. Noi dung tra ve day du:")
    print(resp.text)