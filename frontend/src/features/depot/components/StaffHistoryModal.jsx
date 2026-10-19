import { useState } from 'react';
import { useDepotQuery, money, number } from '../depotApi';
import { Dialog, QueryState, Pager } from './DepotUI';
import { PICKUP_STATUS_LABEL } from '@/lib/utils';
export default function StaffHistoryModal({ staff, period, onClose }) {
  const [page, setPage] = useState(1);
  const query = useDepotQuery(`reports/staff/${staff.id}`, { ...period, page });
  return <Dialog title={`Lịch sử công việc — ${staff.fullName}`} wide onClose={onClose}><p className="text-sm mb-4">Chỉ ghi nhận đơn/chuyến đã hoàn thành trong tháng đang chọn; giá trị công việc không phải tiền lương.</p>
    <QueryState query={query}><div className="space-y-3">{query.data?.items.map(x => <article key={`${x.type}-${x.id}`} className="rounded-xl border border-d-border-subtle bg-white p-4 text-sm space-y-2">
      <p className="font-semibold">{x.type === 'TRANSPORT' ? 'Chuyến vận chuyển' : 'Đơn thu gom'} · {x.status === 'DELIVERED' ? 'Đã giao hàng' : PICKUP_STATUS_LABEL[x.status] ?? x.status}</p>
      <p>{number(x.weightKg)} kg · {money(x.amount)}</p><p>{new Date(x.date).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' })}</p>
    </article>)}{query.data?.totalCount === 0 && <p>Không có công việc hoàn thành trong khoảng ngày này.</p>}</div><Pager page={page} setPage={setPage} total={query.data?.totalCount} /></QueryState>
  </Dialog>;
}
