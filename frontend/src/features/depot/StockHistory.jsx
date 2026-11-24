import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useDepotQuery, date, number } from './depotApi';
import { materialLabel } from './materialLabels';
import { QueryState, GridTable, Pager, inputClass, buttonClass, cellClass } from './components/DepotUI';
import StockReceiptForm from './components/StockReceiptForm';
import StockReceiptReview from './components/StockReceiptReview';
import PaymentDrawer from './components/PaymentDrawer';

const sources = { PICKUP: 'Thu gom Seller', EXTERNAL_PURCHASE: 'Mua ngoài', OPENING_BALANCE: 'Tồn đầu kỳ', BULK_PURCHASE: 'Lô thanh lý' };
const states = { DONE: 'Đã nhập từ thu gom', DRAFT: 'Phiếu nháp · chưa tăng tồn', SUBMITTED: 'Chờ chủ kho duyệt', POSTED: 'Đã ghi kho', REJECTED: 'Đã từ chối', VOIDED: 'Đã đảo phiếu' };
export default function StockHistory({ initialSource = '' }) {
  const [params, setParams] = useSearchParams();
  const [page, setPage] = useState(1), [search, setSearch] = useState(''), [source, setSource] = useState(initialSource), [status, setStatus] = useState('');
  const [pickup, setPickup] = useState(null);
  const id = params.get('receiptId');
  const detail = useDepotQuery(`stock/receipts/${id}`, {}, !!id);
  const query = useDepotQuery('inventory/history', { page, search, source, status });
  const changeParams = patch => setParams(previous => {
    const next = new URLSearchParams(previous);
    Object.entries(patch).forEach(([key, value]) => value == null ? next.delete(key) : next.set(key, value));
    return next;
  });
  const close = () => changeParams({ receiptId: null, newReceipt: null });
  return <section className="space-y-4" aria-label="Lịch sử nhập hàng">
    <div className="flex flex-wrap justify-between items-center gap-3"><p className="text-sm text-d-on-surface-variant">Một nơi xem hàng thu gom, mua ngoài, tồn đầu kỳ và lô trúng. Phiếu nháp chưa cộng tồn; không nhập lại đơn thu gom đã hoàn tất.</p><button type="button" className={buttonClass} onClick={() => changeParams({ newReceipt: '1', receiptId: null })}>Lập phiếu nhập</button></div>
    <div className="grid sm:grid-cols-3 gap-3">
      <label>Tìm người bán / tham chiếu / mã<input type="search" maxLength={255} className={inputClass} value={search} onChange={e => { setSearch(e.target.value); setPage(1); }} /></label>
      <label>Nguồn nhập<select className={inputClass} value={source} onChange={e => { setSource(e.target.value); setPage(1); }}><option value="">Tất cả nguồn</option><option value="RECEIPT">Các phiếu nhập kho</option>{Object.entries(sources).map(([key, label]) => <option key={key} value={key}>{label}</option>)}</select></label>
      <label>Trạng thái nhập<select className={inputClass} value={status} onChange={e => { setStatus(e.target.value); setPage(1); }}><option value="">Tất cả trạng thái</option>{Object.entries(states).map(([key, label]) => <option key={key} value={key}>{label}</option>)}</select></label>
    </div>
    <QueryState query={query}><GridTable headers={['Nguồn / tham chiếu', 'Ngày nhận đã ghi', 'Hàng', 'Trạng thái', 'Thao tác']} empty={!query.data?.items.length}>
      {query.data?.items.map(x => <tr key={`${x.sourceType}-${x.id}`}><td className={cellClass}><p className="font-semibold break-words">{x.title}</p><p className="text-sm text-d-on-surface-variant">{sources[x.sourceType] ?? x.sourceType} · #{x.id.slice(0, 8)}</p></td><td className={cellClass}>{date(x.receivedAt)}</td><td className={cellClass}><ul className="space-y-1">{x.lines.map((line, index) => <li key={index} className="text-sm">{line.materialType ? materialLabel(line.materialType) : line.name}: {number(line.quantity)} {line.unit}</li>)}</ul></td><td className={cellClass}><span className="rounded-full bg-d-surface-accent px-3 py-1 text-sm inline-block">{states[x.status] ?? x.status}</span></td><td className={cellClass}><button type="button" className={buttonClass} onClick={() => x.sourceType === 'PICKUP' ? setPickup({ id: x.id }) : changeParams({ receiptId: x.id, newReceipt: null })}>{['DRAFT', 'SUBMITTED'].includes(x.status) ? 'Kiểm tra phiếu' : 'Xem chi tiết'}</button></td></tr>)}
    </GridTable><Pager page={page} setPage={setPage} total={query.data?.totalCount} /></QueryState>
    {params.get('newReceipt') === '1' && <StockReceiptForm onClose={close} />}
    {id && <QueryState query={detail}>{detail.data && <StockReceiptReview key={`${detail.data.id}-${detail.data.revision}`} receipt={detail.data} onClose={close} />}</QueryState>}
    {pickup && <PaymentDrawer payment={pickup} onClose={() => setPickup(null)} />}
  </section>;
}
