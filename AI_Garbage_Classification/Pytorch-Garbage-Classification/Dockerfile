FROM python:3.11-slim
WORKDIR /app
ENV PYTHONUNBUFFERED=1 PYTHONDONTWRITEBYTECODE=1 HOME=/home/ai \
    MPLCONFIGDIR=/tmp/matplotlib YOLO_CONFIG_DIR=/tmp/ultralytics AI_REQUIRE_WEIGHTS=true
RUN apt-get update && apt-get install -y --no-install-recommends libgl1 libglib2.0-0 \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --create-home --uid 10001 ai \
    && mkdir -p /home/ai/.EasyOCR && chown -R ai:ai /home/ai
COPY requirements.docker.txt ./
RUN pip install --no-cache-dir torch==2.6.0 torchvision==0.21.0 --index-url https://download.pytorch.org/whl/cpu \
    && pip install --no-cache-dir -r requirements.docker.txt
COPY server.py ./
USER ai
EXPOSE 5005
HEALTHCHECK --interval=30s --timeout=10s --start-period=300s --retries=5 \
    CMD python -c "import urllib.request; urllib.request.urlopen('http://127.0.0.1:5005/api/ai/health', timeout=5)" || exit 1
CMD ["python", "server.py"]
