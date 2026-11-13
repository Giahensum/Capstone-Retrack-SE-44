@echo off
title ReTrack AI Computer Vision Microservice (Port 5005)
echo ==================================================
echo   RETRACK - KHOI DONG DICH VU AI THI GIAC MAY TINH
echo ==================================================
cd /d "%~dp0AI_Garbage_Classification\Pytorch-Garbage-Classification"
echo Dang khoi dong FastAPI uvicorn server tren cong 5005...
python server.py
pause
