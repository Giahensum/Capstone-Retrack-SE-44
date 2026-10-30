"""
=============================================================================
ReTrack AI - Data Flow & Incremental Learning Pipeline for Garbage Classification
=============================================================================
Quy trình tự động hóa nạp ảnh tự chụp vào Dataset và Fine-tune mô hình EfficientNet-B4:
1. Chuẩn hóa & tạo cấu trúc thư mục 12 nhóm rác thải
2. Tiền xử lý, lọc ảnh hỏng, sửa góc xoay EXIF điện thoại & phân chia Train / Val
3. Tăng cường dữ liệu (Data Augmentation) cho ảnh chụp thực tế
4. Fine-tuning chuyển giao (Transfer Learning) giữ nguyên trọng số và cập nhật model.pth
=============================================================================
"""

import os
import sys
import shutil
import random
import logging
import argparse
from datetime import datetime
from PIL import Image, ImageOps
import torch
import torch.nn as nn
from torch.utils.data import DataLoader
from torchvision import datasets, transforms, models

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")
logger = logging.getLogger("DataPipeline")

CLASSES = [
    'battery', 'biological', 'brown-glass', 'cardboard', 'clothes',
    'green-glass', 'metal', 'paper', 'plastic', 'shoes', 'trash', 'white-glass',
]

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
DATASET_ROOT = os.path.join(BASE_DIR, "custom_dataset")
RAW_DIR = os.path.join(DATASET_ROOT, "raw")
SPLIT_DIR = os.path.join(DATASET_ROOT, "dataset_split")
TRAIN_DIR = os.path.join(SPLIT_DIR, "train")
VAL_DIR = os.path.join(SPLIT_DIR, "val")
MODEL_PATH = os.path.join(BASE_DIR, "model.pth")


def init_structure():
    """Tạo cấu trúc thư mục cho 12 nhóm rác thải"""
    logger.info("Dang khoi tao cau truc thu muc Data Flow...")
    for c in CLASSES:
        os.makedirs(os.path.join(RAW_DIR, c), exist_ok=True)
        os.makedirs(os.path.join(TRAIN_DIR, c), exist_ok=True)
        os.makedirs(os.path.join(VAL_DIR, c), exist_ok=True)

    print("\n" + "=" * 65)
    print(" DA KHOI TAO THANH CONG THU MUC DATASET TAI:")
    print(f" -> {RAW_DIR}")
    print("=" * 65)
    print("HUONG DAN:")
    print("1. Chup anh phe lieu bang dien thoai hoac may anh.")
    print("2. Chep anh vao dung thu muc tuong ung trong 'custom_dataset/raw/':")
    for idx, c in enumerate(CLASSES, 1):
        print(f"   [{idx:02d}] {c:<14} -> custom_dataset/raw/{c}/")
    print("\n3. Sau khi bo anh vao, chay lenh:")
    print("   python data_pipeline.py --prepare")
    print("   python data_pipeline.py --train --epochs 15")
    print("=" * 65 + "\n")


def clean_and_prepare_image(src_path, dst_path, target_size=(380, 380)):
    """Lam sach anh: sua goc xoay EXIF, kiem tra loi, chuyen RGB va resize"""
    try:
        with Image.open(src_path) as img:
            # 1. Sua goc xoay tu camera dien thoai qua EXIF Orientation
            img = ImageOps.exif_transpose(img)

            # 2. Chuyen he mau RGB
            img = img.convert("RGB")

            # 3. Kiem tra kich thuoc hop le
            if img.width < 32 or img.height < 32:
                logger.warning(f"Bo qua anh qua nho ({img.width}x{img.height}): {src_path}")
                return False

            # 4. Luu anh da chuan hoa
            img.save(dst_path, "JPEG", quality=95)
            return True
    except Exception as e:
        logger.warning(f"Loi doc file anh '{src_path}': {e}")
        return False


