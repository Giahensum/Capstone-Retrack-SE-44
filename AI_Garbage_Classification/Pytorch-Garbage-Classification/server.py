import os
import io
import time
import base64
import logging
from typing import Optional, List
from fastapi import FastAPI, File, UploadFile, HTTPException, Form
from fastapi.middleware.cors import CORSMiddleware
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

logger.info("Dang khoi tao YOLOv8n...")
yolo_model = YOLO(YOLO_PATH if os.path.exists(YOLO_PATH) else 'yolov8n.pt')

logger.info("Dang khoi tao EasyOCR...")
ocr_reader = easyocr.Reader(['vi', 'en'], gpu=False)

# Transforms
img_transform = transforms.Compose([
    transforms.Resize((380, 380)),
    transforms.ToTensor(),
    transforms.Normalize([0.485, 0.456, 0.406], [0.229, 0.224, 0.225]),
])

# Heuristics & Crop configs
YOLO_CONF_THRESH   = 0.20
MIN_BOX_AREA_RATIO = 0.02
CENTER_CROP_RATIO  = 0.65
CROP_PADDING       = 0.08

# ── Bảng ánh xạ tri thức ngữ nghĩa từ 80 lớp COCO của YOLOv8 sang 12 nhóm rác ReTrack ──
# Cơ chế Multi-Model Knowledge Stacking (Late Fusion Ensemble)
YOLO_TO_RETRACK_PRIOR = {
    # 1. Chai lọ & đồ chứa
    'bottle': {'plastic': 3.2, 'white-glass': 2.2, 'brown-glass': 1.8, 'green-glass': 1.8, 'cardboard': -3.5, 'paper': -3.0, 'clothes': -3.0, 'biological': -3.0, 'shoes': -3.0},
    'wine glass': {'white-glass': 3.5, 'green-glass': 2.5, 'brown-glass': 2.2, 'plastic': 1.0, 'cardboard': -3.5, 'paper': -3.0, 'clothes': -3.0},
    'cup': {'plastic': 2.8, 'paper': 2.2, 'white-glass': 1.8, 'metal': 1.2, 'cardboard': -2.0, 'clothes': -3.0},
    'bowl': {'white-glass': 2.2, 'plastic': 2.2, 'metal': 1.8, 'cardboard': -2.5, 'clothes': -3.0},
    'vase': {'white-glass': 3.0, 'brown-glass': 2.2, 'green-glass': 2.2, 'plastic': 1.5, 'cardboard': -3.0, 'paper': -3.0, 'clothes': -3.0},

    # 2. Dụng cụ kim loại
    'fork': {'metal': 4.5, 'plastic': 1.0, 'cardboard': -3.0, 'paper': -3.0, 'clothes': -3.0},
    'knife': {'metal': 4.5, 'plastic': 1.0, 'cardboard': -3.0, 'paper': -3.0, 'clothes': -3.0},
    'spoon': {'metal': 4.5, 'plastic': 1.0, 'cardboard': -3.0, 'paper': -3.0, 'clothes': -3.0},
    'scissors': {'metal': 5.0, 'plastic': 1.0, 'cardboard': -3.0, 'paper': -3.0, 'clothes': -3.0},

    # 3. Giấy & sách báo
    'book': {'paper': 5.0, 'cardboard': -2.0, 'plastic': -3.0, 'metal': -3.0, 'clothes': -3.0, 'biological': -3.0},

    # 4. Rác điện tử & pin
    'laptop': {'battery': 4.5, 'plastic': 1.2, 'metal': 1.5, 'paper': -3.5, 'cardboard': -3.5, 'clothes': -3.5},
    'mouse': {'battery': 4.0, 'plastic': 2.0, 'cardboard': -3.0, 'paper': -3.0},
    'remote': {'battery': 4.2, 'plastic': 2.0, 'cardboard': -3.0, 'paper': -3.0},
    'keyboard': {'battery': 4.0, 'plastic': 2.0, 'cardboard': -3.0, 'paper': -3.0},
    'cell phone': {'battery': 4.8, 'plastic': 1.2, 'metal': 1.5, 'cardboard': -3.5, 'paper': -3.5},
    'clock': {'battery': 3.8, 'metal': 2.0, 'plastic': 1.5, 'cardboard': -2.5},
    'microwave': {'metal': 3.5, 'battery': 2.5, 'plastic': 1.2},
    'oven': {'metal': 3.5, 'battery': 2.5},
    'toaster': {'metal': 3.8, 'battery': 2.2, 'plastic': 1.2},
    'refrigerator': {'metal': 3.5, 'battery': 2.5, 'plastic': 1.5},
    'hair drier': {'metal': 2.5, 'battery': 2.5, 'plastic': 1.8},

    # 5. Vải vóc, trang phục
    'backpack': {'clothes': 4.5, 'cardboard': -2.5, 'metal': -3.0, 'paper': -3.0},
    'handbag': {'clothes': 4.5, 'cardboard': -2.5, 'metal': -3.0, 'paper': -3.0},
    'suitcase': {'clothes': 4.0, 'plastic': 1.8, 'cardboard': -2.5},
    'tie': {'clothes': 4.8, 'cardboard': -3.0},
    'umbrella': {'clothes': 3.0, 'metal': 2.0, 'cardboard': -2.5},
    'teddy bear': {'clothes': 4.5, 'cardboard': -3.0},

    # 6. Rác hữu cơ / Thức ăn thừa
    'banana': {'biological': 5.5, 'plastic': -3.0, 'paper': -3.0, 'metal': -3.0, 'cardboard': -3.0},
    'apple': {'biological': 5.5, 'plastic': -3.0, 'paper': -3.0, 'metal': -3.0, 'cardboard': -3.0},
    'sandwich': {'biological': 5.5, 'plastic': -3.0, 'paper': -3.0, 'metal': -3.0, 'cardboard': -3.0},
    'orange': {'biological': 5.5, 'plastic': -3.0, 'paper': -3.0, 'metal': -3.0, 'cardboard': -3.0},
    'broccoli': {'biological': 5.5, 'plastic': -3.0, 'paper': -3.0, 'metal': -3.0, 'cardboard': -3.0},
    'carrot': {'biological': 5.5, 'plastic': -3.0, 'paper': -3.0, 'metal': -3.0, 'cardboard': -3.0},
    'hot dog': {'biological': 5.5, 'plastic': -3.0, 'paper': -3.0, 'metal': -3.0, 'cardboard': -3.0},
    'pizza': {'biological': 5.5, 'plastic': -3.0, 'paper': -3.0, 'metal': -3.0, 'cardboard': -3.0},
    'donut': {'biological': 5.5, 'plastic': -3.0, 'paper': -3.0, 'metal': -3.0, 'cardboard': -3.0},
    'cake': {'biological': 5.5, 'plastic': -3.0, 'paper': -3.0, 'metal': -3.0, 'cardboard': -3.0},
    'potted plant': {'biological': 4.2, 'plastic': 1.5, 'paper': -3.0},

    # 7. Đồ dùng nhựa & khác
    'toothbrush': {'plastic': 4.2, 'cardboard': -3.0, 'paper': -3.0},
    'sports ball': {'plastic': 3.5, 'shoes': 1.2, 'trash': 1.5},
    'frisbee': {'plastic': 4.0, 'cardboard': -3.0},
}


