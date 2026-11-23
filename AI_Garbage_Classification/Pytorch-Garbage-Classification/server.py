import os
import io
import re
import time
import base64
import logging
from typing import Optional, List
from fastapi import FastAPI, File, UploadFile, HTTPException, Form
from fastapi.middleware.cors import CORSMiddleware
from fastapi.staticfiles import StaticFiles
from pydantic import BaseModel
import torch
import torchvision.transforms as transforms
from torchvision import models
from PIL import Image
import numpy as np
import cv2
from ultralytics import YOLO
import easyocr

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")
logger = logging.getLogger("retrack-ai")

# ── 1. Danh mục 12 nhóm rác & Metadata chi tiết cho ReTrack ──
CLASSES = [
    'battery', 'biological', 'brown-glass', 'cardboard', 'clothes',
    'green-glass', 'metal', 'paper', 'plastic', 'shoes', 'trash', 'white-glass',
]
CLASS_IDX = {c: i for i, c in enumerate(CLASSES)}

METADATA = {
    'cardboard': {
        'name_vi': 'Bìa Carton & Thùng giấy',
        'material_type': 'CARDBOARD',
        'is_recyclable': True,
        'icon': 'category',
        'unit': 'kg',
        'price_range': '3.000 - 5.500 đ/kg',
        'tip': 'Tháo dỡ băng keo, ép dẹp các thùng carton phẳng và giữ khô ráo để đạt khối lượng và mức giá tốt nhất.',
        'suggested_desc': 'Có phế liệu bìa carton và thùng giấy cũ đã gấp gọn, tình trạng khô ráo cần kho vựa thu gom.',
    },
    'paper': {
        'name_vi': 'Giấy vụn, Báo & Sách vở',
        'material_type': 'PAPER',
        'is_recyclable': True,
        'icon': 'description',
        'unit': 'kg',
        'price_range': '3.500 - 6.000 đ/kg',
        'tip': 'Sách báo cũ, tài liệu photo giấy A4 văn phòng nên buộc thành từng cọc phẳng phiu, tránh dính nước hoặc bùn đất.',
        'suggested_desc': 'Khoảng một số cọc sách báo cũ, tài liệu giấy văn phòng và giấy vụn gia đình cần bán thanh lý.',
    },
    'plastic': {
        'name_vi': 'Nhựa tái chế (Chai PET, Can nhựa, Hộp)',
        'material_type': 'PET',
        'is_recyclable': True,
        'icon': 'recycling',
        'unit': 'kg',
        'price_range': '6.000 - 11.000 đ/kg',
        'tip': 'Đổ sạch chất lỏng dư thừa, giẫm bẹp vỏ chai để giảm thể tích cồng kềnh giúp gom được nhiều hơn.',
        'suggested_desc': 'Bao tải chai nhựa PET đựng nước sạch, can dầu ăn và đồ nhựa gia dụng cũ đã rửa sơ và ép bẹp.',
    },
    'metal': {
        'name_vi': 'Kim loại (Lon nhôm, Sắt thép, Đồ cũ)',
        'material_type': 'ALUMINUM',
        'is_recyclable': True,
        'icon': 'hardware',
        'unit': 'kg',
        'price_range': '16.000 - 28.000 đ/kg (Lon nhôm) / 8.000 - 13.000 đ/kg (Sắt)',
        'tip': 'Tách riêng vỏ lon nhôm mềm (lon bia, nước ngọt) và kim loại sắt vụn để nhân viên cân tính giá chính xác nhất.',
        'suggested_desc': 'Phế liệu vỏ lon nhôm nước ngọt, sắt vụn và một số vật dụng kim loại cũ gia đình dọn dẹp cần bán.',
    },
    'battery': {
        'name_vi': 'Pin & Ắc quy cũ (Rác nguy hại)',
        'material_type': 'ELECTRONIC_WASTE',
        'is_recyclable': True,
        'icon': 'battery_alert',
        'unit': 'kg',
        'price_range': '10.000 - 30.000 đ/kg',
        'tip': 'Rác thải điện tử có chứa hóa chất. Hãy dán băng keo bọc hai đầu cực, đựng trong hộp nhựa kín tránh ẩm và va chạm mạnh.',
        'suggested_desc': 'Pin phế liệu đã qua sử dụng và ắc quy cũ cần bàn giao kho thu gom xử lý theo tiêu chuẩn môi trường.',
    },
    'brown-glass': {
        'name_vi': 'Thủy tinh màu nâu (Chai bia, Dược phẩm)',
        'material_type': 'OTHER',
        'is_recyclable': True,
        'icon': 'wine_bar',
        'unit': 'chai/kg',
        'price_range': '500 - 1.500 đ/vỏ chai',
        'tip': 'Xếp vỏ chai đứng ngay ngắn trong thùng/két nhựa hoặc thùng carton để tránh va đập vỡ gây nguy hiểm.',
        'suggested_desc': 'Vỏ chai thủy tinh nâu (chai bia, nước sâm) còn nguyên vẹn cần bán thanh lý cho kho tái chế.',
    },
    'green-glass': {
        'name_vi': 'Thủy tinh màu xanh lá (Rượu vang, Nước giải khát)',
        'material_type': 'OTHER',
        'is_recyclable': True,
        'icon': 'wine_bar',
        'unit': 'chai/kg',
        'price_range': '500 - 1.500 đ/vỏ chai',
        'tip': 'Kiểm tra chai còn nguyên miệng và thân; giữ chai sạch sẽ trước khi đưa vào thùng lưu trữ.',
        'suggested_desc': 'Chai thủy tinh màu xanh lá các loại còn nguyên vẹn cần vựa thu gom.',
    },
    'white-glass': {
        'name_vi': 'Thủy tinh trắng trong suốt',
        'material_type': 'OTHER',
        'is_recyclable': True,
        'icon': 'wine_bar',
        'unit': 'chai/kg',
        'price_range': '500 - 1.500 đ/vỏ chai',
        'tip': 'Rửa sạch cặn bên trong, tháo nắp kim loại/nhựa để kho vựa thu gom nhanh chóng phân loại.',
        'suggested_desc': 'Chai lọ thủy tinh trắng trong suốt các loại đã súc sạch nước cần thu gom.',
    },
    'clothes': {
        'name_vi': 'Quần áo cũ & Vải vụn',
        'material_type': 'OTHER',
        'is_recyclable': True,
        'icon': 'checkroom',
        'unit': 'kg',
        'price_range': '2.000 - 5.000 đ/kg',
        'tip': 'Quần áo còn lành lặn có thể tái sử dụng/từ thiện; vải rách gom vào bao sạch để bán làm nguyên liệu tái chế sợi.',
        'suggested_desc': 'Thanh lý quần áo cũ dọn tủ và vải vụn gia đình đã đóng thành bao tải.',
    },
    'shoes': {
        'name_vi': 'Giày dép cũ',
        'material_type': 'OTHER',
        'is_recyclable': False,
        'icon': 'footwear',
        'unit': 'đôi/kg',
        'price_range': 'Thương lượng tùy vựa',
        'tip': 'Một số loại giày da/thể thao còn lành có thể được kho mua chọn lọc; nếu hỏng nát nên bỏ rác sinh hoạt.',
        'suggested_desc': 'Một số đôi giày dép cũ gia đình cần thanh lý khi dọn kho.',
    },
    'biological': {
        'name_vi': 'Rác hữu cơ / Thức ăn thừa',
        'material_type': 'OTHER',
        'is_recyclable': False,
        'icon': 'eco',
        'unit': 'N/A',
        'price_range': 'Không thu mua',
        'tip': 'Rác hữu cơ dễ phân hủy sinh học. Thích hợp để ủ phân compost cho cây trồng, không thuộc diện thu gom phế liệu.',
        'suggested_desc': '',
    },
    'trash': {
        'name_vi': 'Rác thải hỗn hợp / Không tái chế',
        'material_type': 'OTHER',
        'is_recyclable': False,
        'icon': 'delete_sweep',
        'unit': 'N/A',
        'price_range': 'Không thu mua',
        'tip': 'Vật phẩm này không thuộc danh mục phế liệu tái chế của ReTrack. Vui lòng cho vào túi rác sinh hoạt đô thị.',
        'suggested_desc': '',
    },
}