def prepare_dataset(val_ratio=0.2):
    """
    Data Flow Step:
    - Doc tat ca anh tu custom_dataset/raw/
    - Lam sach, sua loi xoay
    - Chia ngau nhien thanh tap Train (80%) va Val (20%)
    """
    logger.info(f"Bat dau tien xu ly va phan chia dataset (Val ratio: {val_ratio * 100:.0f}%)...")

    # Xoa bo du lieu cu trong dataset_split
    for c in CLASSES:
        train_class_dir = os.path.join(TRAIN_DIR, c)
        val_class_dir = os.path.join(VAL_DIR, c)
        if os.path.exists(train_class_dir):
            shutil.rmtree(train_class_dir)
        if os.path.exists(val_class_dir):
            shutil.rmtree(val_class_dir)
        os.makedirs(train_class_dir, exist_ok=True)
        os.makedirs(val_class_dir, exist_ok=True)

    valid_extensions = {".jpg", ".jpeg", ".png", ".webp", ".bmp"}
    stats = {c: {"raw": 0, "train": 0, "val": 0} for c in CLASSES}
    total_images = 0

    for c in CLASSES:
        raw_class_dir = os.path.join(RAW_DIR, c)
        if not os.path.exists(raw_class_dir):
            continue

        raw_files = [
            f for f in os.listdir(raw_class_dir)
            if os.path.splitext(f.lower())[1] in valid_extensions
        ]
        random.shuffle(raw_files)
        stats[c]["raw"] = len(raw_files)
        total_images += len(raw_files)

        if len(raw_files) == 0:
            continue

        # Neu chi co 1 anh thi dua vao train, nguoc lai chia theo val_ratio
        val_count = max(1, int(len(raw_files) * val_ratio)) if len(raw_files) > 1 else 0
        val_files = raw_files[:val_count]
        train_files = raw_files[val_count:]

        # Copy & clean train images
        for f in train_files:
            src = os.path.join(raw_class_dir, f)
            dst = os.path.join(TRAIN_DIR, c, f"clean_{f}")
            if clean_and_prepare_image(src, dst):
                stats[c]["train"] += 1

        # Copy & clean val images
        for f in val_files:
            src = os.path.join(raw_class_dir, f)
            dst = os.path.join(VAL_DIR, c, f"clean_{f}")
            if clean_and_prepare_image(src, dst):
                stats[c]["val"] += 1

    print("\n" + "=" * 65)
    print(" KET QUA TIEN XU LY VA PHAN CHIA DATASET:")
    print("=" * 65)
    print(f"{'Nhom phe lieu':<16} | {'Anh Raw':<10} | {'Train (80%)':<12} | {'Val (20%)':<10}")
    print("-" * 65)
    for c in CLASSES:
        print(f"{c:<16} | {stats[c]['raw']:<10} | {stats[c]['train']:<12} | {stats[c]['val']:<10}")
    print("-" * 65)
    print(f"Tong so anh da xu ly: {total_images}")
    print("=" * 65 + "\n")

    if total_images == 0:
        logger.warning("Chua co anh nao trong 'custom_dataset/raw/'. Hay them anh truoc khi train!")
    return total_images


def get_data_loaders(batch_size=16):
    """Data Augmentation & DataLoaders cho anh chup thuc te"""
    train_transform = transforms.Compose([
        transforms.Resize((400, 400)),
        transforms.RandomResizedCrop(380, scale=(0.8, 1.0)),
        transforms.RandomHorizontalFlip(p=0.5),
        transforms.RandomRotation(degrees=15),
        transforms.ColorJitter(brightness=0.2, contrast=0.2, saturation=0.2),
        transforms.ToTensor(),
        transforms.Normalize([0.485, 0.456, 0.406], [0.229, 0.224, 0.225]),
    ])

    val_transform = transforms.Compose([
        transforms.Resize((380, 380)),
        transforms.ToTensor(),
        transforms.Normalize([0.485, 0.456, 0.406], [0.229, 0.224, 0.225]),
    ])

    train_dataset = datasets.ImageFolder(TRAIN_DIR, transform=train_transform)
    val_dataset = datasets.ImageFolder(VAL_DIR, transform=val_transform)

    train_loader = DataLoader(
        train_dataset,
        batch_size=batch_size,
        shuffle=True,
        num_workers=0,
        pin_memory=torch.cuda.is_available()
    )
    val_loader = DataLoader(
        val_dataset,
        batch_size=batch_size,
        shuffle=False,
        num_workers=0,
        pin_memory=torch.cuda.is_available()
    )

    return train_loader, val_loader, len(train_dataset), len(val_dataset)


