import { useState, useRef, useEffect } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate, useLocation, Link } from 'react-router-dom';
import toast from 'react-hot-toast';
import { sellerApi } from '../api';
import GoongAutocomplete from '@/components/ui/GoongAutocomplete';
import { MaterialIcon } from '@/features/depot/components/DepotIcon';
import { APIProvider, Map, Marker, InfoWindow } from '@vis.gl/react-google-maps';
import { Loader2, Sparkles, Wand2 } from 'lucide-react';

const TIME_SLOTS = [
  '07:00 - 09:00', '09:00 - 11:00', '11:00 - 13:00',
  '13:00 - 15:00', '15:00 - 17:00', '17:00 - 19:00',
];

// ── Gợi ý mẫu phế liệu phổ biến ──
const SCRAP_RECOMMENDATIONS = [
  {
    id: 'carton',
    icon: 'category',
    title: 'Bìa carton & Giấy vụn',
    desc: 'Thùng giấy carton cũ, sách báo vụn, bìa cứng, giấy photo văn phòng',
    sampleText: 'Có khoảng 15-20kg thùng giấy carton cũ, sách báo và giấy vụn gia đình cần thu gom.',
  },
  {
    id: 'cans_plastic',
    icon: 'recycling',
    title: 'Vỏ lon & Chai nhựa',
    desc: 'Vỏ lon bia, lon nước ngọt, chai nhựa PET, can dầu ăn, can nước giặt',
    sampleText: 'Khoảng 2 bao tải vỏ lon bia, lon nước ngọt và chai nhựa đựng nước sạch đã ép gọn.',
  },
  {
    id: 'scrap_metal',
    icon: 'warehouse',
    title: 'Sắt thép & Kim loại',
    desc: 'Sắt vụn, khung cửa cũ, sắt hộp, đinh ốc, ống đồng, nhôm vụn',
    sampleText: 'Sắt phế liệu thừa sau sửa nhà, vài thanh sắt hộp cũ, đinh ốc và kim loại vụn khoảng 30kg.',
  },
  {
    id: 'electronics',
    icon: 'inventory_2',
    title: 'Đồ điện tử & Gia dụng hỏng',
    desc: 'Quạt hỏng, nồi cơm, lò vi sóng cũ, vỏ máy tính, dây cáp đồng',
    sampleText: 'Thanh lý 2 quạt điện cũ bị cháy motor, 1 lò vi sóng hỏng và mớ dây điện cũ.',
  },
  {
    id: 'glass_bottles',
    icon: 'inventory',
    title: 'Chai lọ & Thủy tinh',
    desc: 'Chai lọ thủy tinh các loại, chai bia, két nhựa cũ, chậu nhựa hỏng',
    sampleText: 'Chai thủy tinh bia nước ngọt các loại cùng một số đồ nhựa cứng cũ cần bán thanh lý.',
  },
  {
    id: 'general_cleanup',
    icon: 'shopping_cart',
    title: 'Dọn nhà / Dọn kho tổng hợp',
    desc: 'Hỗn hợp nhiều loại phế liệu: giấy, kim loại, nhựa cần dọn sạch',
    sampleText: 'Dọn dẹp nhà tổng hợp có thùng carton, đồ sắt vụn, đồ nhựa và thiết bị cũ lẫn lộn, nhờ kho tới phân loại và cân giúp.',
  },
];

