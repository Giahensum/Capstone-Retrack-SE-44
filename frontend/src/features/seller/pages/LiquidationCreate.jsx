import { useRef, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Link, useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import { sellerApi } from '../api';
import { formatCurrency } from '@/lib/utils';

const toLocalInput = (date) => {
  const d = new Date(date.getTime() - date.getTimezoneOffset() * 60000);
  return d.toISOString().slice(0, 16);
};
// randomUUID chỉ có trên ngữ cảnh an toàn (https/localhost); dự phòng cho truy cập qua http trong mạng nội bộ.
const newOperationId = () => (globalThis.crypto?.randomUUID
  ? globalThis.crypto.randomUUID()
  : 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    return (c === 'x' ? r : (r & 0x3) | 0x8).toString(16);
  }));
const inHours = (h) => toLocalInput(new Date(Date.now() + h * 3600000));

const field = 'w-full bg-d-surface-container-lowest border border-d-border-subtle rounded-xl px-4 py-3 text-sm text-d-on-surface focus:border-d-primary focus:ring-1 focus:ring-d-primary outline-none transition-all';
const label = 'block text-xs font-semibold text-d-on-surface-variant mb-1.5';

function validate(f) {
  const errors = {};
  if (f.title.trim().length < 5) errors.title = 'Tiêu đề tối thiểu 5 ký tự.';
  if (f.description.trim().length < 10) errors.description = 'Mô tả tối thiểu 10 ký tự.';
  if (f.scopeDetails.trim().length < 10) errors.scopeDetails = 'Phạm vi tài sản tối thiểu 10 ký tự.';
  if (f.collectionTerms.trim().length < 10) errors.collectionTerms = 'Điều kiện thu gom/thanh toán tối thiểu 10 ký tự.';
  if (f.publicArea.trim().length < 2) errors.publicArea = 'Nhập khu vực hiển thị công khai.';
  if (f.address.trim().length < 5) errors.address = 'Nhập địa chỉ khảo sát.';
  if (!/^[0-9+\-\s]{8,20}$/.test(f.contactPhone.trim())) errors.contactPhone = 'Số điện thoại không hợp lệ.';
  const start = Number(f.startingPrice), step = Number(f.bidIncrement);
  if (!Number.isInteger(start) || start <= 0) errors.startingPrice = 'Giá khởi điểm phải là số nguyên VND dương.';
  if (!Number.isInteger(step) || step <= 0) errors.bidIncrement = 'Bước giá phải là số nguyên VND dương.';
  const pct = Number(f.depositPercent);
  if (!Number.isFinite(pct) || pct < 1 || pct > 50 || Math.round(pct * 100) !== pct * 100) errors.depositPercent = 'Tỷ lệ đặt cọc từ 1% đến 50% (tối đa 2 chữ số thập phân).';
  const t = ['surveyStart', 'surveyEnd', 'biddingStart', 'biddingEnd', 'collectionDeadline'].map((k) => new Date(f[k]).getTime());
  if (t.some(Number.isNaN)) errors.schedule = 'Điền đủ các mốc thời gian.';
  else if (!(t[0] < t[1] && t[1] <= t[2] && t[2] < t[3] && t[3] + 10 * 60000 < t[4]))
    errors.schedule = 'Thứ tự: khảo sát kết thúc trước khi mở trả giá; hạn thu gom phải sau giờ đóng ít nhất 10 phút (thời gian gia hạn tối đa).';
  else if (t[3] - t[2] < 10 * 60000) errors.schedule = 'Phiên trả giá kéo dài tối thiểu 10 phút.';
  return errors;
}