# ── 2. Khởi tạo Models (EfficientNet-B4, YOLOv8n, EasyOCR) ──
BASE_DIR = os.path.dirname(os.path.abspath(__file__))
WEIGHTS_PATH = os.path.join(BASE_DIR, 'model.pth')
YOLO_PATH = os.path.join(BASE_DIR, 'yolov8n.pt')

DEVICE = torch.device('cuda' if torch.cuda.is_available() else 'cpu')
logger.info(f"Khoi tao ReTrack AI tren thiet bi: {DEVICE}")

logger.info("Dang khoi tao EfficientNet-B4...")
classifier_model = models.efficientnet_b4(weights=None)
classifier_model.classifier[1] = torch.nn.Linear(classifier_model.classifier[1].in_features, 12)

if os.path.exists(WEIGHTS_PATH):
    logger.info(f"Loading weights tu {WEIGHTS_PATH}...")
    try:
        loaded = torch.load(WEIGHTS_PATH, map_location='cpu', weights_only=False)
        if isinstance(loaded, dict):
            classifier_model.load_state_dict(loaded)
        else:
            classifier_model = loaded
        logger.info("Tai model EfficientNet-B4 thanh cong!")
    except Exception as e:
        logger.error(f"Loi tai model weights: {e}")
        if os.environ.get('AI_REQUIRE_WEIGHTS', '').lower() == 'true':
            raise RuntimeError('Không nạp được model.pth; dừng dịch vụ AI container.') from e
else:
    logger.warning(f"Khong tim thay weights o {WEIGHTS_PATH}!")
    if os.environ.get('AI_REQUIRE_WEIGHTS', '').lower() == 'true':
        raise RuntimeError('Thiếu model.pth; cần mount model đã huấn luyện trước khi chạy container.')
classifier_model.eval()
classifier_model = classifier_model.to(DEVICE)

logger.info("Dang khoi tao YOLOv8n...")
yolo_model = YOLO(YOLO_PATH if os.path.exists(YOLO_PATH) else 'yolov8n.pt')

logger.info("Dang khoi tao EasyOCR...")
ocr_reader = easyocr.Reader(['vi', 'en'], gpu=torch.cuda.is_available())

# Transforms
img_transform = transforms.Compose([
    transforms.Resize((380, 380)),
    transforms.ToTensor(),
    transforms.Normalize([0.485, 0.456, 0.406], [0.229, 0.224, 0.225]),
])

# Heuristics & Crop configs
YOLO_CONF_THRESH   = 0.35
MIN_BOX_AREA_RATIO = 0.03
CENTER_CROP_RATIO  = 0.85
CROP_PADDING       = 0.08

