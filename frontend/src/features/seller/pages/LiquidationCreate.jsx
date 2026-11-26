import { useRef, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Link, useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import { Plus, Trash2 } from 'lucide-react';
import { sellerApi } from '../api';
import { formatCurrency } from '@/lib/utils';
import VietnamAreaPicker from '@/components/ui/VietnamAreaPicker';
import StreetAddressSelect from '@/components/ui/StreetAddressSelect';
import { GOONG_ENABLED } from '@/lib/goongPlaces';

/*
 * Đăng phiên thanh lý nhiều hạng mục (V2): lịch khảo sát/trả giá/hạn thu gom nhập một lần cho cả phiên;
 * mỗi hạng mục có đơn vị, số lượng, giá khởi điểm và bước giá riêng. Tiền mua = đơn giá trúng × số lượng đã đăng.
 */
const MAX_ITEMS = 20;
const UNITS = [['kg', 'kg (vật liệu)'], ['cai', 'cái (đồ cũ)'], ['bo', 'bộ (đồ cũ)']];
const MATERIALS = [['', 'Không chọn'], ['IRON', 'Sắt'], ['STEEL', 'Thép'], ['COPPER', 'Đồng'], ['ALUMINUM', 'Nhôm'], ['PAPER', 'Giấy'],
  ['CARDBOARD', 'Bìa carton'], ['PET', 'Nhựa PET'], ['HDPE', 'Nhựa HDPE'], ['PVC', 'Nhựa PVC'], ['ELECTRONIC_WASTE', 'Rác thải điện tử'], ['OTHER', 'Khác']];
const DESCRIPTION_HINT = { kg: 'Hiện trạng, độ lẫn tạp', cai: 'Loại và tình trạng của mỗi cái', bo: 'Thành phần của mỗi bộ và tình trạng' };

const toLocalInput = (date) => new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
const inHours = (h) => toLocalInput(new Date(Date.now() + h * 3600000));
const newOperationId = () => (globalThis.crypto?.randomUUID ? globalThis.crypto.randomUUID()
  : 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => { const r = (Math.random() * 16) | 0; return (c === 'x' ? r : (r & 0x3) | 0x8).toString(16); }));
const emptyItem = () => ({ key: newOperationId(), name: '', unit: 'kg', materialCode: '', quantity: '', description: '', startingUnitPrice: '', bidIncrement: '' });

const field = 'w-full bg-d-surface-container-lowest border border-d-border-subtle rounded-xl px-4 py-3 text-sm text-d-on-surface focus:border-d-primary focus:ring-1 focus:ring-d-primary outline-none transition-all';
const label = 'block text-xs font-semibold text-d-on-surface-variant mb-1.5';

