import { Form, Input, Select, Textarea } from '@/components/forms/ValidatedForm';
import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import api from '@/lib/axios';
import AdminLayout from '@/components/layout/AdminLayout';
import { adminApi, apiErrorMessage } from '../api';
import { formatCurrency, formatDate } from '@/lib/utils';
import { inputClass, Panel, PageHeader } from '../components/AdminUI';
import { Banknote, Settings, RotateCcw, Wallet, TrendingUp, Store, Hourglass, FileCheck2, Receipt } from 'lucide-react';
import { StatCard, Badge } from '@/components/ui/kit';
import SubscriptionRefundForm from '@/features/depot/components/SubscriptionRefundForm';
import { REFUND_KINDS, REFUND_STATUS, REFUND_TONE, refundKindsFor } from '@/features/depot/subscriptionRefunds';
import { Dialog, buttonClass, MutationError, Pager, QueryState } from '@/features/depot/components/DepotUI';

function ReceivingAccount() {
  const q = useQuery({ queryKey: ['admin', 'subscription-account'], queryFn: adminApi.getPaymentAccount });
  const client = useQueryClient();
  const [draft, setDraft] = useState(null);
  const form = draft ?? q.data ?? { bankName: '', accountNumber: '', accountHolder: '' };
  const save = useMutation({ mutationFn: adminApi.updatePaymentAccount, onSuccess: () => client.invalidateQueries({ queryKey: ['admin', 'subscription-account'] }) });
  return <details className="rounded-2xl border border-d-border-subtle bg-white p-4"><summary className="cursor-pointer min-h-11 font-semibold">Tài khoản nhận chuyển khoản gói</summary><QueryState query={q}><Form className="space-y-3 pt-3" onSubmit={e => { e.preventDefault(); save.mutate(form); }}><fieldset className="space-y-3" disabled={save.isPending}>
    {[['bankName', 'Ngân hàng'], ['accountNumber', 'Số tài khoản'], ['accountHolder', 'Chủ tài khoản']].map(([key, title]) => <label key={key} className="block text-sm">{title}<Input required maxLength={key === 'accountNumber' ? 30 : key === 'bankName' ? 100 : 120} pattern={key === 'accountNumber' ? '[0-9]{4,30}' : undefined} className={inputClass} value={form[key]} onChange={e => setDraft({ ...form, [key]: e.target.value })} /></label>)}
    <button className={buttonClass}>Lưu tài khoản nhận tiền</button></fieldset><MutationError mutation={save} />{save.isSuccess && <p role="status">Đã lưu tài khoản nhận tiền gói.</p>}</Form></QueryState></details>;
}
function ApproveOrder({ order, onClose }) {
  const client = useQueryClient();
  const [reference, setReference] = useState('');
  const [confirmed, setConfirmed] = useState(false);
  const [reason, setReason] = useState('');
  const save = useMutation({ mutationFn: () => api.post(`/admin/subscriptions/orders/${order.id}/approve`, { reference }),
    onSuccess: async () => { await client.invalidateQueries({ queryKey: ['admin', 'subscriptions'] }); onClose(); } });
  const reject = useMutation({ mutationFn: () => api.post(`/admin/subscriptions/orders/${order.id}/reject-proof`, { reason }),
    onSuccess: async () => { await client.invalidateQueries({ queryKey: ['admin', 'subscriptions'] }); onClose(); } });
  const safeProof = (() => { try { return new URL(order.proofUrl).protocol === 'https:'; } catch { return false; } })();
  return <Dialog title="Đối soát tiền mua gói Depot" busy={save.isPending || reject.isPending} onClose={onClose}>
    <p>Kho <strong>{order.depotName}</strong> · Số tiền cần nhận <strong>{formatCurrency(order.amount)}</strong>.</p>
    <p className="mt-2 text-sm">Ảnh chứng từ chỉ là căn cứ đối chiếu. Admin cần kiểm tra tiền thực nhận trước khi kích hoạt; thao tác này sẽ cộng một tháng cho kho.</p>
    {safeProof && <a className="my-3 inline-block text-d-primary underline" target="_blank" rel="noopener noreferrer" href={order.proofUrl}>Mở chứng từ đã gửi</a>}
    <Form className="space-y-4 mt-3" onSubmit={e => { e.preventDefault(); if (confirmed) save.mutate(); }}><label className="block text-sm">Mã giao dịch thực nhận / căn cứ đối soát<Input required maxLength={255} disabled={save.isPending || reject.isPending} className={inputClass} value={reference} onChange={e => setReference(e.target.value)} /></label>
      <label className="flex gap-2 text-sm"><Input type="checkbox" checked={confirmed} disabled={save.isPending || reject.isPending} onChange={e => setConfirmed(e.target.checked)} />Tôi đã kiểm tra nhận đủ {formatCurrency(order.amount)} cho đúng đơn, không chỉ dựa vào ảnh chứng từ.</label>
      <MutationError mutation={save} /><button className={buttonClass} disabled={save.isPending || reject.isPending || !confirmed}>Xác nhận tiền và kích hoạt một tháng</button>
      <details><summary className="cursor-pointer min-h-11 py-3 text-d-primary">Chứng từ chưa hợp lệ / chưa nhận tiền</summary><label className="block text-sm">Lý do yêu cầu bổ sung<Textarea maxLength={1000} disabled={save.isPending || reject.isPending} className={inputClass} value={reason} onChange={e => setReason(e.target.value)} /></label><button type="button" className="mt-3 inline-flex items-center justify-center gap-2 rounded-full font-semibold transition active:scale-[.98] disabled:pointer-events-none disabled:opacity-50 outline-none focus-visible:ring-2 focus-visible:ring-d-primary/30 focus-visible:ring-offset-1 min-h-11 px-5 py-2 text-sm border border-d-border-subtle bg-white text-d-on-surface hover:bg-d-surface-container" disabled={save.isPending || reject.isPending || reason.trim().length < 5} onClick={() => reject.mutate()}>Yêu cầu bổ sung chứng từ</button><MutationError mutation={reject} /></details>
    </Form>
  </Dialog>;
}
// Admin lập yêu cầu hoàn thay khi đã đối soát sao kê (chuyển thiếu/thừa/tiền về muộn).
function AdminRefundRequest({ order, onClose }) {
  const client = useQueryClient();
  const save = useMutation({ mutationFn: (body) => api.post('/admin/subscriptions/refunds', body),
    onSuccess: async () => { await client.invalidateQueries({ queryKey: ['admin', 'subscriptions'] }); onClose(); } });
  return <SubscriptionRefundForm order={order} onClose={onClose} allowUpload={false} pending={save.isPending} error={<MutationError mutation={save} />} submit={(body) => save.mutate(body)} />;
}

