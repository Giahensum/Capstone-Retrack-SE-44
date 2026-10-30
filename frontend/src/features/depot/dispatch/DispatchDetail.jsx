import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Dialog, QueryState, MutationError, Pager, inputClass } from '../components/DepotUI';
import { useDispatchQuery, useDispatchMutation, dispatchTime } from './dispatchApi';
import { Footer } from './DispatchForms';
import { PICKUP_STATUS_LABEL } from '@/lib/utils';
export default function DispatchDetail({ id, onClose }) {
  const query = useDispatchQuery(`pickups/${id}`);
  const [page, setPage] = useState(1), [candidatePage, setCandidatePage] = useState(1);
  const history = useDispatchQuery(`pickups/${id}/events`, { page });
  const candidates = useDispatchQuery('shifts', { page: candidatePage, availableOnly: true, excludeEmployeeId: query.data?.employeeId });
  const [form, setForm] = useState(() => ({ operationId: crypto.randomUUID(), shiftId: '', reason: '' }));
  const [saved, setSaved] = useState(false);
  const assign = useDispatchMutation('post', `pickups/${id}/assign`, () => { setSaved(true); setForm({ operationId: crypto.randomUUID(), shiftId: '', reason: '' }); });
  const d = query.data;
  const canAssign = d && ['PENDING', 'SCHEDULED'].includes(d.status) && !d.hasCheckIn;
  return <Dialog title="Chi tiết và phân công thu gom" onClose={onClose} busy={assign.isPending} wide>
    <QueryState query={query}>{d && <div className="space-y-5">
      <dl className="grid sm:grid-cols-2 gap-4 rounded-2xl bg-white border border-d-border-subtle p-4">{[['Người bán', d.sellerName], ['Điện thoại', d.sellerPhone || 'Chưa cung cấp'], ['Địa chỉ thu gom', d.address], ['Ngày hẹn', dispatchTime(d.preferredDatetime)], ['Trạng thái', PICKUP_STATUS_LABEL[d.status] || d.status], ['Nhân viên phụ trách', d.employeeName || 'Chưa phân công'], ['Lý do chờ', ['ASSIGNED','ASSIGNED_MANUAL'].includes(d.queueReason) ? 'Đã phân công' : d.queueReason || 'Chờ hệ thống điều phối'], ['Nội dung người bán', d.description || 'Không có ghi chú']].map(([label, value]) => <div key={label} className="min-w-0"><dt className="text-sm text-d-on-surface-variant">{label}</dt><dd className="font-medium mt-1 [overflow-wrap:anywhere]">{value}</dd></div>)}</dl>
      {saved && <p role="status" className="text-d-primary">Đã lưu phân công và thông báo cho người liên quan.</p>}
      {canAssign ? <form className="space-y-4 rounded-2xl border border-d-border-subtle p-4" onSubmit={e => { e.preventDefault(); setSaved(false); assign.mutate(form); }}>
        <h3 className="font-semibold">Chủ kho xử lý phân công</h3><p className="text-sm text-d-on-surface-variant">Chọn ca sẵn sàng của đúng kho. Nhân viên cần nhận được liên hệ trước khi bạn chuyển đơn; không dựa vào vị trí GPS hoặc tải xe chưa được xác nhận.</p>
        <fieldset disabled={assign.isPending} className="space-y-4"><QueryState query={candidates}><label className="block">Ca nhận đơn<select required className={inputClass} value={form.shiftId} onChange={e => setForm({ ...form, shiftId: e.target.value })}><option value="">Chọn ca còn khả năng nhận</option>{candidates.data?.items.map(s => <option key={s.id} value={s.id}>{s.employeeName} — {s.areaName} · {s.assignedCount}/{s.maxPendingOrders} đơn · đến {dispatchTime(s.endsAt)}</option>)}</select></label><Pager page={candidatePage} setPage={p => { setCandidatePage(p); setForm({ ...form, shiftId: '' }); }} total={candidates.data?.totalCount} />{candidates.data?.totalCount === 0 && <p className="text-sm">Chưa có ca phù hợp. Hãy bố trí ca, liên hệ nhân viên hoặc giữ đơn chờ xử lý; không ép giao cho người đã đầy/ngừng ca.</p>}</QueryState>
          <label className="block">Lý do phân công / chuyển người<textarea required minLength={10} maxLength={1000} className={inputClass} value={form.reason} onChange={e => setForm({ ...form, reason: e.target.value })} /></label></fieldset>
        <MutationError mutation={assign} /><Footer busy={assign.isPending} onClose={onClose} label="Xác nhận phân công" />
      </form> : <p role="status" className="rounded-xl bg-d-surface-accent p-4">Đơn đã bắt đầu thu gom hoặc kết thúc. Chỉ xem lịch sử, không chuyển người đang giữ hàng.</p>}
      {['SELLER_CONFIRMED', 'AWAITING_PAYMENT', 'PAYMENT_SENT', 'DONE'].includes(d.status) && <Link className="text-d-primary underline" to={`/depot/payments?requestId=${id}`}>Xem đối soát thanh toán</Link>}
      <section className="space-y-3"><h3 className="font-semibold">Lịch sử phân công</h3><QueryState query={history}>{history.data?.items.map(x => <article key={x.id} className="rounded-xl bg-white border border-d-border-subtle p-4 space-y-2"><p className="font-medium">{x.fromEmployeeName || 'Chưa có người'} → {x.toEmployeeName || 'Trả về hàng chờ'}</p><p className="[overflow-wrap:anywhere]">{x.reason}</p><time className="text-xs text-d-on-surface-variant">{dispatchTime(x.createdAt)}</time></article>)}{history.data?.totalCount === 0 && <p className="text-sm">Chưa có lịch sử phân công.</p>}<Pager page={page} setPage={setPage} total={history.data?.totalCount} /></QueryState></section>
    </div>}</QueryState>
  </Dialog>;
}