function validate(f) {
  const errors = {};
  if (f.title.trim().length < 5) errors.title = 'Tiêu đề tối thiểu 5 ký tự.';
  if (f.description.trim().length < 10) errors.description = 'Mô tả tối thiểu 10 ký tự.';
  if (f.collectionTerms.trim().length < 10) errors.collectionTerms = 'Điều kiện thu gom tối thiểu 10 ký tự.';
  if (!f.area.province) errors.province = 'Chọn tỉnh/thành phố.';
  if (!f.area.ward) errors.ward = 'Chọn phường/xã.';
  if (f.street.trim().length < 2) errors.street = 'Nhập số nhà, tên đường.';
  if (!/^[0-9+\-\s]{8,20}$/.test(f.contactPhone.trim())) errors.contactPhone = 'Số điện thoại không hợp lệ.';
  const names = new Set();
  f.items.forEach((x, i) => {
    const q = Number(x.quantity), start = Number(x.startingUnitPrice), step = Number(x.bidIncrement);
    const key = (k) => `item-${i}-${k}`;
    if (x.name.trim().length < 2) errors[key('name')] = 'Tên tối thiểu 2 ký tự.';
    else if (names.has(x.name.trim().toLowerCase())) errors[key('name')] = 'Tên hạng mục bị trùng.';
    names.add(x.name.trim().toLowerCase());
    if (x.unit === 'kg' ? !(q > 0 && q <= 1_000_000 && Math.round(q * 1000) === q * 1000) : !(Number.isInteger(q) && q >= 1 && q <= 100_000))
      errors[key('quantity')] = x.unit === 'kg' ? 'Khối lượng > 0, tối đa 3 số lẻ.' : 'Số lượng là số nguyên từ 1.';
    if (x.description.trim().length < (x.unit === 'kg' ? 5 : 10)) errors[key('description')] = `${DESCRIPTION_HINT[x.unit]} (tối thiểu ${x.unit === 'kg' ? 5 : 10} ký tự).`;
    if (!Number.isInteger(start) || start <= 0) errors[key('startingUnitPrice')] = 'Số nguyên VND dương.';
    if (!Number.isInteger(step) || step <= 0) errors[key('bidIncrement')] = 'Số nguyên VND dương.';
  });
  const hours = Number(f.collectionDays) * 24;
  if (!Number.isFinite(hours) || hours < 24 || hours > 1440) errors.collectionDays = 'Từ 1 đến 60 ngày.';
  const t = ['surveyStart', 'surveyEnd', 'biddingStart', 'biddingEnd'].map((k) => new Date(f[k]).getTime());
  if (t.some(Number.isNaN)) errors.schedule = 'Điền đủ các mốc thời gian.';
  else if (!(t[0] < t[1] && t[1] <= t[2] && t[2] < t[3])) errors.schedule = 'Thứ tự: khảo sát kết thúc trước khi mở trả giá; đóng sau khi mở.';
  else if (t[3] - t[2] < 60000) errors.schedule = 'Phiên trả giá kéo dài tối thiểu 1 phút.';
  return errors;
}

