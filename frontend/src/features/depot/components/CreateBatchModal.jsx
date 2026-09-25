import { useState } from 'react';
import { useDepotQuery, useDepotMutation, number } from '../depotApi';
import { Dialog, QueryState, MutationError, inputClass, buttonClass, Pager } from './DepotUI';

export default function CreateBatchModal({ isOpen, onClose }) {
  return isOpen ? <CreateBatchForm onClose={onClose} /> : null;
}
function CreateBatchForm({ onClose }) {
  const [form, setForm] = useState({ operationId: crypto.randomUUID(), materialType: '', weightKg: '', description: '', targetFactoryId: '' });
  const [strategy, setStrategy] = useState('public');
  const [factorySearch, setFactorySearch] = useState('');
  const [page, setPage] = useState(1);
  const stock = useDepotQuery('inventory');
  const factories = useDepotQuery('partners', { page, search: factorySearch }, strategy === 'direct');
  const save = useDepotMutation('post', 'batches', onClose);
  const available = stock.data?.find((i) => i.materialType === form.materialType)?.availableKg ?? 0;
  const field = (key, value) => setForm({ ...form, [key]: value });
  return <Dialog title="Tạo Lô Xuất Hàng" onClose={onClose} busy={save.isPending}>
    <form className="space-y-5" onSubmit={(e) => { e.preventDefault(); save.mutate({ body: { ...form, weightKg: Number(form.weightKg), targetFactoryId: strategy === 'direct' ? form.targetFactoryId : null } }); }}>
      <QueryState query={stock}><label className="block">Loại phế liệu<select required className={inputClass} value={form.materialType} onChange={(e) => field('materialType', e.target.value)}>
        <option value="">Chọn loại phế liệu</option>{stock.data?.filter((i) => i.availableKg > 0).map((i) => <option key={i.materialType}>{i.materialType}</option>)}</select></label>
        <p className="text-d-primary mt-2">Tồn kho khả dụng: {number(available)} kg</p></QueryState>
      <label className="block">Khối lượng (kg)<input required type="number" step="any" min="0.000001" max={available} value={form.weightKg} onChange={(e) => field('weightKg', e.target.value)} className={inputClass} /></label>
      <label className="block">Hình thức bán<select value={strategy} onChange={(e) => setStrategy(e.target.value)} className={inputClass}><option value="public">Đăng công khai</option><option value="direct">Chỉ định nhà máy</option></select></label>
      {strategy === 'direct' && <><label className="block">Tìm nhà máy<input className={inputClass} value={factorySearch} onChange={(e) => { setFactorySearch(e.target.value); setPage(1); }} /></label>
        <QueryState query={factories}><label className="block">Nhà máy<select required value={form.targetFactoryId} onChange={(e) => field('targetFactoryId', e.target.value)} className={inputClass}>
          <option value="">Chọn nhà máy</option>{factories.data?.items.map((f) => <option key={f.id} value={f.id} disabled={f.partnershipStatus === 'BLOCKED'}>{f.name}{f.partnershipStatus === 'BLOCKED' ? ' — Đã chặn' : ''}</option>)}</select></label>
          <Pager page={page} setPage={setPage} total={factories.data?.totalCount} /></QueryState>
        <p className="text-sm">Đối tác mới cần nhà máy duyệt trước khi vận chuyển.</p></>}
      <label className="block">Mô tả / ghi chú<textarea className={inputClass} value={form.description} onChange={(e) => field('description', e.target.value)} /></label>
      <p className="text-sm text-d-on-surface-variant">Giá được thỏa thuận sau QC tại nhà máy. Khối lượng tạo lô sẽ được giữ khỏi tồn khả dụng.</p>
      <MutationError mutation={save} /><div className="flex justify-end gap-3"><button type="button" className={buttonClass} onClick={onClose} disabled={save.isPending}>Hủy bỏ</button>
        <button className={buttonClass} disabled={save.isPending || stock.isPending || stock.isError}>{save.isPending ? 'Đang tạo…' : 'Tạo lô xuất hàng'}</button></div>
    </form>
  </Dialog>;
}