export default function CreateRequest() {
  const navigate = useNavigate();
  const location = useLocation();
  const queryClient = useQueryClient();
  const fileInputRef = useRef(null);
  const aiFileInputRef = useRef(null);

  const [step, setStep] = useState(1);
  const [mode, setMode] = useState(null);
  const [showPricesId, setShowPricesId] = useState(null);
  const [inputMethod, setInputMethod] = useState('recommend'); // 'recommend' | 'manual'
  const [selectedPresetId, setSelectedPresetId] = useState(null);
  const [isAiScanning, setIsAiScanning] = useState(false);
  const [detectedAiData, setDetectedAiData] = useState(null);

  const [form, setForm] = useState({
    description: '',
    address: '',
    latitude: null,
    longitude: null,
    preferredDate: '',
    preferredTimeSlot: '',
    targetDepotId: null,
  });

  const [images, setImages] = useState([]); // { file, preview, url, uploading }

  // ── Auto-apply AI result if redirected from AI Scanner ──
  useEffect(() => {
    if (location.state?.aiResult) {
      const ai = location.state.aiResult;
      setDetectedAiData(ai);
      setInputMethod('manual');
      setForm((prev) => ({
        ...prev,
        description: ai.suggested_description || `Phế liệu ${ai.name_vi} (AI nhận diện: ${ai.confidence}%), tình trạng khô ráo cần kho thu gom.`,
      }));

      if (ai.imagePreview || ai.annotated_image) {
        setImages((prev) => {
          if (prev.length === 0) {
            return [{
              file: ai.imageFile || null,
              preview: ai.annotated_image || ai.imagePreview,
              url: ai.uploaded_url || null,
              uploading: false,
            }];
          }
          return prev;
        });
      }
      toast.success(`✨ Đã áp dụng thông tin AI: ${ai.name_vi} (${ai.confidence}%)`);
    }
  }, [location.state]);

  // ── Inline AI scan file handler ──
  const handleAiScanFile = async (e) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setIsAiScanning(true);
    try {
      const res = await sellerApi.classifyAiImage(file, false);
      if (res && res.primary_class) {
        setDetectedAiData(res);
        setInputMethod('manual');
        setForm((prev) => ({
          ...prev,
          description: res.suggested_description || `Phế liệu ${res.name_vi} (AI nhận diện: ${res.confidence}%), tình trạng khô ráo cần kho thu gom.`,
        }));
        setImages((prev) => {
          if (prev.length < 5) {
            return [
              ...prev,
              {
                file,
                preview: res.annotated_image || URL.createObjectURL(file),
                url: null,
                uploading: false,
              },
            ];
          }
          return prev;
        });
        toast.success(`✨ AI đã nhận diện: ${res.name_vi} (${res.confidence}%) và tự động điền mô tả!`);
      }
    } catch (err) {
      console.error('AI scan error:', err);
      toast.error(err?.response?.data?.message || 'Có lỗi khi phân tích ảnh bằng AI.');
    } finally {
      setIsAiScanning(false);
      if (aiFileInputRef.current) aiFileInputRef.current.value = '';
    }
  };
  const [selectedDepot, setSelectedDepot] = useState(null);

  const [viewport, setViewport] = useState({
    latitude: 16.07,
    longitude: 108.15,
    zoom: 13,
  });

  const [selectedDepotPopup, setSelectedDepotPopup] = useState(null);

  // Fetch recent requests to recommend previously used addresses
  const { data: previousRequests = [] } = useQuery({
    queryKey: ['seller-requests-recent-address'],
    queryFn: () => sellerApi.getRequests(),
    staleTime: 60000,
  });

  const lastUsedRequest = previousRequests.find(r => r.address && r.latitude && r.longitude);

  // Fetch depots and calculate real routing distance using Goong API
  const { data: depots = [], isLoading: depotsLoading } = useQuery({
    queryKey: ['nearby-depots', form.latitude, form.longitude],
    queryFn: async () => {
      const dbDepots = await sellerApi.getNearbyDepots(form.latitude, form.longitude);

      if (!form.latitude || !form.longitude || dbDepots.length === 0) return dbDepots;

      try {
        const GOONG_API_KEY = import.meta.env.VITE_GOONG_API_KEY;
        if (!GOONG_API_KEY) return dbDepots;

        const origins = `${form.latitude},${form.longitude}`;
        const destinations = dbDepots.map((d) => `${d.latitude},${d.longitude}`).join('|');

        const res = await fetch(
          `https://rsapi.goong.io/DistanceMatrix?origins=${origins}&destinations=${destinations}&vehicle=bike&api_key=${GOONG_API_KEY}`
        );
        const data = await res.json();

        if (data.rows && data.rows[0]?.elements) {
          const elements = data.rows[0].elements;
          dbDepots.forEach((depot, idx) => {
            if (elements[idx]?.status === 'OK') {
              depot.routingDistanceKm = elements[idx].distance.value / 1000;
              depot.routingDurationText = elements[idx].duration.text;
            }
          });

          return dbDepots.sort(
            (a, b) =>
              (a.routingDistanceKm || a.distanceKm || 999) -
              (b.routingDistanceKm || b.distanceKm || 999)
          );
        }
      } catch (err) {
        console.error('Goong DistanceMatrix API error:', err);
      }
      return dbDepots;
    },
    enabled: step === 2 && mode === 'choose',
  });

  useEffect(() => {
    if (step === 2 && mode === 'choose') {
      if (selectedDepot && selectedDepot.latitude) {
        setViewport((v) => ({
          ...v,
          latitude: selectedDepot.latitude,
          longitude: selectedDepot.longitude,
          zoom: 15,
        }));
      } else if (form.latitude && form.longitude) {
        setViewport((v) => ({
          ...v,
          latitude: form.latitude,
          longitude: form.longitude,
          zoom: 13,
        }));
      }
    }
  }, [selectedDepot, form.latitude, form.longitude, step, mode]);

  const createMutation = useMutation({
    mutationFn: sellerApi.createRequest,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['seller-requests'] });
      queryClient.invalidateQueries({ queryKey: ['seller-stats'] });
      toast.success('Tạo đơn thu gom thành công! 🎉');
      navigate('/seller/requests');
    },
    onError: (err) => toast.error(err?.response?.data?.message || 'Có lỗi xảy ra.'),
  });

  // ── Image handling ──
  const handleFileSelect = (e) => {
    const files = Array.from(e.target.files);
    if (images.length + files.length > 5) return toast.error('Tối đa 5 ảnh');
    const newImages = files.map((f) => ({
      file: f,
      preview: URL.createObjectURL(f),
      url: null,
      uploading: false,
    }));
    setImages((prev) => [...prev, ...newImages]);
  };

  const removeImage = (idx) => {
    setImages((prev) => {
      const next = [...prev];
      URL.revokeObjectURL(next[idx].preview);
      next.splice(idx, 1);
      return next;
    });
  };

  const uploadAllImages = async () => {
    const toUpload = images.filter((img) => !img.url && !img.uploading);
    if (toUpload.length === 0) return images.filter((img) => img.url).map((img) => img.url);

    const updated = [...images];
    const urls = [];

    for (let i = 0; i < updated.length; i++) {
      if (updated[i].url) {
        urls.push(updated[i].url);
        continue;
      }
      updated[i] = { ...updated[i], uploading: true };
      setImages([...updated]);
      try {
        const url = await sellerApi.uploadImage(updated[i].file);
        updated[i] = { ...updated[i], url, uploading: false };
        urls.push(url);
      } catch {
        updated[i] = { ...updated[i], uploading: false };
        toast.error(`Upload ảnh ${i + 1} thất bại`);
      }
      setImages([...updated]);
    }
    return urls;
  };

  // ── Address selection from Goong ──
  const handleAddressSelect = ({ address, lat, lng }) => {
    setForm((f) => ({ ...f, address, latitude: lat, longitude: lng }));
  };

  const applyPreviousAddress = () => {
    if (!lastUsedRequest) return;
    setForm((f) => ({
      ...f,
      address: lastUsedRequest.address,
      latitude: lastUsedRequest.latitude,
      longitude: lastUsedRequest.longitude,
    }));
    toast.success('Đã áp dụng địa chỉ từ đơn trước');
  };

  // ── Recommendation Selection Handler ──
  const handleSelectRecommendation = (rec) => {
    setSelectedPresetId(rec.id);
    setForm((prev) => ({
      ...prev,
      description: rec.sampleText,
    }));
  };

  const handleClearRecommendation = () => {
    setSelectedPresetId(null);
    setForm((prev) => ({
      ...prev,
      description: '',
    }));
  };

  // ── Submit ──
  const handleSubmit = async () => {
    if (!form.address.trim()) return toast.error('Vui lòng nhập địa chỉ');

    let imageUrl = null;
    if (images.length > 0) {
      const urls = await uploadAllImages();
      if (urls.length !== images.length) {
        return toast.error('Có lỗi khi tải ảnh lên. Vui lòng kiểm tra lại Cloudinary hoặc thử lại.');
      }
      imageUrl = urls.join(',');
    }

    let preferredDatetime = null;
    if (form.preferredDate) {
      const timeStr = form.preferredTimeSlot ? form.preferredTimeSlot.split(' - ')[0] : '08:00';
      preferredDatetime = new Date(`${form.preferredDate}T${timeStr}:00`).toISOString();
    }

    createMutation.mutate({
      description: form.description,
      address: form.address,
      latitude: form.latitude,
      longitude: form.longitude,
      preferredDatetime,
      requestImageUrl: imageUrl,
      targetDepotId: mode === 'choose' ? form.targetDepotId : null,
    });
  };

  const goBack = () => {
    if (step === 2 && mode) {
      setMode(null);
      return;
    }
    if (step > 1) {
      setStep(step - 1);
      setMode(null);
      return;
    }
    navigate(-1);
  };

  return (
    <div className="flex flex-col p-4 md:p-6 gap-6 w-full max-w-4xl mx-auto">
      {/* Top Header & Breadcrumb */}
      <div className="flex items-center justify-between">
        <button
          type="button"
          onClick={goBack}
          className="inline-flex items-center gap-1.5 text-sm font-semibold text-d-on-surface-variant hover:text-d-primary transition-colors"
        >
          <MaterialIcon name="arrow_forward" className="rotate-180 text-base" />
          <span>Quay lại</span>
        </button>
        <span className="text-xs font-semibold text-d-on-surface-variant bg-d-surface-container px-3 py-1 rounded-full">
          Bước {step} / 3
        </span>
      </div>

      <div>
        <h1 className="font-d-headline-lg text-d-headline-lg text-d-on-surface">
          Tạo đơn thu gom phế liệu
        </h1>
        <p className="font-d-body-md text-d-body-md text-d-on-surface-variant mt-1">
          Điền thông tin phế liệu cần bán và địa chỉ thu gom để kết nối với kho vựa.
        </p>
      </div>

      {/* Stepper Progress */}
      <div className="bg-d-surface-container-lowest border border-d-border-subtle rounded-2xl p-4 shadow-sm">
        <div className="flex items-center justify-between">
          {[
            { num: 1, label: 'Thông tin phế liệu' },
            { num: 2, label: 'Chọn hình thức' },
            { num: 3, label: 'Xác nhận đơn' },
          ].map((item, idx) => {
            const isCompleted = step > item.num;
            const isCurrent = step === item.num;
            return (
              <div key={item.num} className="flex-1 flex items-center">
                <div className="flex items-center gap-3">
                  <div
                    className={`w-9 h-9 rounded-full flex items-center justify-center font-bold text-xs transition-colors shrink-0 ${
                      isCurrent
                        ? 'bg-d-primary text-d-on-primary ring-4 ring-d-primary-container/40'
                        : isCompleted
                        ? 'bg-d-primary-container text-d-on-primary-container'
                        : 'bg-d-surface-container text-d-on-surface-variant'
                    }`}
                  >
                    {isCompleted ? <MaterialIcon name="check_circle" className="text-base" /> : item.num}
                  </div>
                  <span
                    className={`hidden sm:inline text-sm font-semibold ${
                      isCurrent ? 'text-d-primary' : 'text-d-on-surface-variant'
                    }`}
                  >
                    {item.label}
                  </span>
                </div>
                {idx < 2 && (
                  <div
                    className={`flex-1 h-0.5 mx-4 rounded-full transition-colors ${
                      step > idx + 1 ? 'bg-d-primary-container' : 'bg-d-surface-container'
                    }`}
                  />
                )}
              </div>
            );
          })}
        </div>
      </div>

      {/* ═════════════════════════════════════════════════════
          BƯỚC 1: THÔNG TIN PHẾ LIỆU & GỢI Ý MẪU
      ══════════════════════════════════════════════════════ */}
      {step === 1 && (
        <div className="space-y-6">
          {/* Card: Trợ lý AI Phân loại Phế liệu thông minh */}
          <div className="bg-gradient-to-r from-d-primary-container/30 via-d-surface-container-lowest to-d-surface-container-lowest border border-d-primary/30 rounded-2xl p-5 shadow-xs flex flex-col sm:flex-row sm:items-center justify-between gap-4">
            <div className="flex items-start gap-3.5">
              <div className="w-11 h-11 rounded-2xl bg-d-primary text-d-on-primary flex items-center justify-center shrink-0 shadow-sm">
                <Sparkles size={22} />
              </div>
              <div>
                <div className="flex items-center gap-2">
                  <h3 className="font-bold text-d-on-surface text-sm">
                    Trợ lý AI Phân loại Phế liệu & Định giá tức thì
                  </h3>
                  <span className="text-[10px] font-bold text-d-primary bg-d-primary/10 px-2 py-0.5 rounded-full shrink-0">
                    ✨ AI Vision
                  </span>
                </div>
                <p className="text-xs text-d-on-surface-variant mt-1 leading-relaxed">
                  Chụp hoặc tải ảnh để mô hình AI tự động nhận diện loại phế liệu, tra cứu giá thị trường và tự động điền mô tả đơn.
                </p>
              </div>
            </div>

            <div className="flex items-center gap-2 shrink-0">
              <button
                type="button"
                disabled={isAiScanning}
                onClick={() => aiFileInputRef.current?.click()}
                className="inline-flex items-center gap-1.5 px-4 py-2.5 rounded-xl bg-d-primary text-d-on-primary font-bold text-xs shadow-xs hover:bg-[#385500] transition-colors cursor-pointer disabled:opacity-50"
              >
                {isAiScanning ? (
                  <>
                    <Loader2 size={14} className="animate-spin" />
                    <span>Đang quét AI...</span>
                  </>
                ) : (
                  <>
                    <Wand2 size={14} />
                    <span>Quét ảnh điền tự động</span>
                  </>
                )}
              </button>
              <Link
                to="/seller/ai-scanner"
                className="inline-flex items-center gap-1 px-3.5 py-2.5 rounded-xl border border-d-border-subtle bg-d-surface-container-lowest hover:bg-d-surface-container text-xs font-semibold text-d-on-surface transition-colors"
              >
                <span>Mở máy quét Camera</span>
                <MaterialIcon name="chevron_right" className="text-base" />
              </Link>
              <input
                ref={aiFileInputRef}
                type="file"
                accept="image/*"
                onChange={handleAiScanFile}
                className="hidden"
              />
            </div>
          </div>

          {/* AI Detection Result Toast/Card if active */}
          {detectedAiData && (
            <div className="p-4 rounded-2xl bg-emerald-500/10 border border-emerald-500/30 flex flex-col sm:flex-row sm:items-center justify-between gap-3 text-xs">
              <div className="flex items-center gap-2.5">
                <div className="w-8 h-8 rounded-lg bg-emerald-500 text-white flex items-center justify-center shrink-0">
                  <MaterialIcon name={detectedAiData.icon || 'check_circle'} className="text-lg" />
                </div>
                <div>
                  <div className="flex items-center gap-2 font-bold text-d-on-surface">
                    <span>✨ Đã áp dụng phân loại AI: {detectedAiData.name_vi}</span>
                    <span className="text-emerald-700 dark:text-emerald-400 font-mono">({detectedAiData.confidence}%)</span>
                  </div>
                  <p className="text-d-on-surface-variant text-[11px] mt-0.5">
                    Giá tham khảo: <strong className="text-d-on-surface">{detectedAiData.price_range}</strong> — {detectedAiData.recycling_tip}
                  </p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setDetectedAiData(null)}
                className="self-end sm:self-center text-xs font-bold text-d-on-surface-variant hover:text-d-on-surface px-2 py-1 rounded"
              >
                ✕ Đóng
              </button>
            </div>
          )}

          {/* Card: Mô tả phế liệu (Có lựa chọn gợi ý mẫu hoặc nhập tay) */}
          <div className="bg-d-surface-container-lowest border border-d-border-subtle rounded-2xl p-6 shadow-sm space-y-5">
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-d-border-subtle pb-4">
              <div>
                <h2 className="font-bold text-d-on-surface text-base flex items-center gap-2">
                  <MaterialIcon name="category" className="text-d-primary text-xl" />
                  <span>Mô tả phế liệu cần thu gom</span>
                </h2>
                <p className="text-xs text-d-on-surface-variant mt-0.5">
                  Bạn có thể chọn nhanh từ gợi ý mẫu có sẵn hoặc nhập tay thông tin theo ý muốn
                </p>
              </div>

              {/* Segmented Control: Gợi ý vs Tự nhập */}
              <div className="inline-flex bg-d-surface-container p-1 rounded-xl text-xs font-semibold shrink-0">
                <button
                  type="button"
                  onClick={() => setInputMethod('recommend')}
                  className={`px-3 py-1.5 rounded-lg transition-all flex items-center gap-1.5 ${
                    inputMethod === 'recommend'
                      ? 'bg-d-surface-container-lowest text-d-primary shadow-sm'
                      : 'text-d-on-surface-variant hover:text-d-on-surface'
                  }`}
                >
                  <MaterialIcon name="lightbulb" className="text-sm" />
                  <span>Mẫu gợi ý có sẵn</span>
                </button>
                <button
                  type="button"
                  onClick={() => setInputMethod('manual')}
                  className={`px-3 py-1.5 rounded-lg transition-all flex items-center gap-1.5 ${
                    inputMethod === 'manual'
                      ? 'bg-d-surface-container-lowest text-d-primary shadow-sm'
                      : 'text-d-on-surface-variant hover:text-d-on-surface'
                  }`}
                >
                  <MaterialIcon name="fact_check" className="text-sm" />
                  <span>Tự nhập tay</span>
                </button>
              </div>
            </div>

            {/* Phân vùng Gợi ý có sẵn */}
            {inputMethod === 'recommend' && (
              <div className="space-y-3">
                <div className="flex items-center justify-between">
                  <span className="text-xs font-bold text-d-on-surface uppercase tracking-wider">
                    Chọn loại phế liệu gợi ý:
                  </span>
                  {selectedPresetId && (
                    <button
                      type="button"
                      onClick={handleClearRecommendation}
                      className="text-xs text-d-primary hover:underline font-semibold"
                    >
                      ✕ Bỏ chọn mẫu
                    </button>
                  )}
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
                  {SCRAP_RECOMMENDATIONS.map((rec) => {
                    const isSelected = selectedPresetId === rec.id;
                    return (
                      <button
                        key={rec.id}
                        type="button"
                        onClick={() => handleSelectRecommendation(rec)}
                        className={`text-left p-3.5 rounded-xl border transition-all flex flex-col justify-between ${
                          isSelected
                            ? 'border-d-primary bg-d-primary/5 ring-1 ring-d-primary shadow-sm'
                            : 'border-d-border-subtle bg-d-surface-container-lowest hover:border-d-primary/50 hover:bg-d-surface-container-low'
                        }`}
                      >
                        <div className="flex items-start gap-2.5">
                          <div
                            className={`w-9 h-9 rounded-lg flex items-center justify-center shrink-0 ${
                              isSelected
                                ? 'bg-d-primary text-d-on-primary'
                                : 'bg-d-surface-container text-d-primary'
                            }`}
                          >
                            <MaterialIcon name={rec.icon} className="text-lg" />
                          </div>
                          <div className="min-w-0">
                            <h4 className="font-bold text-d-on-surface text-sm truncate">
                              {rec.title}
                            </h4>
                            <p className="text-xs text-d-on-surface-variant line-clamp-2 mt-0.5">
                              {rec.desc}
                            </p>
                          </div>
                        </div>

                        {isSelected && (
                          <div className="mt-2.5 pt-2 border-t border-d-border-subtle flex items-center justify-between text-[11px] font-bold text-d-primary">
                            <span>✓ Đã áp dụng mẫu</span>
                            <span className="font-normal text-d-on-surface-variant">Có thể sửa bên dưới</span>
                          </div>
                        )}
                      </button>
                    );
                  })}
                </div>
              </div>
            )}

            {/* Ô nhập mô tả chi tiết */}
            <div>
              <label className="text-xs font-semibold text-d-on-surface mb-1.5 block">
                Nội dung mô tả đơn hàng (có thể bổ sung số lượng, loại phế liệu, ghi chú):
              </label>
              <textarea
                value={form.description}
                onChange={(e) => setForm({ ...form, description: e.target.value })}
                placeholder='Ví dụ: "Khoảng 15kg bìa carton, 2 bao lon bia nhôm, một số đồ sắt vụn cũ gia đình..."'
                rows={3}
                className="w-full rounded-xl border border-d-border-subtle bg-d-surface-container-lowest p-3.5 text-sm text-d-on-surface placeholder:text-d-on-surface-variant/60 focus:border-d-primary focus:ring-1 focus:ring-d-primary outline-none transition-all resize-none"
              />
              <p className="text-[11px] text-d-on-surface-variant mt-1">
                * Bạn chỉ cần mô tả sơ bộ, nhân viên thu gom sẽ hỗ trợ phân loại và cân chuẩn xác khi tới nơi.
              </p>
            </div>
          </div>

          {/* Card: Địa chỉ & Thời gian */}
          <div className="bg-d-surface-container-lowest border border-d-border-subtle rounded-2xl p-6 shadow-sm space-y-4">
            <h2 className="font-bold text-d-on-surface text-base flex items-center gap-2 border-b border-d-border-subtle pb-3">
              <MaterialIcon name="local_shipping" className="text-d-primary text-xl" />
              <span>Địa điểm & Thời gian thu gom</span>
            </h2>

            {/* Địa chỉ thu gom */}
            <div>
              <div className="flex items-center justify-between mb-1.5">
                <label className="text-xs font-semibold text-d-on-surface">
                  📍 Địa chỉ thu gom *
                </label>
                {lastUsedRequest && !form.address && (
                  <button
                    type="button"
                    onClick={applyPreviousAddress}
                    className="text-xs text-d-primary hover:underline font-semibold"
                  >
                    + Dùng địa chỉ đơn trước ({lastUsedRequest.address.slice(0, 30)}...)
                  </button>
                )}
              </div>

              <GoongAutocomplete
                value={form.address}
                onChange={(val) => setForm({ ...form, address: val })}
                onSelect={handleAddressSelect}
                placeholder="Nhập địa chỉ hoặc bấm nút GPS để chọn vị trí..."
              />

              {form.latitude && (
                <p className="text-[11px] text-d-primary mt-1 font-mono flex items-center gap-1">
                  <MaterialIcon name="check_circle" className="text-xs" />
                  <span>Tọa độ xác định: {form.latitude.toFixed(5)}, {form.longitude.toFixed(5)}</span>
                </p>
              )}
            </div>

            {/* Ngày & Khung giờ thu gom */}
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 pt-2">
              <div>
                <label className="text-xs font-semibold text-d-on-surface mb-1.5 block">
                  📅 Ngày mong muốn thu gom
                </label>
                <input
                  type="date"
                  value={form.preferredDate}
                  onChange={(e) => setForm({ ...form, preferredDate: e.target.value })}
                  min={new Date().toISOString().split('T')[0]}
                  className="w-full rounded-xl border border-d-border-subtle bg-d-surface-container-lowest p-3 text-sm text-d-on-surface focus:border-d-primary focus:ring-1 focus:ring-d-primary outline-none transition-all"
                />
              </div>

              <div>
                <label className="text-xs font-semibold text-d-on-surface mb-1.5 block">
                  ⏰ Khung giờ mong muốn
                </label>
                <select
                  value={form.preferredTimeSlot}
                  onChange={(e) => setForm({ ...form, preferredTimeSlot: e.target.value })}
                  className="w-full rounded-xl border border-d-border-subtle bg-d-surface-container-lowest p-3 text-sm text-d-on-surface focus:border-d-primary focus:ring-1 focus:ring-d-primary outline-none transition-all"
                >
                  <option value="">Linh hoạt theo điều phối</option>
                  {TIME_SLOTS.map((slot) => (
                    <option key={slot} value={slot}>
                      {slot}
                    </option>
                  ))}
                </select>
              </div>
            </div>
          </div>

          {/* Card: Ảnh phế liệu */}
          <div className="bg-d-surface-container-lowest border border-d-border-subtle rounded-2xl p-6 shadow-sm space-y-4">
            <h2 className="font-bold text-d-on-surface text-base flex items-center gap-2 border-b border-d-border-subtle pb-3">
              <MaterialIcon name="inventory_2" className="text-d-primary text-xl" />
              <span>Hình ảnh phế liệu (tối đa 5 ảnh)</span>
            </h2>

            <div className="grid grid-cols-3 sm:grid-cols-5 gap-3">
              {images.map((img, idx) => (
                <div
                  key={idx}
                  className="relative aspect-square rounded-xl overflow-hidden border border-d-border-subtle group shadow-xs"
                >
                  <img src={img.preview} alt="" className="w-full h-full object-cover" />
                  {img.uploading && (
                    <div className="absolute inset-0 bg-black/60 flex items-center justify-center">
                      <Loader2 size={20} className="text-white animate-spin" />
                    </div>
                  )}
                  {img.url && (
                    <div className="absolute top-1.5 right-1.5 w-5 h-5 bg-d-primary-container text-d-on-primary-container rounded-full flex items-center justify-center text-xs font-bold shadow-sm">
                      ✓
                    </div>
                  )}
                  <button
                    type="button"
                    onClick={() => removeImage(idx)}
                    className="absolute top-1.5 left-1.5 w-6 h-6 bg-red-600 text-white rounded-full flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity shadow-sm"
                  >
                    ✕
                  </button>
                </div>
              ))}

              {images.length < 5 && (
                <button
                  type="button"
                  onClick={() => fileInputRef.current?.click()}
                  className="aspect-square rounded-xl border-2 border-dashed border-d-border-subtle hover:border-d-primary flex flex-col items-center justify-center gap-1.5 transition-all hover:bg-d-surface-container-low"
                >
                  <MaterialIcon name="add_box" className="text-2xl text-d-primary" />
                  <span className="text-xs font-semibold text-d-on-surface-variant">Thêm ảnh</span>
                </button>
              )}
            </div>
            <input
              ref={fileInputRef}
              type="file"
              accept="image/*"
              multiple
              onChange={handleFileSelect}
              className="hidden"
            />
          </div>

          {/* Action button */}
          <div className="flex justify-end pt-2">
            <button
              type="button"
              onClick={() => {
                if (!form.address.trim()) return toast.error('Vui lòng nhập địa chỉ thu gom');
                setStep(2);
              }}
              className="inline-flex items-center gap-2 rounded-full bg-d-primary hover:bg-[#385500] text-d-on-primary font-semibold px-8 py-3.5 transition-all shadow-sm active:scale-95 text-sm"
            >
              <span>Tiếp tục chọn hình thức</span>
              <MaterialIcon name="arrow_forward" className="text-base" />
            </button>
          </div>
        </div>
      )}

      {/* ═════════════════════════════════════════════════════
          BƯỚC 2: CHỌN HÌNH THỨC (CHỌN KHO VỰA / NỔ ĐƠN)
      ══════════════════════════════════════════════════════ */}
      {step === 2 && !mode && (
        <div className="space-y-4">
          <div className="bg-d-surface-container-lowest border border-d-border-subtle rounded-2xl p-6 shadow-sm">
            <h2 className="font-bold text-d-on-surface text-base mb-1">
              Chọn phương thức thu gom
            </h2>
            <p className="text-sm text-d-on-surface-variant mb-6">
              Bạn có thể tự chọn kho vựa mong muốn hoặc phát sóng đơn để kho vựa gần nhất nhận xử lý.
            </p>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <button
                type="button"
                onClick={() => setMode('choose')}
                className="d-bento-card text-left p-6 hover:border-d-primary transition-all group flex flex-col justify-between"
              >
                <div>
                  <div className="w-12 h-12 rounded-2xl bg-d-surface-container text-d-primary flex items-center justify-center mb-4">
                    <MaterialIcon name="domain" className="text-2xl" />
                  </div>
                  <h3 className="font-bold text-d-on-surface text-base group-hover:text-d-primary transition-colors">
                    Chọn kho vựa cụ thể
                  </h3>
                  <p className="text-xs text-d-on-surface-variant mt-2 leading-relaxed">
                    Xem danh sách các kho vựa xung quanh trên bản đồ, bảng giá niêm yết công khai và điểm đánh giá sao uy tín.
                  </p>
                </div>
                <div className="mt-6 flex items-center gap-1 text-xs font-bold text-d-primary">
                  <span>Xem danh sách kho</span>
                  <MaterialIcon name="arrow_forward" className="text-sm" />
                </div>
              </button>

              <button
                type="button"
                onClick={() => {
                  setMode('broadcast');
                  setStep(3);
                }}
                className="d-bento-card text-left p-6 hover:border-d-primary transition-all group flex flex-col justify-between"
              >
                <div>
                  <div className="w-12 h-12 rounded-2xl bg-d-primary-container text-d-on-primary-container flex items-center justify-center mb-4">
                    <MaterialIcon name="public" className="text-2xl" />
                  </div>
                  <h3 className="font-bold text-d-on-surface text-base group-hover:text-d-primary transition-colors">
                    Nổ đơn nhanh (Broadcast)
                  </h3>
                  <p className="text-xs text-d-on-surface-variant mt-2 leading-relaxed">
                    Phát đơn đồng thời tới tất cả các kho vựa trong bán kính 20km. Kho nào nhận trước sẽ tiến hành điều phối thu gom trước.
                  </p>
                </div>
                <div className="mt-6 flex items-center gap-1 text-xs font-bold text-d-primary">
                  <span>Nổ đơn ngay</span>
                  <MaterialIcon name="arrow_forward" className="text-sm" />
                </div>
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ═════════════════════════════════════════════════════
          BƯỚC 2b: DANH SÁCH & BẢN ĐỒ KHO VỰA
      ══════════════════════════════════════════════════════ */}
      {step === 2 && mode === 'choose' && (
        <div className="space-y-4">
          <div className="flex items-center justify-between">
            <div>
              <h2 className="font-bold text-d-on-surface text-base">
                Kho vựa gần bạn
              </h2>
              <p className="text-xs text-d-on-surface-variant mt-0.5">
                {form.latitude
                  ? 'Được sắp xếp theo khoảng cách di chuyển thực tế gần bạn nhất'
                  : 'Danh sách các kho vựa đang hoạt động'}
              </p>
            </div>
            <button
              type="button"
              onClick={() => setMode(null)}
              className="text-xs text-d-on-surface-variant hover:text-d-primary font-semibold"
            >
              Đổi hình thức
            </button>
          </div>

          {/* Bản đồ Google Maps */}
          {(form.latitude || viewport.latitude) && (
            <div className="h-64 w-full rounded-2xl overflow-hidden border border-d-border-subtle relative z-10 shadow-sm">
              <APIProvider apiKey={import.meta.env.VITE_GOOGLE_MAPS_API_KEY || ''}>
                <Map
                  defaultZoom={viewport.zoom}
                  defaultCenter={{ lat: viewport.latitude, lng: viewport.longitude }}
                  center={{ lat: viewport.latitude, lng: viewport.longitude }}
                  onCameraChanged={(ev) =>
                    setViewport({
                      latitude: ev.detail.center.lat,
                      longitude: ev.detail.center.lng,
                      zoom: ev.detail.zoom,
                    })
                  }
                  disableDefaultUI={true}
                >
                  {form.latitude && form.longitude && (
                    <Marker position={{ lat: form.latitude, lng: form.longitude }} title="Vị trí của bạn" />
                  )}

                  {depots.map((d) =>
                    d.latitude && d.longitude ? (
                      <Marker
                        key={d.id}
                        position={{ lat: d.latitude, lng: d.longitude }}
                        onClick={() => {
                          setSelectedDepot(d);
                          setSelectedDepotPopup(d);
                        }}
                        title={d.name}
                      />
                    ) : null
                  )}

                  {selectedDepotPopup && (
                    <InfoWindow
                      position={{
                        lat: selectedDepotPopup.latitude,
                        lng: selectedDepotPopup.longitude,
                      }}
                      onCloseClick={() => setSelectedDepotPopup(null)}
                    >
                      <div className="text-center min-w-[140px] p-2">
                        <strong className="text-d-on-surface text-sm block mb-1">
                          {selectedDepotPopup.name}
                        </strong>
                        <span className="text-d-primary font-bold text-xs">
                          {selectedDepotPopup.routingDistanceKm != null
                            ? `${selectedDepotPopup.routingDistanceKm.toFixed(1)} km`
                            : ''}
                        </span>
                        <button
                          type="button"
                          onClick={(e) => {
                            e.stopPropagation();
                            setForm({ ...form, targetDepotId: selectedDepotPopup.id });
                            setStep(3);
                          }}
                          className="mt-2 bg-d-primary text-d-on-primary px-3 py-1.5 rounded-full text-xs font-bold w-full active:scale-95 transition-transform"
                        >
                          Chọn kho này
                        </button>
                      </div>
                    </InfoWindow>
                  )}
                </Map>
              </APIProvider>
            </div>
          )}

          {/* Danh sách thẻ kho vựa */}
          <div className="space-y-3">
            {depotsLoading ? (
              <div className="flex justify-center py-12">
                <Loader2 size={24} className="text-d-primary animate-spin" />
              </div>
            ) : depots.length === 0 ? (
              <div className="text-center py-12 bg-d-surface-container-lowest rounded-2xl border border-d-border-subtle p-6 text-sm text-d-on-surface-variant">
                Không tìm thấy kho vựa nào đang mở trong khu vực
              </div>
            ) : (
              depots.map((depot) => {
                const isSelected = selectedDepot?.id === depot.id;
                return (
                  <div
                    key={depot.id}
                    className={`bg-d-surface-container-lowest border rounded-2xl p-5 transition-all shadow-sm ${
                      isSelected
                        ? 'border-d-primary ring-1 ring-d-primary'
                        : 'border-d-border-subtle hover:border-d-primary/50'
                    }`}
                  >
                    <div className="flex items-start justify-between gap-4">
                      <div className="flex items-start gap-3.5 min-w-0">
                        <div className="w-12 h-12 rounded-xl bg-d-surface-container text-d-primary flex items-center justify-center shrink-0">
                          <MaterialIcon name="domain" className="text-2xl" />
                        </div>
                        <div className="min-w-0">
                          <h3 className="font-bold text-d-on-surface text-base truncate">
                            {depot.name}
                          </h3>
                          <p className="text-xs text-d-on-surface-variant truncate mt-0.5">
                            📍 {depot.address}
                          </p>

                          <div className="flex items-center gap-3 mt-2 flex-wrap text-xs">
                            <span className="flex items-center gap-1 font-bold text-[#b45309]">
                              <MaterialIcon name="star" className="text-sm" />
                              <span>{depot.avgRating?.toFixed(1) || '5.0'}</span>
                            </span>
                            <span className="text-d-on-surface-variant">
                              • {depot.totalDone} đơn hoàn tất
                            </span>
                            {depot.routingDistanceKm != null ? (
                              <span className="font-semibold text-d-primary">
                                • {depot.routingDistanceKm.toFixed(1)} km ({depot.routingDurationText})
                              </span>
                            ) : depot.distanceKm != null ? (
                              <span className="font-semibold text-d-primary">
                                • {depot.distanceKm.toFixed(1)} km
                              </span>
                            ) : null}
                          </div>

                          {/* Giá thu mua */}
                          {depot.prices && (
                            <div className="mt-3">
                              <button
                                type="button"
                                onClick={() =>
                                  setShowPricesId(showPricesId === depot.id ? null : depot.id)
                                }
                                className="text-xs font-semibold text-d-primary hover:underline flex items-center gap-1"
                              >
                                <span>{showPricesId === depot.id ? '✕ Đóng bảng giá' : '💰 Xem bảng giá thu mua'}</span>
                              </button>

                              {showPricesId === depot.id && (
                                <div className="mt-2.5 pt-2.5 border-t border-d-border-subtle grid grid-cols-2 sm:grid-cols-3 gap-2">
                                  {Object.entries(depot.prices).map(([mat, price]) => (
                                    <div
                                      key={mat}
                                      className="flex justify-between items-center text-xs bg-d-surface-container px-2.5 py-1.5 rounded-lg"
                                    >
                                      <span className="font-medium text-d-on-surface-variant">{mat}</span>
                                      <span className="font-bold text-d-primary">
                                        {price.toLocaleString('vi-VN')} đ/kg
                                      </span>
                                    </div>
                                  ))}
                                </div>
                              )}
                            </div>
                          )}
                        </div>
                      </div>

                      <button
                        type="button"
                        onClick={() => {
                          setSelectedDepot(depot);
                          setForm({ ...form, targetDepotId: depot.id });
                          setStep(3);
                        }}
                        className="rounded-full bg-d-primary hover:bg-[#385500] text-d-on-primary px-5 py-2 text-xs font-bold shrink-0 transition-colors shadow-sm"
                      >
                        Chọn kho này
                      </button>
                    </div>
                  </div>
                );
              })
            )}
          </div>
        </div>
      )}

      {/* ═════════════════════════════════════════════════════
          BƯỚC 3: XÁC NHẬN VÀ GỬI ĐƠN
      ══════════════════════════════════════════════════════ */}
      {step === 3 && (
        <div className="space-y-6">
          <div className="bg-d-surface-container-lowest border border-d-border-subtle rounded-2xl p-6 shadow-sm space-y-4">
            <h2 className="font-bold text-d-on-surface text-base border-b border-d-border-subtle pb-3">
              Xác nhận thông tin đơn thu gom
            </h2>

            <div className="divide-y divide-d-border-subtle text-sm">
              <div className="py-3 flex justify-between gap-4">
                <span className="text-d-on-surface-variant text-xs">Mô tả phế liệu</span>
                <span className="font-medium text-d-on-surface text-right max-w-md">
                  {form.description || '(Chưa nhập mô tả)'}
                </span>
              </div>
              <div className="py-3 flex justify-between gap-4">
                <span className="text-d-on-surface-variant text-xs">Địa chỉ thu gom</span>
                <span className="font-medium text-d-on-surface text-right max-w-md">
                  {form.address}
                </span>
              </div>
              <div className="py-3 flex justify-between gap-4">
                <span className="text-d-on-surface-variant text-xs">Ngày thu gom</span>
                <span className="font-medium text-d-on-surface">
                  {form.preferredDate
                    ? new Date(form.preferredDate).toLocaleDateString('vi-VN')
                    : 'Linh hoạt'}
                </span>
              </div>
              <div className="py-3 flex justify-between gap-4">
                <span className="text-d-on-surface-variant text-xs">Khung giờ</span>
                <span className="font-medium text-d-on-surface">
                  {form.preferredTimeSlot || 'Linh hoạt theo điều phối'}
                </span>
              </div>
              <div className="py-3 flex justify-between gap-4">
                <span className="text-d-on-surface-variant text-xs">Hình thức gửi đơn</span>
                <span className="font-bold text-d-primary">
                  {mode === 'choose' ? `🏢 Kho vựa: ${selectedDepot?.name}` : '📡 Nổ đơn (Broadcast 20km)'}
                </span>
              </div>
              <div className="py-3 flex justify-between gap-4">
                <span className="text-d-on-surface-variant text-xs">Ảnh phế liệu</span>
                <span className="font-medium text-d-on-surface">{images.length} ảnh đã đính kèm</span>
              </div>
            </div>

            {/* Xem trước ảnh */}
            {images.length > 0 && (
              <div className="flex gap-2.5 overflow-x-auto pt-2 pb-1">
                {images.map((img, i) => (
                  <img
                    key={i}
                    src={img.preview}
                    alt=""
                    className="w-16 h-16 rounded-xl object-cover border border-d-border-subtle shrink-0"
                  />
                ))}
              </div>
            )}
          </div>

          {/* Nút gửi đơn */}
          <div className="flex justify-end gap-3 pt-2">
            <button
              type="button"
              onClick={() => setStep(2)}
              disabled={createMutation.isPending}
              className="rounded-full border border-d-border-subtle bg-d-surface hover:bg-d-surface-container px-6 py-3 text-sm font-semibold text-d-on-surface transition-colors"
            >
              Chỉnh sửa lại
            </button>
            <button
              type="button"
              onClick={handleSubmit}
              disabled={createMutation.isPending}
              className="inline-flex items-center gap-2 rounded-full bg-d-primary hover:bg-[#385500] text-d-on-primary font-semibold px-8 py-3.5 transition-all shadow-sm active:scale-95 text-sm disabled:opacity-50"
            >
              {createMutation.isPending ? (
                <>
                  <Loader2 size={18} className="animate-spin" />
                  <span>Đang tạo đơn...</span>
                </>
              ) : (
                <>
                  <MaterialIcon name="publish" className="text-base" />
                  <span>Gửi đơn thu gom ngay</span>
                </>
              )}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