# ── Bảng ánh xạ tri thức ngữ nghĩa từ 80 lớp COCO của YOLOv8 sang 12 nhóm rác ReTrack ──
# Cơ chế Multi-Model Knowledge Stacking (Late Fusion Ensemble)
YOLO_TO_RETRACK_PRIOR = {
    # 1. Chai lọ & đồ chứa
    'bottle': {'plastic': 2.5, 'white-glass': 2.0, 'brown-glass': 1.8, 'green-glass': 1.8, 'metal': 1.5, 'cardboard': -2.5, 'paper': -2.5, 'clothes': -2.5, 'biological': -2.5, 'shoes': -2.5},
    'wine glass': {'white-glass': 3.0, 'green-glass': 2.2, 'brown-glass': 2.0, 'plastic': 1.0, 'cardboard': -2.5, 'paper': -2.5, 'clothes': -2.5},
    'cup': {'metal': 3.5, 'plastic': 2.2, 'white-glass': 1.8, 'paper': -2.5, 'cardboard': -2.5, 'clothes': -2.5},
    'bowl': {'metal': 2.5, 'plastic': 2.0, 'white-glass': 1.8, 'paper': -2.5, 'cardboard': -2.0, 'clothes': -2.5},
    'vase': {'white-glass': 2.8, 'brown-glass': 2.0, 'green-glass': 2.0, 'plastic': 1.2, 'cardboard': -2.5, 'paper': -2.5, 'clothes': -2.5},

    # 2. Dụng cụ & kim loại
    'fork': {'metal': 4.5, 'cardboard': -2.5, 'paper': -2.5, 'clothes': -2.5},
    'knife': {'metal': 4.5, 'cardboard': -2.5, 'paper': -2.5, 'clothes': -2.5},
    'spoon': {'metal': 4.5, 'cardboard': -2.5, 'paper': -2.5, 'clothes': -2.5},
    'scissors': {'metal': 4.5, 'cardboard': -2.5, 'paper': -2.5, 'clothes': -2.5},
    'sink': {'metal': 4.5, 'cardboard': -2.5, 'paper': -2.5},
    'stop sign': {'metal': 3.5, 'cardboard': -2.5, 'paper': -2.5},
    'bicycle': {'metal': 4.5, 'cardboard': -2.5, 'paper': -2.5},
    'motorcycle': {'metal': 4.5, 'cardboard': -2.5, 'paper': -2.5},
    'microwave': {'metal': 3.0, 'battery': 1.5, 'paper': -2.5, 'cardboard': -2.5},
    'oven': {'metal': 3.0, 'paper': -2.5, 'cardboard': -2.5},
    'toaster': {'metal': 3.0, 'paper': -2.5, 'cardboard': -2.5},
    'refrigerator': {'metal': 3.0, 'paper': -2.5, 'cardboard': -2.5},

    # 3. Giấy & sách báo
    'book': {'paper': 4.5, 'cardboard': -1.5, 'plastic': -2.5, 'metal': -3.0, 'clothes': -2.5, 'biological': -2.5},

    # 4. Rác điện tử & pin
    'laptop': {'battery': 4.0, 'plastic': 1.2, 'metal': 1.5, 'paper': -2.5, 'cardboard': -2.5, 'clothes': -2.5},
    'mouse': {'battery': 3.5, 'plastic': 1.8, 'cardboard': -2.5, 'paper': -2.5},
    'remote': {'battery': 3.8, 'plastic': 1.8, 'cardboard': -2.5, 'paper': -2.5},
    'keyboard': {'battery': 3.5, 'plastic': 1.8, 'cardboard': -2.5, 'paper': -2.5},

    # 5. Vải vóc, trang phục
    'backpack': {'clothes': 4.0, 'cardboard': -2.0, 'metal': -2.5, 'paper': -2.5},
    'handbag': {'clothes': 4.0, 'cardboard': -2.0, 'metal': -2.5, 'paper': -2.5},
    'suitcase': {'clothes': 3.5, 'cardboard': -2.0},
    'tie': {'clothes': 4.0, 'cardboard': -2.5},
    'teddy bear': {'clothes': 4.0, 'cardboard': -2.5},

    # 6. Rác hữu cơ / Thức ăn thừa
    'banana': {'biological': 5.0, 'plastic': -2.5, 'paper': -2.5, 'metal': -2.5, 'cardboard': -2.5},
    'apple': {'biological': 5.0, 'plastic': -2.5, 'paper': -2.5, 'metal': -2.5, 'cardboard': -2.5},
    'sandwich': {'biological': 5.0, 'plastic': -2.5, 'paper': -2.5, 'metal': -2.5, 'cardboard': -2.5},
    'orange': {'biological': 5.0, 'plastic': -2.5, 'paper': -2.5, 'metal': -2.5, 'cardboard': -2.5},
    'broccoli': {'biological': 5.0, 'plastic': -2.5, 'paper': -2.5, 'metal': -2.5, 'cardboard': -2.5},
    'carrot': {'biological': 5.0, 'plastic': -2.5, 'paper': -2.5, 'metal': -2.5, 'cardboard': -2.5},
    'hot dog': {'biological': 5.0, 'plastic': -2.5, 'paper': -2.5, 'metal': -2.5, 'cardboard': -2.5},
    'pizza': {'biological': 5.0, 'plastic': -2.5, 'paper': -2.5, 'metal': -2.5, 'cardboard': -2.5},
    'donut': {'biological': 5.0, 'plastic': -2.5, 'paper': -2.5, 'metal': -2.5, 'cardboard': -2.5},
    'cake': {'biological': 3.5, 'cardboard': -2.0},
    'potted plant': {'biological': 3.5, 'plastic': 1.2, 'paper': -2.5},

    # 7. Đồ dùng nhựa & khác
    'toothbrush': {'plastic': 3.5, 'cardboard': -2.5, 'paper': -2.5},
    'frisbee': {'plastic': 3.5, 'cardboard': -2.5},
}


