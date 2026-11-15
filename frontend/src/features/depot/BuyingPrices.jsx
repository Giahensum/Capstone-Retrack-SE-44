import { useState } from 'react';
import { Tags } from 'lucide-react';
import { useDepotQuery, useDepotMutation, money } from './depotApi';
import { Page, Dialog, QueryState, MutationError, GridTable, Pager, cellClass, inputClass, buttonClass } from './components/DepotUI';
import { materialLabel } from './materialLabels';

const materials = ['PET', 'HDPE', 'PVC', 'PAPER', 'CARDBOARD', 'ALUMINUM', 'IRON', 'STEEL', 'COPPER', 'ELECTRONIC_WASTE', 'OTHER'];
function PriceForm({ price, existingMaterials, onClose }) {
  const [form, setForm] = useState({ materialType: price.materialType ?? '', pricePerKg: price.pricePerKg ?? '',
    conditionNote: price.conditionNote ?? '', isActive: price.isActive ?? true });
  const save = useDepotMutation('put', 'buying-prices/' + form.materialType, onClose);
  const update = (field, value) => setForm({ ...form, [field]: value });
  return <Dialog icon={Tags} title={price.materialType ? 'Cập nhật giá thu mua' : 'Công bố giá thu mua'} onClose={onClose} busy={save.isPending}>
    <p className="text-sm text-d-on-surface-variant">Bảng giá thuộc kho đang chọn. Seller sẽ xem giá đang công bố khi chọn kho chỉ định; đơn đã chốt giữ nguyên số tiền.</p>
    <form className="space-y-4" onSubmit={e => { e.preventDefault(); save.mutate({ body: { pricePerKg: Number(form.pricePerKg),
      conditionNote: form.conditionNote, isActive: form.isActive, expectedUpdatedAt: price.updatedAt ?? null } }); }}>
      <fieldset disabled={save.isPending} className="space-y-4">
        <label className="block">Vật liệu<select required disabled={!!price.materialType} className={inputClass} value={form.materialType} onChange={e => update('materialType', e.target.value)}>
          <option value="">Chọn vật liệu</option>{materials.filter(x => x === price.materialType || !existingMaterials.includes(x)).map(x => <option key={x} value={x}>{materialLabel(x)}</option>)}
        </select></label>
        <label className="block">Giá thu mua tham khảo (đ/kg)<input required type="number" min="1" max="1000000000" step="1" className={inputClass} value={form.pricePerKg} onChange={e => update('pricePerKg', e.target.value)} /></label>
        <label className="block">Điều kiện áp dụng<textarea maxLength={500} rows={3} className={inputClass} value={form.conditionNote} onChange={e => update('conditionNote', e.target.value)} placeholder="Ví dụ: PET sạch, không lẫn tạp; giá cuối cùng xác nhận sau cân và phân loại." /></label>
        <label className="flex items-start gap-3"><input type="checkbox" className="mt-1" checked={form.isActive} onChange={e => update('isActive', e.target.checked)} /><span>Công bố cho Seller<span className="block text-sm text-d-on-surface-variant">Tắt để ngừng công bố; dữ liệu và đơn cũ vẫn được giữ. Kho cần được duyệt trước khi xuất hiện trong danh sách chỉ định.</span></span></label>
        <p className="text-sm text-d-on-surface-variant">Đây là giá tham khảo do kho khai báo; giá giao dịch được chốt theo hàng thực tế và Seller xác nhận. Thay giá không tự đổi đơn đang xử lý.</p>
      </fieldset>
      <MutationError mutation={save} />
      <div className="flex flex-wrap justify-end gap-3"><button type="button" className="min-h-11 rounded-full border border-d-border-subtle px-5 py-2" disabled={save.isPending} onClick={onClose}>Hủy bỏ</button><button className={buttonClass} disabled={save.isPending}>{save.isPending ? 'Đang lưu…' : 'Lưu bảng giá'}</button></div>
    </form>
  </Dialog>;
}
export default function BuyingPrices() {
  const [page, setPage] = useState(1);
  const [editing, setEditing] = useState(null);
  const query = useDepotQuery('buying-prices', { page }, true, 30000);
  return <Page title="Giá thu mua của kho" description="Công bố giá riêng theo vật liệu để người bán cân nhắc khi chọn kho chỉ định.">
    <div className="flex flex-wrap items-center justify-between gap-3"><p className="text-sm text-d-on-surface-variant">Giá đ/kg; chỉ giá đang công bố được gửi cho Seller. Giờ cập nhật theo Việt Nam.</p><div className="flex gap-2"><button type="button" className="min-h-11 rounded-full border border-d-border-subtle px-4" onClick={() => query.refetch()}>Tải lại</button><button type="button" className={buttonClass} onClick={() => setEditing({})}>Công bố giá thu mua</button></div></div>
    <QueryState query={query}><GridTable headers={['Vật liệu', 'Giá tham khảo', 'Điều kiện', 'Cập nhật', 'Công bố', 'Thao tác']} empty={!query.data?.items.length}>
      {query.data?.items.map(x => <tr key={x.materialType}><td className={cellClass}>{materialLabel(x.materialType)}</td><td className={cellClass}><strong>{money(x.pricePerKg)} / kg</strong></td><td className={cellClass}><p className="max-w-sm whitespace-pre-wrap break-words">{x.conditionNote || 'Chốt theo hàng thực tế'}</p></td><td className={cellClass}>{new Date(x.updatedAt).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' })}</td><td className={cellClass}><span className={`rounded-full px-3 py-1 text-sm ${x.isActive ? 'bg-d-primary/10 text-d-primary' : 'bg-d-surface-container text-d-on-surface-variant'}`}>{x.isActive ? 'Đang công bố' : 'Đã ẩn'}</span></td><td className={cellClass}><button type="button" className={buttonClass} onClick={() => setEditing(x)}>Cập nhật</button></td></tr>)}
    </GridTable><Pager page={page} setPage={setPage} total={query.data?.totalCount} /></QueryState>
    {editing && <PriceForm price={editing} existingMaterials={query.data?.items.map(x => x.materialType) ?? []} onClose={() => { setEditing(null); query.refetch(); }} />}
  </Page>;
}