export default function LiquidationCreate() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  // Mã thao tác giữ nguyên khi gửi lại sau lỗi mạng để server không tạo trùng phiên.
  const operationId = useRef(newOperationId());
  const [form, setForm] = useState({
    title: '', description: '', collectionTerms: '', area: { province: null, ward: null }, street: '', contactPhone: '',
    surveyStart: inHours(0), surveyEnd: inHours(24), biddingStart: inHours(48), biddingEnd: inHours(72), collectionDays: '7',
    items: [emptyItem()],
  });
  const [images, setImages] = useState([]);
  const [uploading, setUploading] = useState(false);
  const [errors, setErrors] = useState({});
  const set = (key) => (e) => setForm((f) => ({ ...f, [key]: e.target.value }));
  const setItem = (i, key) => (e) => setForm((f) => ({ ...f, items: f.items.map((x, j) => (j === i ? { ...x, [key]: e.target.value } : x)) }));

  const createMutation = useMutation({
    mutationFn: (payload) => sellerApi.createLiquidationV2(payload),
    onSuccess: (result) => {
      ['seller-liquidations', 'seller-liquidations-v2', 'seller-notifications-unread', 'seller-notifications-list', 'seller-notifications-dropdown']
        .forEach((key) => queryClient.invalidateQueries({ queryKey: [key] }));
      toast.success('Đã đăng phiên thanh lý nhiều hạng mục.');
      navigate(`/seller/liquidations/v2/${result.campaign.id}`);
    },
    onError: (err) => {
      // Server trả lời dứt khoát thì nội dung có thể được sửa nên cấp mã mới; mất kết nối thì giữ mã cũ.
      if (err?.response) operationId.current = newOperationId();
      toast.error(err?.response?.data?.message || 'Không đăng được phiên. Vui lòng thử lại.');
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
    // Khu vực công khai chỉ gồm phường/xã + tỉnh; số nhà và điện thoại chỉ kho đã đăng ký mới xem.
    const publicArea = `${form.area.ward.name}, ${form.area.province.name}`;
    createMutation.mutate({
      operationId: operationId.current, title: form.title.trim(), description: form.description.trim(),
      collectionTerms: form.collectionTerms.trim(), publicArea, address: `${form.street.trim()}, ${publicArea}`,
      contactPhone: form.contactPhone.trim(), imageUrls: images,
      surveyStart: new Date(form.surveyStart).toISOString(), surveyEnd: new Date(form.surveyEnd).toISOString(),
      biddingStart: new Date(form.biddingStart).toISOString(), biddingEnd: new Date(form.biddingEnd).toISOString(),
      collectionWindowHours: Math.round(Number(form.collectionDays) * 24),
      items: form.items.map((x) => ({ name: x.name.trim(), unit: x.unit, materialCode: x.unit === 'kg' && x.materialCode ? x.materialCode : null,
        quantity: Number(x.quantity), description: x.description.trim(), startingUnitPrice: Number(x.startingUnitPrice), bidIncrement: Number(x.bidIncrement) })),
    });
  };

  const busy = createMutation.isPending || uploading;
  const err = (name) => (errors[name] ? <p role="alert" className="mt-1 text-xs text-red-600">{errors[name]}</p> : null);
  const startingTotal = form.items.reduce((sum, x) => sum + (Number(x.quantity) > 0 && Number(x.startingUnitPrice) > 0 ? Math.round(Number(x.quantity) * Number(x.startingUnitPrice)) : 0), 0);

  return (
    <form onSubmit={submit} noValidate className="mx-auto flex w-full max-w-5xl flex-col gap-6 p-4 md:p-6">
      <div>
        <Link to="/seller/liquidations" className="text-xs font-semibold text-d-primary">← Lô thanh lý của tôi</Link>
        <h1 className="mt-2 text-d-headline-xl-mobile font-d-headline-xl text-d-on-surface md:text-d-headline-xl">Đăng phiên thanh lý nhiều hạng mục</h1>
        <p className="mt-2 text-d-body-md text-d-on-surface-variant">
          Mỗi hạng mục được trả giá theo <strong>đơn giá</strong> và có kho thắng riêng. Tiền mua = đơn giá trúng × số lượng bạn đăng; số lượng và đơn vị không sửa được sau khi đăng. RETRACK không giữ tiền hay cọc — hai bên tự thanh toán và bàn giao.
        </p>
      </div>

      <section className="grid gap-4 rounded-2xl border border-d-border-subtle bg-d-surface-container-lowest p-5">
        <h2 className="text-sm font-bold text-d-on-surface">Thông tin phiên</h2>
        <div><label htmlFor="title" className={label}>Tiêu đề</label><input id="title" className={field} maxLength={255} value={form.title} onChange={set('title')} />{err('title')}</div>
        <div><label htmlFor="description" className={label}>Mô tả chung</label><textarea id="description" rows={3} className={field} maxLength={5000} value={form.description} onChange={set('description')} />{err('description')}</div>
        <div><label htmlFor="collectionTerms" className={label}>Điều kiện thu gom (bốc xếp, giờ nhận, phương tiện…)</label><textarea id="collectionTerms" rows={2} className={field} maxLength={2000} value={form.collectionTerms} onChange={set('collectionTerms')} />{err('collectionTerms')}</div>
        <div>
          <label className={label} htmlFor="images">Ảnh hiện trạng (tối đa 10)</label>
          <input id="images" type="file" accept="image/jpeg,image/png,image/webp,image/heic" multiple disabled={busy} onChange={handleFiles} className="text-sm" />
          {uploading && <p className="mt-1 text-xs text-d-on-surface-variant">Đang tải ảnh...</p>}
          {images.length > 0 && <div className="mt-2 flex flex-wrap gap-2">{images.map((url, i) => <div key={url} className="relative">
            <img src={url} alt={`Ảnh ${i + 1}`} className="size-20 rounded-lg border border-d-border-subtle object-cover" />
            <button type="button" aria-label={`Xóa ảnh ${i + 1}`} onClick={() => setImages((p) => p.filter((u) => u !== url))} className="absolute -right-2 -top-2 size-6 rounded-full bg-red-600 text-xs text-white">×</button>
          </div>)}</div>}
        </div>
      </section>

      <section className="grid gap-4 rounded-2xl border border-d-border-subtle bg-d-surface-container-lowest p-5">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h2 className="text-sm font-bold text-d-on-surface">Hạng mục ({form.items.length}/{MAX_ITEMS})</h2>
          {startingTotal > 0 && <p className="text-xs text-d-on-surface-variant">Tổng giá trị theo giá khởi điểm ≈ <strong>{formatCurrency(startingTotal)}</strong></p>}
        </div>
        {form.items.map((x, i) => (
          <fieldset key={x.key} className="grid gap-3 rounded-xl border border-d-border-subtle bg-white p-4 md:grid-cols-6">
            <legend className="px-1 text-sm font-bold text-d-primary">Hạng mục {i + 1}</legend>
            <div className="md:col-span-3"><label htmlFor={`name-${i}`} className={label}>Tên</label><input id={`name-${i}`} className={field} maxLength={255} value={x.name} onChange={setItem(i, 'name')} placeholder="VD: Sắt vụn, Tivi CRT cũ" />{err(`item-${i}-name`)}</div>
            <div><label htmlFor={`unit-${i}`} className={label}>Đơn vị</label><select id={`unit-${i}`} className={field} value={x.unit} onChange={setItem(i, 'unit')}>{UNITS.map(([v, t]) => <option key={v} value={v}>{t}</option>)}</select></div>
            <div><label htmlFor={`qty-${i}`} className={label}>Số lượng</label><input id={`qty-${i}`} inputMode="decimal" className={field} value={x.quantity} onChange={setItem(i, 'quantity')} />{err(`item-${i}-quantity`)}</div>
            {x.unit === 'kg' ? <div><label htmlFor={`mat-${i}`} className={label}>Loại vật liệu</label><select id={`mat-${i}`} className={field} value={x.materialCode} onChange={setItem(i, 'materialCode')}>{MATERIALS.map(([v, t]) => <option key={v} value={v}>{t}</option>)}</select></div> : <div />}
            <div className="md:col-span-6"><label htmlFor={`desc-${i}`} className={label}>{DESCRIPTION_HINT[x.unit]}</label><textarea id={`desc-${i}`} rows={2} className={field} maxLength={1000} value={x.description} onChange={setItem(i, 'description')} />{err(`item-${i}-description`)}</div>
            <div className="md:col-span-2"><label htmlFor={`start-${i}`} className={label}>Giá khởi điểm (đ/{x.unit === 'cai' ? 'cái' : x.unit === 'bo' ? 'bộ' : 'kg'})</label><input id={`start-${i}`} inputMode="numeric" className={field} value={x.startingUnitPrice} onChange={setItem(i, 'startingUnitPrice')} />{err(`item-${i}-startingUnitPrice`)}</div>
            <div className="md:col-span-2"><label htmlFor={`step-${i}`} className={label}>Bước giá (đ/{x.unit === 'cai' ? 'cái' : x.unit === 'bo' ? 'bộ' : 'kg'})</label><input id={`step-${i}`} inputMode="numeric" className={field} value={x.bidIncrement} onChange={setItem(i, 'bidIncrement')} />{err(`item-${i}-bidIncrement`)}</div>
            <div className="flex items-end justify-between gap-2 md:col-span-2">
              <p className="text-xs text-d-on-surface-variant">{Number(x.quantity) > 0 && Number(x.startingUnitPrice) > 0 ? `≈ ${formatCurrency(Math.round(Number(x.quantity) * Number(x.startingUnitPrice)))} theo giá khởi điểm` : ''}</p>
              {form.items.length > 1 && <button type="button" aria-label={`Xóa hạng mục ${i + 1}`} onClick={() => setForm((f) => ({ ...f, items: f.items.filter((_, j) => j !== i) }))}
                className="inline-flex min-h-11 items-center gap-1.5 rounded-full px-3 text-sm font-semibold text-d-error hover:bg-d-error-container/40"><Trash2 size={15} />Xóa</button>}
            </div>
          </fieldset>
        ))}
        {form.items.length < MAX_ITEMS && <button type="button" onClick={() => setForm((f) => ({ ...f, items: [...f.items, emptyItem()] }))}
          className="inline-flex min-h-11 items-center justify-center gap-2 self-start rounded-full border border-dashed border-d-primary px-5 text-sm font-semibold text-d-primary hover:bg-d-surface-accent"><Plus size={16} />Thêm hạng mục</button>}
      </section>

      <section className="grid gap-4 rounded-2xl border border-d-border-subtle bg-d-surface-container-lowest p-5 md:grid-cols-2">
        <h2 className="text-sm font-bold text-d-on-surface md:col-span-2">Địa điểm</h2>
        <VietnamAreaPicker idPrefix="lot-area" value={form.area} onChange={(area) => setForm((f) => ({ ...f, area, street: GOONG_ENABLED ? '' : f.street }))} errors={errors} fieldClass={field} labelClass={label} />
        {form.area.province && form.area.ward && <p className="text-xs text-d-on-surface-variant md:col-span-2">Khu vực hiển thị công khai: <strong>{form.area.ward.name}, {form.area.province.name}</strong></p>}
        <div><label htmlFor="street" className={label}>Số nhà, tên đường (chỉ kho đã đăng ký mới xem)</label>
          <StreetAddressSelect id="street" className={field} area={form.area} value={form.street} placeholder="VD: 12 Nguyễn Văn Linh" onChange={(street) => setForm((f) => ({ ...f, street }))} />{err('street')}</div>
        <div><label htmlFor="contactPhone" className={label}>Số điện thoại (chỉ kho đã đăng ký mới xem)</label><input id="contactPhone" className={field} maxLength={20} value={form.contactPhone} onChange={set('contactPhone')} />{err('contactPhone')}</div>
      </section>

      <section className="grid gap-4 rounded-2xl border border-d-border-subtle bg-d-surface-container-lowest p-5 md:grid-cols-2">
        <h2 className="text-sm font-bold text-d-on-surface md:col-span-2">Lịch chung của phiên (giờ địa phương)</h2>
        {[['surveyStart', 'Bắt đầu khảo sát'], ['surveyEnd', 'Kết thúc khảo sát / hạn đăng ký'], ['biddingStart', 'Mở trả giá'],
          ['biddingEnd', 'Đóng trả giá (giá hợp lệ trong 2 phút cuối cộng 2 phút, tối đa 5 lần)']].map(([key, text]) => (
          <div key={key}><label htmlFor={key} className={label}>{text}</label><input id={key} type="datetime-local" className={field} value={form[key]} onChange={set(key)} /></div>
        ))}
        <div><label htmlFor="collectionDays" className={label}>Hạn thu gom sau khi đóng phiên (ngày)</label><input id="collectionDays" inputMode="decimal" className={field} value={form.collectionDays} onChange={set('collectionDays')} />{err('collectionDays')}</div>
        <div className="md:col-span-2">{err('schedule')}</div>
      </section>

      <div className="flex justify-end gap-3">
        <Link to="/seller/liquidations" className="rounded-full border border-d-border-subtle px-6 py-3 text-sm font-semibold text-d-on-surface-variant">Hủy</Link>
        <button type="submit" disabled={busy} className="rounded-full bg-d-primary px-6 py-3 text-sm font-semibold text-d-on-primary transition-all active:scale-95 disabled:opacity-50">
          {createMutation.isPending ? 'Đang đăng...' : 'Đăng phiên'}
        </button>
      </div>
    </form>
  );
}