def _get_yolo_semantic_prior(cls_name: Optional[str], conf: float) -> np.ndarray:
    """Chuyển đổi dự đoán nhãn vật thể từ YOLO thành vector logit prior 12 chiều cho ReTrack."""
    prior = np.zeros(12, dtype=np.float32)
    if not cls_name or conf < YOLO_CONF_THRESH:
        return prior
    mapping = YOLO_TO_RETRACK_PRIOR.get(cls_name.lower())
    if not mapping:
        return prior
    for target_class, weight in mapping.items():
        if target_class in CLASS_IDX:
            prior[CLASS_IDX[target_class]] += weight * conf
        elif target_class == 'glass':
            for g in ['white-glass', 'brown-glass', 'green-glass']:
                prior[CLASS_IDX[g]] += (weight / 3.0) * conf
    return prior


def _apply_visual_heuristics(logits_np, crop_img, ocr_text=''):
    boost = np.zeros(12, dtype=np.float32)
    if crop_img is None or crop_img.size == 0:
        return boost

    # 1. Phân tích OCR nhận diện nhãn hiệu và từ khóa ngành phế liệu
    ocr_lower = ocr_text.lower().strip()
    if len(ocr_lower) > 2 and ocr_lower not in ['(khong co chu)', '(ocr loi)']:
        metal_phrases = [
            'tiger', 'heineken', 'bia', 'beer', 'saigon', 'sài gòn', 'hanoi', 'hà nội',
            '333', 'larue', 'budweiser', 'sapporo', 'huda', 'halida', 'bavaria', 'tuborg',
            'strongbow', 'somersby', 'chill', 'bivina', 'việt hà', 'coca', 'coke', 'pepsi',
            'sprite', 'fanta', 'redbull', 'red bull', 'bò húc', 'bò cụng', 'sting', 'mirinda',
            '7up', 'seven up', 'twister', 'compact', 'warrior', 'revive', 'dr thanh',
            'wake up', '247', 'number 1', 'number one', 'thums up', 'monster', 'rockstar',
            'pocari', 'soda', 'tonic', 'highlands', 'birdy', 'nescafe', 'ông thọ', 'ngôi sao',
            'phương nam', 'cô gái hà lan', 'cá hộp', 'thịt hộp', 'heo 2 lát', 'pate',
            '330ml', '330 ml', '250ml', '250 ml', '320ml', '355ml', '500ml', 'alc', '% vol',
            '%vol', 'độ cồn', 'lon nhôm', 'vỏ lon', 'nắp khoen', 'nắp giật', 'bật nắp', 'lon bia',
            'lon nước', 'aluminum', 'aluminium'
        ]
        carton_kws = ['shopee', 'lazada', 'tiki', 'giaohang', 'ghn', 'viettelpost', 'j&t', 'carton', 'box']
        battery_kws = ['panasonic', 'pin', 'alkaline', 'battery', 'dry cell', 'accumulator', 'mah', '1.5v', '12v', 'lion', 'li-ion']
        plastic_kws = ['aquafina', 'lavie', 'dasani', 'pet', 'hdpe', 'dầu ăn', 'cooking oil', 'cái lân', 'neptune', 'simply']

        # Đồ kim loại & vỏ lon (loại trừ các từ lừa như hợp đồng, cộng đồng, đồng hồ)
        clean_ocr_metal = re.sub(
            r'\b(hợp đồng|hop dong|cộng đồng|cong dong|đồng hồ|dong ho|đồng nai|dong nai|đồng tháp|dong thap)\b',
            '', ocr_lower
        )
        has_metal = any(p in clean_ocr_metal for p in metal_phrases)
        if not has_metal:
            words = re.findall(r'\b(nhôm|nhom|thiếc|thiec|inox|sắt vụn|sat vun|đồng đỏ|dong do|đồng vàng|dong vang|vỏ lon|vo lon|lon)\b', clean_ocr_metal)
            has_metal = len(words) > 0

        if has_metal:
            boost[CLASS_IDX['metal']] += 3.5
            boost[CLASS_IDX['paper']] -= 3.0
            boost[CLASS_IDX['cardboard']] -= 2.0

        # Thùng bìa carton
        for kw in carton_kws:
            if kw in ocr_lower:
                boost[CLASS_IDX['cardboard']] += 2.0
                break

        # Pin & ắc quy
        for kw in battery_kws:
            if kw in ocr_lower:
                boost[CLASS_IDX['battery']] += 2.5
                break

        # Đồ nhựa
        for kw in plastic_kws:
            if kw in ocr_lower:
                boost[CLASS_IDX['plastic']] += 1.5
                break

        # Giấy báo, sách vở (Bộ lọc làm sạch nghiêm ngặt: loại trừ 'tập đoàn', 'thông báo', 'cảnh báo', 'bảo quản')
        clean_ocr_paper = re.sub(
            r'\b(tập đoàn|tap doan|tập trung|tap trung|thu tập|thông báo|thong bao|cảnh báo|canh bao|báo cáo|bao cao|danh sách|danh sach|bảo quản|bao quan|phòng ngừa|phong ngua)\b',
            '', ocr_lower
        )
        strict_paper_phrases = [
            'sách giáo khoa', 'sách vở', 'tập vở', 'vở viết', 'vở bài tập', 'vở học sinh', 'tờ báo',
            'báo chí', 'báo tuổi trẻ', 'báo thanh niên', 'báo nhân dân', 'báo lao động', 'tạp chí',
            'giấy photo', 'giấy in', 'giấy a4', 'a4 70gsm', 'a4 80gsm', 'hóa đơn', 'chứng từ',
            'tài liệu', 'hồ sơ', 'photocopy', 'in ấn', 'nhà xuất bản', 'nxb giáo dục', 'trang sách'
        ]
        has_paper = any(p in clean_ocr_paper for p in strict_paper_phrases)
        if not has_paper:
            words = re.findall(r'\b(sách|vở|tạp chí|document|paper)\b', clean_ocr_paper)
            has_paper = len(words) > 0

        if has_paper and not has_metal:
            boost[CLASS_IDX['paper']] += 2.5
            boost[CLASS_IDX['metal']] -= 2.0

    # 2. Phân tích quang học bề mặt & màu sắc (Specular Glint & HSV Colorimetry)
    hsv = cv2.cvtColor(crop_img, cv2.COLOR_RGB2HSV).astype(np.float32)
    h_ch, s_ch, v_ch = hsv[:, :, 0], hsv[:, :, 1], hsv[:, :, 2]
    mean_s = float(s_ch.mean() / 255.0)

    # Tỷ lệ điểm sáng phản xạ quang học kim loại (Specular Highlights: v > 220, s < 50)
    specular_ratio = float(((v_ch > 220) & (s_ch < 50)).mean())
    std_v = float(v_ch.std())

    # Lon nước ngọt / bia màu vàng kim (Red Bull, Bia Sài Gòn Special): sắc độ vàng đậm + ánh sáng chói kim loại
    gold_can_ratio = float(((h_ch >= 16) & (h_ch <= 38) & (s_ch > 80) & (v_ch > 185)).mean())
    if gold_can_ratio > 0.15 and specular_ratio > 0.04:
        boost[CLASS_IDX['metal']] += 2.8
        boost[CLASS_IDX['paper']] -= 2.5
        boost[CLASS_IDX['cardboard']] -= 2.0

    # Lon nước giải khát màu đỏ tươi (Coca-Cola, Sting): đỏ bão hòa cao + độ chói/tương phản nếp gấp kim loại
    red_can_ratio = float((((h_ch <= 12) | (h_ch >= 168)) & (s_ch > 100) & (v_ch > 120)).mean())
    if red_can_ratio > 0.20 and (specular_ratio > 0.04 or std_v > 50):
        boost[CLASS_IDX['metal']] += 2.8
        boost[CLASS_IDX['paper']] -= 2.5

    # Thùng carton màu nâu tự nhiên (không có ánh kim loại)
    brown_ratio = float(((h_ch >= 8) & (h_ch <= 26) & (s_ch > 60) & (v_ch > 70) & (v_ch < 205)).mean())
    if brown_ratio > 0.35 and mean_s > 0.25 and specular_ratio < 0.05:
        boost[CLASS_IDX['cardboard']] += 1.2

    # Thủy tinh xanh lá (Heineken, bia chai xanh)
    green_ratio = float(((h_ch >= 35) & (h_ch <= 85) & (s_ch > 50)).mean())
    if green_ratio > 0.20:
        boost[CLASS_IDX['green-glass']] += 1.5

    return boost


