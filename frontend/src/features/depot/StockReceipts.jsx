import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useDepotQuery, date, money } from './depotApi';
import { useDepot } from './DepotContext';
import { QueryState, GridTable, Pager, inputClass, buttonClass, cellClass } from './components/DepotUI';
import StockReceiptForm from './components/StockReceiptForm';
import StockReceiptReview from './components/StockReceiptReview';

const labels = { DRAFT: 'Nháp', SUBMITTED: 'Chờ chủ kho duyệt', POSTED: 'Đã ghi sổ', REJECTED: 'Đã từ chối', VOIDED: 'Đã đảo phiếu' };
const sources = { EXTERNAL_PURCHASE: 'Mua ngoài', OPENING_BALANCE: 'Tồn đầu kỳ', BULK_PURCHASE: 'Mua lô thanh lý' };
export default function StockReceipts() {
  const { depotId } = useDepot();
  const [params, setParams] = useSearchParams();
  const receiptId = params.get('receiptId');
  const linked = useDepotQuery(`stock/receipts/${receiptId}`, {}, !!receiptId);
  const clearLink = () => setParams(previous => { previous.delete('receiptId'); return previous; }, { replace: true });
  const [creating, setCreating] = useState(false);
  const [selected, setSelected] = useState(null);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('');
  const query = useDepotQuery('stock/receipts', { page, search, status });
  return <section className="space-y-4">
    <div className="flex flex-wrap justify-between items-center gap-3"><p className="text-sm text-d-on-surface-variant">Bổ sung hàng mua ngoài và tồn đầu kỳ. Không nhập lại đơn thu gom DONE.</p><button type="button" className={buttonClass} onClick={() => setCreating(true)}>Lập phiếu nhập</button></div>
    <div className="grid sm:grid-cols-2 gap-4"><label>Tìm theo tham chiếu nguồn<input type="search" maxLength={255} className={inputClass} value={search} onChange={e => { setSearch(e.target.value); setPage(1); }} /></label><label>Trạng thái phiếu<select className={inputClass} value={status} onChange={e => { setStatus(e.target.value); setPage(1); }}><option value="">Tất cả trạng thái</option>{Object.entries(labels).map(([key, value]) => <option key={key} value={key}>{value}</option>)}</select></label></div>
    <QueryState query={query}><GridTable headers={['Nguồn nhập', 'Ngày nhận', 'Dòng hàng', 'Chi phí phiếu gốc', 'Trạng thái', 'Thao tác']} empty={!query.data?.items.length}>
      {query.data?.items.map(x => <tr key={x.id}><td className={cellClass}><p className="font-medium">{x.reference}</p><p className="text-sm text-d-on-surface-variant">{sources[x.sourceType]} · Kho tự khai</p></td><td className={cellClass}>{date(x.receivedAt)}</td><td className={cellClass}>{x.lines.length}</td><td className={cellClass}>{money(x.totalCost)}<p className="text-sm">{x.costsConfirmed ? 'Đã phân bổ' : 'Giá vốn chưa xác định'}</p></td><td className={cellClass}><span className="inline-block rounded-full bg-d-surface-accent px-3 py-1 text-sm whitespace-nowrap">{labels[x.status] ?? x.status}</span></td><td className={cellClass}><button type="button" className={buttonClass} onClick={() => setSelected(x)}>Chi tiết / duyệt</button></td></tr>)}
    </GridTable><Pager page={page} setPage={setPage} total={query.data?.totalCount} /></QueryState>
    {creating && <StockReceiptForm key={`create-${depotId}`} onClose={() => setCreating(false)} />}
    {receiptId && !linked.isSuccess && <QueryState query={linked} />}
    {(selected || linked.data) && <StockReceiptReview key={`${depotId}-${(selected || linked.data).id}`} receipt={selected || linked.data} onClose={() => { setSelected(null); clearLink(); }} />}
  </section>;
}
