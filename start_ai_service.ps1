# ReTrack AI Computer Vision Microservice Startup Script
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  RETRACK - KHOI DONG DICH VU AI THI GIAC MAY TINH" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

$aiDir = Join-Path $PSScriptRoot "AI_Garbage_Classification\Pytorch-Garbage-Classification"
if (!(Test-Path $aiDir)) {
    Write-Host "[Loi] Khong tim thay thu muc AI tai: $aiDir" -ForegroundColor Red
    exit 1
}

Set-Location $aiDir
Write-Host "[1/2] Dang khoi chay FastAPI Server tren cong 5005..." -ForegroundColor Green
Write-Host "[2/2] Truy cap API Docs tai: http://localhost:5005/docs" -ForegroundColor Yellow
python server.py --port 5005