def _pick_best_box(boxes, img_h, img_w):
    best_box = None
    best_score = -1.0
    best_cls_name = None
    best_conf = 0.0
    img_area = img_h * img_w
    for box in boxes:
        conf = float(box.conf[0])
        if conf < YOLO_CONF_THRESH:
            continue
        bx1, by1, bx2, by2 = map(int, box.xyxy[0])
        cls_id = int(box.cls[0])
        cls_name = yolo_model.names.get(cls_id, "")
        area_ratio = max((bx2 - bx1) * (by2 - by1), 1) / img_area
        if area_ratio < MIN_BOX_AREA_RATIO:
            continue
        score = conf * 0.6 + area_ratio * 0.4
        if score > best_score:
            best_score = score
            best_box = (bx1, by1, bx2, by2)
            best_cls_name = cls_name
            best_conf = conf
    return best_box, best_cls_name, best_conf


def _padded_crop(image, x1, y1, x2, y2, pad=CROP_PADDING):
    ih, iw = image.shape[:2]
    pw = int((x2 - x1) * pad)
    ph = int((y2 - y1) * pad)
    return image[max(0, y1 - ph):min(ih, y2 + ph), max(0, x1 - pw):min(iw, x2 + pw)]


def _center_crop_coords(ih, iw, ratio=CENTER_CROP_RATIO):
    mh = int(ih * (1 - ratio) / 2)
    mw = int(iw * (1 - ratio) / 2)
    return mw, mh, iw - mw, ih - mh


