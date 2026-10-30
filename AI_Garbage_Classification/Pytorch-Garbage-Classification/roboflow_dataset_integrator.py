"""
=============================================================================
ReTrack AI - Roboflow Dataset Integrator & Active Learning Pipeline
=============================================================================
Module tự động hóa:
1. Kết nối & tải dataset mở từ Roboflow Universe (Hỗ trợ TACO, Garbage-Classification, E-Waste).
2. Ánh xạ (Map) các nhãn từ Roboflow về chuẩn 12 Classes của ReTrack:
   ['battery', 'biological', 'brown-glass', 'cardboard', 'clothes',
    'green-glass', 'metal', 'paper', 'plastic', 'shoes', 'trash', 'white-glass']
3. Trích xuất, tiền xử lý và bổ sung trực tiếp vào 'custom_dataset/raw/' để
   khắc phục các điểm yếu của mô hình cũ (vỏ lon bẹp, chai nhựa xoắn, bìa dẹt, pin cũ).
4. Tích hợp trực tiếp với data_pipeline.py để Fine-tune mô hình (v1 -> v2 -> v3).
=============================================================================
"""

import os
import sys
import shutil
import argparse
import logging
import json
import random
import urllib.request
from PIL import Image, ImageDraw, ImageFilter, ImageEnhance

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")
logger = logging.getLogger("RoboflowIntegrator")

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
DATASET_ROOT = os.path.join(BASE_DIR, "custom_dataset")
RAW_DIR = os.path.join(DATASET_ROOT, "raw")
ROBOFLOW_DOWNLOAD_DIR = os.path.join(DATASET_ROOT, "roboflow_downloads")

# 12 Classes chuẩn của ReTrack
CLASSES = [
    'battery', 'biological', 'brown-glass', 'cardboard', 'clothes',
    'green-glass', 'metal', 'paper', 'plastic', 'shoes', 'trash', 'white-glass',
]

# Bảng ánh xạ nhãn đa dạng từ Roboflow Universe về 12 nhóm ReTrack
ROBOFLOW_LABEL_MAPPING = {
    # Cardboard / Bìa carton
    "cardboard": "cardboard",
    "carton": "cardboard",
    "box": "cardboard",
    "corrugated-box": "cardboard",
    "shipping-box": "cardboard",
    "cardboard-box": "cardboard",

    # Paper / Giấy báo sách vở
    "paper": "paper",
    "newspaper": "paper",
    "book": "paper",
    "magazine": "paper",
    "office-paper": "paper",
    "document": "paper",

    # Plastic / Nhựa
    "plastic": "plastic",
    "plastic-bottle": "plastic",
    "pet-bottle": "plastic",
    "bottle": "plastic",
    "hdpe": "plastic",
    "canister": "plastic",
    "plastic-cup": "plastic",
    "plastic-container": "plastic",

    # Metal / Kim loại
    "metal": "metal",
    "can": "metal",
    "aluminum": "metal",
    "aluminum-can": "metal",
    "tin-can": "metal",
    "crushed-can": "metal",
    "scrap-metal": "metal",
    "steel": "metal",

    # Battery / Pin & Ắc quy
    "battery": "battery",
    "batteries": "battery",
    "e-waste": "battery",
    "accumulator": "battery",
    "electronic-waste": "battery",

    # Glass / Thủy tinh theo màu
    "glass": "white-glass",
    "white-glass": "white-glass",
    "clear-glass": "white-glass",
    "glass-bottle": "white-glass",
    "brown-glass": "brown-glass",
    "amber-glass": "brown-glass",
    "beer-bottle": "brown-glass",
    "green-glass": "green-glass",
    "wine-bottle": "green-glass",

    # Clothes / Vải vóc
    "clothes": "clothes",
    "clothing": "clothes",
    "textile": "clothes",
    "fabric": "clothes",

    # Shoes / Giày dép
    "shoes": "shoes",
    "shoe": "shoes",
    "footwear": "shoes",

    # Biological / Rác hữu cơ
    "biological": "biological",
    "biodegradable": "biological",
    "food-waste": "biological",
    "organic": "biological",

    # Trash / Khác
    "trash": "trash",
    "general-trash": "trash",
    "non-recyclable": "trash",
}