function RefundAction({ refund, onClose }) {
  const client = useQueryClient();
  const [amount, setAmount] = useState(String(refund.approvedAmount ?? refund.requestedAmount));
  const [note, setNote] = useState('');
  const [reference, setReference] = useState('');
  const [proof, setProof] = useState('');
  const [checked, setChecked] = useState(false);
  const done = async () => { await client.invalidateQueries({ queryKey: ['admin', 'subscriptions'] }); onClose(); };
  const decide = useMutation({ mutationFn: (approve) => api.post(`/admin/subscriptions/refunds/${refund.id}/decision`, { approve, amount: approve ? Number(amount) : null, note }), onSuccess: done });
  const transfer = useMutation({ mutationFn: () => api.post(`/admin/subscriptions/refunds/${refund.id}/transferred`, { reference, proofUrl: proof || null }), onSuccess: done });
  const busy = decide.isPending || transfer.isPending;
  const safe = (url) => { try { return new URL(url).protocol === 'https:'; } catch { return false; } };
  return <Dialog title={refund.status === 'REQUESTED' ? 'Xem xét yêu cầu hoàn tiền gói' : 'Ghi nhận đã chuyển tiền hoàn'} busy={busy} onClose={onClose}>
    <div className="space-y-1 text-sm">
      <p>Kho <strong>{refund.depotName}</strong> · đơn #{refund.orderCode} ({formatCurrency(refund.orderAmount)}) · {REFUND_KINDS[refund.kind]}</p>
      <p>Người gửi: {refund.requestedRole === 'ADMIN' ? 'Admin' : 'Chủ kho'} · {formatDate(refund.requestedAt)} · đề nghị <strong>{formatCurrency(refund.requestedAmount)}</strong></p>
      <p className="rounded-xl bg-d-surface-container-low p-3">{refund.reason}</p>
      {refund.evidenceReference && <p>Mã giao dịch chứng từ: <strong>{refund.evidenceReference}</strong></p>}
      {safe(refund.evidenceUrl) && <a className="text-d-primary underline" href={refund.evidenceUrl} target="_blank" rel="noopener noreferrer">Mở chứng từ</a>}
    </div>
    {refund.status === 'REQUESTED' ? <Form className="mt-4 space-y-3" onSubmit={(e) => { e.preventDefault(); if (note.trim().length >= 5) decide.mutate(true); }}>
      {refund.kind === 'UNUSED_PERIOD' && <p className="rounded-xl bg-amber-50 p-3 text-sm text-amber-900">Duyệt sẽ rút kỳ {refund.orderPeriodStart && formatDate(refund.orderPeriodStart)} – {refund.orderPeriodEnd && formatDate(refund.orderPeriodEnd)} khỏi hạn dùng của kho. Các kỳ khác giữ nguyên.</p>}
      <label className="block text-sm">Số tiền duyệt (đ, không vượt số đề nghị)<Input inputMode="numeric" className={inputClass} disabled={busy || refund.kind === 'UNUSED_PERIOD'} value={amount} onChange={(e) => setAmount(e.target.value.replace(/\D/g, ''))} /></label>
      <label className="block text-sm">Ghi chú quyết định (gửi cho chủ kho, 5–1000 ký tự)<Textarea rows={3} maxLength={1000} className={inputClass} disabled={busy} value={note} onChange={(e) => setNote(e.target.value)} /></label>
      <MutationError mutation={decide} />
      <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
        <button type="button" className="min-h-11 rounded-full border border-d-error/40 bg-white px-5 text-sm font-semibold text-d-error disabled:opacity-50" disabled={busy || note.trim().length < 5} onClick={() => decide.mutate(false)}>Từ chối</button>
        <button className={buttonClass} disabled={busy || note.trim().length < 5}>Duyệt hoàn tiền</button>
      </div>
    </Form> : <Form className="mt-4 space-y-3" onSubmit={(e) => { e.preventDefault(); if (checked) transfer.mutate(); }}>
      <p className="text-sm">Đã duyệt hoàn <strong>{formatCurrency(refund.approvedAmount)}</strong>. Chỉ ghi nhận sau khi đã chuyển tiền thật cho chủ kho.</p>
      <label className="block text-sm">Mã giao dịch chuyển tiền hoàn<Input required maxLength={255} className={inputClass} disabled={busy} value={reference} onChange={(e) => setReference(e.target.value)} /></label>
      <label className="block text-sm">Đường dẫn chứng từ chuyển tiền (HTTPS, nếu có)<Input type="url" pattern="https://.+" className={inputClass} disabled={busy} value={proof} onChange={(e) => setProof(e.target.value.trim())} /></label>
      <label className="flex gap-2 text-sm"><Input type="checkbox" checked={checked} disabled={busy} onChange={(e) => setChecked(e.target.checked)} />Tôi đã chuyển đủ {formatCurrency(refund.approvedAmount)} cho chủ kho và kiểm tra giao dịch thành công.</label>
      <MutationError mutation={transfer} />
      <div className="flex justify-end"><button className={buttonClass} disabled={busy || !checked || reference.trim().length < 3}>Ghi nhận đã chuyển tiền hoàn</button></div>
    </Form>}
  </Dialog>;
}

