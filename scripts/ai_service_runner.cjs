const http = require('http');

const PORT = 5005;

const METADATA = {
  cardboard: {
    name_vi: 'Bìa Carton & Thùng giấy',
    material_type: 'CARDBOARD',
    is_recyclable: true,
    confidence: 94.8,
    icon: 'category',
    unit: 'kg',
    price_range: '3.000 - 5.500 đ/kg',
    tip: 'Tháo dỡ băng keo, ép dẹp các thùng carton phẳng và giữ khô ráo để đạt khối lượng và mức giá tốt nhất.',
    suggested_desc: 'Có phế liệu bìa carton và thùng giấy cũ đã gấp gọn, tình trạng khô ráo cần kho vựa thu gom.',
  },
  trash: {
    name_vi: 'Rác thải sinh hoạt đô thị',
    material_type: 'OTHER',
    is_recyclable: false,
    confidence: 92.5,
    icon: 'delete',
    unit: 'kg',
    price_range: 'Không thu mua',
    tip: 'Rác sinh hoạt hữu cơ/thức ăn thừa không thuộc danh mục phế liệu tái chế của ReTrack.',
    suggested_desc: 'Rác thải sinh hoạt cần đưa về điểm tập kết đô thị.',
  }
};

const TOP_PREDICTIONS = [
  { class_key: 'cardboard', name_vi: 'Bìa Carton & Thùng giấy', confidence: 94.8, material_type: 'CARDBOARD', is_recyclable: true },
  { class_key: 'paper', name_vi: 'Giấy & Sách báo cũ', confidence: 3.1, material_type: 'PAPER', is_recyclable: true },
  { class_key: 'plastic', name_vi: 'Nhựa tái chế các loại', confidence: 1.1, material_type: 'PLASTIC', is_recyclable: true },
  { class_key: 'metal', name_vi: 'Kim loại phế liệu', confidence: 0.6, material_type: 'METAL', is_recyclable: true },
  { class_key: 'trash', name_vi: 'Rác sinh hoạt không thu gom', confidence: 0.4, material_type: 'OTHER', is_recyclable: false }
];

const server = http.createServer((req, res) => {
  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type, Authorization');

  if (req.method === 'OPTIONS') {
    res.writeHead(200);
    res.end();
    return;
  }

  const url = new URL(req.url, `http://${req.headers.host}`);
  console.log(`[AI SERVICE] ${req.method} ${url.pathname}`);

  if (url.pathname === '/api/ai/status') {
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({
      status: 'healthy',
      service: 'ReTrack AI Computer Vision (FastAPI Fallback)',
      model: 'Multi-Model Knowledge Stacking (EfficientNet-B4 + YOLOv8n + EasyOCR)',
      fusion_mode: 'Late Fusion / Logit Stacking Ensemble',
      classes_count: 12,
      device: 'cpu'
    }));
    return;
  }

  if (url.pathname === '/api/ai/classes') {
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({
      classes: Object.entries(METADATA).map(([key, m]) => ({
        key,
        name_vi: m.name_vi,
        material_type: m.material_type,
        is_recyclable: m.is_recyclable,
        icon: m.icon,
        price_range: m.price_range,
        tip: m.tip,
      }))
    }));
    return;
  }

  if (url.pathname === '/api/ai/classify' || url.pathname === '/api/ai/classify-base64') {
    let body = [];
    req.on('data', chunk => body.push(chunk));
    req.on('end', () => {
      const fullBuffer = Buffer.concat(body);
      const strContent = fullBuffer.toString('utf-8', 0, Math.min(fullBuffer.length, 5000));

      let meta = METADATA.cardboard;
      let topList = TOP_PREDICTIONS;

      // Check if testing household/trash
      if (strContent.includes('trash') || strContent.includes('household') || strContent.includes('rac_huu_co')) {
        meta = METADATA.trash;
        topList = [
          { class_key: 'trash', name_vi: 'Rác thải sinh hoạt đô thị', confidence: 92.5, material_type: 'OTHER', is_recyclable: false },
          { class_key: 'cardboard', name_vi: 'Bìa Carton', confidence: 4.2, material_type: 'CARDBOARD', is_recyclable: true },
          { class_key: 'plastic', name_vi: 'Nhựa', confidence: 2.1, material_type: 'PLASTIC', is_recyclable: true },
          { class_key: 'metal', name_vi: 'Kim loại', confidence: 0.8, material_type: 'METAL', is_recyclable: true },
          { class_key: 'paper', name_vi: 'Giấy', confidence: 0.4, material_type: 'PAPER', is_recyclable: true }
        ];
      }

      const responsePayload = {
        success: true,
        primary_class: meta.material_type === 'OTHER' ? 'trash' : 'cardboard',
        name_vi: meta.name_vi,
        material_type: meta.material_type,
        is_recyclable: meta.is_recyclable,
        confidence: meta.confidence,
        box_status: 'YOLO: ' + (meta.material_type === 'OTHER' ? 'trash' : 'cardboard'),
        bounding_box: {
          x1: 45,
          y1: 30,
          x2: 480,
          y2: 460,
          width: 435,
          height: 430
        },
        ocr_text: meta.is_recyclable ? 'RECYCLE | CARTON KRAFT' : 'HOUSEHOLD WASTE',
        icon: meta.icon,
        unit: meta.unit,
        price_range: meta.price_range,
        recycling_tip: meta.tip,
        suggested_description: meta.suggested_desc,
        top_predictions: topList,
        annotated_image: '',
        ensemble_details: {
          fusion_mode: 'MULTI_MODEL_KNOWLEDGE_STACKING',
          yolo_detected_class: meta.material_type === 'OTHER' ? 'trash' : 'cardboard',
          yolo_confidence: meta.confidence,
          models_combined: [
            'YOLOv8n (Object & Shape Prior)',
            'EfficientNet-B4 (Material & Texture)',
            'Visual Heuristics & EasyOCR'
          ]
        }
      };

      res.writeHead(200, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify(responsePayload));
    });
    return;
  }

  res.writeHead(404, { 'Content-Type': 'application/json' });
  res.end(JSON.stringify({ error: 'Not Found' }));
});

server.listen(PORT, '0.0.0.0', () => {
  console.log(`⚠️  [CẢNH BÁO] Đang chạy ReTrack AI Mock Runner (Fallback) trên http://127.0.0.1:${PORT}`);
  console.log(`💡 Để chạy AI thật (PyTorch + YOLOv8 + CUDA), hãy chạy:`);
  console.log(`   cd AI_Garbage_Classification/Pytorch-Garbage-Classification && python server.py`);
});

