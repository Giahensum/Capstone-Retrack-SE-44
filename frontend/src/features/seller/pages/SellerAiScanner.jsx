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
  ChevronDown, ChevronUp, Coins, Info, Eye, EyeOff,
} from 'lucide-react';
import { normalizeAiPredictions } from '../aiWasteMetadata';
import {
  calculateDepotsDistanceWithGoong,
  calculateSmartPriceRecommendation,
  calculateMixedPilePricingRecommendation,
  QUALITY_CONDITIONS,
  FULFILLMENT_METHODS,
} from '../pricingRecommendation';

const DEMO_SAMPLES = [
  {
    id: 'carton_box',
    title: 'Thùng carton đơn lẻ (Exi.jpg)',
    category: 'cardboard',
    icon: 'category',
    url: '/samples/exi.jpg',
    mode: 'single',
  },
  {
    id: 'mixed_pile_demo',
    title: 'Đống phế liệu hỗn hợp (Carton + Nhựa + Lon)',
    category: 'mixed_pile',
    icon: 'layers',
    url: '/samples/exi.jpg',
    mode: 'mixed_pile',
  },
];

export default function SellerAiScanner() {
  const navigate = useNavigate();
  const fileInputRef = useRef(null);
  const videoRef = useRef(null);
  const canvasRef = useRef(null);

  const [activeTab, setActiveTab] = useState('upload'); // 'upload' | 'camera'
  const [scanMode, setScanMode] = useState('single'); // 'single' | 'mixed_pile'
  const [cameraActive, setCameraActive] = useState(false);
  const [cameraLoading, setCameraLoading] = useState(false);
  const [selectedImage, setSelectedImage] = useState(null); // { file, preview, base64 }
  const [isScanning, setIsScanning] = useState(false);
  const [aiResult, setAiResult] = useState(null);
  const [selectedPredictionIndex, setSelectedPredictionIndex] = useState(0);
  const [aiStatus, setAiStatus] = useState(null);

  // Multi-box interaction state for mixed pile
  const [hoveredBoxId, setHoveredBoxId] = useState(null);
  const [hiddenBoxIds, setHiddenBoxIds] = useState(new Set());

  // ── Smart Price Recommendation inputs & state ──
  const [userLocation, setUserLocation] = useState({ address: '', lat: null, lng: null });
  const [estimatedWeight, setEstimatedWeight] = useState(20);
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

  // Start Camera
  const startCamera = async () => {
    setCameraLoading(true);
    try {
      const stream = await navigator.mediaDevices.getUserMedia({
        video: {
          facingMode: { ideal: 'environment' },
          width: { ideal: 1280 },
          height: { ideal: 720 },
        },
      });
      if (videoRef.current) {
        videoRef.current.srcObject = stream;
        videoRef.current.onloadedmetadata = () => {
          videoRef.current.play();
          setCameraActive(true);
          setCameraLoading(false);
        };
      }
    } catch (err) {
      console.error('Camera error:', err);
      toast.error('Không thể truy cập camera. Vui lòng cho phép quyền truy cập hoặc sử dụng tính năng tải ảnh.');
      setActiveTab('upload');
      setCameraLoading(false);
    }
  };

  // Stop Camera
  const stopCamera = () => {
    if (videoRef.current && videoRef.current.srcObject) {
      const tracks = videoRef.current.srcObject.getTracks();
      tracks.forEach((track) => track.stop());
      videoRef.current.srcObject = null;
    }
    setCameraActive(false);
  };

  // Handle Tab Change
  useEffect(() => {
    if (activeTab === 'camera') {
      startCamera();
    } else {
      stopCamera();
    }
    return () => stopCamera();
  }, [activeTab]);

  // Capture Photo from Camera
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

    canvas.toBlob((blob) => {
      const file = new File([blob], `ai_snap_${Date.now()}.jpg`, { type: 'image/jpeg' });
      setSelectedImage({
        file,
        preview: base64,
        base64,
      });
      runScan(file, base64, scanMode);
    }, 'image/jpeg', 0.9);
  };

  // Handle File Upload from disk
  const handleFileSelect = (e) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (!file.type.startsWith('image/')) {
      toast.error('Vui lòng chọn một tệp hình ảnh hợp lệ (JPG, PNG, WebP).');
      return;
    }

    const reader = new FileReader();
    reader.onload = (event) => {
      const base64 = event.target.result;
      setSelectedImage({
        file,
        preview: base64,
        base64,
      });
      runScan(file, base64, scanMode);
    };
    reader.readAsDataURL(file);
  };

  // Run AI Scan
  const runScan = async (file, base64, modeToUse = scanMode) => {
    setIsScanning(true);
    setAiResult(null);

    try {
      let res;
      if (file) {
        res = await sellerApi.classifyAiImage(file, false, modeToUse);
      } else if (base64) {
        res = await sellerApi.classifyAiBase64(base64, false, modeToUse);
      }

      if (res) {
        setAiResult(res);
        setSelectedPredictionIndex(0);
        if (res.scan_mode) {
          setScanMode(res.scan_mode);
        }
        if (res.scan_mode === 'mixed_pile') {
          toast.success(`🎉 Đã nhận diện đống phế liệu hỗn hợp (${res.detected_objects?.length || 0} vật thể)!`);
        } else {
          toast.success(`🎉 Đã nhận diện: ${res.name_vi} (${res.confidence}%)`);
        }
      }
    } catch (err) {
      console.error(err);
      const msg = err.response?.data?.message || 'Không thể kết nối dịch vụ AI. Vui lòng thử lại sau.';
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
    setHoveredBoxId(null);
    setHiddenBoxIds(new Set());
    if (fileInputRef.current) fileInputRef.current.value = '';
    if (activeTab === 'camera') startCamera();
  };

  // Handle Quick Demo Sample selection
  const handleSelectSample = async (sample) => {
    try {
      const targetMode = sample.mode || 'single';
      setScanMode(targetMode);
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
      runScan(file, null, targetMode);
    } catch (err) {
      console.error(err);
      toast.error('Không thể tải ảnh mẫu thử nghiệm');
      setIsScanning(false);
    }
  };

  // Toggle Visibility of a Bounding Box
  const toggleBoxVisibility = (boxId) => {
    setHiddenBoxIds((prev) => {
      const next = new Set(prev);
      if (next.has(boxId)) {
        next.delete(boxId);
      } else {
        next.add(boxId);
      }
      return next;
    });
  };

  // Transfer AI Result to Create Request
  const handleProceedToCreateRequest = (targetItem = null, customPrice = null) => {
    if (!aiResult) return;

    // Chế độ 2: Đống phế liệu hỗn hợp (Mixed Pile)
    if (scanMode === 'mixed_pile') {
      const mixedPricing = calculateMixedPilePricingRecommendation({
        detectedObjects: aiResult.detected_objects || [],
        totalWeightKg: Number(estimatedWeight) || 20,
        nearestDepot,
        qualityId: qualityCondition,
        fulfillment: fulfillmentMethod,
        userLocation,
      });

      const qualityMeta = QUALITY_CONDITIONS.find((q) => q.id === qualityCondition);
      const itemsSummary = mixedPricing.items.map(
        (it) => `• ${it.name_vi}: ${it.estimated_share_percent}% (~${it.itemWeightKg}kg x ${it.unitPrice.toLocaleString('vi-VN')} đ/kg = ${it.subTotal.toLocaleString('vi-VN')} đ)`
      ).join('\n');

      navigate('/seller/create', {
        state: {
          aiResult: {
            ...aiResult,
            scan_mode: 'mixed_pile',
            primary_class: 'mixed_pile',
            name_vi: 'Đống phế liệu hỗn hợp đa vật thể',
            confidence: aiResult.confidence,
            icon: 'layers',
            price_range: `${mixedPricing.averageUnitPrice.toLocaleString('vi-VN')} đ/kg (Trung bình)`,
            unit: 'kg',
            suggested_description: `Đống phế liệu hỗn hợp đa vật thể (AI nhận diện ${mixedPricing.items.length} nhóm vật liệu), tổng khối lượng ~${estimatedWeight}kg. Tình trạng: ${qualityMeta?.shortLabel || 'Chuẩn'}.\n${itemsSummary}\nTổng giá trị AI ước tính: ~${mixedPricing.totalEstimatedValue.toLocaleString('vi-VN')} đ.`,
            is_recyclable: true,
            material_type: 'MIXED',
            imagePreview: selectedImage?.preview,
            imageFile: selectedImage?.file,
            address: userLocation.address,
            latitude: userLocation.lat,
            longitude: userLocation.lng,
            targetDepotId: nearestDepot?.id || null,
            nearestDepotName: nearestDepot?.name || null,
            estimatedWeight: Number(estimatedWeight) || 20,
            recommendedPricePerKg: mixedPricing.averageUnitPrice,
            estimatedTotalPayout: mixedPricing.totalEstimatedValue,
            qualityCondition,
            fulfillmentMethod,
            mixedPricing,
          },
        },
      });
      return;
    }

    // Chế độ 1: Vật thể đơn lẻ (Single Item)
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
          scan_mode: 'single',
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
            Chụp ảnh phế liệu để mô hình EfficientNet-B4 + YOLOv8 tự động nhận diện phế liệu, vẽ nhiều bounding box cho đống hỗn hợp và định giá thị trường.
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

      {/* ── Scan Mode Switcher (Chế độ Đơn lẻ vs Đống hỗn hợp) ── */}
      <div className="bg-d-surface-container-lowest border border-d-border-subtle rounded-3xl p-3.5 shadow-xs flex flex-col sm:flex-row items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-2xl bg-d-primary/10 text-d-primary flex items-center justify-center shrink-0">
            {scanMode === 'mixed_pile' ? <Layers size={20} /> : <Sparkles size={20} />}
          </div>
          <div>
            <div className="text-xs font-bold text-d-on-surface uppercase tracking-wider flex items-center gap-1.5">
              <span>Chế độ quét AI:</span>
              <span className={`px-2 py-0.5 rounded-full text-[10px] font-bold ${
                scanMode === 'mixed_pile'
                  ? 'bg-amber-500/15 text-amber-700 dark:text-amber-300 border border-amber-500/30'
                  : 'bg-d-primary/15 text-d-primary border border-d-primary/30'
              }`}>
                {scanMode === 'mixed_pile' ? 'Đa vật thể (Multi-Box Bounding)' : 'Đơn vật thể (Single Item)'}
              </span>
            </div>
            <p className="text-[11px] text-d-on-surface-variant mt-0.5">
              {scanMode === 'mixed_pile'
                ? 'Nhận diện đống phế liệu hỗn hợp, vẽ nhiều bounding box riêng biệt và ước lượng cơ cấu % thành phần.'
                : 'Tập trung phân loại chuyên sâu 1 đối tượng phế liệu cụ thể với độ chính xác Top 5 cao nhất.'}
            </p>
          </div>
        </div>

        <div className="flex items-center gap-1.5 p-1 bg-d-surface-container-low rounded-2xl border border-d-border-subtle shrink-0 w-full sm:w-auto">
          <button
            type="button"
            id="btn-mode-single"
            onClick={() => {
              setScanMode('single');
              if (selectedImage && !isScanning) {
                runScan(selectedImage.file, selectedImage.base64, 'single');
              }
            }}
            className={`flex-1 sm:flex-none px-4 py-2 rounded-xl text-xs font-bold transition-all flex items-center justify-center gap-2 cursor-pointer ${
              scanMode === 'single'
                ? 'bg-d-primary text-d-on-primary shadow-xs'
                : 'text-d-on-surface-variant hover:text-d-on-surface hover:bg-d-surface-container'
            }`}
          >
            <MaterialIcon name="crop_free" className="text-sm" />
            <span>Vật thể đơn lẻ</span>
          </button>
          <button
            type="button"
            id="btn-mode-mixed"
            onClick={() => {
              setScanMode('mixed_pile');
              if (selectedImage && !isScanning) {
                runScan(selectedImage.file, selectedImage.base64, 'mixed_pile');
              }
            }}
            className={`flex-1 sm:flex-none px-4 py-2 rounded-xl text-xs font-bold transition-all flex items-center justify-center gap-2 cursor-pointer ${
              scanMode === 'mixed_pile'
                ? 'bg-d-primary text-d-on-primary shadow-xs'
                : 'text-d-on-surface-variant hover:text-d-on-surface hover:bg-d-surface-container'
            }`}
          >
            <Layers size={14} />
            <span>Đống phế liệu hỗn hợp</span>
          </button>
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
                  className={`px-3 py-1.5 rounded-xl border text-xs font-bold flex items-center gap-1.5 transition-all cursor-pointer disabled:opacity-50 shadow-2xs ${
                    sample.mode === 'mixed_pile'
                      ? 'border-amber-500/40 bg-amber-500/10 hover:bg-amber-500/20 text-amber-800 dark:text-amber-300'
                      : 'border-d-primary/40 bg-d-primary/5 hover:bg-d-primary/15 text-d-primary'
                  }`}
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
                    <span>{scanMode === 'mixed_pile' ? 'HƯỚNG CAMERA VÀO ĐỐNG PHẾ LIỆU HỖN HỢP' : 'HƯỚNG CAMERA VÀO VẬT THỂ PHẾ LIỆU'}</span>
                  </div>
                  <div className="text-center text-xs text-white/90 bg-black/50 py-1.5 px-3 rounded-full backdrop-blur-xs self-center">
                    {scanMode === 'mixed_pile'
                      ? 'Lấy toàn cảnh đống phế liệu để AI phát hiện nhiều vật thể'
                      : 'Đặt vật thể vào giữa khung để AI nhận diện chuẩn nhất'}
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
                      <div className="w-12 h-12 rounded-full bg-d-primary" />
                    </button>
                  </div>
                )}
              </div>
            )}

            {/* View 2: Upload Zone */}
            {activeTab === 'upload' && !selectedImage && (
              <div
                onClick={() => fileInputRef.current?.click()}
                className="w-full h-full flex flex-col items-center justify-center p-8 text-center cursor-pointer hover:bg-zinc-900 transition-colors"
              >
                <div className="w-16 h-16 rounded-2xl bg-zinc-800 text-zinc-300 flex items-center justify-center mb-3">
                  <Upload size={28} />
                </div>
                <h3 className="font-bold text-d-on-surface text-base">
                  {scanMode === 'mixed_pile' ? 'Tải ảnh đống phế liệu hỗn hợp' : 'Tải ảnh phế liệu lên'}
                </h3>
                <p className="text-xs text-zinc-400 mt-1 max-w-xs">
                  {scanMode === 'mixed_pile'
                    ? 'Kéo thả hoặc chọn ảnh chụp góc rộng chứa nhiều loại phế liệu khác nhau (JPG, PNG)'
                    : 'Kéo thả hoặc chọn ảnh chụp phế liệu (JPG, PNG, WebP) để AI nhận diện'}
                </p>
                <button
                  type="button"
                  className="mt-4 px-5 py-2 rounded-full bg-d-primary text-d-on-primary text-xs font-bold shadow-xs hover:bg-[#385500] transition-colors"
                >
                  Chọn tệp từ máy
                </button>
              </div>
            )}

            {/* View 3: Image Preview with Interactive AI Bounding Boxes */}
            {selectedImage && (
              <div className="relative w-full h-full flex items-center justify-center bg-zinc-950 overflow-hidden select-none">
                <img
                  src={aiResult?.annotated_image || selectedImage.preview}
                  alt="Scanned waste"
                  className="w-full h-full object-contain"
                />

                {/* ── Interactive Bounding Boxes Overlay ── */}
                {aiResult && !isScanning && (
                  <div className="absolute inset-0 pointer-events-none">
                    {scanMode === 'mixed_pile' && aiResult.detected_objects?.length > 0 ? (
                      aiResult.detected_objects
                        .filter((obj) => !hiddenBoxIds.has(obj.id))
                        .map((obj, i) => {
                          const box = obj.bounding_box || {};
                          // Coordinate normalization (assumes standard 640x480 coordinate space)
                          const x1 = Math.max(0, Math.min(100, (box.x1 / 640) * 100));
                          const y1 = Math.max(0, Math.min(100, (box.y1 / 480) * 100));
                          const width = Math.max(5, Math.min(100 - x1, (box.width / 640) * 100));
                          const height = Math.max(5, Math.min(100 - y1, (box.height / 480) * 100));
                          const isHovered = hoveredBoxId === obj.id;
                          const color = obj.color || '#10b981';

                          return (
                            <div
                              key={obj.id || i}
                              onMouseEnter={() => setHoveredBoxId(obj.id)}
                              onMouseLeave={() => setHoveredBoxId(null)}
                              className={`absolute pointer-events-auto transition-all duration-150 cursor-pointer rounded-lg ${
                                isHovered ? 'ring-4 ring-white shadow-2xl scale-[1.01] z-30' : 'z-20'
                              }`}
                              style={{
                                left: `${x1}%`,
                                top: `${y1}%`,
                                width: `${width}%`,
                                height: `${height}%`,
                                border: `2.5px solid ${color}`,
                                backgroundColor: `${color}18`,
                              }}
                            >
                              {/* Label Tag on Bounding Box */}
                              <div
                                className="absolute -top-6 left-0 px-2 py-0.5 rounded-md text-[10px] font-bold text-white shadow-md flex items-center gap-1.5 whitespace-nowrap"
                                style={{ backgroundColor: color }}
                              >
                                <span>{obj.name_vi}</span>
                                <span className="opacity-90 font-mono font-normal">
                                  ({obj.estimated_share_percent || obj.confidence}%)
                                </span>
                              </div>
                            </div>
                          );
                        })
                    ) : aiResult.bounding_box ? (
                      /* Single Item Box */
                      (() => {
                        const box = aiResult.bounding_box;
                        const x1 = Math.max(0, Math.min(100, (box.x1 / 640) * 100));
                        const y1 = Math.max(0, Math.min(100, (box.y1 / 480) * 100));
                        const width = Math.max(5, Math.min(100 - x1, (box.width / 640) * 100));
                        const height = Math.max(5, Math.min(100 - y1, (box.height / 480) * 100));
                        return (
                          <div
                            className="absolute pointer-events-none rounded-lg border-2 border-emerald-500 bg-emerald-500/10 z-20"
                            style={{
                              left: `${x1}%`,
                              top: `${y1}%`,
                              width: `${width}%`,
                              height: `${height}%`,
                            }}
                          >
                            <div className="absolute -top-6 left-0 px-2 py-0.5 rounded-md text-[10px] font-bold text-white bg-emerald-600 shadow-md">
                              {aiResult.name_vi} ({aiResult.confidence}%)
                            </div>
                          </div>
                        );
                      })()
                    ) : null}
                  </div>
                )}

                {/* Scanning Laser Animation */}
                {isScanning && (
                  <div className="absolute inset-0 bg-black/40 backdrop-blur-xs flex flex-col items-center justify-center p-6 text-white text-center">
                    <div className="absolute inset-x-0 h-1 bg-gradient-to-r from-transparent via-emerald-400 to-transparent shadow-[0_0_15px_#10b981] animate-bounce" />
                    <Loader2 size={36} className="animate-spin text-emerald-400 mb-3" />
                    <p className="font-bold text-base tracking-wide">
                      {scanMode === 'mixed_pile'
                        ? 'Đang phân tích đống phế liệu hỗn hợp đa vật thể...'
                        : 'Mô hình AI đang phân tích phế liệu...'}
                    </p>
                    <p className="text-xs text-zinc-300 mt-1 max-w-xs">
                      {scanMode === 'mixed_pile'
                        ? 'YOLOv8 đang định vị nhiều bounding box và ước tính cơ cấu %...'
                        : 'Đang chạy YOLOv8 định vị đối tượng, trích xuất OCR văn bản và xếp loại EfficientNet-B4...'}
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
                Gợi ý: {scanMode === 'mixed_pile' ? 'Chụp góc rộng bao quát đống phế liệu' : 'Chụp ở nơi đủ sáng để đạt độ chính xác tối đa'}.
              </span>
            )}

            {selectedImage && !isScanning && (
              <button
                type="button"
                onClick={() => runScan(selectedImage.file, selectedImage.base64, scanMode)}
                className="inline-flex items-center gap-2 px-5 py-2.5 rounded-full bg-d-primary text-d-on-primary text-xs font-bold hover:bg-[#385500] transition-colors cursor-pointer"
              >
                <Sparkles size={14} />
                <span>Phân tích lại ({scanMode === 'mixed_pile' ? 'Đa vật thể' : 'Đơn vật thể'})</span>
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
                  {scanMode === 'mixed_pile'
                    ? 'Chế độ Đống hỗn hợp giúp bạn chụp toàn bộ đống phế liệu, AI sẽ tự động tách từng loại (hộp bounding box), tính % khối lượng và tổng tiền.'
                    : 'Chụp ảnh hoặc chọn ảnh phế liệu của bạn. Trí tuệ nhân tạo sẽ tự động nhận biết loại vật liệu, giá bán tham khảo và tư vấn cách phân loại tối ưu.'}
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

          {/* AI Result Card: MIXED PILE MODE */}
          {aiResult && scanMode === 'mixed_pile' && (() => {
            const detectedObjects = aiResult.detected_objects || [];
            const mixedPricing = calculateMixedPilePricingRecommendation({
              detectedObjects,
              totalWeightKg: Number(estimatedWeight) || 20,
              nearestDepot,
              qualityId: qualityCondition,
              fulfillment: fulfillmentMethod,
              userLocation,
            });

            return (
              <div className="bg-d-surface-container-lowest border border-amber-500/30 rounded-3xl p-6 shadow-md space-y-6">
                {/* Header Banner: Mixed Pile Overview */}
                <div className="flex items-start justify-between gap-3 pb-4 border-b border-d-border-subtle">
                  <div className="flex items-start gap-3">
                    <div className="w-12 h-12 rounded-2xl bg-amber-500/20 text-amber-700 dark:text-amber-300 flex items-center justify-center shrink-0 shadow-sm border border-amber-500/30">
                      <Layers size={24} />
                    </div>
                    <div>
                      <div className="flex items-center gap-2 flex-wrap">
                        <h3 className="font-bold text-d-on-surface text-lg">
                          Đống phế liệu hỗn hợp
                        </h3>
                        <span className="inline-flex items-center gap-1 text-[11px] font-bold px-2 py-0.5 rounded-full bg-amber-500/15 text-amber-700 dark:text-amber-300 border border-amber-500/30">
                          📦 {detectedObjects.length} Vật thể phát hiện
                        </span>
                      </div>
                      <p className="text-xs text-d-on-surface-variant mt-0.5">
                        {aiResult.mixed_summary || 'Phát hiện nhiều loại phế liệu khác nhau trong một khung hình.'}
                      </p>
                    </div>
                  </div>
                </div>

                {/* ── Visual Stacked Composition Bar ── */}
                <div className="space-y-1.5">
                  <div className="flex justify-between text-xs font-bold text-d-on-surface">
                    <span>Cơ cấu thành phần đống phế liệu:</span>
                    <span className="font-mono text-d-primary">100% diện tích</span>
                  </div>
                  <div className="w-full h-3.5 bg-d-surface-container rounded-full overflow-hidden flex shadow-inner">
                    {detectedObjects.map((obj) => (
                      <div
                        key={obj.id}
                        className="h-full transition-all duration-300 relative group cursor-pointer"
                        style={{
                          width: `${obj.estimated_share_percent}%`,
                          backgroundColor: obj.color,
                        }}
                        onMouseEnter={() => setHoveredBoxId(obj.id)}
                        onMouseLeave={() => setHoveredBoxId(null)}
                        title={`${obj.name_vi}: ${obj.estimated_share_percent}%`}
                      />
                    ))}
                  </div>
                </div>

                {/* ── Detected Objects Component Cards List ── */}
                <div className="space-y-2.5">
                  <div className="flex items-center justify-between text-xs font-bold text-d-on-surface">
                    <span>Danh sách vật thể phân tách (Bấm để bật/tắt Box):</span>
                    <span className="text-[11px] text-d-on-surface-variant font-normal">
                      Rê chuột để sáng vị trí
                    </span>
                  </div>

                  <div className="space-y-2 max-h-72 overflow-y-auto pr-1">
                    {mixedPricing.items.map((item) => {
                      const isHovered = hoveredBoxId === item.id;
                      const isHidden = hiddenBoxIds.has(item.id);

                      return (
                        <div
                          key={item.id}
                          onMouseEnter={() => setHoveredBoxId(item.id)}
                          onMouseLeave={() => setHoveredBoxId(null)}
                          className={`p-3 rounded-2xl border transition-all flex items-center justify-between gap-3 ${
                            isHovered
                              ? 'border-2 shadow-xs ring-2 scale-[1.01]'
                              : 'border-d-border-subtle bg-d-surface-container-low hover:bg-d-surface-container'
                          }`}
                          style={{
                            borderColor: isHovered ? item.color : undefined,
                            backgroundColor: isHovered ? `${item.color}08` : undefined,
                          }}
                        >
                          <div className="flex items-center gap-2.5 min-w-0">
                            {/* Color Dot & Visibility Toggle */}
                            <button
                              type="button"
                              onClick={(e) => {
                                e.stopPropagation();
                                toggleBoxVisibility(item.id);
                              }}
                              className="w-6 h-6 rounded-lg flex items-center justify-center transition-colors cursor-pointer"
                              title={isHidden ? 'Hiện Bounding Box' : 'Ẩn Bounding Box'}
                              style={{ backgroundColor: `${item.color}20`, color: item.color }}
                            >
                              {isHidden ? <EyeOff size={14} /> : <Eye size={14} />}
                            </button>

                            <div className="min-w-0">
                              <div className="flex items-center gap-1.5 flex-wrap">
                                <span className="font-bold text-xs text-d-on-surface truncate">
                                  {item.name_vi}
                                </span>
                                <span
                                  className="text-[10px] font-bold px-1.5 py-0.2 rounded font-mono text-white"
                                  style={{ backgroundColor: item.color }}
                                >
                                  {item.estimated_share_percent}%
                                </span>
                              </div>
                              <p className="text-[11px] text-d-on-surface-variant truncate mt-0.5">
                                Ước tính: <strong className="text-d-on-surface font-mono">~{item.itemWeightKg} kg</strong> · {item.unitPrice.toLocaleString('vi-VN')} đ/kg
                              </p>
                            </div>
                          </div>

                          {/* Subtotal Payout */}
                          <div className="text-right shrink-0">
                            <span className="font-mono font-bold text-sm text-emerald-600 block">
                              ~{item.subTotal.toLocaleString('vi-VN')} đ
                            </span>
                            <span className="text-[10px] text-d-on-surface-variant font-mono">
                              {item.confidence}% tin cậy
                            </span>
                          </div>
                        </div>
                      );
                    })}
                  </div>
                </div>

                {/* ── Parameter Inputs for Mixed Pile Valuation ── */}
                <div className="rounded-2xl border border-d-border-subtle bg-d-surface-container-low/60 p-4 space-y-4">
                  {/* Location Goong.io */}
                  <div className="space-y-1.5">
                    <label className="text-xs font-bold text-d-on-surface flex items-center gap-1.5">
                      <MapPin size={14} className="text-d-primary" />
                      <span>Địa điểm thu gom thực tế:</span>
                    </label>
                    <GoongAutocomplete
                      value={userLocation.address}
                      onChange={(addr) => setUserLocation((prev) => ({ ...prev, address: addr }))}
                      onSelect={handleLocationSelect}
                      placeholder="Gõ số nhà, tên đường hoặc bấm GPS..."
                    />
                  </div>

                  {/* Total Weight Input */}
                  <div className="space-y-1.5">
                    <div className="flex items-center justify-between">
                      <label className="text-xs font-bold text-d-on-surface flex items-center gap-1.5">
                        <Scale size={14} className="text-d-primary" />
                        <span>Tổng khối lượng cả đống ước tính:</span>
                      </label>
                      <span className="text-xs font-bold text-d-primary font-mono">
                        {estimatedWeight} kg
                      </span>
                    </div>
                    <div className="flex items-center gap-2 flex-wrap">
                      <input
                        type="number"
                        min="1"
                        max="10000"
                        value={estimatedWeight}
                        onChange={(e) => setEstimatedWeight(Math.max(1, Number(e.target.value) || 1))}
                        className="w-28 px-3 py-2 rounded-xl border border-d-border-subtle bg-d-surface text-d-on-surface text-sm font-bold focus:outline-d-primary text-center"
                      />
                      <div className="flex flex-wrap gap-1.5">
                        {[20, 50, 100, 200, 500].map((w) => (
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

                  {/* Quality Selector */}
                  <div className="space-y-1.5">
                    <label className="text-xs font-bold text-d-on-surface flex items-center gap-1.5">
                      <Award size={14} className="text-d-primary" />
                      <span>Tình trạng phân loại thực tế:</span>
                    </label>
                    <div className="grid grid-cols-3 gap-2">
                      {QUALITY_CONDITIONS.map((cond) => {
                        const isSelected = qualityCondition === cond.id;
                        return (
                          <div
                            key={cond.id}
                            onClick={() => setQualityCondition(cond.id)}
                            className={`p-2.5 rounded-xl border transition-all cursor-pointer text-center ${
                              isSelected
                                ? 'border-2 border-d-primary bg-d-primary/5 shadow-xs'
                                : 'border-d-border-subtle bg-d-surface hover:bg-d-surface-container'
                            }`}
                          >
                            <span className="font-bold text-xs block text-d-on-surface">{cond.shortLabel}</span>
                          </div>
                        );
                      })}
                    </div>
                  </div>

                  {/* Fulfillment Method */}
                  <div className="grid grid-cols-2 gap-2 pt-1">
                    {FULFILLMENT_METHODS.map((f) => {
                      const isSelected = fulfillmentMethod === f.id;
                      return (
                        <div
                          key={f.id}
                          onClick={() => setFulfillmentMethod(f.id)}
                          className={`p-2.5 rounded-xl border transition-all cursor-pointer flex items-center gap-2 ${
                            isSelected
                              ? 'border-2 border-d-primary bg-d-primary/5 shadow-xs'
                              : 'border-d-border-subtle bg-d-surface hover:bg-d-surface-container'
                          }`}
                        >
                          <MaterialIcon name={f.icon} className="text-base text-d-primary" />
                          <span className="text-xs font-bold text-d-on-surface truncate">{f.label}</span>
                        </div>
                      );
                    })}
                  </div>
                </div>

                {/* ── Grand Total Payout Banner ── */}
                <div className="p-4 rounded-2xl bg-gradient-to-br from-amber-500/15 via-emerald-500/10 to-transparent border border-amber-500/30 space-y-3">
                  <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                    <div>
                      <span className="text-[11px] font-bold text-amber-800 dark:text-amber-300 uppercase tracking-wider block">
                        💰 Tổng giá trị AI ước tính cả đống:
                      </span>
                      <div className="text-2xl font-black text-emerald-600 font-mono mt-0.5">
                        ~{mixedPricing.totalEstimatedValue.toLocaleString('vi-VN')} đ
                      </div>
                      <p className="text-[11px] text-d-on-surface-variant">
                        Đơn giá bình quân: <strong className="font-mono text-d-on-surface">~{mixedPricing.averageUnitPrice.toLocaleString('vi-VN')} đ/kg</strong> (Tổng ~{estimatedWeight} kg)
                      </p>
                    </div>

                    <button
                      type="button"
                      onClick={() => handleProceedToCreateRequest()}
                      className="px-5 py-3 rounded-full bg-emerald-600 hover:bg-emerald-700 text-white font-bold text-xs shadow-md transition-all flex items-center justify-center gap-2 cursor-pointer shrink-0"
                    >
                      <span>Tạo đơn gom cả đống phế liệu</span>
                      <ArrowRight size={14} />
                    </button>
                  </div>
                </div>
              </div>
            );
          })()}

          {/* AI Result Card: SINGLE ITEM MODE */}
          {aiResult && scanMode === 'single' && (() => {
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
                              <Store size={16} />
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

                    {/* Trường 4: Hình thức bàn giao thu gom */}
                    <div className="space-y-1.5">
                      <label className="text-xs font-bold text-d-on-surface flex items-center gap-1.5">
                        <Truck size={14} className="text-d-primary" />
                        <span>Hình thức bàn giao thu gom:</span>
                      </label>
                      <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                        {FULFILLMENT_METHODS.map((f) => {
                          const isSelected = fulfillmentMethod === f.id;
                          return (
                            <div
                              key={f.id}
                              onClick={() => setFulfillmentMethod(f.id)}
                              className={`p-3 rounded-2xl border transition-all cursor-pointer flex items-center gap-3 ${
                                isSelected
                                  ? 'border-2 border-d-primary bg-d-primary/5 shadow-xs ring-1 ring-d-primary/20'
                                  : 'border-d-border-subtle bg-d-surface-container-low hover:border-d-primary/30'
                              }`}
                            >
                              <div className={`w-8 h-8 rounded-xl flex items-center justify-center shrink-0 ${
                                isSelected ? 'bg-d-primary text-d-on-primary' : 'bg-d-surface text-d-on-surface-variant'
                              }`}>
                                <MaterialIcon name={f.icon} className="text-lg" />
                              </div>
                              <div className="min-w-0">
                                <span className="font-bold text-xs text-d-on-surface block truncate">{f.label}</span>
                                <span className="text-[10px] text-d-on-surface-variant block truncate mt-0.5">{f.desc}</span>
                              </div>
                            </div>
                          );
                        })}
                      </div>
                    </div>

                    {/* ── Banner Hiển thị Đơn giá và Tổng tiền đề xuất ── */}
                    <div className="p-4 rounded-2xl bg-gradient-to-br from-emerald-500/15 via-emerald-500/5 to-transparent border border-emerald-500/30 space-y-3">
                      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                        <div>
                          <span className="text-[11px] font-bold text-emerald-800 dark:text-emerald-300 uppercase tracking-wider block">
                            Mức giá thị trường AI khuyến nghị:
                          </span>
                          <div className="flex items-baseline gap-1 mt-0.5">
                            <span className="text-2xl font-black text-emerald-600 font-mono">
                              {smartPrice.recommendedUnitPrice.toLocaleString('vi-VN')}
                            </span>
                            <span className="text-xs font-bold text-d-on-surface-variant">đ/kg</span>
                          </div>
                        </div>

                        <div className="sm:text-right bg-white dark:bg-d-surface-container-low p-2.5 rounded-xl border border-emerald-500/20 shadow-xs">
                          <span className="text-[11px] font-semibold text-d-on-surface-variant block">
                            Ước tính số tiền thực nhận (~{estimatedWeight} {currentSelected.unit || 'kg'}):
                          </span>
                          <span className="text-base font-extrabold text-emerald-600 font-mono">
                            ~ {smartPrice.estimatedTotal.toLocaleString('vi-VN')} đ
                          </span>
                        </div>
                      </div>

                      {/* Source Footnote */}
                      <div className="flex items-center justify-between pt-2 border-t border-emerald-500/20 text-xs flex-wrap gap-2">
                        <span className="text-[11px] text-d-on-surface-variant flex items-center gap-1">
                          <CheckCircle2 size={12} className="text-emerald-600" />
                          {smartPrice.priceSource === 'DEPOT_PUBLISHED' && 'Dựa trên giá niêm yết của kho gần bạn nhất'}
                          {smartPrice.priceSource === 'SYSTEM_LATEST' && 'Khớp chỉ số giá thị trường mới nhất của hệ thống'}
                          {smartPrice.priceSource === 'MARKET_INDEX' && 'Tham chiếu theo bảng giá thị trường chuẩn ReTrack'}
                        </span>

                        {/* Accordion Toggle: Bóc tách minh bạch */}
                        <button
                          type="button"
                          onClick={() => setShowPricingBreakdown(!showPricingBreakdown)}
                          className="text-[11px] font-bold text-d-primary hover:underline flex items-center gap-1 cursor-pointer"
                        >
                          <span>Bóc tách công thức định giá (Minh bạch AI)</span>
                          {showPricingBreakdown ? <ChevronUp size={12} /> : <ChevronDown size={12} />}
                        </button>
                      </div>

                      {/* Bóc tách công thức tính toán */}
                      {showPricingBreakdown && smartPrice.breakdown && (
                        <div className="p-3 rounded-xl bg-d-surface-container-lowest border border-d-border-subtle text-xs space-y-1.5 animate-fadeIn">
                          <p className="font-bold text-d-on-surface text-[11px] flex items-center gap-1">
                            <Info size={12} className="text-d-primary" />
                            <span>Chi tiết cấu thành đơn giá:</span>
                          </p>
                          <div className="space-y-1 text-[11px]">
                            <div className="flex justify-between text-d-on-surface-variant">
                              <span>• Giá sàn tham chiếu ({currentSelected.name_vi}):</span>
                              <span className="font-mono font-bold text-d-on-surface">{smartPrice.breakdown.basePrice.toLocaleString('vi-VN')} đ/kg</span>
                            </div>
                            <div className="flex justify-between text-d-on-surface-variant">
                              <span>• Điều chỉnh phẩm chất ({smartPrice.breakdown.qualityLabel}):</span>
                              <span className={`font-mono font-bold ${smartPrice.breakdown.qualityAdjustment >= 0 ? 'text-emerald-600' : 'text-amber-600'}`}>
                                {smartPrice.breakdown.qualityAdjustment >= 0 ? `+${smartPrice.breakdown.qualityAdjustment.toLocaleString('vi-VN')}` : smartPrice.breakdown.qualityAdjustment.toLocaleString('vi-VN')} đ/kg
                              </span>
                            </div>
                            {smartPrice.breakdown.volumeIncentivePerKg > 0 && (
                              <div className="flex justify-between text-d-on-surface-variant">
                                <span>• Trợ giá số lượng lớn ({estimatedWeight} kg):</span>
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

                    {/* Nút 1-Click: Tạo đơn với mức giá đề xuất */}
                    <button
                      type="button"
                      onClick={() => handleProceedToCreateRequest(currentSelected, smartPrice)}
                      className="w-full py-3.5 px-6 rounded-2xl bg-d-primary hover:bg-[#385500] text-d-on-primary font-bold text-sm shadow-md transition-all flex items-center justify-center gap-2 cursor-pointer active:scale-[0.99]"
                    >
                      <Sparkles size={16} />
                      <span>Tạo đơn với mức giá đề xuất này ({smartPrice.recommendedUnitPrice.toLocaleString('vi-VN')} đ/kg)</span>
                      <ArrowRight size={16} />
                    </button>
                  </div>
                )}

                {/* Recycling Tips Banner */}
                {currentSelected.recycling_tip && (
                  <div className="p-3.5 rounded-xl bg-blue-500/5 border border-blue-500/20 text-xs space-y-1">
                    <span className="font-bold text-blue-700 dark:text-blue-400 flex items-center gap-1.5">
                      <ShieldCheck size={14} />
                      <span>Hướng dẫn chuẩn bị phế liệu đạt giá cao:</span>
                    </span>
                    <p className="text-d-on-surface-variant leading-relaxed">
                      {currentSelected.recycling_tip}
                    </p>
                  </div>
                )}

                {/* AI Model Architecture Footnote */}
                <div className="p-3.5 rounded-xl bg-d-surface-container-lowest border border-d-border-subtle text-xs space-y-1">
                  <span className="font-bold text-d-on-surface flex items-center gap-1.5">
                    <MaterialIcon name="document_scanner" className="text-sm text-d-primary" />
                    <span>Mô hình thị giác máy tính ReTrack AI</span>
                  </span>
                  <p className="text-d-on-surface-variant text-[11px] leading-relaxed">
                    Hệ thống ứng dụng kiến trúc <strong>Multi-Model Knowledge Stacking</strong>: YOLOv8n định vị đa vật thể, EfficientNet-B4 phân loại chất liệu và EasyOCR đọc nhãn hiệu để đưa ra đề xuất tối ưu nhất.
                  </p>
                </div>
              </div>
            );
          })()}
        </div>
      </div>
    </div>
  );
}
