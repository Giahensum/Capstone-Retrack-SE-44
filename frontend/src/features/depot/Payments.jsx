import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import api from '@/lib/axios';
import { MaterialIcon } from '../../components/ui/MaterialIcon';
import { depotError, useDepot } from './DepotContext';
import PaymentDrawer from './components/PaymentDrawer';

const money = (n) => Number(n ?? 0).toLocaleString('vi-VN') + ' đ';
const labels = { AWAITING_PAYMENT: 'Chờ chuyển tiền', PAYMENT_SENT: 'Đã chuyển tiền', DONE: 'Hoàn tất' };
export default function Payments() {
  const { depotId, userId } = useDepot();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('AWAITING_PAYMENT');
  const [sort, setSort] = useState('newest');
  const [selected, setSelected] = useState(null);
  const query = useQuery({
    queryKey: ['depot', userId, depotId, 'payments', page, search, status, sort],
    queryFn: async () => (await api.get('/depot/payments', { params: { depotId, page, pageSize: 20, search, status, sort } })).data.data,
  });
  const summary = useQuery({
    queryKey: ['depot', userId, depotId, 'payment-summary'],
    queryFn: async () => (await api.get('/depot/payments/summary', { params: { depotId } })).data.data,
  });
  const cards = [
    ['account_balance_wallet', 'Tổng nợ cần trả', money(summary.data?.pendingAmount)],
    ['schedule', 'Tổng đơn chờ', summary.data?.pendingCount ?? 0],
    ['check_circle', 'Đã chuyển hôm nay', money(summary.data?.sentTodayAmount)],
  ];
  return <div className="flex-1 p-4 md:p-6 w-full flex flex-col gap-8 min-h-full bg-d-background">
    {selected && <PaymentDrawer key={selected.id} payment={selected} onClose={() => setSelected(null)} />}
    <div className="mb-2"><h1 className="font-d-headline-xl-mobile md:font-d-headline-xl text-d-headline-xl-mobile md:text-d-headline-xl text-d-on-surface mb-2">Thanh toán chờ duyệt</h1>
      <p className="font-d-body-md text-d-on-surface-variant">Quản lý và phê duyệt các khoản thanh toán cho người bán.</p></div>
    {summary.isError ? <p role="alert">{depotError(summary.error)}</p> : <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
      {cards.map(([icon, label, value], i) => <div key={label} className={`${i === 0 ? 'bg-d-surface-accent' : 'bg-white'} rounded-[20px] p-8 border border-d-border-subtle relative overflow-hidden`}>
        <MaterialIcon name={icon} className="absolute right-6 top-6 text-5xl text-d-primary opacity-15" />
        <p className="font-d-label-md text-d-on-surface-variant mb-2 uppercase tracking-wider">{label}</p>
        <p className="font-d-headline-lg text-d-headline-lg text-d-primary">{summary.isPending ? '…' : value}</p>
      </div>)}</div>}
    <div className="bg-white rounded-[20px] border border-d-border-subtle p-4 flex flex-col xl:flex-row gap-4">
      <label className="flex-1">Tìm người bán hoặc mã đơn<input value={search} onChange={(e) => { setSearch(e.target.value); setPage(1); }}
        className="block w-full bg-d-surface-container border border-d-border-subtle rounded-full py-3 px-4 mt-1" /></label>
      <label>Trạng thái<select value={status} onChange={(e) => { setStatus(e.target.value); setPage(1); }} className="block rounded-full border p-3 mt-1">
        <option value="">Tất cả</option>{Object.entries(labels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
      </select></label>
      <label>Sắp xếp<select value={sort} onChange={(e) => { setSort(e.target.value); setPage(1); }} className="block rounded-full border p-3 mt-1">
        <option value="newest">Mới nhất</option><option value="oldest">Cũ nhất trước</option><option value="amount">Giá trị cao</option>
      </select></label>
    </div>
    <div className="bg-white rounded-[20px] border border-d-border-subtle overflow-hidden shadow-sm">
      {query.isPending ? <p role="status" className="p-8">Đang tải thanh toán…</p> : query.isError ? <div role="alert" className="p-8 text-d-error">{depotError(query.error)} <button onClick={() => query.refetch()}>Thử lại</button></div> : <>
        <div className="overflow-x-auto"><table className="w-full text-left border-collapse">
          <thead><tr className="bg-d-surface-container/50 border-b border-d-border-subtle">{['Mã đơn', 'Người bán & SĐT', 'Tiền gốc', 'Phí nền tảng', 'Thực trả', 'Trạng thái', 'Hành động'].map((h) => <th key={h} className="py-4 px-6 font-d-label-sm text-d-on-surface-variant whitespace-nowrap">{h}</th>)}</tr></thead>
          <tbody className="divide-y divide-d-border-subtle">{query.data.items.map((p) => <tr key={p.id} className="hover:bg-d-surface-container-low">
            <td className="py-4 px-6 font-medium" title={p.id}>#{p.id.slice(0, 8)}</td>
            <td className="py-4 px-6"><div>{p.sellerName}</div><div className="text-sm text-d-on-surface-variant">{p.sellerPhone}</div></td>
            <td className="py-4 px-6 whitespace-nowrap">{money(p.grossAmount)}</td>
            <td className="py-4 px-6 whitespace-nowrap">{money(p.platformFeeAmount)} ({p.platformFeePercentage}%)</td>
            <td className="py-4 px-6 font-bold text-d-primary bg-d-surface-accent/10 whitespace-nowrap">{money(p.netAmount)}</td>
            <td className="py-4 px-6">{labels[p.status]}</td>
            <td className="py-4 px-6"><button onClick={() => setSelected(p)} className="rounded-full px-4 py-2 bg-d-on-surface text-white whitespace-nowrap">{p.status === 'AWAITING_PAYMENT' ? 'Duyệt' : 'Chi tiết'}</button></td>
          </tr>)}</tbody>
        </table></div>
        {query.data.items.length === 0 && <p role="status" className="p-8 text-center">Không có thanh toán phù hợp.</p>}
        <div className="p-4 border-t border-d-border-subtle flex justify-between items-center gap-4">
          <p>Trang {page} · {query.data.totalCount} mục</p><div className="flex gap-3">
            <button disabled={page === 1} onClick={() => setPage(page - 1)} className="border rounded-full px-4 py-2 disabled:opacity-40">Trước</button>
            <button disabled={page * 20 >= query.data.totalCount} onClick={() => setPage(page + 1)} className="border rounded-full px-4 py-2 disabled:opacity-40">Sau</button>
          </div></div>
      </>}
    </div>
  </div>;
}