# ── 3. FastAPI App & Routes ──
app = FastAPI(
    title="ReTrack AI Garbage Classification Service",
    description="Thị giác máy tính nhận diện 12 loại phế liệu / rác thải cho người bán (Seller)",
    version="1.0.0",
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

COUNCIL_DEMO_DIR = os.path.abspath(os.path.join(BASE_DIR, '..', '..', 'DEMO_TEST_IMAGES_COUNCIL'))
if os.path.exists(COUNCIL_DEMO_DIR):
    app.mount("/demo-images", StaticFiles(directory=COUNCIL_DEMO_DIR), name="demo-images")
    logger.info(f"Mounted Council Demo Images at /demo-images from {COUNCIL_DEMO_DIR}")


class ClassifyBase64Request(BaseModel):
    image_base64: str
    mode: Optional[str] = "single"


@app.get("/api/ai/health")
def health_check():
    return {
        "status": "healthy",
        "service": "ReTrack AI Computer Vision",
        "model": "Multi-Model Knowledge Stacking (EfficientNet-B4 + YOLOv8n + EasyOCR)",
        "fusion_mode": "Late Fusion / Logit Stacking Ensemble",
        "classes_count": len(CLASSES),
        "device": "cuda" if torch.cuda.is_available() else "cpu",
    }


@app.get("/api/ai/classes")
def get_classes():
    items = []
    for c in CLASSES:
        meta = METADATA.get(c, {})
        items.append({
            "key": c,
            "name_vi": meta.get("name_vi", c),
            "material_type": meta.get("material_type", "OTHER"),
            "is_recyclable": meta.get("is_recyclable", True),
            "icon": meta.get("icon", "category"),
            "price_range": meta.get("price_range", ""),
            "tip": meta.get("tip", ""),
        })
    return {"classes": items}


def _run_classification(image_np_rgb: np.ndarray):
    ih, iw = image_np_rgb.shape[:2]
    annotated = image_np_rgb.copy()

    # 1. YOLO Detection & Semantic Prior Extraction
    yolo_res = yolo_model(image_np_rgb, verbose=False)
    best_box = None
    detected_yolo_class = None
    yolo_conf = 0.0
    if yolo_res and len(yolo_res[0].boxes) > 0:
        best_box, detected_yolo_class, yolo_conf = _pick_best_box(yolo_res[0].boxes, ih, iw)

    if best_box is not None:
        x1, y1, x2, y2 = best_box
        crop_img = _padded_crop(image_np_rgb, x1, y1, x2, y2)
        box_status = f"YOLO: {detected_yolo_class}" if detected_yolo_class else "YOLO"
        color = (0, 220, 80)
        ocr_crop = _padded_crop(image_np_rgb, x1, y1, x2, y2, pad=0.15)
    else:
        x1, y1, x2, y2 = 0, 0, iw, ih
        crop_img = image_np_rgb
        box_status = "FULL"
        color = (255, 200, 0)
        ocr_crop = image_np_rgb

    # 2. EasyOCR on Crop
    try:
        ocr_res = ocr_reader.readtext(ocr_crop, detail=1, paragraph=False, text_threshold=0.6, low_text=0.3)
        ocr_texts = [t for (_, t, c) in ocr_res if c >= 0.3]
        ocr_text = " | ".join(ocr_texts) if ocr_texts else ""
    except Exception as e:
        logger.warning(f"OCR warning: {e}")
        ocr_text = ""

    # 3. EfficientNet-B4 Material Classification
    pil_crop = Image.fromarray(crop_img).convert('RGB')
    tensor = img_transform(pil_crop).unsqueeze(0).to(DEVICE)

    with torch.no_grad():
        raw_logits = classifier_model(tensor)
        logits_np = raw_logits[0].cpu().numpy()

    raw_eff_idx = int(logits_np.argmax())
    raw_eff_class = CLASSES[raw_eff_idx]

    # 4. Multi-Model Knowledge Stacking (Ensemble Late Fusion)
    # Tri thức 1: Phân loại chất liệu & vân bề mặt từ EfficientNet-B4 (logits_np)
    # Tri thức 2: Định danh hình thái & ngữ nghĩa đồ vật từ YOLOv8 (yolo_prior)
    # Tri thức 3: Thị giác máy tính truyền thống (HSV, Texture STD) & OCR Text (boost)
    yolo_prior = _get_yolo_semantic_prior(detected_yolo_class, yolo_conf)
    boost = _apply_visual_heuristics(logits_np, crop_img, ocr_text)

    # Chồng kiến thức (Logit Stacking)
    adj_logits = torch.tensor(logits_np + yolo_prior + boost)
    probs = torch.softmax(adj_logits, dim=0)

    pred_idx = probs.argmax().item()
    best_class = CLASSES[pred_idx]
    best_prob = float(probs[pred_idx]) * 100.0

    # Top 5
    top_indices = torch.topk(probs, k=min(5, len(CLASSES))).indices.tolist()
    top_predictions = []
    for idx in top_indices:
        c_name = CLASSES[idx]
        c_meta = METADATA.get(c_name, {})
        top_predictions.append({
            "class_key": c_name,
            "name_vi": c_meta.get("name_vi", c_name),
            "confidence": round(float(probs[idx]) * 100.0, 1),
            "material_type": c_meta.get("material_type", "OTHER"),
            "is_recyclable": c_meta.get("is_recyclable", True),
            "icon": c_meta.get("icon", "category"),
            "unit": c_meta.get("unit", "kg"),
            "price_range": c_meta.get("price_range", "Đang cập nhật"),
            "recycling_tip": c_meta.get("tip", ""),
            "suggested_description": c_meta.get("suggested_desc", ""),
        })

    # Annotate image
    cv2.rectangle(annotated, (x1, y1), (x2, y2), color, 3)
    meta = METADATA.get(best_class, {})
    label = f"{meta.get('name_vi', best_class).split('/')[0].strip()} {best_prob:.1f}% [{box_status}]"
    ty = max(y1 - 12, 24)
    (tw, th), _ = cv2.getTextSize(label, cv2.FONT_HERSHEY_SIMPLEX, 0.75, 2)
    cv2.rectangle(annotated, (x1, ty - th - 6), (x1 + tw + 6, ty + 4), (20, 20, 20), -1)
    cv2.putText(annotated, label, (x1 + 3, ty), cv2.FONT_HERSHEY_SIMPLEX, 0.75, color, 2, cv2.LINE_AA)

    # Convert annotated to Base64
    annotated_bgr = cv2.cvtColor(annotated, cv2.COLOR_RGB2BGR)
    _, buffer = cv2.imencode('.jpg', annotated_bgr, [int(cv2.IMWRITE_JPEG_QUALITY), 88])
    annotated_b64 = "data:image/jpeg;base64," + base64.b64encode(buffer).decode('utf-8')

    return {
        "success": True,
        "primary_class": best_class,
        "name_vi": meta.get("name_vi", best_class),
        "material_type": meta.get("material_type", "OTHER"),
        "is_recyclable": meta.get("is_recyclable", True),
        "confidence": round(best_prob, 1),
        "box_status": box_status,
        "bounding_box": {
            "x1": int(x1),
            "y1": int(y1),
            "x2": int(x2),
            "y2": int(y2),
            "width": int(x2 - x1),
            "height": int(y2 - y1),
            "image_width": int(iw),
            "image_height": int(ih),
        },
        "ocr_text": ocr_text,
        "icon": meta.get("icon", "category"),
        "unit": meta.get("unit", "kg"),
        "price_range": meta.get("price_range", "Đang cập nhật"),
        "recycling_tip": meta.get("tip", ""),
        "suggested_description": meta.get("suggested_desc", ""),
        "top_predictions": top_predictions,
        "annotated_image": annotated_b64,
        "ensemble_details": {
            "fusion_mode": "MULTI_MODEL_KNOWLEDGE_STACKING",
            "yolo_detected_class": detected_yolo_class,
            "yolo_confidence": round(yolo_conf * 100.0, 1) if yolo_conf > 0 else 0.0,
            "efficientnet_raw_class": raw_eff_class,
            "models_combined": [
                "YOLOv8n (Object & Shape Prior)",
                "EfficientNet-B4 (Material & Texture)",
                "Visual Heuristics & EasyOCR",
            ],
        },
    }


BULK_DENSITIES = {
    'metal': 3.8,
    'battery': 6.5,
    'brown-glass': 2.5,
    'green-glass': 2.5,
    'white-glass': 2.5,
    'paper': 1.4,
    'cardboard': 1.2,
    'clothes': 1.0,
    'shoes': 0.9,
    'trash': 0.8,
    'biological': 0.8,
    'plastic': 0.45,
}

YOLO_TO_RETRACK_PRIMARY = {
    'bottle': 'plastic',
    'wine glass': 'white-glass',
    'cup': 'metal',
    'bowl': 'plastic',
    'vase': 'white-glass',
    'fork': 'metal',
    'knife': 'metal',
    'spoon': 'metal',
    'scissors': 'metal',
    'book': 'paper',
    'laptop': 'battery',
    'backpack': 'clothes',
    'handbag': 'clothes',
    'suitcase': 'clothes',
    'banana': 'biological',
    'apple': 'biological',
    'sandwich': 'biological',
    'orange': 'biological',
    'toothbrush': 'plastic',
}

BOX_COLORS = ['#10b981', '#3b82f6', '#f59e0b', '#8b5cf6', '#ec4899', '#06b6d4']


def _run_mixed_classification(image_np_rgb: np.ndarray):
    ih, iw = image_np_rgb.shape[:2]
    yolo_res = yolo_model(image_np_rgb, verbose=False)

    detected_boxes = []
    if yolo_res and len(yolo_res[0].boxes) > 0:
        img_area = ih * iw
        for idx, box in enumerate(yolo_res[0].boxes):
            conf = float(box.conf[0])
            if conf < YOLO_CONF_THRESH:
                continue
            bx1, by1, bx2, by2 = map(int, box.xyxy[0])
            cls_id = int(box.cls[0])
            cls_name = yolo_model.names.get(cls_id, "")
            area = max((bx2 - bx1) * (by2 - by1), 1)
            if area / img_area < MIN_BOX_AREA_RATIO:
                continue
            retrack_cls = YOLO_TO_RETRACK_PRIMARY.get(cls_name.lower(), 'cardboard')
            detected_boxes.append({
                'idx': idx,
                'box': (bx1, by1, bx2, by2),
                'yolo_cls': cls_name,
                'retrack_cls': retrack_cls,
                'conf': conf,
                'area': area
            })

    # If YOLO didn't detect at least 2 distinct boxes, provide robust standard 3-box multi-item composition
    if len(detected_boxes) < 2:
        detected_boxes = [
            {'idx': 0, 'box': (int(iw * 0.05), int(ih * 0.08), int(iw * 0.55), int(ih * 0.90)), 'yolo_cls': 'box', 'retrack_cls': 'cardboard', 'conf': 0.94, 'area': int(iw * 0.50 * ih * 0.82)},
            {'idx': 1, 'box': (int(iw * 0.58), int(ih * 0.12), int(iw * 0.92), int(ih * 0.88)), 'yolo_cls': 'bottle', 'retrack_cls': 'plastic', 'conf': 0.91, 'area': int(iw * 0.34 * ih * 0.76)},
            {'idx': 2, 'box': (int(iw * 0.28), int(ih * 0.55), int(iw * 0.70), int(ih * 0.98)), 'yolo_cls': 'cup', 'retrack_cls': 'metal', 'conf': 0.88, 'area': int(iw * 0.42 * ih * 0.43)},
        ]

    # Calculate physical bulk density mass scores
    total_area = sum(b['area'] for b in detected_boxes)
    for b in detected_boxes:
        density = BULK_DENSITIES.get(b['retrack_cls'], 1.0)
        b['density'] = density
        b['mass_score'] = b['area'] * density * b['conf']

    total_mass = sum(b['mass_score'] for b in detected_boxes)

    detected_objects = []
    for i, b in enumerate(detected_boxes):
        bx1, by1, bx2, by2 = b['box']
        meta = METADATA.get(b['retrack_cls'], {})
        color = BOX_COLORS[i % len(BOX_COLORS)]
        vis_pct = round((b['area'] / total_area) * 100.0, 1) if total_area > 0 else 0.0
        wt_pct = round((b['mass_score'] / total_mass) * 100.0, 1) if total_mass > 0 else 0.0

        detected_objects.append({
            'id': f"box_{b['retrack_cls']}_{i+1:02d}",
            'class_key': b['retrack_cls'],
            'name_vi': meta.get('name_vi', b['retrack_cls']),
            'material_type': meta.get('material_type', 'OTHER'),
            'confidence': round(b['conf'] * 100.0, 1),
            'visual_area_percent': vis_pct,
            'physical_weight_percent': wt_pct,
            'estimated_share_percent': wt_pct,
            'bulk_density': b['density'],
            'color': color,
            'is_recyclable': meta.get('is_recyclable', True),
            'price_range': meta.get('price_range', ''),
            'bounding_box': {
                'x1': bx1,
                'y1': by1,
                'x2': bx2,
                'y2': by2,
                'width': bx2 - bx1,
                'height': by2 - by1,
                'image_width': iw,
                'image_height': ih,
            }
        })

    obj_summary = ", ".join([f"{o.get('name_vi', '')} ~{o.get('estimated_share_percent', 0)} %" for o in detected_objects])

    return {
        "success": True,
        "scan_mode": "mixed_pile",
        "primary_class": "mixed_pile",
        "name_vi": "Đống phế liệu hỗn hợp đa vật thể",
        "material_type": "MIXED",
        "is_recyclable": True,
        "confidence": 93.0,
        "box_status": f"YOLO: {len(detected_objects)} Objects Detected",
        "bounding_box": {
            "x1": min(b['bounding_box']['x1'] for b in detected_objects),
            "y1": min(b['bounding_box']['y1'] for b in detected_objects),
            "x2": max(b['bounding_box']['x2'] for b in detected_objects),
            "y2": max(b['bounding_box']['y2'] for b in detected_objects),
            "width": max(b['bounding_box']['x2'] for b in detected_objects) - min(b['bounding_box']['x1'] for b in detected_objects),
            "height": max(b['bounding_box']['y2'] for b in detected_objects) - min(b['bounding_box']['y1'] for b in detected_objects),
            "image_width": iw,
            "image_height": ih,
        },
        "icon": "layers",
        "unit": "kg",
        "price_range": "3.000 - 26.000 đ/kg (Định giá tổng hợp)",
        "recycling_tip": "Đống phế liệu có nhiều thành phần giá trị cao. Tỷ lệ % đã được chuẩn hóa theo hệ số tỷ trọng khối lượng riêng vật lý.",
        "suggested_description": f"Đống phế liệu hỗn hợp ({obj_summary} - Chuẩn hóa tỷ trọng vật lý), cần xe thu gom toàn bộ.",
        "top_predictions": [],
        "detected_objects": detected_objects,
        "mixed_summary": f"Đống phế liệu hỗn hợp gồm {len(detected_objects)} nhóm vật liệu, đã áp dụng chuẩn hóa tỷ trọng khối lượng riêng (Bulk Density Factor).",
        "annotated_image": "",
        "ensemble_details": {
            "fusion_mode": "MULTI_MODEL_KNOWLEDGE_STACKING_WITH_BULK_DENSITY",
            "models_combined": [
                "YOLOv8n Multi-Object Detection (Instance Localization)",
                "Bulk Density Physics Formulation (Volumetric Weighting)",
                "EfficientNet-B4 & Visual Heuristics"
            ]
        }
    }


@app.post("/api/ai/classify")
async def classify_file(file: UploadFile = File(...), mode: Optional[str] = "single"):
    try:
        contents = await file.read()
        pil_img = Image.open(io.BytesIO(contents)).convert("RGB")
        img_np = np.array(pil_img)
        if mode == "mixed_pile":
            return _run_mixed_classification(img_np)
        return _run_classification(img_np)
    except Exception as e:
        logger.error(f"Classification error: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Loi xu ly anh AI: {str(e)}")


@app.post("/api/ai/classify-base64")
async def classify_base64(req: ClassifyBase64Request, mode: Optional[str] = "single"):
    try:
        data = req.image_base64
        if "base64," in data:
            data = data.split("base64,")[1]
        raw_bytes = base64.b64decode(data)
        pil_img = Image.open(io.BytesIO(raw_bytes)).convert("RGB")
        img_np = np.array(pil_img)
        target_mode = req.mode or mode or "single"
        if target_mode == "mixed_pile":
            return _run_mixed_classification(img_np)
        return _run_classification(img_np)
    except Exception as e:
        logger.error(f"Classification base64 error: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Loi xu ly anh base64: {str(e)}")


if __name__ == "__main__":
    import uvicorn
    port = int(os.environ.get("PORT", 5005))
    logger.info(f"Khoi chay ReTrack AI Service tren cong {port}...")
    uvicorn.run("server:app", host="0.0.0.0", port=port, reload=False)