export default function LiquidationCreate() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  // Mã thao tác giữ nguyên khi bấm gửi lại để server không tạo trùng lô.
  const operationId = useRef(newOperationId());
  const [form, setForm] = useState({
    title: '', description: '', scopeDetails: '', collectionTerms: '', publicArea: '', address: '', contactPhone: '',
    startingPrice: '', bidIncrement: '', depositPercent: '10',
    surveyStart: inHours(0), surveyEnd: inHours(24), biddingStart: inHours(48), biddingEnd: inHours(72), collectionDeadline: inHours(24 * 10),
  });
  const [images, setImages] = useState([]);
  const [uploading, setUploading] = useState(false);
  const [errors, setErrors] = useState({});
  const set = (key) => (e) => setForm((f) => ({ ...f, [key]: e.target.value }));

  const createMutation = useMutation({
    mutationFn: (payload) => sellerApi.createLiquidation(payload),
    onSuccess: (lot) => {
      queryClient.invalidateQueries({ queryKey: ['seller-liquidations'] });
      toast.success('Đã đăng lô thanh lý.');
      navigate(`/seller/liquidations/${lot.lot.id}`);
    },
    onError: (err) => {
      // Server đã trả lời dứt khoát (4xx/5xx): nội dung có thể được sửa nên cấp mã mới.
      // Mất kết nối (không có response) giữ mã cũ để gửi lại không tạo trùng lô.
      if (err?.response) operationId.current = newOperationId();
      toast.error(err?.response?.data?.message || 'Không đăng được lô. Vui lòng thử lại.');
    },
  });

  const handleFiles = async (e) => {
    const files = Array.from(e.target.files ?? []);
    e.target.value = '';
    if (images.length + files.length > 10) return toast.error('Tối đa 10 ảnh.');
    setUploading(true);
    try {
      const urls = [];
      for (const file of files) urls.push(await sellerApi.uploadImage(file));
      setImages((prev) => [...prev, ...urls]);
    } catch (err) {
      toast.error(err?.response?.data?.message || 'Tải ảnh thất bại.');
    } finally {
      setUploading(false);
    }
  };

  const submit = (e) => {
    e.preventDefault();
    const found = validate(form);
    setErrors(found);
    if (Object.keys(found).length) return toast.error('Vui lòng kiểm tra lại các trường được đánh dấu.');
    createMutation.mutate({
      operationId: operationId.current,
      title: form.title.trim(), description: form.description.trim(), scopeDetails: form.scopeDetails.trim(),
      collectionTerms: form.collectionTerms.trim(), publicArea: form.publicArea.trim(), address: form.address.trim(),
      contactPhone: form.contactPhone.trim(), imageUrls: images,
      surveyStart: new Date(form.surveyStart).toISOString(), surveyEnd: new Date(form.surveyEnd).toISOString(),
      biddingStart: new Date(form.biddingStart).toISOString(), biddingEnd: new Date(form.biddingEnd).toISOString(),
      collectionDeadline: new Date(form.collectionDeadline).toISOString(),
      startingPrice: Number(form.startingPrice), bidIncrement: Number(form.bidIncrement),
      depositPercent: Number(form.depositPercent),
    });
  };

  const busy = createMutation.isPending || uploading;
  const err = (name) => (errors[name] ? <p role="alert" className="text-xs text-red-600 mt-1">{errors[name]}</p> : null);

  return (
    <form onSubmit={submit} noValidate className="flex flex-col p-4 md:p-6 gap-6 w-full max-w-4xl mx-auto">
      <div>
        <Link to="/seller/liquidations" className="text-xs font-semibold text-d-primary">← Lô thanh lý của tôi</Link>
        <h1 className="text-d-headline-xl-mobile md:text-d-headline-xl font-d-headline-xl text-d-on-surface mt-2">Đăng lô thanh lý</h1>
        <p className="text-d-body-md text-d-on-surface-variant mt-2">
          Chủ kho khảo sát rồi trả giá <strong>trọn lô</strong> (VND). Phiên đóng đúng giờ; giá cao nhất hợp lệ thắng. Nền tảng không giữ tiền — hai bên tự chuyển tiền và giao nhận.
        </p>
      </div>

      <section className="bg-d-surface-container-lowest border border-d-border-subtle rounded-2xl p-5 grid gap-4">
        <h2 className="text-sm font-bold text-d-on-surface">Thông tin lô</h2>
        <div>
          <label htmlFor="title" className={label}>Tiêu đề</label>
          <input id="title" className={field} maxLength={255} value={form.title} onChange={set('title')} />
          {err("title")}
        </div>
        <div>
          <label htmlFor="description" className={label}>Mô tả</label>
          <textarea id="description" rows={3} className={field} maxLength={5000} value={form.description} onChange={set('description')} />
          {err("description")}
        </div>
        <div>
          <label htmlFor="scopeDetails" className={label}>Phạm vi tài sản (ước lượng, không cần cân chính xác)</label>
          <textarea id="scopeDetails" rows={2} className={field} maxLength={3000} value={form.scopeDetails} onChange={set('scopeDetails')} />
          {err("scopeDetails")}
        </div>
        <div>
          <label htmlFor="collectionTerms" className={label}>Điều kiện thu gom và thanh toán</label>
          <textarea id="collectionTerms" rows={2} className={field} maxLength={2000} value={form.collectionTerms} onChange={set('collectionTerms')} />
          {err("collectionTerms")}
        </div>
        <div>
          <label className={label} htmlFor="images">Ảnh (tối đa 10)</label>
          <input id="images" type="file" accept="image/jpeg,image/png,image/webp,image/heic" multiple disabled={busy} onChange={handleFiles} className="text-sm" />
          {uploading && <p className="text-xs text-d-on-surface-variant mt-1">Đang tải ảnh...</p>}
          {images.length > 0 && (
            <div className="flex flex-wrap gap-2 mt-2">
              {images.map((url, i) => (
                <div key={url} className="relative">
                  <img src={url} alt={`Ảnh lô ${i + 1}`} className="w-20 h-20 object-cover rounded-lg border border-d-border-subtle" />
                  <button type="button" aria-label={`Xóa ảnh ${i + 1}`} onClick={() => setImages((p) => p.filter((u) => u !== url))}
                    className="absolute -top-2 -right-2 w-6 h-6 rounded-full bg-red-600 text-white text-xs">×</button>
                </div>
              ))}
            </div>
          )}
        </div>
      </section>

      <section className="bg-d-surface-container-lowest border border-d-border-subtle rounded-2xl p-5 grid gap-4 md:grid-cols-2">
        <h2 className="text-sm font-bold text-d-on-surface md:col-span-2">Địa điểm</h2>
        <div>
          <label htmlFor="publicArea" className={label}>Khu vực hiển thị công khai</label>
          <input id="publicArea" className={field} maxLength={255} value={form.publicArea} onChange={set('publicArea')} placeholder="VD: Ngũ Hành Sơn, Đà Nẵng" />
          {err("publicArea")}
        </div>
        <div>
          <label htmlFor="contactPhone" className={label}>Số điện thoại liên hệ</label>
          <input id="contactPhone" className={field} maxLength={20} value={form.contactPhone} onChange={set('contactPhone')} />
          {err("contactPhone")}
        </div>
        <div className="md:col-span-2">
          <label htmlFor="address" className={label}>Địa chỉ khảo sát (chỉ kho đã đăng ký mới xem được)</label>
          <input id="address" className={field} maxLength={1000} value={form.address} onChange={set('address')} />
          {err("address")}
        </div>
      </section>

      <section className="bg-d-surface-container-lowest border border-d-border-subtle rounded-2xl p-5 grid gap-4 md:grid-cols-2">
        <h2 className="text-sm font-bold text-d-on-surface md:col-span-2">Giá</h2>
        <div>
          <label htmlFor="startingPrice" className={label}>Giá khởi điểm (VND)</label>
          <input id="startingPrice" inputMode="numeric" className={field} value={form.startingPrice} onChange={set('startingPrice')} />
          {Number(form.startingPrice) > 0 && <p className="text-xs text-d-on-surface-variant mt-1">{formatCurrency(Number(form.startingPrice))}</p>}
          {err("startingPrice")}
        </div>
        <div>
          <label htmlFor="bidIncrement" className={label}>Bước giá tối thiểu (VND)</label>
          <input id="bidIncrement" inputMode="numeric" className={field} value={form.bidIncrement} onChange={set('bidIncrement')} />
          {err("bidIncrement")}
        </div>
        <div className="md:col-span-2">
          <label htmlFor="depositPercent" className={label}>Tiền đặt cọc khi ký hợp đồng (% giá trúng)</label>
          <input id="depositPercent" inputMode="decimal" className={field} value={form.depositPercent} onChange={set('depositPercent')} />
          {Number(form.startingPrice) > 0 && Number(form.depositPercent) > 0 && (
            <p className="text-xs text-d-on-surface-variant mt-1">Nếu trúng đúng giá khởi điểm, cọc ≈ {formatCurrency(Math.round(Number(form.startingPrice) * Number(form.depositPercent) / 100))}. Cọc chuyển trực tiếp cho bạn ngoài hệ thống; công bố công khai trước phiên và cố định khi đã đăng.</p>
          )}
          {err("depositPercent")}
        </div>
      </section>

      <section className="bg-d-surface-container-lowest border border-d-border-subtle rounded-2xl p-5 grid gap-4 md:grid-cols-2">
        <h2 className="text-sm font-bold text-d-on-surface md:col-span-2">Lịch phiên (giờ địa phương)</h2>
        {[['surveyStart', 'Bắt đầu khảo sát'], ['surveyEnd', 'Kết thúc khảo sát / hạn đăng ký'], ['biddingStart', 'Mở trả giá'],
          ['biddingEnd', 'Đóng trả giá (có thể gia hạn tối đa 10 phút nếu có giá phút chót)'], ['collectionDeadline', 'Hạn thu gom']].map(([key, text]) => (
          <div key={key}>
            <label htmlFor={key} className={label}>{text}</label>
            <input id={key} type="datetime-local" className={field} value={form[key]} onChange={set(key)} />
          </div>
        ))}
        <div className="md:col-span-2">{err("schedule")}</div>
      </section>

      <div className="flex justify-end gap-3">
        <Link to="/seller/liquidations" className="px-6 py-3 rounded-full border border-d-border-subtle text-sm font-semibold text-d-on-surface-variant">Hủy</Link>
        <button type="submit" disabled={busy}
          className="px-6 py-3 rounded-full bg-d-primary text-d-on-primary text-sm font-semibold disabled:opacity-50 active:scale-95 transition-all">
          {createMutation.isPending ? 'Đang đăng...' : 'Đăng lô'}
        </button>
      </div>
    </form>
  );
}
