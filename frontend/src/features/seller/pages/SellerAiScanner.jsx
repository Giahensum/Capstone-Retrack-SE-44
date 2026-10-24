import { useState, useRef, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import { sellerApi } from '../api';
import { MaterialIcon } from '@/features/depot/components/DepotIcon';
import { Loader2, Camera, Upload, Sparkles, RefreshCw, ArrowRight, CheckCircle2, AlertTriangle, ShieldCheck } from 'lucide-react';

const DEMO_SAMPLES = [
  {
    id: 'carton_box',
    title: 'Thùng carton kraft mẫu (Exi.jpg)',
    category: 'cardboard',
    icon: 'category',
    url: '/samples/exi.jpg',
  },
];

export default function SellerAiScanner() {
  const navigate = useNavigate();
  const fileInputRef = useRef(null);
  const videoRef = useRef(null);
  const canvasRef = useRef(null);

  const [activeTab, setActiveTab] = useState('upload'); // 'upload' | 'camera'
  const [cameraActive, setCameraActive] = useState(false);
  const [cameraLoading, setCameraLoading] = useState(false);
  const [selectedImage, setSelectedImage] = useState(null); // { file, preview, base64 }
  const [isScanning, setIsScanning] = useState(false);
  const [aiResult, setAiResult] = useState(null);
  const [aiStatus, setAiStatus] = useState(null);

  // Check AI Service status on mount
  useEffect(() => {
    sellerApi.getAiStatus()
      .then((res) => setAiStatus(res))
      .catch(() => setAiStatus({ status: 'offline' }));
  }, []);

  // Handle Camera Stream
  useEffect(() => {
    if (activeTab === 'camera' && !selectedImage && !aiResult) {
      startCamera();
    } else {
      stopCamera();
    }
    return () => {
      stopCamera();
    };
  }, [activeTab, selectedImage, aiResult]);

  const startCamera = async () => {
    setCameraLoading(true);
    try {
      if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
        toast.error('Trình duyệt không hỗ trợ truy cập Camera');
        setActiveTab('upload');
        setCameraLoading(false);
        return;
      }
      const stream = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: 'environment', width: { ideal: 1280 }, height: { ideal: 720 } },
        audio: false,
      });
      if (videoRef.current) {
        videoRef.current.srcObject = stream;
        videoRef.current.play();
        setCameraActive(true);
      }
    } catch (err) {
      console.warn('Camera access denied or unavailable:', err);
      toast.error('Không thể mở Camera. Vui lòng cho phép quyền hoặc dùng chế độ Tải ảnh');
      setActiveTab('upload');
    } finally {
      setCameraLoading(false);
    }
  };

  const stopCamera = () => {
    if (videoRef.current && videoRef.current.srcObject) {
      const stream = videoRef.current.srcObject;
      const tracks = stream.getTracks();
      tracks.forEach((track) => track.stop());
      videoRef.current.srcObject = null;
    }
    setCameraActive(false);
  };

  // Capture Frame from Camera
  const capturePhoto = () => {
    if (!videoRef.current || !canvasRef.current) return;
    const video = videoRef.current;
    const canvas = canvasRef.current;
    canvas.width = video.videoWidth || 640;
    canvas.height = video.videoHeight || 480;
    const ctx = canvas.getContext('2d');
    ctx.drawImage(video, 0, 0, canvas.width, canvas.height);

    const base64 = canvas.toDataURL('image/jpeg', 0.9);
    stopCamera();

    // Convert to Blob & File
    canvas.toBlob((blob) => {
      const file = new File([blob], `ai_snap_${Date.now()}.jpg`, { type: 'image/jpeg' });
      setSelectedImage({
        file,
        preview: base64,
        base64,
      });
      runScan(file, base64);
    }, 'image/jpeg', 0.9);
  };

  // Handle File Upload from disk
  const handleFileSelect = (e) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (!file.type.startsWith('image/')) {
      return toast.error('Vui lòng chọn tệp hình ảnh hợp lệ (JPG, PNG, WebP)');
    }

    const reader = new FileReader();
    reader.onload = (event) => {
      const base64 = event.target.result;
      setSelectedImage({
        file,
        preview: URL.createObjectURL(file),
        base64,
      });
      runScan(file, base64);
    };
    reader.readAsDataURL(file);
  };

  // Run AI Scan
  const runScan = async (file, base64) => {
    setIsScanning(true);
    setAiResult(null);

    try {
      let res;
      if (file) {
        res = await sellerApi.classifyAiImage(file, false);
      } else if (base64) {
        res = await sellerApi.classifyAiBase64(base64, false);
      }

      if (res && res.primary_class) {
        setAiResult(res);
        toast.success(`Đã nhận diện: ${res.name_vi} (${res.confidence}%) 🎉`);
      } else {
        toast.error('Không thể xác định loại phế liệu. Vui lòng thử góc chụp rõ hơn.');
      }
    } catch (err) {
      console.error('AI Scan error:', err);
      const msg = err?.response?.data?.message || err?.message || 'Có lỗi khi phân tích ảnh AI.';
      toast.error(msg);
    } finally {
      setIsScanning(false);
    }
  };

  // Reset Scanner
  const resetScanner = () => {
    setSelectedImage(null);
    setAiResult(null);
    setIsScanning(false);
    if (activeTab === 'camera') {
      setTimeout(() => startCamera(), 100);
    }
  };

  // Handle Quick Demo Sample selection
  const handleSelectSample = async (sample) => {
    try {
      setIsScanning(true);
      const res = await fetch(sample.url);
      const blob = await res.blob();
      const file = new File([blob], `${sample.id}.jpg`, { type: 'image/jpeg' });
      const preview = URL.createObjectURL(blob);
      setSelectedImage({
        file,
        preview,
        base64: null,
      });
      runScan(file, null);
    } catch (err) {
      console.error(err);
      toast.error('Không thể tải ảnh mẫu thử nghiệm');
      setIsScanning(false);
    }
  };

  // Transfer AI Result to Create Request
  const handleProceedToCreateRequest = () => {
    if (!aiResult) return;
    navigate('/seller/create', {
      state: {
        aiResult: {
          ...aiResult,
          imagePreview: selectedImage?.preview,
          imageFile: selectedImage?.file,
        },
      },
    });
  };

  return (
    <div className="flex flex-col p-4 md:p-6 gap-6 w-full max-w-6xl mx-auto">
      {/* Hidden Canvas for Camera Snapshots */}
      <canvas ref={canvasRef} className="hidden" />

      {/* Header & Status */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-d-primary-container text-d-on-primary-container text-xs font-bold mb-2">
            <Sparkles size={14} className="animate-spin-slow" />
            <span>Thị giác máy tính AI (Computer Vision)</span>
          </div>
          <h1 className="font-d-headline-lg text-d-headline-lg text-d-on-surface">
            Trợ lý AI Phân loại Phế liệu & Định giá
          </h1>
          <p className="font-d-body-md text-d-body-md text-d-on-surface-variant mt-1">
            Chụp ảnh phế liệu để mô hình EfficientNet-B4 + YOLOv8 tự động nhận diện 12 loại rác, kiểm tra khả năng tái chế và định giá thị trường.
          </p>
        </div>

        {/* AI Service Health Badge */}
        <div className="shrink-0">
          {aiStatus?.status === 'healthy' || aiStatus?.status === 'ok' ? (
            <div className="inline-flex items-center gap-2 px-3 py-1.5 rounded-xl bg-emerald-500/10 text-emerald-600 border border-emerald-500/20 text-xs font-semibold">
              <span className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse" />
              <span>AI Service Sẵn sàng ({aiStatus.device || 'CUDA/CPU'})</span>
            </div>
          ) : (
            <div className="inline-flex items-center gap-2 px-3 py-1.5 rounded-xl bg-amber-500/10 text-amber-600 border border-amber-500/20 text-xs font-semibold">
              <span className="w-2 h-2 rounded-full bg-amber-500" />
              <span>Máy chủ AI cục bộ: Port 5005</span>
            </div>
          )}
        </div>
      </div>

      {/* Main Scan Panel */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        {/* Left Column: Camera / Upload / Image Preview (7 cols) */}
        <div className="lg:col-span-7 space-y-4">
          {/* Mode Switcher Tabs */}
          <div className="bg-d-surface-container-lowest border border-d-border-subtle rounded-2xl p-1.5 flex gap-1 shadow-xs">
            <button
              type="button"
              onClick={() => {
                setActiveTab('upload');
                stopCamera();
              }}
              className={`flex-1 py-2.5 px-4 rounded-xl text-xs font-bold transition-all flex items-center justify-center gap-2 ${
                activeTab === 'upload'
                  ? 'bg-d-primary text-d-on-primary shadow-sm'
                  : 'text-d-on-surface-variant hover:text-d-on-surface hover:bg-d-surface-container-low'
              }`}
            >
              <Upload size={16} />
              <span>Tải ảnh từ máy</span>
            </button>
            <button
              type="button"
              onClick={() => {
                setActiveTab('camera');
                setSelectedImage(null);
                setAiResult(null);
              }}
              className={`flex-1 py-2.5 px-4 rounded-xl text-xs font-bold transition-all flex items-center justify-center gap-2 ${
                activeTab === 'camera'
                  ? 'bg-d-primary text-d-on-primary shadow-sm'
                  : 'text-d-on-surface-variant hover:text-d-on-surface hover:bg-d-surface-container-low'
              }`}
            >
              <Camera size={16} />
              <span>Quét Camera Trực tiếp</span>
            </button>
          </div>

          {/* Quick Demo Sample Test Chips */}
          <div className="bg-d-surface-container-lowest border border-d-border-subtle rounded-2xl p-3.5 shadow-xs flex flex-wrap items-center justify-between gap-2.5">
            <span className="text-xs font-bold text-d-on-surface flex items-center gap-1.5">
              <Sparkles size={14} className="text-d-primary" />
              <span>Thử nhanh với mẫu AI:</span>
            </span>
            <div className="flex flex-wrap gap-2">
              {DEMO_SAMPLES.map((sample) => (
                <button
                  key={sample.id}
                  id={`btn-sample-${sample.id}`}
                  type="button"
                  disabled={isScanning}
                  onClick={() => handleSelectSample(sample)}
                  className="px-3 py-1.5 rounded-xl border border-d-primary/40 bg-d-primary/5 hover:bg-d-primary/15 text-xs font-bold text-d-primary flex items-center gap-1.5 transition-all cursor-pointer disabled:opacity-50 shadow-2xs"
                >
                  <MaterialIcon name={sample.icon || 'category'} className="text-sm" />
                  <span>{sample.title}</span>
                </button>
              ))}
            </div>
          </div>

          {/* Viewport Box */}
          <div className="relative bg-black rounded-3xl overflow-hidden border border-d-border-subtle shadow-md aspect-4/3 flex items-center justify-center">
            {/* View 1: Live Video Camera */}
            {activeTab === 'camera' && !selectedImage && (
              <div className="relative w-full h-full flex items-center justify-center bg-black">
                <video
                  ref={videoRef}
                  autoPlay
                  playsInline
                  muted
                  className="w-full h-full object-cover"
                />

                {/* Target Frame / Reticle */}
                <div className="absolute inset-8 border-2 border-dashed border-white/60 rounded-2xl pointer-events-none flex flex-col justify-between p-4">
                  <div className="flex justify-between items-center text-[11px] font-mono font-bold text-white/80 bg-black/40 px-2 py-1 rounded backdrop-blur-xs w-fit">
                    <span>HƯỚNG CAMERA VÀO VẬT THỂ PHẾ LIỆU</span>
                  </div>
                  <div className="text-center text-xs text-white/90 bg-black/50 py-1.5 px-3 rounded-full backdrop-blur-xs self-center">
                    Đặt vật thể vào giữa khung để AI nhận diện chuẩn nhất
                  </div>
                </div>

                {cameraLoading && (
                  <div className="absolute inset-0 bg-black/80 flex flex-col items-center justify-center gap-3 text-white">
                    <Loader2 size={32} className="animate-spin text-d-primary" />
                    <span className="text-sm font-semibold">Đang kích hoạt Camera...</span>
                  </div>
                )}

                {/* Camera Shutter Button */}
                {cameraActive && (
                  <div className="absolute bottom-5 inset-x-0 flex justify-center items-center gap-4">
                    <button
                      type="button"
                      onClick={capturePhoto}
                      className="w-16 h-16 rounded-full bg-white border-4 border-d-primary shadow-lg flex items-center justify-center active:scale-90 transition-transform cursor-pointer"
                      title="Chụp ảnh để AI phân tích"
                    >
                      <div className="w-12 h-12 rounded-full bg-d-primary flex items-center justify-center text-white">
                        <Camera size={22} />
                      </div>
                    </button>
                  </div>
                )}
              </div>
            )}

            {/* View 2: Upload Zone */}
            {activeTab === 'upload' && !selectedImage && (
              <div
                onClick={() => fileInputRef.current?.click()}
                className="w-full h-full flex flex-col items-center justify-center p-6 text-center cursor-pointer bg-d-surface-container-low hover:bg-d-surface-container transition-colors group"
              >
                <div className="w-16 h-16 rounded-2xl bg-d-surface-container text-d-primary flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                  <Upload size={28} />
                </div>
                <h3 className="font-bold text-d-on-surface text-base">
                  Chọn hoặc kéo thả ảnh phế liệu vào đây
                </h3>
                <p className="text-xs text-d-on-surface-variant mt-1 max-w-sm">
                  Hỗ trợ định dạng JPG, PNG, WebP (Tối đa 15MB). Bạn có thể chụp ảnh thùng carton, chai lọ, vỏ lon, giấy báo...
                </p>
                <button
                  type="button"
                  className="mt-4 px-5 py-2 rounded-full bg-d-primary text-d-on-primary text-xs font-bold shadow-xs hover:bg-[#385500] transition-colors"
                >
                  Chọn tệp từ máy
                </button>
              </div>
            )}

            {/* View 3: Image Preview with AI Annotation */}
            {selectedImage && (
              <div className="relative w-full h-full flex items-center justify-center bg-zinc-950">
                <img
                  src={aiResult?.annotated_image || selectedImage.preview}
                  alt="Scanned waste"
                  className="w-full h-full object-contain"
                />

                {/* Scanning Laser Animation */}
                {isScanning && (
                  <div className="absolute inset-0 bg-black/40 backdrop-blur-xs flex flex-col items-center justify-center p-6 text-white text-center">
                    {/* Animated laser line */}
                    <div className="absolute inset-x-0 h-1 bg-gradient-to-r from-transparent via-emerald-400 to-transparent shadow-[0_0_15px_#10b981] animate-bounce" />
                    <Loader2 size={36} className="animate-spin text-emerald-400 mb-3" />
                    <p className="font-bold text-base tracking-wide">
                      Mô hình AI đang phân tích phế liệu...
                    </p>
                    <p className="text-xs text-zinc-300 mt-1 max-w-xs">
                      Đang chạy YOLOv8 định vị đối tượng, trích xuất OCR văn bản và xếp loại EfficientNet-B4...
                    </p>
                  </div>
                )}
              </div>
            )}
          </div>

          {/* Action Bar under Viewport */}
          <div className="flex items-center justify-between gap-3">
            <input
              ref={fileInputRef}
              type="file"
              accept="image/*"
              onChange={handleFileSelect}
              className="hidden"
            />

            {selectedImage ? (
              <button
                type="button"
                onClick={resetScanner}
                className="inline-flex items-center gap-2 px-4 py-2 rounded-xl border border-d-border-subtle bg-d-surface-container-lowest hover:bg-d-surface-container text-xs font-bold text-d-on-surface transition-colors cursor-pointer"
              >
                <RefreshCw size={14} />
                <span>Quét ảnh khác</span>
              </button>
            ) : (
              <span className="text-xs text-d-on-surface-variant">
                Gợi ý: Chụp ở nơi đủ sáng để đạt độ chính xác tối đa.
              </span>
            )}

            {selectedImage && !isScanning && !aiResult && (
              <button
                type="button"
                onClick={() => runScan(selectedImage.file, selectedImage.base64)}
                className="inline-flex items-center gap-2 px-5 py-2.5 rounded-full bg-d-primary text-d-on-primary text-xs font-bold hover:bg-[#385500] transition-colors cursor-pointer"
              >
                <Sparkles size={14} />
                <span>Phân tích lại</span>
              </button>
            )}
          </div>
        </div>

        {/* Right Column: AI Analysis Results Card (5 cols) */}
        <div className="lg:col-span-5 space-y-4">
          {!aiResult && !isScanning && (
            <div className="bg-d-surface-container-lowest border border-d-border-subtle rounded-3xl p-6 text-center space-y-4 shadow-sm">
              <div className="w-14 h-14 rounded-2xl bg-d-primary-container text-d-on-primary-container flex items-center justify-center mx-auto">
                <MaterialIcon name="smart_toy" className="text-3xl text-d-primary" />
              </div>
              <div>
                <h3 className="font-bold text-d-on-surface text-base">
                  Sẵn sàng nhận diện phế liệu
                </h3>
                <p className="text-xs text-d-on-surface-variant mt-1.5 leading-relaxed">
                  Chụp ảnh hoặc chọn ảnh phế liệu của bạn. Trí tuệ nhân tạo sẽ tự động nhận biết loại vật liệu, giá bán tham khảo và tư vấn cách phân loại tối ưu.
                </p>
              </div>

              {/* Supported Materials Tags */}
              <div className="pt-2 border-t border-d-border-subtle">
                <p className="text-[11px] font-bold text-d-on-surface-variant uppercase tracking-wider mb-2.5">
                  12 Nhóm phế liệu AI hỗ trợ:
                </p>
                <div className="flex flex-wrap gap-1.5 justify-center">
                  {[
                    'Bìa Carton', 'Giấy vụn', 'Chai nhựa PET', 'Vỏ lon nhôm',
                    'Sắt phế liệu', 'Chai thủy tinh', 'Pin cũ', 'Vải vụn',
                  ].map((cat) => (
                    <span
                      key={cat}
                      className="text-[11px] px-2.5 py-1 rounded-lg bg-d-surface-container text-d-on-surface font-medium"
                    >
                      {cat}
                    </span>
                  ))}
                </div>
              </div>
            </div>
          )}

          {/* AI Result Card */}
          {aiResult && (
            <div className="bg-d-surface-container-lowest border border-d-border-subtle rounded-3xl p-6 shadow-md space-y-5">
              {/* Top Result Banner */}
              <div className="flex items-start justify-between gap-3 pb-4 border-b border-d-border-subtle">
                <div className="flex items-start gap-3">
                  <div className="w-12 h-12 rounded-2xl bg-d-primary text-d-on-primary flex items-center justify-center shrink-0 shadow-sm">
                    <MaterialIcon name={aiResult.icon || 'category'} className="text-2xl" />
                  </div>
                  <div>
                    <div className="flex items-center gap-2">
                      <h3 className="font-bold text-d-on-surface text-lg">
                        {aiResult.name_vi}
                      </h3>
                    </div>
                    <p className="text-xs text-d-on-surface-variant font-mono mt-0.5">
                      Class: {aiResult.primary_class} | Detection: {aiResult.box_status}
                    </p>
                  </div>
                </div>

                {/* Recyclability Badge */}
                <div className="shrink-0">
                  {aiResult.is_recyclable ? (
                    <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full bg-emerald-500/10 text-emerald-600 border border-emerald-500/20 text-xs font-bold">
                      <CheckCircle2 size={12} />
                      <span>Thu gom được</span>
                    </span>
                  ) : (
                    <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full bg-amber-500/10 text-amber-600 border border-amber-500/20 text-xs font-bold">
                      <AlertTriangle size={12} />
                      <span>Rác không thu</span>
                    </span>
                  )}
                </div>
              </div>

              {/* Confidence Meter */}
              <div className="space-y-1.5">
                <div className="flex justify-between text-xs font-bold">
                  <span className="text-d-on-surface">Độ tin cậy nhận diện:</span>
                  <span className="text-d-primary font-mono text-sm">{aiResult.confidence}%</span>
                </div>
                <div className="w-full h-2.5 bg-d-surface-container rounded-full overflow-hidden">
                  <div
                    className="h-full bg-d-primary transition-all duration-500 rounded-full"
                    style={{ width: `${Math.min(aiResult.confidence, 100)}%` }}
                  />
                </div>
              </div>

              {/* Price Estimate Card */}
              <div className="p-4 rounded-2xl bg-d-surface-container-low border border-d-border-subtle space-y-1.5">
                <div className="flex items-center justify-between">
                  <span className="text-xs font-bold text-d-on-surface flex items-center gap-1.5">
                    <MaterialIcon name="payments" className="text-d-primary text-base" />
                    <span>Giá thu mua tham khảo:</span>
                  </span>
                  <span className="text-xs font-bold text-d-primary bg-d-primary/10 px-2.5 py-0.5 rounded-full">
                    {aiResult.unit || 'kg'}
                  </span>
                </div>
                <div className="text-lg font-bold text-d-on-surface">
                  {aiResult.current_market_price
                    ? `${aiResult.current_market_price.toLocaleString('vi-VN')} đ/kg (Bảng giá sàn ReTrack)`
                    : aiResult.price_range}
                </div>
                <p className="text-[11px] text-d-on-surface-variant">
                  * Mức giá thực tế sẽ do kho vựa đối tác thẩm định và cân chuẩn khi điều phối thu gom.
                </p>
              </div>

              {/* OCR Text if detected */}
              {aiResult.ocr_text && (
                <div className="p-3.5 rounded-xl bg-d-surface-container-lowest border border-d-border-subtle text-xs space-y-1">
                  <span className="font-bold text-d-on-surface flex items-center gap-1.5">
                    <MaterialIcon name="document_scanner" className="text-sm text-d-primary" />
                    <span>Chữ / Nhãn hiệu nhận diện (OCR):</span>
                  </span>
                  <p className="text-d-on-surface-variant font-mono bg-d-surface-container-low p-2 rounded-lg break-all">
                    "{aiResult.ocr_text}"
                  </p>
                </div>
              )}

              {/* Recycling Tips */}
              {aiResult.recycling_tip && (
                <div className="p-3.5 rounded-xl bg-blue-500/5 border border-blue-500/20 text-xs space-y-1">
                  <span className="font-bold text-blue-700 dark:text-blue-400 flex items-center gap-1.5">
                    <ShieldCheck size={14} />
                    <span>Lời khuyên xử lý & phân loại:</span>
                  </span>
                  <p className="text-d-on-surface-variant leading-relaxed">
                    {aiResult.recycling_tip}
                  </p>
                </div>
              )}

              {/* Top 5 Breakdown */}
              {aiResult.top_predictions && aiResult.top_predictions.length > 1 && (
                <div className="space-y-2 pt-2 border-t border-d-border-subtle">
                  <span className="text-[11px] font-bold text-d-on-surface-variant uppercase tracking-wider block">
                    Xác suất các nhóm tương đồng:
                  </span>
                  <div className="space-y-1.5">
                    {aiResult.top_predictions.slice(1, 4).map((pred) => (
                      <div key={pred.class_key} className="flex items-center justify-between text-xs">
                        <span className="text-d-on-surface truncate max-w-[200px]">{pred.name_vi}</span>
                        <span className="text-d-on-surface-variant font-mono">{pred.confidence}%</span>
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {/* Primary Call to Action */}
              <div className="pt-3">
                <button
                  id="btn-proceed-create-request"
                  type="button"
                  onClick={handleProceedToCreateRequest}
                  className="w-full py-3.5 px-6 rounded-full bg-d-primary text-d-on-primary font-bold text-sm shadow-md hover:bg-[#385500] active:scale-95 transition-all flex items-center justify-center gap-2 cursor-pointer"
                >
                  <span>Tạo đơn thu gom với kết quả này</span>
                  <ArrowRight size={16} />
                </button>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
