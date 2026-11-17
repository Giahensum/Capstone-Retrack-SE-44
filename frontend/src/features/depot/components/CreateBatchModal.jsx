import { materialLabel } from '../materialLabels';
import { useEffect, useId, useState } from 'react';
import { useDepotQuery, useDepotMutation, number, money, date } from '../depotApi';
import { Dialog, QueryState, MutationError, inputClass, buttonClass, Pager } from './DepotUI';

export default function CreateBatchModal({ isOpen, onClose, initialMaterial = '', initialFactoryId = '', initialDemand = null }) {
  return isOpen ? <CreateBatchForm onClose={onClose} initialMaterial={initialMaterial} initialFactoryId={initialFactoryId} initialDemand={initialDemand} /> : null;
}
function CreateBatchForm({ onClose, initialMaterial, initialFactoryId, initialDemand }) {
  const formId = useId();
  const [form, setForm] = useState({ operationId: crypto.randomUUID(), materialType: initialMaterial, weightKg: '', description: '', targetFactoryId: initialFactoryId, sourceDemandId: initialDemand?.id ?? null });
  const [strategy, setStrategy] = useState(initialFactoryId ? 'direct' : 'public');
  const [factorySearch, setFactorySearch] = useState('');
  const [page, setPage] = useState(1);
  const [photos, setPhotos] = useState([]);
  const [photoError, setPhotoError] = useState('');
  const [previews, setPreviews] = useState([]);
  useEffect(() => {
    const urls = photos.map((file) => URL.createObjectURL(file));
    setPreviews(urls);
    return () => urls.forEach((url) => URL.revokeObjectURL(url));
  }, [photos]);
  const stock = useDepotQuery('inventory');
  const factories = useDepotQuery('partners', { page, search: factorySearch, materialType: form.materialType, factoryId: initialDemand?.factoryId }, strategy === 'direct' && !!form.materialType);
  const save = useDepotMutation('post', (_id, body) => body instanceof FormData ? 'batches/with-images' : 'batches', onClose);
  const available = stock.data?.find((i) => i.materialType === form.materialType)?.availableKg ?? 0;
  const listedFactory = factories.data?.items.find((factory) => factory.id === form.targetFactoryId);
  const needsSelectedLookup = strategy === 'direct' && !!form.targetFactoryId && !!form.materialType && !listedFactory;
  const selectedLookup = useDepotQuery('partners', { factoryId: form.targetFactoryId, materialType: form.materialType }, needsSelectedLookup && !factories.isPending);
  const selectedFactory = listedFactory ?? selectedLookup.data?.items[0];
  const selectedFactoryBlocked = strategy === 'direct' && selectedFactory?.partnershipStatus === 'BLOCKED';
  const selectedFactoryUnavailable = strategy === 'direct' && !selectedFactory;
  const maximumWeight = initialDemand ? Math.min(available, initialDemand.requiredWeightKg) : available;
  const field = (key, value) => setForm({ ...form, [key]: value });
  return <Dialog title="Tạo Lô Xuất Hàng" onClose={onClose} busy={save.isPending} wide footer={<div className="flex flex-wrap justify-end gap-3">
    <button type="button" className={buttonClass} onClick={onClose} disabled={save.isPending}>Hủy bỏ</button>
    <button type="submit" form={formId} className={buttonClass} disabled={save.isPending || stock.isPending || stock.isError || !!photoError || available <= 0 || selectedFactoryBlocked || selectedFactoryUnavailable || strategy === 'direct' && (factories.isPending || factories.isError)}>{save.isPending ? 'Đang tạo…' : 'Tạo lô xuất hàng'}</button>
  </div>}>
    <form id={formId} className="grid grid-cols-1 sm:grid-cols-2 gap-x-5 gap-y-4" onSubmit={(e) => {
      e.preventDefault();
      const body = { ...form, weightKg: Number(form.weightKg), targetFactoryId: strategy === 'direct' ? form.targetFactoryId : null };
      if (photos.length === 0) return save.mutate({ body });
      const data = new FormData();
      Object.entries(body).forEach(([key, value]) => { if (value !== null && value !== '') data.append(key, value); });
      photos.forEach((file) => data.append('images', file));
      save.mutate({ body: data });
    }}>
      {initialDemand && <section className="sm:col-span-2 rounded-2xl border border-d-border-subtle bg-white p-4 space-y-2" aria-label="Nhu cầu thu mua đang chọn"><p className="font-semibold">Theo nhu cầu của {initialDemand.factoryName}</p><p className="text-sm">{materialLabel(initialDemand.materialType)} · tối đa {number(initialDemand.requiredWeightKg)} kg/lô · hạn {date(initialDemand.deadline)}</p><p className="text-sm">Giá tham khảo: {initialDemand.minPricePerKg == null ? 'Thỏa thuận' : money(initialDemand.minPricePerKg)} — {initialDemand.maxPricePerKg == null ? 'Thỏa thuận' : money(initialDemand.maxPricePerKg)} / kg. Giá cuối cùng cần hai bên đồng ý sau KCS.</p><p className="text-xs text-d-on-surface-variant">Đây là đề nghị theo nhu cầu, chưa phải nhà máy đã nhận mua. Đóng popup để tạo lô vật liệu hoặc nhà máy khác.</p></section>}
      <div><QueryState query={stock}><label className="block">Loại phế liệu<select required disabled={!!initialDemand} className={inputClass} value={form.materialType} onChange={(e) => { setForm({ ...form, materialType: e.target.value, targetFactoryId: form.targetFactoryId || initialFactoryId || '' }); setPage(1); }}>
        <option value="">Chọn loại phế liệu</option>
        {form.materialType && !stock.data?.some((i) => i.materialType === form.materialType && i.availableKg > 0) &&
          <option value={form.materialType} disabled>{materialLabel(form.materialType)} — hết tồn</option>}
        {stock.data?.filter((i) => i.availableKg > 0).map((i) => <option key={i.materialType} value={i.materialType}>{materialLabel(i.materialType)}</option>)}</select></label>
        <p className="text-d-primary mt-2">Tồn kho khả dụng: {number(available)} kg</p>
        {form.materialType && available <= 0 && <p role="status" className="mt-1 text-sm text-d-error">Kho hiện không có tồn khả dụng cho loại này.</p>}</QueryState></div>
      <label className="block">Khối lượng (kg)<input required type="number" step="0.001" min="0.001" max={maximumWeight} value={form.weightKg} onChange={(e) => field('weightKg', e.target.value)} className={inputClass} /></label>
      <label className="block">Hình thức bán<select disabled={!!initialDemand} value={strategy} onChange={(e) => setStrategy(e.target.value)} className={inputClass}><option value="public">Đăng công khai</option><option value="direct">Chỉ định nhà máy</option></select></label>
      {strategy === 'direct' && <div className="sm:col-span-2 grid grid-cols-1 sm:grid-cols-2 gap-4">{!initialDemand && <label className="block">Tìm nhà máy<input className={inputClass} value={factorySearch} onChange={(e) => { setFactorySearch(e.target.value); setPage(1); }} /></label>}
        <div><QueryState query={factories}><label className="block">Nhà máy<select required disabled={!!initialDemand} value={form.targetFactoryId} onChange={(e) => field('targetFactoryId', e.target.value)} className={inputClass}>
          <option value="">Chọn nhà máy</option>
          {selectedFactory && !listedFactory && <option value={selectedFactory.id} disabled={selectedFactory.partnershipStatus === 'BLOCKED'}>{selectedFactory.name}{selectedFactory.partnershipStatus === 'BLOCKED' ? ' — Đã chặn' : ''}</option>}
          {form.targetFactoryId && !selectedFactory && <option value={form.targetFactoryId} disabled>{needsSelectedLookup && selectedLookup.isPending ? 'Đang kiểm tra nhà máy đã chọn…' : 'Nhà máy đã chọn không nhận vật liệu này hoặc không còn khả dụng'}</option>}
          {factories.data?.items.map((f) => <option key={f.id} value={f.id} disabled={f.partnershipStatus === 'BLOCKED'}>{f.name}{f.distanceKm == null ? '' : ` — ~${number(Math.round(f.distanceKm))} km`}{f.partnershipStatus === 'BLOCKED' ? ' — Đã chặn' : ''}</option>)}</select></label>
          {!initialDemand && <div className="mt-3"><Pager page={page} setPage={setPage} total={factories.data?.totalCount} /></div>}
          <p className="mt-1 text-xs text-d-on-surface-variant">Khoảng cách đường chim bay, chỉ để tham khảo.</p>
          {selectedFactoryBlocked && <p role="status" className="mt-2 text-sm text-d-error">Quan hệ với nhà máy này đang bị chặn; chưa thể tạo lô.</p>}</QueryState></div>
        <p className="text-sm sm:col-span-2">{selectedFactory?.canDirectDelivery ? 'Hợp tác còn hiệu lực: lô được giao thẳng, không phải chờ duyệt nhận lô lại.' : 'Chưa hợp tác hoặc hợp đồng hết hạn/chưa hiệu lực: chờ nhà máy nhận lô trước khi vận chuyển. Có thể đề nghị ký hoặc gia hạn hợp đồng sau KCS.'}</p></div>}
      <label className="block sm:col-span-2">Mô tả / ghi chú<textarea className={inputClass} value={form.description} onChange={(e) => field('description', e.target.value)} /></label>
      <div className="sm:col-span-2"><label htmlFor="batch-material-photos" className="block font-medium">Ảnh vật liệu của lô (tối đa 5 ảnh)</label>
        <p className="text-sm text-d-on-surface-variant">PNG, JPEG hoặc WebP; tối đa 10 MB mỗi ảnh. Nhà máy sẽ xem ảnh trước khi nhận lô.</p>
        <input id="batch-material-photos" type="file" accept="image/png,image/jpeg,image/webp" multiple className={inputClass}
          onChange={(e) => {
            const files = Array.from(e.target.files ?? []);
            const error = files.length > 5 ? 'Chỉ chọn tối đa 5 ảnh.' : files.some((file) => file.size === 0 || file.size > 10 * 1024 * 1024)
              ? 'Mỗi ảnh phải có dung lượng từ 1 byte đến 10 MB.' : files.some((file) => !['image/png', 'image/jpeg', 'image/webp'].includes(file.type))
                ? 'Chỉ chọn ảnh PNG, JPEG hoặc WebP.' : '';
            setPhotoError(error);
            setPhotos(error ? [] : files);
          }} />
        {photoError && <p role="alert" className="text-d-error mt-2">{photoError}</p>}
        {previews.length > 0 && <div className="mt-3 grid grid-cols-3 sm:grid-cols-5 gap-2">{previews.map((url, index) =>
          <img key={url} src={url} alt={`Ảnh vật liệu ${index + 1}: ${photos[index].name}`} className="h-20 w-full rounded-lg object-cover border border-d-border-subtle" />)}</div>}
      </div>
      <p className="text-sm text-d-on-surface-variant sm:col-span-2">Giá được thỏa thuận sau QC tại nhà máy. Khối lượng tạo lô sẽ được giữ khỏi tồn khả dụng.</p>
      <div className="sm:col-span-2"><MutationError mutation={save} /></div>
    </form>
  </Dialog>;
}