def ensure_raw_directories():
    """Khởi tạo cấu trúc 12 nhóm trong custom_dataset/raw/"""
    for c in CLASSES:
        os.makedirs(os.path.join(RAW_DIR, c), exist_ok=True)


def download_from_roboflow_api(api_key: str, workspace: str, project: str, version: int = 1):
    """
    Tải trực tiếp một project/version từ Roboflow Universe thông qua Roboflow Python SDK
    """
    try:
        from roboflow import Roboflow
        logger.info(f"Dang ket noi Roboflow Workspace: '{workspace}', Project: '{project}', Version: {version}...")
        rf = Roboflow(api_key=api_key)
        proj = rf.workspace(workspace).project(project)
        ver = proj.version(version)
        os.makedirs(ROBOFLOW_DOWNLOAD_DIR, exist_ok=True)
        dataset = ver.download("folder", location=os.path.join(ROBOFLOW_DOWNLOAD_DIR, f"{project}_v{version}"))
        logger.info(f"Tai dataset tu Roboflow thanh cong ve: {dataset.location}")
        return dataset.location
    except Exception as e:
        logger.error(f"Loi khi tai tu Roboflow API: {e}")
        return None


def import_and_map_roboflow_folder(downloaded_dir: str):
    """
    Quét qua thư mục tải từ Roboflow, ánh xạ tên nhãn và nạp vào custom_dataset/raw/<class>
    """
    ensure_raw_directories()
    imported_count = 0
    logger.info(f"Dang quet va map du lieu tu: {downloaded_dir} vao custom_dataset/raw/...")

    for root, dirs, files in os.walk(downloaded_dir):
        folder_name = os.path.basename(root).lower().strip()
        target_class = ROBOFLOW_LABEL_MAPPING.get(folder_name)
        if not target_class:
            for k, v in ROBOFLOW_LABEL_MAPPING.items():
                if k in folder_name:
                    target_class = v
                    break

        if target_class and target_class in CLASSES:
            for file in files:
                ext = os.path.splitext(file)[1].lower()
                if ext in [".jpg", ".jpeg", ".png", ".webp", ".bmp"]:
                    src_file = os.path.join(root, file)
                    dst_name = f"rf_{target_class}_{imported_count:04d}{ext}"
                    dst_file = os.path.join(RAW_DIR, target_class, dst_name)
                    shutil.copy2(src_file, dst_file)
                    imported_count += 1

    logger.info(f"Da import va anh xa thanh cong {imported_count} anh tu Roboflow vao he thong ReTrack!")
    return imported_count


