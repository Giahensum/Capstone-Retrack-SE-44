import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import api from '@/lib/axios';
import { MaterialIcon } from './components/DepotIcon';
import { depotError, useDepot } from './DepotContext';
import PaymentDrawer from './components/PaymentDrawer';
import { inputClass, Pager } from './components/DepotUI';
import { PICKUP_STATUS_LABEL } from '@/lib/utils';
import { PageHeader, StatCard, ErrorState } from '@/components/ui/kit';

const money = (n) => Number(n ?? 0).toLocaleString('vi-VN') + ' đ';
const paymentStates = ['SELLER_CONFIRMED', 'AWAITING_PAYMENT', 'PAYMENT_SENT', 'DONE'];
export default function Payments() {
  const { depotId, userId } = useDepot();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('');
  const [sort, setSort] = useState('newest');
  const [params, setParams] = useSearchParams();
  const [selected, setSelected] = useState(() => params.get('requestId') ? { id: params.get('requestId') } : null);
  const query = useQuery({
    queryKey: ['depot', userId, depotId, 'payments', page, search, status, sort],
    queryFn: async () => (await api.get('/depot/payments', { params: { depotId, page, pageSize: 20, search, status, sort } })).data.data,
    enabled: !!depotId,
  });
  const summary = useQuery({
    queryKey: ['depot', userId, depotId, 'payment-summary'],
    queryFn: async () => (await api.get('/depot/payments/summary', { params: { depotId } })).data.data,
    enabled: !!depotId,
  });
  const hasLegacyFees = query.data?.items.some(p => p.platformFeeAmount > 0);
  const cards = [
    ['account_balance_wallet', 'Tổng nợ cần trả', money(summary.data?.pendingAmount)],
    ['schedule', 'Tổng đơn chờ', summary.data?.pendingCount ?? 0],
    ['check_circle', 'Đã báo thanh toán hôm nay', money(summary.data?.sentTodayAmount)],
  ];
  return <div className="flex-1 p-4 md:p-6 w-full max-w-7xl mx-auto flex flex-col gap-6 min-h-full bg-d-background">
    {selected && <PaymentDrawer key={selected.id} payment={selected} onClose={() => { setSelected(null); setParams(prev => { prev.delete('requestId'); return prev; }, { replace: true }); }} />}
    <PageHeader title="Thanh toán cho người bán" description="Sau khi nhân viên bàn giao, kho trả tiền theo thỏa thuận rồi ghi nhận tại đây; người bán xác nhận tiền thực nhận. RETRACK không thu hộ tiền hàng mới." />
    {summary.isError ? <ErrorState onRetry={() => summary.refetch()}>{depotError(summary.error)}</ErrorState> : <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
      {cards.map(([icon, label, value]) => <StatCard key={label} label={label} value={value} loading={summary.isPending} icon={<MaterialIcon name={icon} className="text-xl" />} />)}</div>}
    <div className="bg-white rounded-2xl border border-d-border-subtle shadow-sm p-4 flex flex-col xl:flex-row gap-4">
      <label className="flex-1">Tìm người bán hoặc mã đơn<input value={search} onChange={(e) => { setSearch(e.target.value); setPage(1); }}
        className={inputClass} /></label>
      <label>Trạng thái<select value={status} onChange={(e) => { setStatus(e.target.value); setPage(1); }} className={inputClass}>
        <option value="">Tất cả</option>{paymentStates.map(value => <option key={value} value={value}>{PICKUP_STATUS_LABEL[value]}</option>)}
      </select></label>
      <label>Sắp xếp<select value={sort} onChange={(e) => { setSort(e.target.value); setPage(1); }} className={inputClass}>
        <option value="newest">Mới nhất</option><option value="oldest">Cũ nhất trước</option><option value="amount">Giá trị cao</option>
      </select></label>
    </div>
    <div className="bg-white rounded-2xl border border-d-border-subtle overflow-hidden shadow-sm">
      {query.isPending ? <p role="status" className="p-8">Đang tải thanh toán…</p> : query.isError ? <div role="alert" className="p-8 text-d-error">{depotError(query.error)} <button onClick={() => query.refetch()} className="ml-2 rounded-full border border-d-error px-4 py-2 font-semibold hover:bg-d-surface-container-low">Thử lại</button></div> : <>
        <div className="overflow-x-auto"><table className="w-full text-left border-collapse">
          <thead><tr className="bg-d-surface-container/50 border-b border-d-border-subtle">{['Mã đơn', 'Người bán & SĐT', ...(hasLegacyFees ? ['Tiền hàng', 'Phí cũ'] : []), 'Trả người bán', 'Trạng thái', 'Hành động'].map((h) => <th key={h} className="py-4 px-6 font-d-label-sm text-d-on-surface-variant whitespace-nowrap">{h}</th>)}</tr></thead>
          <tbody className="divide-y divide-d-border-subtle">{query.data.items.map((p) => <tr key={p.id} className="hover:bg-d-surface-container-low">
            <td className="py-4 px-6 font-medium" title={p.id}>#{p.id.slice(0, 8)}</td>
            <td className="py-4 px-6"><div>{p.sellerName}</div><div className="text-sm text-d-on-surface-variant">{p.sellerPhone}</div></td>
            {hasLegacyFees && <><td className="py-4 px-6 whitespace-nowrap">{money(p.grossAmount)}</td>
            <td className="py-4 px-6 whitespace-nowrap">{money(p.platformFeeAmount)} ({p.platformFeePercentage}%)<p className="text-xs text-d-on-surface-variant">{p.feePolicy === "BUYER_SEPARATE_V1" ? "Kho trả riêng" : "Khấu trừ — đơn cũ"}</p></td></>}
            <td className="py-4 px-6 font-bold text-d-primary bg-d-surface-accent/10 whitespace-nowrap">{money(p.netAmount)}</td>
            <td className="py-4 px-6">{PICKUP_STATUS_LABEL[p.status] ?? p.status}</td>
            <td className="py-4 px-6"><button type="button" onClick={() => setSelected(p)} className="min-h-11 rounded-full px-4 py-2 bg-d-primary-container text-d-on-primary-container font-semibold whitespace-nowrap hover:opacity-90 focus-visible:outline-2 focus-visible:outline-d-primary">{p.status === 'AWAITING_PAYMENT' ? 'Đối soát' : 'Chi tiết'}</button></td>
          </tr>)}</tbody>
        </table></div>
        {query.data.items.length === 0 && <p role="status" className="p-8 text-center">Không có thanh toán phù hợp.</p>}
        <div className="p-4 border-t border-d-border-subtle"><Pager page={page} setPage={setPage} total={query.data.totalCount} /></div>
      </>}
    </div>
  </div>;
}
