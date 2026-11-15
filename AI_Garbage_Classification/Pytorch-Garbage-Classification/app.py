import gradio as gr
import torch
import torchvision.transforms as transforms
from torchvision import models
from PIL import Image
import os
import easyocr
import cv2
import numpy as np
import time
from datetime import datetime
from ultralytics import YOLO

# 1. 12 classes
classes = [
    'battery', 'biological', 'brown-glass', 'cardboard', 'clothes',
    'green-glass', 'metal', 'paper', 'plastic', 'shoes', 'trash', 'white-glass',
]
CLASS_IDX = {c: i for i, c in enumerate(classes)}

# 2. EfficientNet-B4
print('Dang khoi tao EfficientNet-B4...')
model = models.efficientnet_b4(weights=None)
model.classifier[1] = torch.nn.Linear(model.classifier[1].in_features, 12)
WEIGHTS_PATH = 'model.pth'
if os.path.exists(WEIGHTS_PATH):
    print(f'Dang load {WEIGHTS_PATH}...')
    try:
        loaded = torch.load(WEIGHTS_PATH, map_location='cpu', weights_only=False)
        if isinstance(loaded, dict):
            model.load_state_dict(loaded)
        else:
            model = loaded
        print('Load model thanh cong!')
    except Exception as e:
        print(f'[Error] {e}')
else:
    print(f'[Warning] Khong tim thay {WEIGHTS_PATH}!')
model.eval()

# 3. OCR
print('Dang khoi tao EasyOCR...')
reader = easyocr.Reader(['vi', 'en'], gpu=False)

# 4. YOLO
print('Dang khoi tao YOLOv8n...')
yolo_model = YOLO('yolov8n.pt')

# 5. Transform
transform = transforms.Compose([
    transforms.Resize((380, 380)),
    transforms.ToTensor(),
    transforms.Normalize([0.485, 0.456, 0.406], [0.229, 0.224, 0.225]),
])

# Cau hinh
YOLO_CONF_THRESH   = 0.20
MIN_BOX_AREA_RATIO = 0.02
CENTER_CROP_RATIO  = 0.65
CROP_PADDING       = 0.08
DEBOUNCE_SEC       = 1.5

# ── Bảng ánh xạ tri thức ngữ nghĩa từ 80 lớp COCO của YOLOv8 sang 12 nhóm rác ReTrack ──
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


