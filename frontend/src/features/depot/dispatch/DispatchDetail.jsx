import { Form, Input, Select, Textarea } from '@/components/forms/ValidatedForm';
import { lazy, Suspense, useState } from 'react';
import { Link } from 'react-router-dom';
import { Dialog, QueryState, MutationError, Pager, inputClass } from '../components/DepotUI';
import { useDispatchQuery, useDispatchMutation, dispatchTime } from './dispatchApi';
import { Footer } from './DispatchForms';
import { PICKUP_STATUS_LABEL } from '@/lib/utils';
import { dispatchReason, isDispatchDue } from './dispatchPresentation';
const DispatchMap = lazy(() => import('./DispatchMap'));
export default function DispatchDetail({ id, onClose }) {
  const query = useDispatchQuery(`pickups/${id}`);
  const [page, setPage] = useState(1), [candidatePage, setCandidatePage] = useState(1);
  const history = useDispatchQuery(`pickups/${id}/events`, { page });
  const d = query.data;
  const due = d && isDispatchDue(d.collectionDate ? `${d.collectionDate}T00:00:00+07:00` : d.preferredDatetime);
  const canAssign = d && due && !d.requiresOwnerReview && ['PENDING', 'SCHEDULED'].includes(d.status) && !d.hasCheckIn;
  const candidates = useDispatchQuery('shifts', { page: candidatePage, availableOnly: true, excludeEmployeeId: d?.employeeId, pickupId: id }, !!canAssign);
  const staff = useDispatchQuery('employees', { page: 1, pageSize: 100 }, !!canAssign);
  const [form, setForm] = useState(() => ({ operationId: crypto.randomUUID(), shiftId: '', employeeId: '', reason: '' }));
  const [saved, setSaved] = useState(false);
  const assign = useDispatchMutation('post', `pickups/${id}/assign`, () => { setSaved(true); setForm({ operationId: crypto.randomUUID(), shiftId: '', employeeId: '', reason: '' }); });
  return <Dialog title="Chi tiết và phân công thu gom" onClose={onClose} busy={assign.isPending} wide>
    <QueryState query={query}>{d && <div className="space-y-5">
      <dl className="grid sm:grid-cols-2 gap-4 rounded-2xl bg-white border border-d-border-subtle p-4">{[['Người bán', d.sellerName], ['Điện thoại', d.sellerPhone || 'Chưa cung cấp'], ['Địa chỉ thu gom', d.address], ['Ngày hẹn', d.collectionDate || dispatchTime(d.preferredDatetime)], ['Khung giờ', d.preferredDatetime ? `${dispatchTime(d.preferredDatetime)} → ${dispatchTime(d.preferredEndDatetime)}` : 'Linh hoạt trong ngày, 07:00–19:00'], ['Trạng thái', PICKUP_STATUS_LABEL[d.status] || d.status], ['Nhân viên phụ trách', d.employeeName || 'Chưa phân công'], ['Lý do chờ', dispatchReason(d.queueReason)], ['Nội dung người bán', d.description || 'Không có ghi chú']].map(([label, value]) => <div key={label} className="min-w-0"><dt className="text-sm text-d-on-surface-variant">{label}</dt><dd className="font-medium mt-1 [overflow-wrap:anywhere]">{value}</dd></div>)}</dl>
      {saved && <p role="status" className="text-d-primary">Đã lưu phân công và thông báo cho người liên quan.</p>}
      <Suspense fallback={<p role="status">Đang tải bản đồ…</p>}><DispatchMap items={[d]} title="Vị trí thu gom" /></Suspense>
      <div className="flex flex-wrap gap-3">
        {d.sellerPhone && <a className="inline-flex min-h-11 items-center rounded-full border border-d-border-subtle bg-white px-4 py-2 text-sm font-semibold" href={`tel:${d.sellerPhone.replace(/[^\d+]/g, '')}`}>Liên hệ Seller</a>}
        <Link className="inline-flex min-h-11 items-center rounded-full border border-d-border-subtle bg-white px-4 py-2 text-sm font-semibold" to="/depot/dispatch?tab=shifts" onClick={onClose}>Xem / bố trí ca thu gom</Link>
      </div>
      {canAssign ? <Form className="space-y-4 rounded-2xl border border-d-border-subtle p-4" onSubmit={e => { e.preventDefault(); setSaved(false); assign.mutate({ ...form, shiftId: form.shiftId || '00000000-0000-0000-0000-000000000000', employeeId: form.employeeId || null }); }}>
        <h3 className="font-semibold">Chủ kho xử lý phân công</h3><p className="text-sm text-d-on-surface-variant">Chọn ca sẵn sàng của đúng kho. Nhân viên cần nhận được liên hệ trước khi bạn chuyển đơn; không dựa vào vị trí GPS hoặc tải xe chưa được xác nhận.</p>
        <fieldset disabled={assign.isPending} className="space-y-4"><QueryState query={candidates}><label className="block">Ca nhận đơn<Select required={!form.employeeId} className={inputClass} value={form.shiftId} onChange={e => setForm({ ...form, shiftId: e.target.value, employeeId: '' })}><option value="">Chọn ca còn khả năng nhận</option>{candidates.data?.items.map(s => <option key={s.id} value={s.id}>{s.employeeName} — {s.areaName} · {s.assignedCount}{s.isAutomatic ? ' (tự động)' : `/${s.maxPendingOrders}`} đơn · đến {dispatchTime(s.endsAt)}</option>)}</Select></label><Pager page={candidatePage} setPage={p => { setCandidatePage(p); setForm({ ...form, shiftId: '' }); }} total={candidates.data?.totalCount} />{candidates.data?.totalCount === 0 && <p className="text-sm">Chưa có ca phù hợp. Hãy bố trí ca, liên hệ nhân viên hoặc giữ đơn chờ xử lý; không ép giao cho người đã đầy/ngừng ca.</p>}</QueryState>
          <QueryState query={staff}><label className="block">Hoặc bố trí người dự phòng cùng kho<Select className={inputClass} value={form.employeeId} onChange={e => setForm({ ...form, employeeId: e.target.value, shiftId: '' })}><option value="">Không chọn người dự phòng</option>{staff.data?.items.filter(s => s.userId !== d.employeeId).map(s => <option key={s.id} value={s.userId}>{s.fullName}</option>)}</Select></label><p className="text-sm">Hệ thống dùng ca sẵn sàng hiện có hoặc tạo ca 07:00–19:00 nếu người này chưa có ca. Người đang FULL hoặc trùng ca không bị tự bật lại; yêu cầu không hợp lệ sẽ được giải thích.</p></QueryState>
          <label className="block">Lý do phân công / chuyển người<Textarea required minLength={10} maxLength={1000} className={inputClass} value={form.reason} onChange={e => setForm({ ...form, reason: e.target.value })} /></label></fieldset>
        <MutationError mutation={assign} /><Footer busy={assign.isPending} onClose={onClose} label="Xác nhận phân công" />
      </Form> : <p role="status" className="rounded-xl bg-d-surface-accent p-4">{d.requiresOwnerReview ? 'Đơn cần chủ kho xác nhận lịch mới với người bán trước khi giao lại.' : !due ? 'Đơn hẹn từ ngày mai. Hãy bố trí ca phù hợp; hệ thống chỉ phân công vào đúng ngày Việt Nam.' : 'Đơn đã bắt đầu thu gom hoặc kết thúc. Chỉ xem lịch sử, không chuyển người đang giữ hàng.'}</p>}
      {['PENDING', 'SCHEDULED'].includes(d.status) && !d.hasCheckIn && <Reschedule pickup={d} />}
      {['SELLER_CONFIRMED', 'AWAITING_PAYMENT', 'PAYMENT_SENT', 'DONE'].includes(d.status) && <Link className="text-d-primary underline" to={`/depot/payments?requestId=${id}`}>Xem đối soát thanh toán</Link>}
      <section className="space-y-3"><h3 className="font-semibold">Lịch sử phân công</h3><QueryState query={history}>{history.data?.items.map(x => <article key={x.id} className="rounded-xl bg-white border border-d-border-subtle p-4 space-y-2"><p className="font-medium">{x.fromEmployeeName || 'Chưa có người'} → {x.toEmployeeName || 'Trả về hàng chờ'}</p><p className="[overflow-wrap:anywhere]">{x.reason}</p><time className="text-xs text-d-on-surface-variant">{dispatchTime(x.createdAt)}</time></article>)}{history.data?.totalCount === 0 && <p className="text-sm">Chưa có lịch sử phân công.</p>}<Pager page={page} setPage={setPage} total={history.data?.totalCount} /></QueryState></section>
    </div>}</QueryState>
  </Dialog>;
}

