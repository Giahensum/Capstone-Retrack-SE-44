import { useState, useEffect } from 'react';
import { useDepotQuery, useDepotMutation, money } from '../depotApi';
import { QueryState, Pager, inputClass, buttonClass, MutationError } from './DepotUI';

export default function StockCostAdjustments({ receipt, onBusyChange }) {
  const [page, setPage] = useState(1);
  const [form, setForm] = useState({ operationId: crypto.randomUUID(), lineId: receipt.lines[0]?.id ?? '', kind: 'ADDITIONAL_COST', amount: '', reason: '' });
  const query = useDepotQuery(`stock/receipts/${receipt.id}/adjustments`, { page });
  const balance = useDepotQuery(`stock/receipts/${receipt.id}/cost-balance`);
  const save = useDepotMutation('post', `stock/receipts/${receipt.id}/adjustments`, () => setForm({ ...form, operationId: crypto.randomUUID(), amount: '', reason: '' }));
  useEffect(() => { onBusyChange(save.isPending); }, [save.isPending, onBusyChange]);
  const field = (key, value) => setForm({ ...form, [key]: value });
  return <section className="border-t border-d-border-subtle pt-4 space-y-4"><h3 className="font-semibold text-d-primary">Điều chỉnh chi phí sau ghi sổ</h3>
    <p className="text-sm text-d-on-surface-variant">Chọn đúng dòng hàng và nhập phần chi phí phân bổ cho dòng đó (công, xăng xe, vận chuyển, thuế không hoàn lại…). Lịch sử và snapshot của lô đã xuất giữ nguyên; giao dịch bị ảnh hưởng cần đối soát riêng.</p>
    <QueryState query={balance}><div className="space-y-2">{balance.data?.map(x => <p key={x.lineId} className="text-sm">{x.name}: vốn gốc {x.originalCost == null ? 'chưa xác định' : money(x.originalCost)} · vốn dùng cho lần xuất/bán mới {x.effectiveCost == null ? 'chưa xác định' : money(x.effectiveCost)}</p>)}</div></QueryState>
    <form onSubmit={e => { e.preventDefault(); save.mutate({ body: { ...form, amount: Number(form.amount) } }); }}><fieldset disabled={save.isPending} className="grid sm:grid-cols-2 gap-4">
      <label>Dòng hàng cần điều chỉnh<select className={inputClass} value={form.lineId} onChange={e => field('lineId', e.target.value)}>{receipt.lines.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></label>
      <label>Loại khoản ghi<select className={inputClass} value={form.kind} onChange={e => field('kind', e.target.value)}><option value="ADDITIONAL_COST">Chi phí bổ sung / điều chỉnh</option><option value="CONFIRM_BASE_COST">Xác nhận vốn gốc còn thiếu</option></select></label>
      <label>Số tiền điều chỉnh (đ)<input className={inputClass} required type="number" step="1" min={form.kind === 'CONFIRM_BASE_COST' ? 0 : -1000000000000} max="1000000000000" value={form.amount} onChange={e => field('amount', e.target.value)} /></label>
      <label>Lý do / chứng từ điều chỉnh<input className={inputClass} required maxLength={1000} value={form.reason} onChange={e => field('reason', e.target.value)} /></label>
      <div className="sm:col-span-2"><p className="text-xs mb-3">Số dương cộng chi phí; số âm sửa giảm khoản đã ghi, không được làm vốn âm. Xác nhận vốn gốc chỉ dùng một lần cho dòng chưa biết vốn.</p><MutationError mutation={save} /><button className={buttonClass} disabled={save.isPending}>{save.isPending ? 'Đang ghi…' : 'Ghi khoản điều chỉnh'}</button></div>
    </fieldset></form>
    <QueryState query={query}><div className="space-y-3">{query.data?.items.map(x => <article key={x.id} className="rounded-xl bg-d-surface-accent p-3 text-sm"><p className="font-semibold">{x.lineName} · {money(x.amount)} · {x.kind === 'CONFIRM_BASE_COST' ? 'Xác nhận vốn gốc' : 'Điều chỉnh chi phí'}</p><p className="break-words">{x.reason}</p><p className="mt-1">{new Date(x.createdAt).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' })}</p></article>)}{query.data?.totalCount === 0 && <p>Chưa có khoản điều chỉnh sau ghi sổ.</p>}</div><Pager page={page} setPage={setPage} total={query.data?.totalCount} /></QueryState>
  </section>;
}