def _get_yolo_semantic_prior(cls_name, conf):
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
    '''
    Hieu chinh logits dua tren mau sac, texture va OCR cua anh crop.
    Giai quyet triet de:
    - Model bi bias sang cardboard thay vi paper
    - Model bi bias clothes do dataset lech
    '''
    boost = np.zeros(12, dtype=np.float32)
    if crop_img is None or crop_img.size == 0:
        return boost

    # Tinh cac dac trung anh
    hsv  = cv2.cvtColor(crop_img, cv2.COLOR_RGB2HSV).astype(np.float32)
    h_ch = hsv[:, :, 0]   # Hue   0-180
    s_ch = hsv[:, :, 1]   # Sat   0-255
    v_ch = hsv[:, :, 2]   # Value 0-255

    mean_s = float(s_ch.mean() / 255.0)   # Do bao hoa trung binh (0-1)
    mean_v = float(v_ch.mean() / 255.0)   # Do sang trung binh (0-1)

    gray = cv2.cvtColor(crop_img, cv2.COLOR_RGB2GRAY).astype(np.float32)
    texture_std = float(gray.std() / 255.0)  # Texture complexity (0-1)

    # Ti le vung mau
    # Vung sang trung tinh (mau trang/kem/xam nhat cua giay trong phong)
    light_neutral = float(((v_ch > 115) & (s_ch < 55)).mean())
    # Nau ro net cua thung/hop carton kraft
    brown_ratio   = float(((h_ch >= 8) & (h_ch <= 26) & (s_ch > 60) & (v_ch > 70) & (v_ch < 205)).mean())
    # Xam kim loai
    gray_ratio    = float(((s_ch < 40) & (v_ch > 80) & (v_ch < 210)).mean())
    # Toi (pin, rac)
    dark_ratio    = float((v_ch < 60).mean())
    # Xanh la (chai thuy tinh)
    green_ratio   = float(((h_ch >= 35) & (h_ch <= 85) & (s_ch > 50)).mean())

    has_text = len(ocr_text.strip()) > 3 and ocr_text not in ['(Khong co chu)', '(OCR loi)']

    # 1. PAPER BOOST (Giay trang/vo/sach/hoa don/bao)
    # Model thuong bi bias logit cardboard (+2.6) cao hon paper (+0.6) gap ~2.0
    if light_neutral > 0.30:
        boost[CLASS_IDX['paper']]     += 2.2 * light_neutral
        boost[CLASS_IDX['cardboard']] -= 1.8 * light_neutral

    if mean_s < 0.20 and mean_v > 0.45:
        boost[CLASS_IDX['paper']]     += 1.6
        boost[CLASS_IDX['cardboard']] -= 1.2

    if has_text:
        # Phat hien chu in/viet: Cuc ky co loi the la paper
        boost[CLASS_IDX['paper']]     += 2.2
        boost[CLASS_IDX['cardboard']] -= 1.2

    if texture_std < 0.18 and light_neutral > 0.25:
        boost[CLASS_IDX['paper']]     += 0.8

    # 2. CARDBOARD BOOST (Chi khi co mau nau carton thuc su)
    if brown_ratio > 0.25 and mean_s > 0.22:
        boost[CLASS_IDX['cardboard']] += 2.8
        boost[CLASS_IDX['paper']]     -= 1.8

    # 3. METAL BOOST (Xam, sang, it texture)
    if gray_ratio > 0.35 and mean_v > 0.50 and texture_std < 0.15:
        boost[CLASS_IDX['metal']]     += 1.5
        boost[CLASS_IDX['cardboard']] -= 1.0

    # 4. PLASTIC (Trong suot/mau tuoi, texture thap)
    if mean_s > 0.12 and mean_v > 0.65 and texture_std < 0.18:
        boost[CLASS_IDX['plastic']] += 0.8

    # 5. GLASS - GREEN (Xanh la chai)
    if green_ratio > 0.15:
        boost[CLASS_IDX['green-glass']] += 1.8
        boost[CLASS_IDX['brown-glass']] += 0.5

    # 6. GLASS - WHITE (Trang trong, rat it bao hoa, cuc it texture)
    if light_neutral > 0.45 and mean_s < 0.08 and texture_std < 0.10:
        boost[CLASS_IDX['white-glass']] += 1.2

    # 7. BATTERY (Toi, it texture)
    if dark_ratio > 0.35 and texture_std < 0.20:
        boost[CLASS_IDX['battery']] += 1.2

    # 8. TRASH (Phuc tap, nhieu mau, texture cao)
    if texture_std > 0.25 and mean_s > 0.20:
        boost[CLASS_IDX['trash']] += 0.8
        boost[CLASS_IDX['paper']] -= 0.5

    # 9. DE-BIAS CLOTHES (Bi train qua nhieu trong dataset)
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