def load_model_for_finetuning(device):
    """
    Nap mo hinh hien co, dong bang (freeze) cac tang dau (Backbone)
    va chi mo khoa tang phan loai (Classifier) de Fine-tune an toan.
    """
    logger.info("Dang khoi tao EfficientNet-B4...")
    model = models.efficientnet_b4(weights=None)
    model.classifier[1] = nn.Linear(model.classifier[1].in_features, 12)

    if os.path.exists(MODEL_PATH):
        logger.info(f"Nap trong so hien tai tu {MODEL_PATH}...")
        try:
            loaded = torch.load(MODEL_PATH, map_location='cpu', weights_only=False)
            if isinstance(loaded, dict):
                model.load_state_dict(loaded)
            else:
                model = loaded
            logger.info("Nap model.pth hien tai thanh cong!")
        except Exception as e:
            logger.warning(f"Khong the nap model.pth ({e}), se dung EfficientNet pretrain tu torchvision.")
            model = models.efficientnet_b4(weights=models.EfficientNet_B4_Weights.DEFAULT)
            model.classifier[1] = nn.Linear(model.classifier[1].in_features, 12)
    else:
        logger.info("Chua co model.pth cu, tai pre-trained weights mac dinh tu PyTorch...")
        model = models.efficientnet_b4(weights=models.EfficientNet_B4_Weights.DEFAULT)
        model.classifier[1] = nn.Linear(model.classifier[1].in_features, 12)

    # Freeze toan bo tham so ban dau
    for param in model.parameters():
        param.requires_grad = False

    # Unfreeze cac tang cuoi tuy theo kien truc model
    if hasattr(model, '_blocks'):
        # efficientnet_pytorch architecture
        for param in model._blocks[-2:].parameters():
            param.requires_grad = True
        if hasattr(model, '_conv_head'):
            for param in model._conv_head.parameters():
                param.requires_grad = True
        if hasattr(model, '_fc'):
            for param in model._fc.parameters():
                param.requires_grad = True
    elif hasattr(model, 'features'):
        # torchvision architecture
        for param in model.features[-2:].parameters():
            param.requires_grad = True
        if hasattr(model, 'classifier'):
            for param in model.classifier.parameters():
                param.requires_grad = True
    else:
        # Fallback: unfreeze 15% cuoi cac parameters
        all_params = list(model.parameters())
        for p in all_params[-int(len(all_params) * 0.15):]:
            p.requires_grad = True

    model = model.to(device)
    trainable_params = sum(p.numel() for p in model.parameters() if p.requires_grad)
    logger.info(f"So tham so duoc huan luyen (Fine-tune parameters): {trainable_params:,}")
    return model