function RefundsPanel() {
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState('REQUESTED');
  const [selected, setSelected] = useState(null);
  const q = useQuery({ queryKey: ['admin', 'subscriptions', 'refunds', page, status], queryFn: async () => (await api.get('/admin/subscriptions/refunds', { params: { page, pageSize: 20, status } })).data.data });
  return <Panel icon={RotateCcw} title="Hoàn tiền gói" toolbar={<label className="text-sm">Trạng thái<Select className={inputClass} value={status} onChange={(e) => { setStatus(e.target.value); setPage(1); }}>
    <option value="">Tất cả</option>{Object.entries(REFUND_STATUS).map(([k, v]) => <option key={k} value={k}>{v}</option>)}</Select></label>}>
    <QueryState query={q}><div className="overflow-x-auto"><table className="w-full min-w-[820px] text-left text-sm"><thead><tr>{['Kho / đơn', 'Loại', 'Đề nghị', 'Duyệt', 'Trạng thái', 'Gửi lúc', 'Thao tác'].map((x) => <th key={x} className="p-4">{x}</th>)}</tr></thead>
      <tbody>{q.data?.items.map((r) => <tr key={r.id} className="border-t border-d-border-subtle align-top">
        <td className="p-4"><strong>{r.depotName}</strong><p className="text-xs text-d-on-surface-variant">#{r.orderCode} · {r.requestedRole === 'ADMIN' ? 'Admin lập' : 'Chủ kho gửi'}</p></td>
        <td className="p-4">{REFUND_KINDS[r.kind]}</td><td className="p-4">{formatCurrency(r.requestedAmount)}</td>
        <td className="p-4">{r.approvedAmount != null ? formatCurrency(r.approvedAmount) : '—'}</td>
        <td className="p-4"><Badge tone={REFUND_TONE[r.status]}>{REFUND_STATUS[r.status]}</Badge>{r.refundReference && <p className="mt-1 text-xs">Mã CK {r.refundReference}</p>}</td>
        <td className="p-4">{formatDate(r.requestedAt)}</td>
        <td className="p-4">{r.status === 'REQUESTED' ? <button className={buttonClass} onClick={() => setSelected(r)}>Xem xét</button>
          : r.status === 'APPROVED' ? <button className={buttonClass} onClick={() => setSelected(r)}>Ghi nhận đã chuyển</button> : <span className="text-xs text-d-on-surface-variant">{r.decisionNote}</span>}</td>
      </tr>)}</tbody></table>{q.data?.items.length === 0 && <p className="p-5">Không có yêu cầu hoàn tiền ở trạng thái này.</p>}</div>
      <div className="p-4"><Pager page={page} setPage={setPage} total={q.data?.totalCount} /></div></QueryState>
    {selected && <RefundAction refund={selected} onClose={() => setSelected(null)} />}
  </Panel>;
}

