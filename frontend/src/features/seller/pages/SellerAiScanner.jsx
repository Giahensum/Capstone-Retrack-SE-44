import { useState, useRef, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import { sellerApi } from '../api';
import { MaterialIcon } from '@/features/depot/components/DepotIcon';
import GoongAutocomplete from '@/components/ui/GoongAutocomplete';
import {
  Loader2, Camera, Upload, Sparkles, RefreshCw, ArrowRight,
  CheckCircle2, AlertTriangle, ShieldCheck, Check, Layers,
  MapPin, Scale, Award, Truck, Store, Calculator,
  ChevronDown, ChevronUp, Coins, Info,
} from 'lucide-react';
import { normalizeAiPredictions } from '../aiWasteMetadata';
import {
  calculateDepotsDistanceWithGoong,
  calculateSmartPriceRecommendation,
  QUALITY_CONDITIONS,
  FULFILLMENT_METHODS,
} from '../pricingRecommendation';
import PriceForecastWidget from '../components/PriceForecastWidget';

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
  const [selectedPredictionIndex, setSelectedPredictionIndex] = useState(0);
  const [aiStatus, setAiStatus] = useState(null);

  // ── Smart Price Recommendation inputs & state ──
  const [userLocation, setUserLocation] = useState({ address: '', lat: null, lng: null });
  const [estimatedWeight, setEstimatedWeight] = useState(10);
  const [qualityCondition, setQualityCondition] = useState('STANDARD'); // 'CLEAN_COMPACTED' | 'STANDARD' | 'DIRTY_MIXED'
  const [fulfillmentMethod, setFulfillmentMethod] = useState('DOORSTEP'); // 'DOORSTEP' | 'DROP_OFF'
  const [_depotsList, setDepotsList] = useState([]);
  const [nearestDepot, setNearestDepot] = useState(null);
  const [isCalculatingDistance, setIsCalculatingDistance] = useState(false);
  const [showPricingBreakdown, setShowPricingBreakdown] = useState(false);

  // Check AI Service status on mount
  useEffect(() => {
    sellerApi.getAiStatus()
      .then((res) => setAiStatus(res))
      .catch(() => setAiStatus({ status: 'offline' }));
  }, []);

  // Tự động tải địa chỉ từ Profile người bán nếu đã lưu
  useEffect(() => {
    sellerApi.getProfile()
      .then((profile) => {
        if (profile?.address) {
          setUserLocation({
            address: profile.address,
            lat: profile.latitude ?? null,
            lng: profile.longitude ?? null,
          });
          if (profile.latitude != null && profile.longitude != null) {
            loadNearbyDepots(profile.latitude, profile.longitude);
          }
        }
      })
      .catch(() => {});
  }, []);

  const loadNearbyDepots = async (lat, lng) => {
    if (lat == null || lng == null) return;
    setIsCalculatingDistance(true);
    try {
      const rawDepots = await sellerApi.getNearbyDepots(lat, lng);
      const sorted = await calculateDepotsDistanceWithGoong(rawDepots, lat, lng);
      setDepotsList(sorted);
      setNearestDepot(sorted.length > 0 ? sorted[0] : null);
    } catch (err) {
      console.warn('Lỗi tính toán khoảng cách kho vựa:', err);
    } finally {
      setIsCalculatingDistance(false);
    }
  };

  const handleLocationSelect = ({ address, lat, lng }) => {
    setUserLocation({ address, lat, lng });
    if (lat != null && lng != null) {
      loadNearbyDepots(lat, lng);
    }
  };

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
        setSelectedPredictionIndex(0);
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
    setSelectedPredictionIndex(0);
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

  // Transfer AI Result to Create Request (cho phép chọn bất kỳ loại nào trong Top 5 kèm giá đề xuất thông minh)
  const handleProceedToCreateRequest = (targetItem = null, customPrice = null) => {
    if (!aiResult) return;
    const predictions = normalizeAiPredictions(aiResult);
    const itemToUse = targetItem || predictions[selectedPredictionIndex] || predictions[0];
    if (!itemToUse) return;

    const calcPrice = customPrice || calculateSmartPriceRecommendation({
      wasteItem: itemToUse,
      nearestDepot,
      weightKg: Number(estimatedWeight) || 1,
      qualityId: qualityCondition,
      fulfillment: fulfillmentMethod,
      userLocation,
    });

    const qualityMeta = QUALITY_CONDITIONS.find((q) => q.id === qualityCondition);

    navigate('/seller/create', {
      state: {
        aiResult: {
          ...aiResult,
          primary_class: itemToUse.class_key,
          name_vi: itemToUse.name_vi,
          confidence: itemToUse.confidence,
          icon: itemToUse.icon,
          price_range: itemToUse.price_range,
          unit: itemToUse.unit,
          recycling_tip: itemToUse.recycling_tip,
          suggested_description: `Phế liệu ${itemToUse.name_vi} (AI nhận diện: ${itemToUse.confidence}%), khối lượng ước tính ~${estimatedWeight}kg. Tình trạng: ${qualityMeta?.shortLabel || 'Chuẩn'}. Giá AI đề xuất: ${calcPrice.recommendedUnitPrice.toLocaleString('vi-VN')} đ/kg.`,
          is_recyclable: itemToUse.is_recyclable,
          material_type: itemToUse.material_type,
          selectedRank: itemToUse.rank,
          top_predictions: predictions,
          imagePreview: selectedImage?.preview,
          imageFile: selectedImage?.file,

          // Các trường thị trường thông minh truyền sang CreateRequest
          address: userLocation.address,
          latitude: userLocation.lat,
          longitude: userLocation.lng,
          targetDepotId: nearestDepot?.id || null,
          nearestDepotName: nearestDepot?.name || null,
          estimatedWeight: Number(estimatedWeight) || 10,
          recommendedPricePerKg: calcPrice.recommendedUnitPrice,
          estimatedTotalPayout: calcPrice.estimatedTotal,
          qualityCondition,
          fulfillmentMethod,
          priceSource: calcPrice.priceSource,
          breakdown: calcPrice.breakdown,
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
          {aiResult && (() => {
            const predictions = normalizeAiPredictions(aiResult);
            const currentSelected = predictions[selectedPredictionIndex] || predictions[0];
            const smartPrice = calculateSmartPriceRecommendation({
              wasteItem: currentSelected,
              nearestDepot,
              weightKg: Number(estimatedWeight) || 1,
              qualityId: qualityCondition,
              fulfillment: fulfillmentMethod,
              userLocation,
            });

            return (
              <div className="bg-d-surface-container-lowest border border-d-border-subtle rounded-3xl p-6 shadow-md space-y-6">
                {/* Header Banner: Currently Selected Waste Material */}
                <div className="flex items-start justify-between gap-3 pb-4 border-b border-d-border-subtle">
                  <div className="flex items-start gap-3">
                    <div className="w-12 h-12 rounded-2xl bg-d-primary text-d-on-primary flex items-center justify-center shrink-0 shadow-sm">
                      <MaterialIcon name={currentSelected.icon || 'category'} className="text-2xl" />
                    </div>
                    <div>
                      <div className="flex items-center gap-2 flex-wrap">
                        <h3 className="font-bold text-d-on-surface text-lg">
                          {currentSelected.name_vi}
                        </h3>
                        {currentSelected.is_top1 ? (
                          <span className="inline-flex items-center gap-1 text-[11px] font-bold px-2 py-0.5 rounded-full bg-amber-500/15 text-amber-700 dark:text-amber-300 border border-amber-500/30">
                            🏆 Top 1 (Khuyến nghị cao nhất)
                          </span>
                        ) : (
                          <span className="inline-flex items-center gap-1 text-[11px] font-bold px-2 py-0.5 rounded-full bg-blue-500/10 text-blue-600 dark:text-blue-400 border border-blue-500/20">
                            Đã chọn Top {currentSelected.rank}
                          </span>
                        )}
                      </div>
                      <p className="text-xs text-d-on-surface-variant font-mono mt-0.5">
                        Mã: {currentSelected.class_key} • Độ tin cậy: {currentSelected.confidence}% • Detection: {aiResult.box_status}
                      </p>
                    </div>
                  </div>

                  {/* Recyclability Badge */}
                  <div className="shrink-0">
                    {currentSelected.is_recyclable ? (
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

                {/* Top 5 Selector Section */}
                <div className="space-y-3">
                  <div>
                    <h4 className="font-bold text-d-on-surface text-sm flex items-center gap-1.5">
                      <Layers size={16} className="text-d-primary" />
                      <span>Chọn loại phế liệu để tạo đơn (Top 5 AI nhận diện):</span>
                    </h4>
                    <p className="text-[11px] text-d-on-surface-variant mt-0.5">
                      Hệ thống tự động ưu tiên Top 1 có độ khớp cao nhất. Bạn có thể bấm chọn bất kỳ loại nào trong 5 gợi ý bên dưới để tạo đơn thu gom:
                    </p>
                  </div>

                  {/* Cards List for Top 5 */}
                  <div className="space-y-2">
                    {predictions.map((pred, idx) => {
                      const isSelected = selectedPredictionIndex === idx;
                      return (
                        <div
                          key={pred.class_key || idx}
                          onClick={() => setSelectedPredictionIndex(idx)}
                          className={`p-3.5 rounded-2xl border transition-all cursor-pointer flex items-center justify-between gap-3 ${
                            isSelected
                              ? 'border-2 border-d-primary bg-d-primary/5 shadow-xs ring-1 ring-d-primary/20'
                              : 'border-d-border-subtle bg-d-surface-container-low hover:border-d-primary/40 hover:bg-d-surface-container'
                          }`}
                        >
                          <div className="flex items-center gap-3 min-w-0">
                            {/* Radio check icon */}
                            <div className="shrink-0">
                              {isSelected ? (
                                <div className="w-5 h-5 rounded-full bg-d-primary text-d-on-primary flex items-center justify-center">
                                  <Check size={12} strokeWidth={3} />
                                </div>
                              ) : (
                                <div className="w-5 h-5 rounded-full border-2 border-d-border-subtle" />
                              )}
                            </div>

                            {/* Icon */}
                            <div className={`w-9 h-9 rounded-xl flex items-center justify-center shrink-0 ${
                              isSelected ? 'bg-d-primary text-d-on-primary' : 'bg-d-surface-container text-d-on-surface-variant'
                            }`}>
                              <MaterialIcon name={pred.icon || 'category'} className="text-lg" />
                            </div>

                            {/* Name & price */}
                            <div className="min-w-0">
                              <div className="flex items-center gap-2 flex-wrap">
                                <span className="font-bold text-sm text-d-on-surface truncate">
                                  {pred.name_vi}
                                </span>
                                {pred.is_top1 ? (
                                  <span className="text-[10px] font-bold px-1.5 py-0.2 rounded-md bg-amber-500/20 text-amber-700 dark:text-amber-300">
                                    🏆 Top 1
                                  </span>
                                ) : (
                                  <span className="text-[10px] font-semibold px-1.5 py-0.2 rounded-md bg-d-surface-container text-d-on-surface-variant">
                                    Top {idx + 1}
                                  </span>
                                )}
                              </div>
                              <p className="text-[11px] text-d-on-surface-variant truncate mt-0.5">
                                Giá tham khảo: <strong className="text-d-on-surface">{pred.price_range}</strong>
                              </p>
                            </div>
                          </div>

                          {/* Confidence & Action */}
                          <div className="flex items-center gap-3 shrink-0">
                            <div className="text-right">
                              <span className={`font-mono font-bold text-sm block ${
                                isSelected ? 'text-d-primary' : 'text-d-on-surface-variant'
                              }`}>
                                {pred.confidence}%
                              </span>
                              <span className="text-[10px] text-d-on-surface-variant">độ khớp</span>
                            </div>

                            <button
                              type="button"
                              onClick={(e) => {
                                e.stopPropagation();
                                setSelectedPredictionIndex(idx);
                                handleProceedToCreateRequest(pred);
                              }}
                              className={`px-3 py-1.5 rounded-full text-xs font-bold transition-all shrink-0 ${
                                isSelected
                                  ? 'bg-d-primary text-d-on-primary shadow-xs hover:bg-[#385500]'
                                  : 'bg-d-surface-container text-d-on-surface hover:bg-d-primary/10 hover:text-d-primary'
                              }`}
                            >
                              {isSelected ? 'Tạo đơn ngay' : 'Chọn tạo đơn'}
                            </button>
                          </div>
                        </div>
                      );
                    })}
                  </div>
                </div>

                {/* Confidence Meter for selected */}
                <div className="space-y-1.5 pt-2 border-t border-d-border-subtle">
                  <div className="flex justify-between text-xs font-bold">
                    <span className="text-d-on-surface">Độ tin cậy của lựa chọn ({currentSelected.name_vi}):</span>
                    <span className="text-d-primary font-mono text-sm">{currentSelected.confidence}%</span>
                  </div>
                  <div className="w-full h-2.5 bg-d-surface-container rounded-full overflow-hidden">
                    <div
                      className="h-full bg-d-primary transition-all duration-500 rounded-full"
                      style={{ width: `${Math.min(currentSelected.confidence, 100)}%` }}
                    />
                  </div>
                </div>

                {/* ── Dynamic AI Market Pricing Recommendation Engine ── */}
                {!currentSelected.is_recyclable ? (
                  /* Card từ chối rác không thu mua */
                  <div className="p-4 rounded-2xl bg-amber-500/10 border border-amber-500/20 space-y-2">
                    <div className="flex items-center gap-2 text-amber-700 dark:text-amber-400 font-bold text-sm">
                      <AlertTriangle size={18} />
                      <span>Danh mục không thu gom ({currentSelected.name_vi})</span>
                    </div>
                    <div className="text-lg font-bold text-d-on-surface">
                      Mức giá: Không thu mua
                    </div>
                    <p className="text-xs text-d-on-surface-variant leading-relaxed">
                      {currentSelected.recycling_tip || 'Vật phẩm này không thuộc danh mục phế liệu tái chế của ReTrack. Vui lòng cho vào túi rác sinh hoạt đô thị hoặc ủ phân vi sinh.'}
                    </p>
                  </div>
                ) : (
                  /* Khối tương tác tính giá thị trường AI */
                  <div className="rounded-3xl border border-emerald-500/30 bg-d-surface-container-low/70 p-5 space-y-5 shadow-xs">
                    {/* Header: AI Market Valuation Tool */}
                    <div className="flex items-start justify-between gap-3 pb-3 border-b border-d-border-subtle">
                      <div className="flex items-center gap-2.5">
                        <div className="w-9 h-9 rounded-xl bg-emerald-500/15 text-emerald-600 dark:text-emerald-400 flex items-center justify-center shrink-0">
                          <Calculator size={18} />
                        </div>
                        <div>
                          <h4 className="font-bold text-d-on-surface text-sm flex items-center gap-1.5">
                            <span>Định giá & Gợi ý thị trường thông minh AI</span>
                          </h4>
                          <p className="text-[11px] text-d-on-surface-variant">
                            Tính toán giá thu mua tối ưu theo địa điểm thật (Goong.io), khối lượng và tình trạng
                          </p>
                        </div>
                      </div>
                      <span className="shrink-0 inline-flex items-center gap-1 text-[10px] font-bold px-2 py-0.5 rounded-full bg-emerald-500/15 text-emerald-700 dark:text-emerald-300 border border-emerald-500/30">
                        <Coins size={11} />
                        <span>Dữ liệu thực tế</span>
                      </span>
                    </div>

                    {/* Trường 1: Địa điểm thu gom thực tế (Goong.io) */}
                    <div className="space-y-1.5">
                      <div className="flex items-center justify-between">
                        <label className="text-xs font-bold text-d-on-surface flex items-center gap-1.5">
                          <MapPin size={14} className="text-d-primary" />
                          <span>Địa điểm thu mua thực tế:</span>
                        </label>
                        {isCalculatingDistance && (
                          <span className="text-[11px] text-d-primary flex items-center gap-1 font-semibold animate-pulse">
                            <Loader2 size={12} className="animate-spin" /> Đang tính cự ly Goong...
                          </span>
                        )}
                      </div>
                      <GoongAutocomplete
                        value={userLocation.address}
                        onChange={(addr) => setUserLocation((prev) => ({ ...prev, address: addr }))}
                        onSelect={handleLocationSelect}
                        placeholder="Gõ số nhà, tên đường hoặc bấm biểu tượng GPS định vị vị trí..."
                      />
                      {nearestDepot ? (
                        <div className="p-3 rounded-2xl bg-emerald-500/10 border border-emerald-500/20 text-xs flex items-center justify-between flex-wrap gap-2">
                          <div className="flex items-center gap-2 min-w-0">
                            <div className="w-8 h-8 rounded-lg bg-emerald-600 text-white flex items-center justify-center shrink-0">
                              <Store size={15} />
                            </div>
                            <div className="min-w-0">
                              <div className="flex items-center gap-1.5 flex-wrap">
                                <span className="font-bold text-d-on-surface truncate">
                                  Kho gần bạn nhất: {nearestDepot.name}
                                </span>
                                {smartPrice.depotPublishedPrice && (
                                  <span className="text-[10px] px-1.5 py-0.5 rounded-md bg-emerald-600/20 text-emerald-800 dark:text-emerald-300 font-bold">
                                    Giá niêm yết: {smartPrice.depotPublishedPrice.toLocaleString('vi-VN')} đ/kg
                                  </span>
                                )}
                              </div>
                              <p className="text-[11px] text-d-on-surface-variant truncate mt-0.5">
                                {nearestDepot.address}
                              </p>
                            </div>
                          </div>
                          <div className="text-right shrink-0">
                            <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full bg-emerald-600 text-white font-bold text-xs shadow-2xs">
                              {nearestDepot.routingDistanceKm != null ? `${nearestDepot.routingDistanceKm.toFixed(1)} km` : '~2.0 km'}
                              {nearestDepot.routingDurationText && ` (${nearestDepot.routingDurationText})`}
                            </span>
                            <p className="text-[9px] text-emerald-700 dark:text-emerald-400 font-mono mt-0.5">
                              {nearestDepot.isGoongRouting ? 'Tuyến xe máy Goong.io' : 'Cự ly ước tính'}
                            </p>
                          </div>
                        </div>
                      ) : (
                        <p className="text-[11px] text-d-on-surface-variant italic">
                          * Bạn có thể chọn địa chỉ cụ thể để hệ thống tìm kho vựa gần nhất và tính cước vận chuyển chuẩn xác.
                        </p>
                      )}
                    </div>

                    {/* Trường 2: Khối lượng ước tính */}
                    <div className="space-y-1.5">
                      <div className="flex items-center justify-between">
                        <label className="text-xs font-bold text-d-on-surface flex items-center gap-1.5">
                          <Scale size={14} className="text-d-primary" />
                          <span>Khối lượng ước tính ({currentSelected.unit || 'kg'}):</span>
                        </label>
                        <span className="text-xs font-bold text-d-primary">
                          {estimatedWeight} {currentSelected.unit || 'kg'}
                        </span>
                      </div>
                      <div className="flex items-center gap-2 flex-wrap">
                        <div className="relative">
                          <input
                            type="number"
                            min="1"
                            max="5000"
                            value={estimatedWeight}
                            onChange={(e) => setEstimatedWeight(Math.max(1, Number(e.target.value) || 1))}
                            className="w-28 px-3 py-2 rounded-xl border border-d-border-subtle bg-d-surface text-d-on-surface text-sm font-bold focus:outline-d-primary text-center"
                          />
                        </div>
                        <div className="flex flex-wrap gap-1.5">
                          {[5, 10, 20, 50, 100].map((w) => (
                            <button
                              key={w}
                              type="button"
                              onClick={() => setEstimatedWeight(w)}
                              className={`px-3 py-1.5 rounded-xl text-xs font-bold transition-all cursor-pointer ${
                                estimatedWeight === w
                                  ? 'bg-d-primary text-d-on-primary shadow-xs'
                                  : 'bg-d-surface-container text-d-on-surface-variant hover:bg-d-surface-container-high'
                              }`}
                            >
                              {w} kg
                            </button>
                          ))}
                        </div>
                      </div>
                    </div>

                    {/* Trường 3: Tình trạng phân loại chất lượng */}
                    <div className="space-y-1.5">
                      <label className="text-xs font-bold text-d-on-surface flex items-center gap-1.5">
                        <Award size={14} className="text-d-primary" />
                        <span>Tình trạng phân loại thực tế:</span>
                      </label>
                      <div className="grid grid-cols-1 sm:grid-cols-3 gap-2">
                        {QUALITY_CONDITIONS.map((cond) => {
                          const isSelected = qualityCondition === cond.id;
                          return (
                            <div
                              key={cond.id}
                              onClick={() => setQualityCondition(cond.id)}
                              className={`p-3 rounded-2xl border transition-all cursor-pointer flex flex-col justify-between gap-1.5 ${
                                isSelected
                                  ? 'border-2 border-d-primary bg-d-primary/5 shadow-xs ring-1 ring-d-primary/20'
                                  : 'border-d-border-subtle bg-d-surface-container-low hover:border-d-primary/30'
                              }`}
                            >
                              <div className="flex items-center justify-between">
                                <span className="font-bold text-xs text-d-on-surface">{cond.shortLabel}</span>
                                {isSelected ? (
                                  <div className="w-4 h-4 rounded-full bg-d-primary text-d-on-primary flex items-center justify-center">
                                    <Check size={10} strokeWidth={3} />
                                  </div>
                                ) : (
                                  <div className="w-4 h-4 rounded-full border border-d-border-subtle" />
                                )}
                              </div>
                              <p className="text-[10px] text-d-on-surface-variant leading-tight">{cond.desc}</p>
                            </div>
                          );
                        })}
                      </div>
                    </div>

                    {/* Trường 4: Hình thức thu gom */}
                    <div className="space-y-1.5">
                      <label className="text-xs font-bold text-d-on-surface flex items-center gap-1.5">
                        <Truck size={14} className="text-d-primary" />
                        <span>Hình thức bàn giao thu gom:</span>
                      </label>
                      <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                        {FULFILLMENT_METHODS.map((method) => {
                          const isSelected = fulfillmentMethod === method.id;
                          return (
                            <div
                              key={method.id}
                              onClick={() => setFulfillmentMethod(method.id)}
                              className={`p-3 rounded-2xl border transition-all cursor-pointer flex items-center gap-3 ${
                                isSelected
                                  ? 'border-2 border-d-primary bg-d-primary/5 shadow-xs ring-1 ring-d-primary/20'
                                  : 'border-d-border-subtle bg-d-surface-container-low hover:border-d-primary/30'
                              }`}
                            >
                              <div className={`w-9 h-9 rounded-xl flex items-center justify-center shrink-0 ${
                                isSelected ? 'bg-d-primary text-d-on-primary' : 'bg-d-surface-container text-d-on-surface-variant'
                              }`}>
                                <MaterialIcon name={method.icon} className="text-lg" />
                              </div>
                              <div className="min-w-0">
                                <div className="flex items-center justify-between">
                                  <p className="font-bold text-xs text-d-on-surface truncate">{method.label}</p>
                                  {isSelected && <Check size={12} className="text-d-primary stroke-[3]" />}
                                </div>
                                <p className="text-[10px] text-d-on-surface-variant line-clamp-1 mt-0.5">{method.desc}</p>
                              </div>
                            </div>
                          );
                        })}
                      </div>
                    </div>

                    {/* KẾT QUẢ ĐỀ XUẤT GIÁ THỊ TRƯỜNG THÔNG MINH (BANNER) */}
                    <div className="p-4 rounded-2xl bg-gradient-to-br from-emerald-500/15 via-emerald-500/5 to-transparent border border-emerald-500/30 space-y-3">
                      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                        <div>
                          <div className="flex items-center gap-1.5 text-xs font-bold text-emerald-800 dark:text-emerald-300">
                            <Coins size={15} />
                            <span>MỨC GIÁ THỊ TRƯỜNG AI KHUYẾN NGHỊ:</span>
                          </div>
                          <div className="text-2xl font-black text-d-on-surface tracking-tight mt-0.5 flex items-baseline gap-1">
                            <span>{smartPrice.recommendedUnitPrice.toLocaleString('vi-VN')}</span>
                            <span className="text-xs font-bold text-d-on-surface-variant">đ/{currentSelected.unit || 'kg'}</span>
                          </div>
                        </div>

                        <div className="sm:text-right bg-white dark:bg-d-surface-container-low p-2.5 rounded-xl border border-emerald-500/20 shadow-xs">
                          <span className="text-[11px] font-semibold text-d-on-surface-variant block">
                            Ước tính số tiền thực nhận (~{estimatedWeight} {currentSelected.unit || 'kg'}):
                          </span>
                          <span className="text-lg font-black text-emerald-600 dark:text-emerald-400">
                            ~ {smartPrice.estimatedTotal.toLocaleString('vi-VN')} đ
                          </span>
                        </div>
                      </div>

                      {/* Source tag & Breakdown Toggle */}
                      <div className="flex items-center justify-between pt-2 border-t border-emerald-500/20 text-xs flex-wrap gap-2">
                        <span className="text-[11px] text-d-on-surface-variant flex items-center gap-1">
                          <CheckCircle2 size={12} className="text-emerald-600" />
                          {smartPrice.priceSource === 'DEPOT_PUBLISHED'
                            ? `Khớp trực tiếp từ bảng giá niêm yết của ${nearestDepot?.name}`
                            : 'Tham chiếu theo bảng giá thị trường chuẩn ReTrack'}
                        </span>
                        <button
                          type="button"
                          onClick={() => setShowPricingBreakdown(!showPricingBreakdown)}
                          className="text-[11px] font-bold text-emerald-700 dark:text-emerald-400 hover:underline flex items-center gap-0.5 cursor-pointer"
                        >
                          <span>{showPricingBreakdown ? 'Thu gọn' : 'Xem công thức định giá'}</span>
                          {showPricingBreakdown ? <ChevronUp size={13} /> : <ChevronDown size={13} />}
                        </button>
                      </div>

                      {/* Detailed Pricing Breakdown Accordion */}
                      {showPricingBreakdown && smartPrice.breakdown && (
                        <div className="p-3 rounded-xl bg-d-surface-container-lowest border border-d-border-subtle text-xs space-y-1.5 animate-fadeIn">
                          <p className="font-bold text-d-on-surface text-[11px] flex items-center gap-1">
                            <Info size={12} className="text-d-primary" />
                            <span>Bóc tách công thức định giá phế liệu thực tế (Zero Hallucination):</span>
                          </p>
                          <div className="space-y-1 text-[11px]">
                            <div className="flex justify-between text-d-on-surface-variant">
                              <span>• Giá cơ sở ({smartPrice.priceSource === 'DEPOT_PUBLISHED' ? `Kho ${nearestDepot?.name}` : 'Thị trường chuẩn'}):</span>
                              <span className="font-mono font-bold text-d-on-surface">{smartPrice.breakdown.basePrice.toLocaleString('vi-VN')} đ/kg</span>
                            </div>
                            <div className="flex justify-between text-d-on-surface-variant">
                              <span>• Tình trạng ({smartPrice.breakdown.qualityLabel}):</span>
                              <span className={`font-mono font-bold ${smartPrice.breakdown.qualityAdjustment >= 0 ? 'text-emerald-600' : 'text-amber-600'}`}>
                                {smartPrice.breakdown.qualityAdjustment >= 0 ? '+' : ''}{smartPrice.breakdown.qualityAdjustment.toLocaleString('vi-VN')} đ/kg ({smartPrice.breakdown.qualityRatePercent > 0 ? `+${smartPrice.breakdown.qualityRatePercent}%` : `${smartPrice.breakdown.qualityRatePercent}%`})
                              </span>
                            </div>
                            {smartPrice.breakdown.volumeIncentivePerKg > 0 && (
                              <div className="flex justify-between text-d-on-surface-variant">
                                <span>• Trợ giá số lượng lớn ({smartPrice.breakdown.weightKg} kg):</span>
                                <span className="font-mono font-bold text-emerald-600">+{smartPrice.breakdown.volumeIncentivePerKg.toLocaleString('vi-VN')} đ/kg</span>
                              </div>
                            )}
                            <div className="flex justify-between text-d-on-surface-variant">
                              <span>• Chi phí điều phối xe ({fulfillmentMethod === 'DOORSTEP' ? `Tận nơi · ${smartPrice.distanceKm ? `${smartPrice.distanceKm} km` : '~2.5 km'}` : 'Tự mang đến kho'}):</span>
                              <span className="font-mono font-bold text-d-on-surface">
                                {smartPrice.breakdown.logisticsDeductionPerKg > 0
                                  ? `-${smartPrice.breakdown.logisticsDeductionPerKg.toLocaleString('vi-VN')} đ/kg`
                                  : '0 đ (Miễn phí điều phối)'}
                              </span>
                            </div>
                            <div className="pt-1.5 border-t border-d-border-subtle flex justify-between font-bold text-d-on-surface">
                              <span>= Đơn giá khuyến nghị chốt:</span>
                              <span className="font-mono text-emerald-600 text-xs">{smartPrice.recommendedUnitPrice.toLocaleString('vi-VN')} đ/kg</span>
                            </div>
                          </div>
                        </div>
                      )}
                    </div>
                  </div>
                )}

                {/* ── AI Price Trend Forecasting & Optimal Timing Engine ── */}
                {currentSelected.is_recyclable && (
                  <PriceForecastWidget
                    initialMaterial={currentSelected.material_type || 'CARDBOARD'}
                    weightKg={Number(estimatedWeight) || 15}
                    onApplyPrice={null}
                    className="mt-3"
                  />
                )}

                {/* Recycling Tips */}
                {currentSelected.recycling_tip && (
                  <div className="p-3.5 rounded-xl bg-blue-500/5 border border-blue-500/20 text-xs space-y-1">
                    <span className="font-bold text-blue-700 dark:text-blue-400 flex items-center gap-1.5">
                      <ShieldCheck size={14} />
                      <span>Lời khuyên xử lý & phân loại:</span>
                    </span>
                    <p className="text-d-on-surface-variant leading-relaxed">
                      {currentSelected.recycling_tip}
                    </p>
                  </div>
                )}

                {/* OCR Text if detected */}
                {aiResult.ocr_text && (
                  <div className="p-3.5 rounded-xl bg-d-surface-container-lowest border border-d-border-subtle text-xs space-y-1">
                    <span className="font-bold text-d-on-surface flex items-center gap-1.5">
                      <MaterialIcon name="document_scanner" className="text-sm text-d-primary" />
                      <span>Chữ / Nhãn hiệu nhận diện trên ảnh (OCR):</span>
                    </span>
                    <p className="text-d-on-surface-variant font-mono bg-d-surface-container-low p-2 rounded-lg break-all">
                      "{aiResult.ocr_text}"
                    </p>
                  </div>
                )}

                {/* Primary Call to Action */}
                <div className="pt-2">
                  <button
                    id="btn-proceed-create-request"
                    type="button"
                    onClick={() => handleProceedToCreateRequest(currentSelected, smartPrice)}
                    className="w-full py-3.5 px-6 rounded-full bg-d-primary text-d-on-primary font-bold text-sm shadow-md hover:bg-[#385500] active:scale-95 transition-all flex items-center justify-center gap-2 cursor-pointer"
                  >
                    <span>
                      {currentSelected.is_recyclable
                        ? `Tạo đơn với mức giá đề xuất này (${smartPrice.recommendedUnitPrice.toLocaleString('vi-VN')} đ/kg)`
                        : `Tạo đơn thu gom với: ${currentSelected.name_vi}`}
                    </span>
                    <ArrowRight size={16} />
                  </button>
                </div>
              </div>
            );
          })()}
        </div>
      </div>
    </div>
  );
}
