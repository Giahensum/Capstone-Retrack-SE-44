import { useState } from 'react';
import { useDepotQuery, useDepotMutation } from '../depotApi';
import { Dialog, QueryState, Pager, inputClass, buttonClass, MutationError } from './DepotUI';
const actions = { checkin: 'Đã lấy hàng', start: 'Khởi hành', checkout: 'Báo đã giao', cancel: 'Trả chuyến về pool', reject: 'Từ chối chuyến', incident: 'Báo sự cố' };
const statuses = { NOTED: 'Đã ghi nhận', CONTACTING: 'Đang phối hợp xử lý', CLOSED: 'Đã khép theo dõi' };
export default function DriverEventsPanel({ id }) {
  const [page, setPage] = useState(1), [selected, setSelected] = useState(null);
  const query = useDepotQuery(`batches/${id}/driver-events`, { page });
  return <section className="rounded-2xl border border-d-border-subtle bg-white p-4 space-y-4"><h3 className="font-semibold text-d-primary">Nhật ký vận chuyển và sự cố</h3>
    <QueryState query={query}><div className="space-y-3">{query.data?.items.map(x => <article key={x.id} className="rounded-xl bg-d-surface-accent p-3 space-y-2">
      <p className="font-semibold">{actions[x.action] ?? x.action} · {x.driverName}</p><p className="text-sm">{new Date(x.createdAt).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' })}{x.phone ? ` · Điện thoại: ${x.phone}` : ''}</p>
      <p className="whitespace-pre-wrap break-words">{x.reason}</p>{/^https:\/\/\S+$/i.test(x.imageUrl ?? '') && <a className="text-d-primary underline" href={x.imageUrl} target="_blank" rel="noopener noreferrer">Xem ảnh ghi nhận</a>}
      {['incident', 'cancel', 'reject'].includes(x.action) && <div><p className="text-sm mb-2">{statuses[x.followupStatus] ?? 'Chưa có ghi chú xử lý của kho'}</p><button type="button" className={buttonClass} onClick={() => setSelected(x)}>Theo dõi xử lý</button></div>}
    </article>)}{query.data?.totalCount === 0 && <p className="text-sm">Chưa có nhật ký vận chuyển.</p>}</div><Pager page={page} setPage={setPage} total={query.data?.totalCount} /></QueryState>
    {selected && <Followup event={selected} onClose={() => setSelected(null)} />}
  </section>;
}
function Followup({ event, onClose }) {
  const [page, setPage] = useState(1), [form, setForm] = useState({ operationId: crypto.randomUUID(), status: 'NOTED', note: '' });
  const query = useDepotQuery(`driver-events/${event.id}/followups`, { page });
  const save = useDepotMutation('post', `driver-events/${event.id}/followups`, () => setForm({ ...form, operationId: crypto.randomUUID(), note: '' }));
  return <Dialog title="Theo dõi xử lý vận chuyển" wide busy={save.isPending} onClose={onClose}>
    <p className="mb-4 text-sm">Ghi nhận phối hợp thực tế với tài xế/nhà máy. Khép theo dõi không xác nhận giao hàng, hoàn hàng hoặc tự cộng hàng vào kho.</p>
    <form className="space-y-4" onSubmit={e => { e.preventDefault(); save.mutate({ body: form }); }}><fieldset className="space-y-4" disabled={save.isPending}>
      <label className="block">Trạng thái theo dõi<select className={inputClass} value={form.status} onChange={e => setForm({ ...form, status: e.target.value })}>{Object.entries(statuses).map(([key, label]) => <option value={key} key={key}>{label}</option>)}</select></label>
      <label className="block">Nội dung đã phối hợp / hướng xử lý<textarea required maxLength={1000} className={inputClass} value={form.note} onChange={e => setForm({ ...form, note: e.target.value })} /></label>
      <MutationError mutation={save} /><button className={buttonClass}>{save.isPending ? 'Đang ghi…' : 'Lưu ghi chú xử lý'}</button>
    </fieldset></form>
    <QueryState query={query}><div className="space-y-3 mt-4">{query.data?.items.map(x => <article className="rounded-xl bg-white border border-d-border-subtle p-3 text-sm" key={x.id}><strong>{statuses[x.status]}</strong><p className="whitespace-pre-wrap break-words my-2">{x.note}</p><p>{new Date(x.createdAt).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' })}</p></article>)}</div><Pager page={page} setPage={setPage} total={query.data?.totalCount} /></QueryState>
  </Dialog>;
}
