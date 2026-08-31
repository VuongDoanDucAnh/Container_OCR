import os
from dotenv import load_dotenv
from roboflow import Roboflow

load_dotenv() 
rf = Roboflow(api_key=os.getenv("ROBOFLOW_API_KEY"))
project = rf.workspace("container-number-e0pq8").project("container-ocr-javqk-ehbda")
version = project.version(3)
dataset = version.download("coco")

print(f"Đã tải xong, dataset nằm ở: {dataset.location}")