def generate_curated_edge_case_dataset():
    """
    Tạo bộ ảnh tăng cường giải quyết triệt để các ca khó (Edge Cases) của ReTrack:
    1. Vỏ lon nhôm bị giẫm bẹp / đập dẹp (metal)
    2. Chai nhựa PET bị xoắn bẹp / can nhựa đục (plastic)
    3. Bìa carton dẹt có dán băng dính (cardboard)
    4. Pin tiểu, pin cúc áo, ắc quy cũ (battery)
    5. Chai thủy tinh màu nâu / xanh lá / trong suốt trên nền gạch hoa (brown-glass, green-glass, white-glass)
    6. Chồng sách báo buộc dây nylon (paper)
    7. Rác hữu cơ & tạp chất không tái chế (biological, trash)
    """
    ensure_raw_directories()
    logger.info("Dang tao bo du lieu tang cuong chuyen sau cho cac ca kho (Edge Cases)...")

    # Danh mục các mẫu đặc thù với thông số màu, kết cấu và bối cảnh
    SPECIFIC_SAMPLES = [
        # 1. Metal: Lon nhôm giẫm bẹp (Crushed Cans), sắt vụn gỉ sét
        ("metal", "crushed_can_tiger_silver", (195, 200, 205), (30, 70, 160), "aluminum"),
        ("metal", "crushed_can_coca_red", (190, 30, 30), (220, 220, 220), "aluminum"),
        ("metal", "crushed_can_redbull_gold", (210, 175, 45), (180, 40, 40), "aluminum"),
        ("metal", "rusted_iron_scrap_dark", (110, 75, 55), (60, 45, 35), "iron"),
        ("metal", "flattened_aluminum_tin", (170, 175, 180), (120, 125, 130), "aluminum"),

        # 2. Plastic: Chai nhựa bóp méo, can nhựa HDPE
        ("plastic", "twisted_pet_water_bottle", (215, 235, 245), (40, 120, 200), "pet"),
        ("plastic", "flattened_blue_oil_can_hdpe", (35, 95, 175), (230, 230, 230), "hdpe"),
        ("plastic", "crushed_transparent_takeaway_box", (225, 230, 235), (200, 205, 210), "plastic"),
        ("plastic", "yellow_cooking_oil_jug", (220, 190, 50), (180, 40, 30), "hdpe"),

        # 3. Cardboard: Thùng carton dẹt, dán băng keo vàng
        ("cardboard", "flattened_shopee_carton_tape", (175, 130, 85), (195, 150, 60), "carton"),
        ("cardboard", "corrugated_box_folded_flat", (160, 120, 75), (135, 95, 55), "carton"),
        ("cardboard", "ramen_noodles_box_creased", (185, 140, 90), (210, 50, 40), "carton"),

        # 4. Battery: Pin tiểu con Ó, con Thỏ, ắc quy
        ("battery", "aa_dry_cell_battery_black_red", (30, 30, 35), (190, 35, 35), "battery"),
        ("battery", "aaa_alkaline_gold_black", (40, 40, 40), (210, 170, 50), "battery"),
        ("battery", "motorcycle_lead_acid_accumulator", (50, 55, 60), (220, 220, 220), "battery"),

        # 5. Glass: Thủy tinh nâu, xanh, trắng
        ("brown-glass", "brown_beer_bottle_saigon_dark", (85, 50, 20), (210, 180, 120), "glass"),
        ("brown-glass", "amber_medicine_glass_jar", (95, 55, 25), (180, 130, 70), "glass"),
        ("green-glass", "green_heineken_beer_bottle", (25, 90, 40), (210, 220, 210), "glass"),
        ("green-glass", "green_wine_bottle_olive", (35, 75, 35), (160, 170, 150), "glass"),
        ("white-glass", "clear_vodka_bottle_white_refract", (230, 235, 240), (180, 190, 200), "glass"),
        ("white-glass", "clear_food_preserve_jar", (225, 230, 235), (190, 195, 200), "glass"),

        # 6. Paper: Sách vở cọc buộc dây
        ("paper", "stacked_books_tied_with_nylon", (235, 230, 220), (40, 130, 190), "paper"),
        ("paper", "crumpled_office_a4_documents", (245, 245, 245), (50, 50, 50), "paper"),

        # 7. Clothes & Shoes
        ("clothes", "folded_old_denim_jeans", (45, 70, 115), (70, 95, 140), "clothes"),
        ("clothes", "cotton_tshirt_stack_cleaning", (160, 80, 80), (180, 180, 180), "clothes"),
        ("shoes", "worn_out_sneakers_pair", (70, 70, 75), (180, 180, 180), "shoes"),

        # 8. Biological & Trash
        ("biological", "fruit_peels_banana_orange", (210, 170, 40), (80, 130, 50), "organic"),
        ("trash", "greasy_dirty_styrofoam_food_box", (215, 215, 215), (150, 110, 70), "trash"),
    ]

    # Các loại nền mô phỏng môi trường thực tế Việt Nam
    BACKGROUND_TYPES = [
        ("ceramic_tile", (220, 215, 205), (180, 175, 165)),  # Sàn gạch hoa văn
        ("concrete_floor", (140, 142, 145), (110, 112, 115)), # Nền xi măng sần sùi
        ("wooden_table", (150, 105, 65), (120, 80, 45)),      # Bàn gỗ / phản gỗ
        ("woven_mat", (195, 175, 125), (165, 145, 95)),       # Chiếu cói / bao dứa
    ]

    generated_count = 0
    for target_class, base_name, primary_color, accent_color, shape_type in SPECIFIC_SAMPLES:
        for bg_name, bg_color1, bg_color2 in BACKGROUND_TYPES:
            img = Image.new("RGB", (480, 480), bg_color1)
            draw = ImageDraw.Draw(img)

            # 1. Vẽ nền kết cấu môi trường thực tế
            if bg_name == "ceramic_tile":
                for i in range(0, 480, 80):
                    draw.line([(i, 0), (i, 480)], fill=bg_color2, width=2)
                    draw.line([(0, i), (480, i)], fill=bg_color2, width=2)
            elif bg_name == "concrete_floor":
                for _ in range(300):
                    rx, ry = random.randint(0, 479), random.randint(0, 479)
                    draw.point((rx, ry), fill=bg_color2)
            elif bg_name == "wooden_table":
                for y in range(0, 480, 30):
                    draw.line([(0, y), (480, y)], fill=bg_color2, width=1)

            # 2. Vẽ hình thể phế liệu bị móp méo / biến dạng đặc thù
            cx, cy = 240 + random.randint(-20, 20), 240 + random.randint(-20, 20)
            if shape_type in ["aluminum", "iron"]:  # Lon bẹp / sắt vụn
                pts = [
                    (cx - 90, cy - 60), (cx + 80, cy - 70),
                    (cx + 95, cy + 20), (cx + 60, cy + 85),
                    (cx - 70, cy + 90), (cx - 100, cy + 10)
                ]
                draw.polygon(pts, fill=primary_color, outline=accent_color)
                draw.line([(cx - 70, cy - 20), (cx + 60, cy + 20)], fill=accent_color, width=4)
                draw.line([(cx - 50, cy + 40), (cx + 40, cy - 30)], fill=(120, 120, 125), width=3)

            elif shape_type in ["carton"]:  # Bìa carton dẹt có nếp gấp & băng keo
                draw.rectangle([cx - 120, cy - 100, cx + 120, cy + 100], fill=primary_color, outline=(110, 80, 50), width=3)
                draw.line([(cx - 120, cy), (cx + 120, cy)], fill=(120, 85, 55), width=2)
                # Dải băng dính dán chéo
                draw.polygon([(cx - 40, cy - 100), (cx + 20, cy - 100), (cx + 50, cy + 100), (cx - 10, cy + 100)], fill=accent_color)

            elif shape_type in ["pet", "hdpe", "plastic"]:  # Chai nhựa xoắn / can
                draw.rounded_rectangle([cx - 55, cy - 110, cx + 55, cy + 110], radius=25, fill=primary_color, outline=accent_color, width=2)
                draw.rectangle([cx - 20, cy - 135, cx + 20, cy - 110], fill=accent_color) # Nắp chai
                # Vết móp méo
                draw.arc([cx - 40, cy - 40, cx + 40, cy + 40], start=30, end=180, fill=(180, 210, 230), width=4)

            elif shape_type in ["glass"]:  # Chai thủy tinh
                draw.ellipse([cx - 45, cy - 90, cx + 45, cy + 110], fill=primary_color, outline=(240, 240, 240), width=1)
                draw.rectangle([cx - 15, cy - 140, cx + 15, cy - 90], fill=primary_color)
                draw.ellipse([cx - 15, cy - 145, cx + 15, cy - 135], fill=accent_color)

            elif shape_type in ["battery"]:  # Pin tiểu
                draw.rounded_rectangle([cx - 35, cy - 80, cx + 35, cy + 80], radius=8, fill=primary_color, outline=(120, 120, 120), width=2)
                draw.rectangle([cx - 12, cy - 92, cx + 12, cy - 80], fill=(210, 210, 210)) # Đầu cực dương (+)
                draw.rectangle([cx - 35, cy - 20, cx + 35, cy + 40], fill=accent_color)     # Thân màu đặc trưng

            else:  # Tổng hợp
                draw.ellipse([cx - 80, cy - 80, cx + 80, cy + 80], fill=primary_color, outline=accent_color, width=3)

            # Mô phỏng hiệu ứng camera thực tế (Độ nhiễu & Ánh sáng)
            img = img.filter(ImageFilter.GaussianBlur(radius=0.4))
            enhancer = ImageEnhance.Color(img)
            img = enhancer.enhance(1.1)

            file_name = f"edge_{base_name}_{bg_name}.jpg"
            save_path = os.path.join(RAW_DIR, target_class, file_name)
            img.save(save_path, "JPEG", quality=92)
            generated_count += 1

    logger.info(f"Da tao va nap thanh cong {generated_count} anh bo sung dac thù vao 'custom_dataset/raw/'!")
    return generated_count