def train_model(epochs=15, batch_size=16, lr=1e-4):
    """Fine-tuning Loop"""
    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    logger.info(f"Thiet bi huan luyen (Device): {device}")

    try:
        train_loader, val_loader, n_train, n_val = get_data_loaders(batch_size=batch_size)
    except Exception as e:
        logger.error(f"Loi doc du lieu: {e}. Vui long chay '--prepare' truoc!")
        return

    if n_train == 0:
        logger.error("Khong co du lieu huan luyen nao trong 'custom_dataset/dataset_split/train'!")
        return

    logger.info(f"Du lieu huan luyen: {n_train} anh | Du lieu kiem thu: {n_val} anh")

    model = load_model_for_finetuning(device)
    criterion = nn.CrossEntropyLoss()
    optimizer = torch.optim.AdamW(
        filter(lambda p: p.requires_grad, model.parameters()),
        lr=lr,
        weight_decay=1e-3
    )
    scheduler = torch.optim.lr_scheduler.CosineAnnealingLR(optimizer, T_max=epochs)

    best_val_acc = 0.0
    best_weights = None

    print("\n" + "=" * 65)
    print(f" BAT DAU FINE-TUNING ({epochs} EPOCHS, BATCH SIZE: {batch_size}, LR: {lr})")
    print("=" * 65)

    for epoch in range(1, epochs + 1):
        # 1. Training Phase
        model.train()
        running_loss = 0.0
        correct_train = 0
        total_train = 0

        for images, labels in train_loader:
            images = images.to(device)
            labels = labels.to(device)

            optimizer.zero_grad()
            outputs = model(images)
            loss = criterion(outputs, labels)
            loss.backward()
            optimizer.step()

            running_loss += loss.item() * images.size(0)
            _, preds = torch.max(outputs, 1)
            correct_train += torch.sum(preds == labels.data).item()
            total_train += labels.size(0)

        epoch_train_loss = running_loss / max(1, total_train)
        epoch_train_acc = (correct_train / max(1, total_train)) * 100.0

        # 2. Validation Phase
        model.eval()
        val_loss = 0.0
        correct_val = 0
        total_val = 0

        with torch.no_grad():
            for images, labels in val_loader:
                images = images.to(device)
                labels = labels.to(device)
                outputs = model(images)
                loss = criterion(outputs, labels)

                val_loss += loss.item() * images.size(0)
                _, preds = torch.max(outputs, 1)
                correct_val += torch.sum(preds == labels.data).item()
                total_val += labels.size(0)

        epoch_val_loss = val_loss / max(1, total_val) if total_val > 0 else 0.0
        epoch_val_acc = (correct_val / max(1, total_val) * 100.0) if total_val > 0 else 0.0
        scheduler.step()

        print(
            f"Epoch {epoch:02d}/{epochs:02d} | "
            f"Train Loss: {epoch_train_loss:.4f} - Acc: {epoch_train_acc:5.1f}% | "
            f"Val Loss: {epoch_val_loss:.4f} - Acc: {epoch_val_acc:5.1f}%"
        )

        # Check best model
        if epoch_val_acc >= best_val_acc and total_val > 0:
            best_val_acc = epoch_val_acc
            best_weights = model.state_dict().copy()

    # Sao luu model cu truoc khi ghi de
    if os.path.exists(MODEL_PATH):
        backup_path = os.path.join(BASE_DIR, f"model_backup_{datetime.now().strftime('%Y%m%d_%H%M%S')}.pth")
        logger.info(f"Sao luu model.pth cu sang: {backup_path}")
        shutil.copy2(MODEL_PATH, backup_path)

    # Luu model moi
    save_weights = best_weights if best_weights is not None else model.state_dict()
    torch.save(save_weights, MODEL_PATH)
    logger.info(f"DA LUU MODEL MOI VAO: {MODEL_PATH} (Do chinh xac Val tot nhat: {best_val_acc:.1f}%)")
    print("\n" + "=" * 65)
    print(" HUAN LUYEN HOAN TAT THANH CONG!")
    print(f" -> Trong so da cap nhat vao: {MODEL_PATH}")
    print(" -> Ban co the khoi dong lai server: python server.py de dung model moi!")
    print("=" * 65 + "\n")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="ReTrack AI Data Pipeline & Fine-tuning Flow")
    parser.add_argument("--init", action="store_true", help="Tao cau truc thu muc 12 nhom rac de bo anh vao")
    parser.add_argument("--prepare", action="store_true", help="Lam sach anh, sua EXIF va chia tap Train/Val")
    parser.add_argument("--train", action="store_true", help="Bat dau Fine-tune mo hinh voi du lieu moi")
    parser.add_argument("--auto", action="store_true", help="Tu dong chay: Prepare -> Train trong 1 lenh")
    parser.add_argument("--epochs", type=int, default=15, help="So epoch huan luyen (mac dinh: 15)")
    parser.add_argument("--batch-size", type=int, default=16, help="Kich thuoc Batch (mac dinh: 16)")
    parser.add_argument("--lr", type=float, default=1e-4, help="Toc do hoc Learning Rate (mac dinh: 0.0001)")

    args = parser.parse_args()

    if args.init:
        init_structure()
    elif args.prepare:
        prepare_dataset()
    elif args.train:
        train_model(epochs=args.epochs, batch_size=args.batch_size, lr=args.lr)
    elif args.auto:
        total = prepare_dataset()
        if total > 0:
            train_model(epochs=args.epochs, batch_size=args.batch_size, lr=args.lr)
    else:
        parser.print_help()
