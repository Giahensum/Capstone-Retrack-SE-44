"""
=============================================================================
ReTrack AI - Fast Parallel Dataset Puller from Open Source Waste Repository
=============================================================================
Tải song song đa luồng (Multi-threaded parallel download) ảnh phế liệu thật:
- Nguồn: kdkd1/waste-garbage-management-dataset (Hugging Face)
- Tự động lọc và ánh xạ về 12 nhóm chuẩn của ReTrack
=============================================================================
"""

import os
import sys
import shutil
import logging
from concurrent.futures import ThreadPoolExecutor, as_completed
from PIL import Image
import numpy as np
from huggingface_hub import HfFileSystem, hf_hub_download

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")
logger = logging.getLogger("FastDatasetPuller")

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
DATASET_ROOT = os.path.join(BASE_DIR, "custom_dataset")
RAW_DIR = os.path.join(DATASET_ROOT, "raw")

CLASSES = [
    'battery', 'biological', 'brown-glass', 'cardboard', 'clothes',
    'green-glass', 'metal', 'paper', 'plastic', 'shoes', 'trash', 'white-glass',
]

def ensure_dirs():
    for c in CLASSES:
        os.makedirs(os.path.join(RAW_DIR, c), exist_ok=True)

def classify_glass_color(image_path):
    try:
        with Image.open(image_path) as img:
            rgb = np.array(img.convert("RGB"))
            r = rgb[:, :, 0].astype(float)
            g = rgb[:, :, 1].astype(float)
            b = rgb[:, :, 2].astype(float)

            brown_mask = (r > 60) & (g > 30) & (b < 40) & (r > g) & (g > b)
            green_mask = (g > r * 1.15) & (g > b * 1.15) & (g > 40)

            brown_ratio = np.mean(brown_mask)
            green_ratio = np.mean(green_mask)

            if brown_ratio > 0.08:
                return "brown-glass"
            elif green_ratio > 0.08:
                return "green-glass"
            else:
                return "white-glass"
    except Exception:
        return "white-glass"

def download_single_file(repo_id, full_path, target_class_hint, idx):
    try:
        filename = full_path.replace(f"datasets/{repo_id}/", "")
        downloaded = hf_hub_download(repo_id=repo_id, filename=filename, repo_type="dataset")
        
        if target_class_hint == "glass":
            actual_class = classify_glass_color(downloaded)
        else:
            actual_class = target_class_hint

        dest_name = f"real_{actual_class}_{idx:03d}.jpg"
        dest_path = os.path.join(RAW_DIR, actual_class, dest_name)
        shutil.copy2(downloaded, dest_path)
        return actual_class, True
    except Exception as e:
        return target_class_hint, False

def fast_pull_dataset(samples_per_class=15, max_workers=8):
    ensure_dirs()
    fs = HfFileSystem()
    repo_id = "kdkd1/waste-garbage-management-dataset"

    mapping = {
        "battery": "battery",
        "cardboard": "cardboard",
        "metal": "metal",
        "plastic": "plastic",
        "paper": "paper",
        "clothes": "clothes",
        "shoes": "shoes",
        "biological": "biological",
        "trash": "trash",
        "glass": "glass",
    }

    logger.info(f"Bat dau quet danh sach anh tu '{repo_id}'...")
    download_tasks = []

    for source_dir, target_hint in mapping.items():
        try:
            limit = samples_per_class * 3 if source_dir == "glass" else samples_per_class
            files = fs.ls(f"datasets/{repo_id}/{source_dir}", detail=False)
            jpg_files = [f for f in files if f.lower().endswith(('.jpg', '.jpeg', '.png'))][:limit]
            for idx, f in enumerate(jpg_files, 1):
                download_tasks.append((repo_id, f, target_hint, idx))
        except Exception as e:
            logger.warning(f"Loi lay danh sach {source_dir}: {e}")

    logger.info(f"Tong so task can tai: {len(download_tasks)} anh. Chay song song voi {max_workers} threads...")
    stats = {c: 0 for c in CLASSES}

    with ThreadPoolExecutor(max_workers=max_workers) as executor:
        futures = [executor.submit(download_single_file, *task) for task in download_tasks]
        for future in as_completed(futures):
            c_name, success = future.result()
            if success and c_name in stats:
                stats[c_name] += 1

    print("\n" + "=" * 65)
    print(" KET QUA TAI DU LIEU SONG SONG (PARALLEL DOWNLOAD):")
    print("=" * 65)
    print(f"{'Nhom phe lieu':<16} | {'So anh tai ve':<15} | {'Trang thai':<20}")
    print("-" * 65)
    total = 0
    for c in CLASSES:
        print(f"{c:<16} | {stats[c]:<15} | Da them vao raw/")
        total += stats[c]
    print("-" * 65)
    print(f"Tong cong da bo sung: {total} anh that vao custom_dataset/raw/")
    print("=" * 65 + "\n")
    return total

if __name__ == "__main__":
    count = int(sys.argv[1]) if len(sys.argv) > 1 else 15
    fast_pull_dataset(samples_per_class=count, max_workers=10)