def _get_yolo_semantic_prior(cls_name: Optional[str], conf: float) -> np.ndarray:
    """Chuyển đổi dự đoán nhãn vật thể từ YOLO thành vector logit prior 12 chiều cho ReTrack."""
    prior = np.zeros(12, dtype=np.float32)
    if not cls_name or conf <= 0.0:
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

    hsv = cv2.cvtColor(crop_img, cv2.COLOR_RGB2HSV).astype(np.float32)
    h_ch, s_ch, v_ch = hsv[:, :, 0], hsv[:, :, 1], hsv[:, :, 2]
    mean_s = float(s_ch.mean() / 255.0)
    mean_v = float(v_ch.mean() / 255.0)

    gray = cv2.cvtColor(crop_img, cv2.COLOR_RGB2GRAY).astype(np.float32)
    texture_std = float(gray.std() / 255.0)

    light_neutral = float(((v_ch > 115) & (s_ch < 55)).mean())
    brown_ratio   = float(((h_ch >= 8) & (h_ch <= 26) & (s_ch > 60) & (v_ch > 70) & (v_ch < 205)).mean())
    gray_ratio    = float(((s_ch < 40) & (v_ch > 80) & (v_ch < 210)).mean())
    dark_ratio    = float((v_ch < 60).mean())
    green_ratio   = float(((h_ch >= 35) & (h_ch <= 85) & (s_ch > 50)).mean())

    has_text = len(ocr_text.strip()) > 3 and ocr_text not in ['(Khong co chu)', '(OCR loi)']

    if light_neutral > 0.30:
        boost[CLASS_IDX['paper']]     += 2.2 * light_neutral
        boost[CLASS_IDX['cardboard']] -= 1.8 * light_neutral

    if mean_s < 0.20 and mean_v > 0.45:
        boost[CLASS_IDX['paper']]     += 1.6
        boost[CLASS_IDX['cardboard']] -= 1.2

    if has_text:
        boost[CLASS_IDX['paper']]     += 2.2
        boost[CLASS_IDX['cardboard']] -= 1.2

    if texture_std < 0.18 and light_neutral > 0.25:
        boost[CLASS_IDX['paper']]     += 0.8

    if brown_ratio > 0.25 and mean_s > 0.22:
        boost[CLASS_IDX['cardboard']] += 2.8
        boost[CLASS_IDX['paper']]     -= 1.8

    if gray_ratio > 0.35 and mean_v > 0.50 and texture_std < 0.15:
        boost[CLASS_IDX['metal']]     += 1.5
        boost[CLASS_IDX['cardboard']] -= 1.0

    if mean_s > 0.12 and mean_v > 0.65 and texture_std < 0.18:
        boost[CLASS_IDX['plastic']] += 0.8

    if green_ratio > 0.15:
        boost[CLASS_IDX['green-glass']] += 1.8
        boost[CLASS_IDX['brown-glass']] += 0.5

    if light_neutral > 0.45 and mean_s < 0.08 and texture_std < 0.10:
        boost[CLASS_IDX['white-glass']] += 1.2

    if dark_ratio > 0.35 and texture_std < 0.20:
        boost[CLASS_IDX['battery']] += 1.2

    if texture_std > 0.25 and mean_s > 0.20:
        boost[CLASS_IDX['trash']] += 0.8
        boost[CLASS_IDX['paper']] -= 0.5

    if light_neutral > 0.35 or brown_ratio > 0.20 or dark_ratio > 0.35:
        boost[CLASS_IDX['clothes']] -= 1.5

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


