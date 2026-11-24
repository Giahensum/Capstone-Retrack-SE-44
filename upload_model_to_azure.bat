@echo off
setlocal
echo ================================================================
echo        DANG TAI MODEL LEN MAY AO AZURE (DUNG GIT SCP)
echo ================================================================
echo.

set SCP_EXE=
if exist "C:\Program Files\Git\usr\bin\scp.exe" (
    set SCP_EXE="C:\Program Files\Git\usr\bin\scp.exe"
) else if exist "%SystemRoot%\System32\OpenSSH\scp.exe" (
    set SCP_EXE="%SystemRoot%\System32\OpenSSH\scp.exe"
) else (
    set SCP_EXE=scp
)

set MODEL_DIR=D:\Ky 9\Capstone-Retrack-SE-44\AI_Garbage_Classification\Pytorch-Garbage-Classification
set FILES_TO_UPLOAD="%MODEL_DIR%\model.pth" "%MODEL_DIR%\yolov8n.pt"

if exist "%MODEL_DIR%\best.pt" (
    echo Phat hien Model Custom YOLO: best.pt
    set FILES_TO_UPLOAD=%FILES_TO_UPLOAD% "%MODEL_DIR%\best.pt"
)
if exist "%MODEL_DIR%\custom_model.pth" (
    echo Phat hien Model Custom Classifier: custom_model.pth
    set FILES_TO_UPLOAD=%FILES_TO_UPLOAD% "%MODEL_DIR%\custom_model.pth"
)

echo Cong cu SCP: %SCP_EXE%
echo Dang gui cac file model len Azure (20.89.132.173)...
echo (Vui long nhap mat khau azureuser khi duoc hoi)
echo.

%SCP_EXE% -o StrictHostKeyChecking=accept-new %FILES_TO_UPLOAD% azureuser@20.89.132.173:~/retrack-deploy/

if %ERRORLEVEL% equ 0 (
    echo.
    echo ================================================================
    echo [THÀNH CÔNG RỰC RỠ] Đã tải xong cả 2 file lên máy ảo Azure!
    echo ================================================================
) else (
    echo.
    echo [LỖI] Tải thất bại. Vui lòng kiểm tra mật khẩu.
)

pause