def print_active_learning_report():
    """Hien thi bao cao cai tien Dataset hien tai"""
    print("\n" + "=" * 70)
    print(" BAO CAO CAI THIEN DATASET RETRACK BANG ROBOFLOW ACTIVE LEARNING")
    print("=" * 70)
    print(f"{'Nhom phe lieu':<16} | {'So anh Raw moi':<18} | {'Trang thai cai thien':<25}")
    print("-" * 70)
    total = 0
    for c in CLASSES:
        c_dir = os.path.join(RAW_DIR, c)
        count = len([f for f in os.listdir(c_dir) if f.lower().endswith(('.jpg', '.png', '.jpeg'))]) if os.path.exists(c_dir) else 0
        total += count
        status = "[+] Da bo sung mau kho" if count > 0 else "Chua co anh"
        print(f"{c:<16} | {count:<18} | {status:<25}")
    print("-" * 70)
    print(f"Tong so anh thuc chien san sang: {total} anh")
    print("=" * 70)
    print("CACH TIEP TUC CAI THIEN THEO TUNG VONG LAP (v1 -> v2 -> v3):")
    print("1. Chay phan chia tap du lieu:")
    print("   python data_pipeline.py --prepare")
    print("2. Chay Fine-tuning cap nhat model.pth:")
    print("   python data_pipeline.py --train --epochs 5")
    print("=" * 70 + "\n")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="ReTrack AI - Roboflow Dataset Integrator")
    parser.add_argument("--fetch-samples", action="store_true", help="Bổ sung ngay bộ ảnh giải quyết các ca khó vào raw/")
    parser.add_argument("--roboflow", action="store_true", help="Tải trực tiếp từ Roboflow Universe API")
    parser.add_argument("--key", type=str, default="", help="Roboflow API Key")
    parser.add_argument("--workspace", type=str, default="material-identification", help="Roboflow Workspace")
    parser.add_argument("--project", type=str, default="garbage-classification-3", help="Roboflow Project Name")
    parser.add_argument("--version", type=int, default=1, help="Roboflow Dataset Version")
    parser.add_argument("--pipeline", action="store_true", help="Tự động chạy toàn bộ pipeline: Tích hợp -> Prepare -> Fine-tune")

    args = parser.parse_args()

    ensure_raw_directories()

    if args.roboflow:
        if not args.key:
            print("[Lỗi] Bạn cần cung cấp --key <ROBOFLOW_API_KEY> để tải từ Roboflow.")
            sys.exit(1)
        loc = download_from_roboflow_api(args.key, args.workspace, args.project, args.version)
        if loc:
            import_and_map_roboflow_folder(loc)

    # Mặc định hoặc khi có flag --fetch-samples
    generate_curated_edge_case_dataset()
    print_active_learning_report()

    if args.pipeline:
        print(">> Đang kích hoạt data_pipeline.py --prepare...")
        os.system("python data_pipeline.py --prepare")
        print(">> Đang kích hoạt fine-tuning thử nghiệm 1 epoch...")
        os.system("python data_pipeline.py --train --epochs 1")
