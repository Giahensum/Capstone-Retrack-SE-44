import { useState } from 'react';
import { useDepotMutation, useDepotQuery, money, number, date } from './depotApi';
import { QueryState, GridTable, Pager, inputClass, buttonClass, cellClass, Dialog, MutationError } from './components/DepotUI';

export default function ReusableInventory() {
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [view, setView] = useState('stock');
  const [selected, setSelected] = useState(null);
  const stock = useDepotQuery('stock/reusable', { page, search }, view === 'stock');
  const sales = useDepotQuery('stock/sales', { page }, view === 'sales');
  return <section className="space-y-4">
    <div className="flex flex-wrap items-center justify-between gap-3"><p className="text-sm text-d-on-surface-variant">Tivi, điện lạnh, nội thất… đã nhận và duyệt nhập. Ghi bán ngoài app sau khi giao hàng thực tế.</p><div className="flex gap-2">{[['stock', 'Hàng còn trong kho'], ['sales', 'Lịch sử bán ngoài']].map(([key, label]) => <button type="button" key={key} aria-pressed={view === key} className={view === key ? buttonClass : 'rounded-full border border-d-border-subtle px-4 py-2.5'} onClick={() => { setView(key); setPage(1); }}>{label}</button>)}</div></div>
    {view === 'stock' ? <>
      <label className="block max-w-lg">Tìm theo tên đồ<input type="search" maxLength={255} className={inputClass} value={search} onChange={e => { setSearch(e.target.value); setPage(1); }} /></label>
      <QueryState query={stock}><GridTable headers={['Món đồ / tình trạng', 'Nguồn', 'Đã nhận', 'Đã bán', 'Khả dụng', 'Giá vốn / đơn vị', 'Thao tác']} empty={!stock.data?.items.length}>
        {stock.data?.items.map(x => <tr key={x.id}><td className={cellClass}><p className="font-semibold">{x.name}</p><p className="text-sm text-d-on-surface-variant">{x.conditionNote || 'Chưa kiểm tra / chưa ghi tình trạng'}</p></td><td className={cellClass}>{x.source}</td><td className={cellClass}>{number(x.receivedQuantity)} {x.unit}</td><td className={cellClass}>{number(x.soldQuantity)}</td><td className={cellClass}>{number(x.availableQuantity)} {x.unit}</td><td className={cellClass}>{x.unitCost == null ? 'Chưa xác định' : money(x.unitCost)}</td><td className={cellClass}><button type="button" className={buttonClass} disabled={x.availableQuantity <= 0} onClick={() => setSelected(x)}>Ghi bán ngoài</button></td></tr>)}
      </GridTable><Pager page={page} setPage={setPage} total={stock.data?.totalCount} /></QueryState>
    </> : <QueryState query={sales}><GridTable headers={['Ngày ghi nhận', 'Món đồ', 'Lượng bán', 'Doanh thu', 'Giá vốn phần bán', 'Chi phí bán', 'Chênh lệch trước thuế', 'Ghi chú']} empty={!sales.data?.items.length}>
      {sales.data?.items.map(x => <tr key={x.id}><td className={cellClass}>{date(x.createdAt)}</td><td className={cellClass}>{x.name}</td><td className={cellClass}>{number(x.quantity)}</td><td className={cellClass}>{money(x.revenue)}</td><td className={cellClass}>{x.costOfGoods == null ? 'Chưa xác định' : money(x.costOfGoods)}{x.hasReconciliation && <p className="text-xs text-d-primary mt-1">Sau đối soát: {x.adjustedCostOfGoods == null ? 'Chưa xác định' : money(x.adjustedCostOfGoods)}</p>}</td><td className={cellClass}>{money(x.sellingCost)}</td><td className={cellClass}>{x.pretaxDifference == null ? 'Chưa đủ giá vốn' : money(x.pretaxDifference)}</td><td className={cellClass}>{x.note}</td></tr>)}
    </GridTable><Pager page={page} setPage={setPage} total={sales.data?.totalCount} /></QueryState>}
    <p className="text-sm text-d-on-surface-variant">Giá vốn gốc tại lúc ghi bán được giữ nguyên. Chênh lệch dùng vốn sau các bút toán đối soát chi phí nguồn. Chưa gồm thuế thu nhập và chi phí chưa ghi, chưa xác nhận tiền về ngân hàng. Hàng chưa bán không tính doanh thu.</p>
    {selected && <ReuseSaleForm item={selected} onClose={() => setSelected(null)} />}
  </section>;
}
function ReuseSaleForm({ item, onClose }) {
  const [form, setForm] = useState({ operationId: crypto.randomUUID(), quantity: '1', unitPrice: '', sellingCost: '', note: '' });
  const save = useDepotMutation('post', `stock/reusable/${item.id}/sales`, onClose);
  const field = (key, value) => setForm({ ...form, [key]: value });
  const difference = item.unitCost == null ? null : Number(form.quantity) * (Number(form.unitPrice) - item.unitCost) - Number(form.sellingCost || 0);
  return <Dialog title={`Ghi bán ngoài: ${item.name}`} busy={save.isPending} onClose={onClose}>
    <form className="space-y-4" onSubmit={e => { e.preventDefault(); save.mutate({ body: { ...form, quantity: Number(form.quantity), unitPrice: Number(form.unitPrice), sellingCost: Number(form.sellingCost || 0) } }); }}>
      <p className="text-sm">Còn {number(item.availableQuantity)} {item.unit}. Thao tác xác nhận đã giao sẽ giảm tồn, không phải tạo yêu cầu hỏi mua.</p>
      <fieldset disabled={save.isPending} className="space-y-4"><label className="block">Lượng đã giao ({item.unit})<input required type="number" min="1" max={item.availableQuantity} step="1" className={inputClass} value={form.quantity} onChange={e => field('quantity', e.target.value)} /></label>
        <label className="block">Giá bán / {item.unit} (đ)<input required type="number" min="0" max="1000000000000" step="1" className={inputClass} value={form.unitPrice} onChange={e => field('unitPrice', e.target.value)} /></label>
        <label className="block">Chi phí bán và khoản liên quan đã xác định (đ)<input type="number" min="0" max="1000000000000" step="1" className={inputClass} value={form.sellingCost} onChange={e => field('sellingCost', e.target.value)} /></label>
        <label className="block">Ghi chú giao nhận / chứng từ<input required maxLength={1000} className={inputClass} value={form.note} onChange={e => field('note', e.target.value)} /></label>
      </fieldset>
      <p className="rounded-xl bg-d-surface-accent p-3">Chênh lệch dự kiến trước thuế thu nhập: {difference == null ? 'Chưa xác định giá vốn' : money(difference)}</p>
      <MutationError mutation={save} /><div className="flex justify-end gap-3"><button type="button" disabled={save.isPending} className="rounded-full border border-d-border-subtle px-4 py-2.5" onClick={onClose}>Hủy bỏ</button><button className={buttonClass} disabled={save.isPending}>{save.isPending ? 'Đang ghi…' : 'Xác nhận đã giao và ghi bán'}</button></div>
    </form>
  </Dialog>;
}
