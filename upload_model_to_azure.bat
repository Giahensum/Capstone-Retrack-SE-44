@echo off
setlocal
chcp 65001 >nul
echo ================================================================
echo    RETRACK AI - TẢI MODEL & CODE LÊN MÁY ẢO AZURE (MIỀN JAPANEAST)
echo ================================================================
echo.

set AZURE_HOST=retrack-api-se44.japaneast.cloudapp.azure.com
set AZURE_IP=20.89.132.173
set AZURE_USER=azureuser
set REMOTE_DIR=~/retrack-deploy

set SCP_EXE=
if exist "C:\Program Files\Git\usr\bin\scp.exe" (
    set SCP_EXE="C:\Program Files\Git\usr\bin\scp.exe"
) else if exist "%SystemRoot%\System32\OpenSSH\scp.exe" (
    set SCP_EXE="%SystemRoot%\System32\OpenSSH\scp.exe"
) else (
    set SCP_EXE=scp
)

set MODEL_DIR=D:\Ky 9\Capstone-Retrack-SE-44\AI_Garbage_Classification\Pytorch-Garbage-Classification
set FILES_TO_UPLOAD=

if exist "%MODEL_DIR%\weights.pt" (
    echo [OK] Tìm thấy Model Custom ViT-DETR: weights.pt (138.9 MB)
    set FILES_TO_UPLOAD=%FILES_TO_UPLOAD% "%MODEL_DIR%\weights.pt"
) else (
    echo [CẢNH BÁO] Không thấy weights.pt trong thư mục AI!
)

if exist "%MODEL_DIR%\server.py" (
    echo [OK] Tìm thấy server.py (Code cập nhật hỗ trợ weights.pt ^& /api/ai/custom-model)
    set FILES_TO_UPLOAD=%FILES_TO_UPLOAD% "%MODEL_DIR%\server.py"
)

if exist "%MODEL_DIR%\model.pth" (
    echo [OK] Tìm thấy Classifier: model.pth
    set FILES_TO_UPLOAD=%FILES_TO_UPLOAD% "%MODEL_DIR%\model.pth"
)

if exist "%MODEL_DIR%\yolov8n.pt" (
    echo [OK] Tìm thấy YOLO: yolov8n.pt
    set FILES_TO_UPLOAD=%FILES_TO_UPLOAD% "%MODEL_DIR%\yolov8n.pt"
)

if exist "%MODEL_DIR%\best.pt" (
    echo [OK] Tìm thấy Model Custom YOLO: best.pt
    set FILES_TO_UPLOAD=%FILES_TO_UPLOAD% "%MODEL_DIR%\best.pt"
)

if exist "%MODEL_DIR%\custom_model.pth" (
    echo [OK] Tìm thấy Model Custom Classifier: custom_model.pth
    set FILES_TO_UPLOAD=%FILES_TO_UPLOAD% "%MODEL_DIR%\custom_model.pth"
)

echo.
echo Công cụ SCP: %SCP_EXE%
echo Đang gửi các file lên máy ảo Azure (Miền: Japan East - %AZURE_HOST% / %AZURE_IP%)...
echo (Vui lòng nhập mật khẩu tài khoản %AZURE_USER% khi được hỏi)
echo.

%SCP_EXE% -o StrictHostKeyChecking=accept-new %FILES_TO_UPLOAD% %AZURE_USER%@%AZURE_HOST%:%REMOTE_DIR%/

if %ERRORLEVEL% equ 0 (
    echo.
    echo ================================================================
    echo [THÀNH CÔNG RỰC RỠ] Đã tải xong toàn bộ file lên máy ảo Azure!
    echo ================================================================
    echo.
    echo BƯỚC TIẾP THEO TRÊN MÁY ẢO AZURE:
    echo 1. SSH vào máy ảo:
    echo    ssh %AZURE_USER%@%AZURE_HOST%
    echo 2. Cập nhật model và server.py vào container retrack-ai đang chạy:
    echo    docker cp %REMOTE_DIR%/weights.pt retrack-ai:/app/weights.pt
    echo    docker cp %REMOTE_DIR%/server.py retrack-ai:/app/server.py
    echo    docker restart retrack-ai
    echo 3. Kiểm tra xem model đã sẵn sàng:
    echo    curl http://localhost:5005/api/ai/health
    echo    curl http://localhost:5005/api/ai/custom-model
    echo ================================================================
) else (
    echo.
    echo [LỖI] Tải thất bại. Vui lòng kiểm tra lại mật khẩu hoặc kết nối mạng.
)

pause