export default function SubscriptionBilling({ onLegacy }) {
  const client = useQueryClient();
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState('');
  const [price, setPrice] = useState('');
  const [selected, setSelected] = useState(null);
  const [refundOrder, setRefundOrder] = useState(null);
  const summary = useQuery({ queryKey: ['admin', 'subscriptions', 'summary'], queryFn: async () => (await api.get('/admin/subscriptions/summary')).data.data });
  const orders = useQuery({ queryKey: ['admin', 'subscriptions', 'orders', page, status], queryFn: async () => (await api.get('/admin/subscriptions/orders', { params: { page, pageSize: 20, status } })).data.data });
  const save = useMutation({ mutationFn: () => api.put('/admin/subscriptions/price', { monthlyPrice: Number(price) }), onSuccess: () => client.invalidateQueries({ queryKey: ['admin', 'subscriptions'] }) });
  const sync = useMutation({ mutationFn: id => api.post(`/admin/subscriptions/orders/${id}/payos-sync`), onSuccess: () => client.invalidateQueries({ queryKey: ['admin', 'subscriptions'] }) });
  return <AdminLayout crumb="Gói dịch vụ Depot"><div className="w-full max-w-7xl mx-auto space-y-6 p-4 md:p-6">
    <PageHeader icon={Banknote} title="Gói dịch vụ Depot" description="Chỉ thu tiền phần mềm quản lý kho. Tiền mua bán hàng do các bên thanh toán trực tiếp, không tính vào doanh thu nền tảng." />
    <QueryState query={summary}>{summary.data && <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      <StatCard label="Tiền gói đã thu" value={formatCurrency(summary.data.paidRevenue)} hint={`${summary.data.paidOrderCount} đơn đã kích hoạt`} icon={Wallet} />
      <StatCard label="Doanh thu gói ròng" value={formatCurrency(summary.data.netRevenue)} hint={`Đã chuyển hoàn ${formatCurrency(summary.data.refundedAmount)}`} icon={TrendingUp} />
      <StatCard label="Thu trong tháng này" value={formatCurrency(summary.data.revenueThisMonth)} hint={`Giá hiện tại ${formatCurrency(summary.data.monthlyPrice)} / kho / tháng`} icon={Receipt} />
      <StatCard label="Kho đang dùng gói" value={summary.data.activeDepotCount} hint={`${summary.data.trialDepotCount} dùng thử · ${summary.data.expiredDepotCount} đã hết hạn`} icon={Store} />
      <StatCard label="Chứng từ cần đối soát" value={summary.data.pendingProofCount} tone="amber" icon={FileCheck2} />
      <StatCard label="Yêu cầu hoàn chờ xử lý" value={summary.data.openRefundCount} tone="amber" icon={Hourglass} />
      <StatCard label="Đã duyệt, chờ chuyển hoàn" value={formatCurrency(summary.data.approvedAwaitingTransfer)} tone="blue" icon={RotateCcw} />
    </div>}</QueryState>
    <p className="text-xs text-d-on-surface-variant">Chỉ là tiền dịch vụ của RETRACK; tiền hàng (GMV) giữa các bên không được cộng vào doanh thu. Giá chưa kết luận thuế; phiếu mua gói không thay thế hóa đơn điện tử hợp pháp.</p>
    <Panel title="Giá dịch vụ" icon={Settings}><Form className="space-y-3 p-4" onSubmit={e => { e.preventDefault(); save.mutate(); }}><p className="text-sm">Một gói đầy đủ cho một kho, tháng lịch, không tự gia hạn. Thay đổi giá chỉ áp dụng cho đơn mới; các đơn đang chờ giữ nguyên số tiền.</p><label className="block text-sm">Giá tháng mới (đ)<Input required type="number" min="1000" max="10000000" step="1" disabled={save.isPending} className={inputClass} value={price} onChange={e => setPrice(e.target.value)} /></label><button disabled={save.isPending} className={buttonClass}>Lưu giá cho đơn mới</button><MutationError mutation={save} />{save.isSuccess && <p role="status">Đã lưu giá gói.</p>}</Form></Panel>
    <ReceivingAccount />
    <section className="space-y-4"><div className="flex flex-wrap justify-between gap-3"><h2 className="text-xl font-bold">Đơn mua và gia hạn</h2><label className="text-sm">Trạng thái<Select className={inputClass} value={status} onChange={e => { setStatus(e.target.value); setPage(1); }}><option value="">Tất cả</option><option value="PENDING">Chờ thanh toán</option><option value="PAID">Đã kích hoạt</option><option value="CANCELLED">Đã đóng</option></Select></label></div>
      <QueryState query={orders}><div className="overflow-x-auto rounded-2xl border border-d-border-subtle bg-white"><table className="min-w-[700px] w-full text-left text-sm"><thead><tr>{['Kho', 'Ngày lập', 'Số tiền', 'Trạng thái', 'Hạn được cộng', 'Thao tác'].map(x => <th key={x} className="p-4">{x}</th>)}</tr></thead><tbody>{orders.data?.items.map(o => <tr key={o.id} className="border-t border-d-border-subtle"><td className="p-4">{o.depotName}</td><td className="p-4">{formatDate(o.createdAt)}</td><td className="p-4">{formatCurrency(o.amount)}</td><td className="p-4">{({ PENDING: o.proofUrl ? 'Chờ đối soát' : 'Chờ thanh toán', PAID: 'Đã kích hoạt', CANCELLED: 'Đã đóng' })[o.status]}</td><td className="p-4">{o.periodRevokedAt ? 'Đã hoàn kỳ' : o.periodEnd ? formatDate(o.periodEnd) : 'Chưa có'}</td><td className="p-4">{o.status === 'PENDING' && <div className="flex flex-wrap gap-2">{o.payOsStarted ? <button disabled={sync.isPending} className="inline-flex items-center justify-center gap-2 rounded-full font-semibold transition active:scale-[.98] disabled:pointer-events-none disabled:opacity-50 outline-none focus-visible:ring-2 focus-visible:ring-d-primary/30 focus-visible:ring-offset-1 min-h-11 px-5 py-2 text-sm border border-d-border-subtle bg-white text-d-on-surface hover:bg-d-surface-container" onClick={() => sync.mutate(o.id)}>Kiểm tra PayOS</button> : o.proofUrl ? <button className={buttonClass} onClick={() => setSelected(o)}>Đối soát chứng từ</button> : <span>Chờ chủ kho thanh toán</span>}</div>}{refundKindsFor(o).length > 0 && <button type="button" className="mt-1 block min-h-9 text-xs font-semibold text-d-primary underline" onClick={() => setRefundOrder(o)}>Lập yêu cầu hoàn</button>}</td></tr>)}</tbody></table>{orders.data?.items.length === 0 && <p className="p-5">Chưa có đơn mua gói.</p>}</div><Pager page={page} setPage={setPage} total={orders.data?.totalCount} /></QueryState>
      {sync.isError && <p role="alert" className="text-sm text-d-error">{apiErrorMessage(sync.error)}</p>}
      {sync.isSuccess && <p role="status">Đã kiểm tra PayOS và cập nhật khi có đủ tiền.</p>}
    </section>
    <RefundsPanel />
    {refundOrder && <AdminRefundRequest order={refundOrder} onClose={() => setRefundOrder(null)} />}
    <p className="text-sm text-d-on-surface-variant">Phiếu mua gói chưa thay thế hóa đơn điện tử hợp pháp. Dữ liệu hoa hồng cũ giữ riêng để đối soát.</p>
    <button className="min-h-11 text-d-primary underline" onClick={onLegacy}>Xem lịch sử phí và hóa đơn cũ</button>
    {selected && <ApproveOrder order={selected} onClose={() => setSelected(null)} />}
  </div></AdminLayout>;
}
