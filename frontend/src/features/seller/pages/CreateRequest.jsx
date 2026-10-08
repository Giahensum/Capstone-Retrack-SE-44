import { useState, useRef, useEffect } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import { sellerApi } from '../api';
import GoongAutocomplete from '@/components/ui/GoongAutocomplete';
import { Camera, Calendar, Send, Building2, Star, ArrowLeft, Radio, ChevronRight, X, ImagePlus, Loader2 } from 'lucide-react';
import { APIProvider, Map, Marker, InfoWindow } from '@vis.gl/react-google-maps';

const GOONG_MAPTILES_KEY = import.meta.env.VITE_GOONG_API_KEY;



const TIME_SLOTS = [
  '07:00 - 09:00', '09:00 - 11:00', '11:00 - 13:00',
  '13:00 - 15:00', '15:00 - 17:00', '17:00 - 19:00',
];

export default function CreateRequest() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const fileInputRef = useRef(null);

  const [step, setStep] = useState(1);
  const [mode, setMode] = useState(null);
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
  const [selectedDepot, setSelectedDepot] = useState(null);
  
  const [viewport, setViewport] = useState({
    latitude: 16.07,
    longitude: 108.15,
    zoom: 13
  });
  
  const [selectedDepotPopup, setSelectedDepotPopup] = useState(null);

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
        const destinations = dbDepots.map(d => `${d.latitude},${d.longitude}`).join('|');
        
        const res = await fetch(`https://rsapi.goong.io/DistanceMatrix?origins=${origins}&destinations=${destinations}&vehicle=bike&api_key=${GOONG_API_KEY}`);
        const data = await res.json();
        
        if (data.rows && data.rows[0]?.elements) {
          const elements = data.rows[0].elements;
          dbDepots.forEach((depot, idx) => {
            if (elements[idx]?.status === 'OK') {
              depot.routingDistanceKm = elements[idx].distance.value / 1000; // convert to km
              depot.routingDurationText = elements[idx].duration.text;
            }
          });
          
          // Sort by real routing distance
          return dbDepots.sort((a, b) => (a.routingDistanceKm || a.distanceKm || 999) - (b.routingDistanceKm || b.distanceKm || 999));
        }
      } catch (err) {
        console.error("Goong DistanceMatrix API error:", err);
      }
      return dbDepots;
    },
    enabled: step === 2 && mode === 'choose',
  });

  useEffect(() => {
    if (step === 2 && mode === 'choose') {
      if (selectedDepot && selectedDepot.latitude) {
        setViewport(v => ({ ...v, latitude: selectedDepot.latitude, longitude: selectedDepot.longitude, zoom: 15 }));
      } else if (form.latitude && form.longitude) {
        setViewport(v => ({ ...v, latitude: form.latitude, longitude: form.longitude, zoom: 13 }));
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
    const newImages = files.map(f => ({
      file: f,
      preview: URL.createObjectURL(f),
      url: null,
      uploading: false,
    }));
    setImages(prev => [...prev, ...newImages]);
  };

  const removeImage = (idx) => {
    setImages(prev => {
      const next = [...prev];
      URL.revokeObjectURL(next[idx].preview);
      next.splice(idx, 1);
      return next;
    });
  };

  const uploadAllImages = async () => {
    const toUpload = images.filter(img => !img.url && !img.uploading);
    if (toUpload.length === 0) return images.filter(img => img.url).map(img => img.url);

    const updated = [...images];
    const urls = [];

    for (let i = 0; i < updated.length; i++) {
      if (updated[i].url) { urls.push(updated[i].url); continue; }
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
    setForm(f => ({ ...f, address, latitude: lat, longitude: lng }));
  };

  // ── Submit ──
  const handleSubmit = async () => {
    if (!form.address.trim()) return toast.error('Vui lòng nhập địa chỉ');

    // Upload images first
    let imageUrl = null;
    if (images.length > 0) {
      const urls = await uploadAllImages();
      if (urls.length !== images.length) {
        return toast.error('Có lỗi khi tải ảnh lên. Vui lòng kiểm tra lại API Key Cloudinary hoặc thử lại.');
      }
      imageUrl = urls.join(','); // store as comma-separated URLs
    }

    // Build preferred datetime from date + time slot
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
    if (step === 2 && mode) { setMode(null); return; }
    if (step > 1) { setStep(step - 1); setMode(null); return; }
    navigate(-1);
  };

  return (
    <div className="p-4 lg:p-8 max-w-2xl mx-auto">
      {/* Header */}
      <button onClick={goBack} className="flex items-center gap-2 text-gray-600 hover:text-gray-800 mb-6 text-sm font-medium transition-colors">
        <ArrowLeft size={16} /> Quay lại
      </button>

      {/* Progress */}
      <div className="flex items-center gap-2 mb-8">
        {['Thông tin', 'Hình thức', 'Xác nhận'].map((label, i) => (
          <div key={i} className="flex-1 flex items-center gap-2">
            <div className={`w-8 h-8 rounded-full flex items-center justify-center text-xs font-bold transition-all ${
              step >= i + 1 ? 'bg-green-200 text-gray-900 shadow-lg shadow-green-200/50' : 'bg-gray-100 text-gray-500'
            }`}>{i + 1}</div>
            {i < 2 && <div className={`flex-1 h-0.5 rounded-full transition-all ${step > i + 1 ? 'bg-green-200' : 'bg-gray-100'}`} />}
          </div>
        ))}
      </div>

      {/* ═══ STEP 1: Thông tin ═══ */}
      {step === 1 && (
        <div className="space-y-5 animate-in fade-in">
          <div>
            <h2 className="text-xl font-black text-gray-900 mb-1">Mô tả phế liệu</h2>
            <p className="text-sm text-gray-500">Bạn chỉ cần mô tả sơ bộ, nhân viên sẽ phân loại và cân giúp bạn</p>
          </div>

          <div className="space-y-4">
            {/* Description */}
            <div>
              <label className="text-sm font-semibold text-gray-700 mb-1.5 block">Mô tả phế liệu</label>
              <textarea
                value={form.description}
                onChange={e => setForm({ ...form, description: e.target.value })}
                placeholder='VD: "Có 1 đống sắt vụn cũ, ít nhôm lon bia, giấy carton..."'
                rows={3}
                className="w-full bg-white border border-gray-300 rounded-xl px-4 py-3 text-sm text-gray-800 placeholder-slate-600 focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500 outline-none transition-all resize-none"
              />
            </div>

            {/* Address with Goong autocomplete */}
            <div>
              <label className="text-sm font-semibold text-gray-700 mb-1.5 block">📍 Địa chỉ thu gom *</label>
              <GoongAutocomplete
                value={form.address}
                onChange={(val) => setForm({ ...form, address: val })}
                onSelect={handleAddressSelect}
                placeholder="Nhập địa chỉ hoặc bấm nút GPS →"
              />
              {form.latitude && (
                <p className="text-[10px] text-green-800 mt-1 font-mono">
                  📌 {form.latitude.toFixed(5)}, {form.longitude.toFixed(5)}
                </p>
              )}
            </div>

            {/* Date + Time Slot */}
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="text-sm font-semibold text-gray-700 mb-1.5 flex items-center gap-2">
                  <Calendar size={14} className="text-blue-400" /> Ngày thu gom
                </label>
                <input
                  type="date"
                  value={form.preferredDate}
                  onChange={e => setForm({ ...form, preferredDate: e.target.value })}
                  min={new Date().toISOString().split('T')[0]}
                  className="w-full bg-white border border-gray-300 rounded-xl px-4 py-3 text-sm text-gray-800 focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500 outline-none transition-all"
                />
              </div>
              <div>
                <label className="text-sm font-semibold text-gray-700 mb-1.5 block">⏰ Khung giờ</label>
                <select
                  value={form.preferredTimeSlot}
                  onChange={e => setForm({ ...form, preferredTimeSlot: e.target.value })}
                  className="w-full bg-white border border-gray-300 rounded-xl px-4 py-3 text-sm text-gray-800 focus:border-emerald-500 outline-none appearance-none"
                >
                  <option value="">Linh hoạt</option>
                  {TIME_SLOTS.map(slot => <option key={slot} value={slot}>{slot}</option>)}
                </select>
              </div>
            </div>

            {/* Image Upload */}
            <div>
              <label className="text-sm font-semibold text-gray-700 mb-1.5 flex items-center gap-2">
                <Camera size={14} className="text-purple-400" /> Hình ảnh phế liệu (tối đa 5 ảnh)
              </label>
              <div className="grid grid-cols-3 sm:grid-cols-5 gap-2">
                {images.map((img, idx) => (
                  <div key={idx} className="relative aspect-square rounded-xl overflow-hidden border border-gray-300 group">
                    <img src={img.preview} alt="" className="w-full h-full object-cover" />
                    {img.uploading && (
                      <div className="absolute inset-0 bg-black/60 flex items-center justify-center">
                        <Loader2 size={20} className="text-gray-900 animate-spin" />
                      </div>
                    )}
                    {img.url && (
                      <div className="absolute top-1 right-1 w-5 h-5 bg-green-200 rounded-full flex items-center justify-center">
                        <span className="text-gray-900 text-[10px]">✓</span>
                      </div>
                    )}
                    <button
                      onClick={() => removeImage(idx)}
                      className="absolute top-1 left-1 w-5 h-5 bg-red-500/80 rounded-full flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity"
                    >
                      <X size={10} className="text-gray-900" />
                    </button>
                  </div>
                ))}
                {images.length < 5 && (
                  <button
                    onClick={() => fileInputRef.current?.click()}
                    className="aspect-square rounded-xl border-2 border-dashed border-gray-300 hover:border-emerald-500/50 flex flex-col items-center justify-center gap-1 transition-all hover:bg-gray-100/50"
                  >
                    <ImagePlus size={20} className="text-gray-500" />
                    <span className="text-[10px] text-gray-500">Thêm ảnh</span>
                  </button>
                )}
              </div>
              <input ref={fileInputRef} type="file" accept="image/*" multiple onChange={handleFileSelect} className="hidden" />
            </div>
          </div>

          <button
            onClick={() => {
              if (!form.address.trim()) return toast.error('Vui lòng nhập địa chỉ');
              setStep(2);
            }}
            className="w-full bg-green-200 hover:bg-green-300 text-gray-900 font-bold py-3.5 rounded-xl transition-all shadow-lg shadow-emerald-500/20 active:scale-[0.98] flex items-center justify-center gap-2"
          >
            Tiếp tục <ChevronRight size={18} />
          </button>
        </div>
      )}

      {/* ═══ STEP 2: Choose mode ═══ */}
      {step === 2 && !mode && (
        <div className="space-y-5 animate-in fade-in">
          <div>
            <h2 className="text-xl font-black text-gray-900 mb-1">Chọn hình thức</h2>
            <p className="text-sm text-gray-500">Bạn muốn chọn kho vựa cụ thể hay để hệ thống tìm giúp?</p>
          </div>

          <div className="space-y-3">
            <button onClick={() => setMode('choose')} className="w-full text-left bg-gray-100/70 border border-gray-300/50 rounded-2xl p-5 hover:border-blue-500/50 hover:bg-gray-100 transition-all group">
              <div className="flex items-center gap-4">
                <div className="w-14 h-14 bg-blue-500/15 rounded-2xl flex items-center justify-center">
                  <Building2 size={24} className="text-blue-400" />
                </div>
                <div className="flex-1">
                  <h3 className="font-bold text-gray-800 group-hover:text-gray-900 text-base">Chọn kho vựa</h3>
                  <p className="text-xs text-gray-500 mt-1">Xem danh sách kho gần bạn, đánh giá sao, số đơn hoàn tất</p>
                </div>
                <ChevronRight size={18} className="text-slate-600 group-hover:text-blue-400 transition-colors" />
              </div>
            </button>

            <button onClick={() => { setMode('broadcast'); setStep(3); }} className="w-full text-left bg-gray-100/70 border border-gray-300/50 rounded-2xl p-5 hover:border-orange-500/50 hover:bg-gray-100 transition-all group">
              <div className="flex items-center gap-4">
                <div className="w-14 h-14 bg-orange-500/15 rounded-2xl flex items-center justify-center">
                  <Radio size={24} className="text-orange-400" />
                </div>
                <div className="flex-1">
                  <h3 className="font-bold text-gray-800 group-hover:text-gray-900 text-base">Nổ đơn (Broadcast)</h3>
                  <p className="text-xs text-gray-500 mt-1">Gửi đến tất cả kho trong bán kính 10km. Ai nhận trước xử lý trước</p>
                </div>
                <ChevronRight size={18} className="text-slate-600 group-hover:text-orange-400 transition-colors" />
              </div>
            </button>
          </div>
        </div>
      )}

      {/* ═══ STEP 2b: Choose depot ═══ */}
      {step === 2 && mode === 'choose' && (
        <div className="space-y-5 animate-in fade-in flex flex-col h-full">
          <div>
            <h2 className="text-xl font-black text-gray-900 mb-1">Chọn kho vựa</h2>
            <p className="text-sm text-gray-500">
              {form.latitude ? 'Sắp xếp theo khoảng cách gần bạn nhất' : 'Danh sách kho vựa đang hoạt động'}
            </p>
          </div>

          {/* MAP VIEW */}
          {(form.latitude || viewport.latitude) && (
            <div className="h-56 w-full rounded-2xl overflow-hidden shrink-0 border border-gray-300/50 relative z-10">
              <APIProvider apiKey={import.meta.env.VITE_GOOGLE_MAPS_API_KEY || ''}>
                <Map
                  defaultZoom={viewport.zoom}
                  defaultCenter={{ lat: viewport.latitude, lng: viewport.longitude }}
                  center={{ lat: viewport.latitude, lng: viewport.longitude }}
                  onCameraChanged={(ev) => setViewport({ latitude: ev.detail.center.lat, longitude: ev.detail.center.lng, zoom: ev.detail.zoom })}
                  disableDefaultUI={true}
                >
                  {form.latitude && form.longitude && (
                    <Marker position={{ lat: form.latitude, lng: form.longitude }} title="Vị trí của bạn" />
                  )}

                  {depots.map(d => d.latitude && d.longitude ? (
                    <Marker 
                      key={d.id} 
                      position={{ lat: d.latitude, lng: d.longitude }} 
                      onClick={() => { setSelectedDepot(d); setSelectedDepotPopup(d); }} 
                      title={d.name}
                    />
                  ) : null)}

                  {selectedDepotPopup && (
                    <InfoWindow position={{ lat: selectedDepotPopup.latitude, lng: selectedDepotPopup.longitude }} onCloseClick={() => setSelectedDepotPopup(null)}>
                      <div className="text-center min-w-[120px] p-1">
                        <strong className="text-slate-800 text-sm block mb-1">{selectedDepotPopup.name}</strong>
                        <span className="text-emerald-600 font-bold text-xs">{selectedDepotPopup.routingDistanceKm != null ? `${selectedDepotPopup.routingDistanceKm.toFixed(1)} km` : ''}</span>
                        <br/>
                        <button 
                           onClick={(e) => { e.stopPropagation(); setForm({ ...form, targetDepotId: selectedDepotPopup.id }); setStep(3); }}
                           className="mt-2 bg-green-200 text-gray-900 px-3 py-1.5 rounded text-xs font-bold w-full active:scale-95 transition-transform">
                           Chọn kho này
                        </button>
                      </div>
                    </InfoWindow>
                  )}
                </Map>
              </APIProvider>
            </div>
          )}

          <div className="space-y-3 overflow-y-auto pr-1 flex-1 pb-4">
            {depotsLoading ? (
              <div className="flex justify-center py-10"><Loader2 size={24} className="text-green-700 animate-spin" /></div>
            ) : depots.length === 0 ? (
              <div className="text-center py-10 text-gray-500 text-sm">Chưa có kho vựa nào hoạt động</div>
            ) : depots.map(depot => (
              <button
                key={depot.id}
                onClick={() => { setSelectedDepot(depot); setForm({ ...form, targetDepotId: depot.id }); setStep(3); }}
                className={`w-full text-left border rounded-2xl p-4 transition-all hover:border-emerald-500/50 ${
                  selectedDepot?.id === depot.id ? 'border-emerald-500 bg-green-200/5' : 'border-gray-300/50 bg-gray-100/50'
                }`}
              >
                <div className="flex items-start gap-3">
                  <div className="w-11 h-11 bg-blue-500/15 rounded-xl flex items-center justify-center shrink-0 mt-0.5">
                    <Building2 size={18} className="text-blue-400" />
                  </div>
                  <div className="flex-1 min-w-0">
                    <h3 className="font-bold text-gray-800 text-sm">{depot.name}</h3>
                    <p className="text-xs text-gray-500 truncate mt-0.5">{depot.address}</p>
                    <div className="flex items-center gap-3 mt-2 flex-wrap">
                      <span className="flex items-center gap-1 text-xs text-yellow-400">
                        <Star size={12} fill="currentColor" /> {depot.avgRating?.toFixed(1) || '–'}
                      </span>
                      <span className="text-xs text-gray-500">{depot.totalDone} đơn hoàn tất</span>
                      {depot.routingDistanceKm != null ? (
                        <span className="text-xs font-semibold text-green-700">
                          📍 {depot.routingDistanceKm.toFixed(1)} km ({depot.routingDurationText})
                        </span>
                      ) : depot.distanceKm != null ? (
                        <span className="text-xs font-semibold text-green-700">📍 {depot.distanceKm.toFixed(1)} km</span>
                      ) : null}
                    </div>
                  </div>
                  <ChevronRight size={16} className="text-slate-600 mt-3" />
                </div>
              </button>
            ))}
          </div>
        </div>
      )}

      {/* ═══ STEP 3: Confirm ═══ */}
      {step === 3 && (
        <div className="space-y-5 animate-in fade-in">
          <div>
            <h2 className="text-xl font-black text-gray-900 mb-1">Xác nhận đơn</h2>
            <p className="text-sm text-gray-500">Kiểm tra lại thông tin trước khi gửi</p>
          </div>

          <div className="bg-gray-100/50 border border-gray-300/50 rounded-2xl divide-y divide-slate-700/50">
            <InfoRow label="Mô tả" value={form.description || '(chưa nhập)'} />
            <InfoRow label="Địa chỉ" value={form.address} />
            <InfoRow label="Ngày" value={form.preferredDate ? new Date(form.preferredDate).toLocaleDateString('vi-VN') : 'Linh hoạt'} />
            <InfoRow label="Khung giờ" value={form.preferredTimeSlot || 'Linh hoạt'} />
            <InfoRow label="Hình thức" value={mode === 'choose' ? `🏢 ${selectedDepot?.name}` : '📡 Nổ đơn (Broadcast)'} />
            <InfoRow label="Ảnh đính kèm" value={`${images.length} ảnh`} />
          </div>

          {/* Image preview */}
          {images.length > 0 && (
            <div className="flex gap-2 overflow-x-auto pb-1">
              {images.map((img, i) => (
                <img key={i} src={img.preview} alt="" className="w-16 h-16 rounded-xl object-cover border border-gray-300 shrink-0" />
              ))}
            </div>
          )}

          <button
            onClick={handleSubmit}
            disabled={createMutation.isPending}
            className="w-full bg-green-200 hover:bg-green-300 text-gray-900 font-bold py-3.5 rounded-xl transition-all shadow-lg shadow-emerald-500/20 active:scale-[0.98] flex items-center justify-center gap-2 disabled:opacity-60"
          >
            {createMutation.isPending ? (
              <Loader2 size={20} className="animate-spin" />
            ) : (
              <><Send size={18} /> Gửi đơn thu gom</>
            )}
          </button>
        </div>
      )}
    </div>
  );
}

function InfoRow({ label, value }) {
  return (
    <div className="flex justify-between items-center px-5 py-3.5">
      <span className="text-xs text-gray-500 font-medium">{label}</span>
      <span className="text-sm text-gray-800 font-medium text-right max-w-[60%] truncate">{value}</span>
    </div>
  );
}