function Reschedule({ pickup }) {
  const [day,setDay]=useState(''), [slot,setSlot]=useState(''), [reason,setReason]=useState('');
  const [operationId,setOperationId]=useState(()=>crypto.randomUUID());
  const [confirmed,setConfirmed]=useState(false);
  const save=useDispatchMutation('post',`pickups/${pickup.id}/reschedule`,()=>{setOperationId(crypto.randomUUID());setConfirmed(false);});
  const start=day && slot ? new Date(`${day}T${slot}:00+07:00`).toISOString() : null;
  return <details className="rounded-xl border p-4"><summary>Xác nhận lịch mới với người bán</summary>
    <Form className="space-y-3 mt-3" onSubmit={e=>{e.preventDefault();save.mutate({operationId,collectionDate:day,preferredDatetime:start,preferredEndDatetime:start ? new Date(new Date(start).getTime()+7200000).toISOString() : null,reason});}}>
      <label className="block">Ngày thu gom<Input required type="date" className={inputClass} value={day} onChange={e=>{setDay(e.target.value);setOperationId(crypto.randomUUID());}} /></label>
      <label className="block">Khung giờ<Select className={inputClass} value={slot} onChange={e=>{setSlot(e.target.value);setOperationId(crypto.randomUUID());}}><option value="">Linh hoạt 07:00–19:00</option>{[7,9,11,13,15,17].map(h=><option key={h} value={`${String(h).padStart(2,'0')}:00`}>{h}:00–{h+2}:00</option>)}</Select></label>
      <label className="block">Lý do đã thống nhất với người bán<Textarea required minLength={10} maxLength={1000} className={inputClass} value={reason} onChange={e=>{setReason(e.target.value);setOperationId(crypto.randomUUID());}} /></label>
      <label className="flex gap-2"><input required type="checkbox" checked={confirmed} onChange={e=>setConfirmed(e.target.checked)} />Tôi đã liên hệ và thống nhất lịch mới với người bán.</label>
      <p>Lịch cũ được ghi trong lịch sử. Đơn sẽ trở lại hàng điều phối đúng ngày, không tự chuyển kho.</p>
      <MutationError mutation={save}/>{save.isSuccess && <p role="status">Đã lưu lịch mới và thông báo người bán.</p>}
      <button className="rounded-full border px-4 py-2" disabled={save.isPending || !confirmed}>Lưu lịch đã xác nhận</button>
    </Form></details>;
}