class ClassifyBase64Request(BaseModel):
    image_base64: str


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
    else:
        x1, y1, x2, y2 = _center_crop_coords(ih, iw)
        crop_img = image_np_rgb[y1:y2, x1:x2]
        box_status = "AUTO"
        color = (255, 200, 0)

    # 2. EasyOCR on Crop
    ocr_crop = _padded_crop(image_np_rgb, x1, y1, x2, y2, pad=0.15)
    try:
        ocr_res = ocr_reader.readtext(ocr_crop, detail=1, paragraph=False, text_threshold=0.6, low_text=0.3)
        ocr_texts = [t for (_, t, c) in ocr_res if c >= 0.3]
        ocr_text = " | ".join(ocr_texts) if ocr_texts else ""
    except Exception as e:
        logger.warning(f"OCR warning: {e}")
        ocr_text = ""

    # 3. EfficientNet-B4 Material Classification
    pil_crop = Image.fromarray(crop_img).convert('RGB')
    tensor = img_transform(pil_crop).unsqueeze(0)

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
        top_predictions.append({
            "class_key": c_name,
            "name_vi": METADATA.get(c_name, {}).get("name_vi", c_name),
            "confidence": round(float(probs[idx]) * 100.0, 1),
            "material_type": METADATA.get(c_name, {}).get("material_type", "OTHER"),
            "is_recyclable": METADATA.get(c_name, {}).get("is_recyclable", True),
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


@app.post("/api/ai/classify")
async def classify_file(file: UploadFile = File(...)):
    try:
        contents = await file.read()
        pil_img = Image.open(io.BytesIO(contents)).convert("RGB")
        img_np = np.array(pil_img)
        res = _run_classification(img_np)
        return res
    except Exception as e:
        logger.error(f"Classification error: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Loi xu ly anh AI: {str(e)}")


@app.post("/api/ai/classify-base64")
async def classify_base64(req: ClassifyBase64Request):
    try:
        data = req.image_base64
        if "base64," in data:
            data = data.split("base64,")[1]
        raw_bytes = base64.b64decode(data)
        pil_img = Image.open(io.BytesIO(raw_bytes)).convert("RGB")
        img_np = np.array(pil_img)
        res = _run_classification(img_np)
        return res
    except Exception as e:
        logger.error(f"Classification base64 error: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Loi xu ly anh base64: {str(e)}")


if __name__ == "__main__":
    import uvicorn
    port = int(os.environ.get("PORT", 5005))
    logger.info(f"Khoi chay ReTrack AI Service tren cong {port}...")
    uvicorn.run("server:app", host="0.0.0.0", port=port, reload=False)