def predict(image, state):
    if state is None:
        state = {'best_prob': 0.0, 'best_class': '', 'last_update_time': 0.0, 'records': []}
    if image is None:
        return None, None, 'Hay cho camera khoi dong!', '', state['records'], 'Chua co du lieu', state

    ih, iw = image.shape[:2]
    annotated = image.copy()

    # 1. YOLO Detection & Semantic Prior Extraction
    results = yolo_model(image, verbose=False)
    best_box = None
    detected_yolo_class = None
    yolo_conf = 0.0
    if results and len(results[0].boxes) > 0:
        best_box, detected_yolo_class, yolo_conf = _pick_best_box(results[0].boxes, ih, iw)

    if best_box is not None:
        x1, y1, x2, y2 = best_box
        crop_img = _padded_crop(image, x1, y1, x2, y2)
        box_status = f"YOLO: {detected_yolo_class}" if detected_yolo_class else "YOLO"
        color = (0, 220, 80)
        cv2.rectangle(annotated, (x1, y1), (x2, y2), color, 3)
        cv2.putText(annotated, f'YOLO: {detected_yolo_class}', (iw - 200, 30), cv2.FONT_HERSHEY_SIMPLEX, 0.7, color, 2)
    else:
        x1, y1, x2, y2 = _center_crop_coords(ih, iw)
        crop_img = image[y1:y2, x1:x2]
        box_status = 'AUTO'
        color = (0, 200, 255)
        cv2.rectangle(annotated, (x1, y1), (x2, y2), color, 2)
        cv2.putText(annotated, 'AUTO', (iw - 100, 30), cv2.FONT_HERSHEY_SIMPLEX, 0.7, color, 2)

    # 2. OCR tren crop
    ocr_crop = _padded_crop(image, x1, y1, x2, y2, pad=0.15)
    try:
        ocr_res  = reader.readtext(ocr_crop, detail=1, paragraph=False,
                                   text_threshold=0.6, low_text=0.3)
        ocr_texts = [t for (_, t, c) in ocr_res if c >= 0.3]
        ocr_text  = ' | '.join(ocr_texts) if ocr_texts else '(Khong co chu)'
    except Exception:
        ocr_text = '(OCR loi)'

    # 3. EfficientNet-B4 + Multi-Model Knowledge Stacking
    pil_img = Image.fromarray(crop_img).convert('RGB')
    tensor  = transform(pil_img).unsqueeze(0)

    with torch.no_grad():
        raw_logits = model(tensor)
        logits_np  = raw_logits[0].cpu().numpy()

    # Chồng kiến thức: EfficientNet logits + YOLO Prior + Visual Heuristics & OCR
    yolo_prior = _get_yolo_semantic_prior(detected_yolo_class, yolo_conf)
    boost = _apply_visual_heuristics(logits_np, crop_img, ocr_text)
    adj_logits = torch.tensor(logits_np + yolo_prior + boost)
    probs = torch.softmax(adj_logits, dim=0)
    pred_idx = probs.argmax().item()

    confidences = {classes[i]: float(probs[i]) for i in range(12)}
    best_class  = classes[pred_idx]
    best_prob   = float(probs[pred_idx]) * 100

    raw_probs   = torch.softmax(torch.tensor(logits_np), dim=0)
    raw_top     = classes[raw_probs.argmax().item()]
    debug_str   = f'RAW(Eff):{raw_top} + YOLO:{detected_yolo_class} -> ENSEMBLE:{best_class}'

    result_text = f'[{box_status}] {best_class.upper()} ({best_prob:.1f}%)'

    # Ve nhan
    label = f'{best_class.upper()} {best_prob:.1f}%'
    ty = max(y1 - 12, 24)
    (tw, th), _ = cv2.getTextSize(label, cv2.FONT_HERSHEY_SIMPLEX, 0.85, 2)
    cv2.rectangle(annotated, (x1, ty - th - 6), (x1 + tw + 6, ty + 4), (20, 20, 20), -1)
    cv2.putText(annotated, label, (x1 + 3, ty), cv2.FONT_HERSHEY_SIMPLEX, 0.85, color, 2, cv2.LINE_AA)
    cv2.putText(annotated, debug_str, (8, ih - 12), cv2.FONT_HERSHEY_SIMPLEX, 0.55, (200, 200, 0), 1, cv2.LINE_AA)

    # State update
    now = time.time()
    if best_prob > state['best_prob']:
        state['best_prob']  = best_prob
        state['best_class'] = best_class
    best_txt = f"[KY LUC] {state['best_class'].upper()} ({state['best_prob']:.2f}%)"

    if now - state['last_update_time'] > DEBOUNCE_SEC:
        ts = datetime.now().strftime('%H:%M:%S')
        state['records'].insert(0, [ts, best_class.upper(), f'{best_prob:.1f}%', ocr_text])
        if len(state['records']) > 20:
            state['records'] = state['records'][:20]
        state['last_update_time'] = now

    return annotated, confidences, result_text, ocr_text, state['records'], best_txt, state


def reset_state():
    s = {'best_prob': 0.0, 'best_class': '', 'last_update_time': 0.0, 'records': []}
    return s['records'], 'Chua co du lieu', s


# Gradio UI
with gr.Blocks(title='Garbage Classification AI') as demo:
    gr.Markdown('# Phan loai rac bang AI (EfficientNet-B4 + Heuristic Boost)')
    gr.Markdown(
        '**YOLO OK** = tim thay chu the chinh | '
        '**AUTO** = dung center crop | '
        'Goc duoi: `RAW -> ADJ` hien thi ket qua truoc va sau khi tinh chinh heuristic'
    )
    session_state = gr.State({'best_prob': 0.0, 'best_class': '', 'last_update_time': 0.0, 'records': []})

    with gr.Row():
        with gr.Column(scale=2):
            camera = gr.Image(sources=['webcam'], streaming=True, label='Live Camera')
            with gr.Row():
                result_txt = gr.Textbox(label='Ket luan', lines=1)
                ocr_txt    = gr.Textbox(label='OCR', lines=1)
            confidences_out = gr.Label(num_top_classes=5, label='Top-5 (sau heuristic)')
        with gr.Column(scale=1):
            best_txt   = gr.Textbox(label='Ky luc phien', value='Chua co du lieu', lines=1)
            reset_btn  = gr.Button('Xoa Lich su', variant='stop')
            history_table = gr.Dataframe(
                headers=['Gio', 'Loai rac', 'Do tin', 'OCR'],
                datatype=['str', 'str', 'str', 'str'],
                label='Lich su phien', value=[],
            )

    camera.stream(
        fn=predict,
        inputs=[camera, session_state],
        outputs=[camera, confidences_out, result_txt, ocr_txt, history_table, best_txt, session_state],
    )
    reset_btn.click(fn=reset_state, inputs=[], outputs=[history_table, best_txt, session_state])

if __name__ == '__main__':
    print('Khoi dong Web App...')
    demo.launch(server_name='0.0.0.0')
