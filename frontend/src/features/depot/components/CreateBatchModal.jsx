import { materialLabel } from '../materialLabels';
import { useEffect, useState } from 'react';
import { useDepotQuery, useDepotMutation, number } from '../depotApi';
import { Dialog, QueryState, MutationError, inputClass, buttonClass, Pager } from './DepotUI';

export default function CreateBatchModal({ isOpen, onClose, initialMaterial = '', initialFactoryId = '' }) {
  return isOpen ? <CreateBatchForm onClose={onClose} initialMaterial={initialMaterial} initialFactoryId={initialFactoryId} /> : null;
}
function CreateBatchForm({ onClose, initialMaterial, initialFactoryId }) {
  const [form, setForm] = useState({ operationId: crypto.randomUUID(), materialType: initialMaterial, weightKg: '', description: '', targetFactoryId: initialFactoryId });
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
  const factories = useDepotQuery('partners', { page, search: factorySearch }, strategy === 'direct');
  const save = useDepotMutation('post', 'batches', onClose);
  const available = stock.data?.find((i) => i.materialType === form.materialType)?.availableKg ?? 0;
  const field = (key, value) => setForm({ ...form, [key]: value });
  return <Dialog title="Tạo Lô Xuất Hàng" onClose={onClose} busy={save.isPending}>
    <form className="space-y-5" onSubmit={(e) => {
      e.preventDefault();
      const body = { ...form, weightKg: Number(form.weightKg), targetFactoryId: strategy === 'direct' ? form.targetFactoryId : null };
      if (photos.length === 0) return save.mutate({ body });
      const data = new FormData();
      Object.entries(body).forEach(([key, value]) => { if (value !== null && value !== '') data.append(key, value); });
      photos.forEach((file) => data.append('images', file));
      save.mutate({ body: data });
    }}>
      <QueryState query={stock}><label className="block">Loại phế liệu<select required className={inputClass} value={form.materialType} onChange={(e) => field('materialType', e.target.value)}>
        <option value="">Chọn loại phế liệu</option>{stock.data?.filter((i) => i.availableKg > 0).map((i) => <option key={i.materialType} value={i.materialType}>{materialLabel(i.materialType)}</option>)}</select></label>
        <p className="text-d-primary mt-2">Tồn kho khả dụng: {number(available)} kg</p></QueryState>
      <label className="block">Khối lượng (kg)<input required type="number" step="any" min="0.000001" max={available} value={form.weightKg} onChange={(e) => field('weightKg', e.target.value)} className={inputClass} /></label>
      <label className="block">Hình thức bán<select value={strategy} onChange={(e) => setStrategy(e.target.value)} className={inputClass}><option value="public">Đăng công khai</option><option value="direct">Chỉ định nhà máy</option></select></label>
      {strategy === 'direct' && <><label className="block">Tìm nhà máy<input className={inputClass} value={factorySearch} onChange={(e) => { setFactorySearch(e.target.value); setPage(1); }} /></label>
        <QueryState query={factories}><label className="block">Nhà máy<select required value={form.targetFactoryId} onChange={(e) => field('targetFactoryId', e.target.value)} className={inputClass}>
          <option value="">Chọn nhà máy</option>
          {initialFactoryId && !factories.data?.items.some((f) => f.id === initialFactoryId) && <option value={initialFactoryId}>Nhà máy đã chọn từ danh sách</option>}
          {factories.data?.items.map((f) => <option key={f.id} value={f.id} disabled={f.partnershipStatus === 'BLOCKED'}>{f.name}{f.partnershipStatus === 'BLOCKED' ? ' — Đã chặn' : ''}</option>)}</select></label>
          <Pager page={page} setPage={setPage} total={factories.data?.totalCount} /></QueryState>
        <p className="text-sm">Chưa hợp tác: chờ nhà máy nhận lô trước khi vận chuyển. Đã hợp tác: lô được gửi thẳng. Nhà máy quyết định hợp tác lâu dài sau khi kiểm tra hàng.</p></>}
      <label className="block">Mô tả / ghi chú<textarea className={inputClass} value={form.description} onChange={(e) => field('description', e.target.value)} /></label>
      <div><label htmlFor="batch-material-photos" className="block font-medium">Ảnh vật liệu của lô (tối đa 5 ảnh)</label>
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
      <p className="text-sm text-d-on-surface-variant">Giá được thỏa thuận sau QC tại nhà máy. Khối lượng tạo lô sẽ được giữ khỏi tồn khả dụng.</p>
      <MutationError mutation={save} /><div className="flex justify-end gap-3"><button type="button" className={buttonClass} onClick={onClose} disabled={save.isPending}>Hủy bỏ</button>
        <button className={buttonClass} disabled={save.isPending || stock.isPending || stock.isError || !!photoError}>{save.isPending ? 'Đang tạo…' : 'Tạo lô xuất hàng'}</button></div>
    </form>
  </Dialog>;
}
