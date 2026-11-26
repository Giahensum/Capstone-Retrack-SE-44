import { Form, Input, Select } from '@/components/forms/ValidatedForm';
import { useState } from 'react';
import { useDepotMutation, useDepotQuery, money, date } from './depotApi';
import { buttonClass, inputClass, MutationError, Pager, QueryState } from './components/DepotUI';
import ProofUpload from './components/ProofUpload';
import PlatformFees from './PlatformFees';
import { Link } from 'react-router-dom';
import { PageHeader, Badge, buttonClass as kitButton } from '@/components/ui/kit';
import SubscriptionRefundForm from './components/SubscriptionRefundForm';
import { REFUND_KINDS, REFUND_STATUS, REFUND_TONE, refundKindsFor } from './subscriptionRefunds';

const labels = { ACTIVE: 'Đang sử dụng', TRIAL: 'Đang dùng thử', NOT_STARTED: 'Chưa bắt đầu', EXPIRED: 'Đã hết hạn', DISABLED: 'Chưa mở bán gói' };
const orderLabels = { PENDING: 'Chờ thanh toán', PAID: 'Đã kích hoạt', CANCELLED: 'Đã đóng' };
// Hoàn tiền gói: Chủ kho gửi yêu cầu, Admin duyệt rồi ghi nhận khi đã chuyển tiền thật.
function RefundRequest({ order, onClose }) {
  const save = useDepotMutation('post', 'subscription/refunds', onClose);
  return <SubscriptionRefundForm order={order} onClose={onClose} pending={save.isPending} error={<MutationError mutation={save} />}
    submit={(body) => save.mutate({ body })} />;
}
function Refunds() {
  const [page, setPage] = useState(1);
  const q = useDepotQuery('subscription/refunds', { page, pageSize: 10 });
  if (q.isSuccess && !q.data.totalCount) return null;
  return <section className="space-y-3"><h2 className="text-lg font-bold">Yêu cầu hoàn tiền gói</h2><QueryState query={q}>
    <div className="space-y-2">{q.data?.items.map(r => <article key={r.id} className="rounded-2xl border border-d-border-subtle bg-white p-4 text-sm shadow-sm">
      <div className="flex flex-wrap items-center justify-between gap-2"><strong>{REFUND_KINDS[r.kind]} · đơn #{r.orderCode}</strong><Badge tone={REFUND_TONE[r.status]}>{REFUND_STATUS[r.status]}</Badge></div>
      <p className="mt-1">Đề nghị {money(r.requestedAmount)}{r.approvedAmount != null && <> · duyệt {money(r.approvedAmount)}</>} · gửi {date(r.requestedAt)}</p>
      <p className="mt-1 text-d-on-surface-variant">{r.reason}</p>
      {r.decisionNote && <p className="mt-1">Admin: {r.decisionNote}</p>}
      {r.refundReference && <p className="mt-1 font-semibold text-d-primary">Đã chuyển hoàn {date(r.refundedAt)} · mã {r.refundReference}</p>}
      {r.periodRevoked && <p className="mt-1 text-xs">Kỳ của đơn này đã được rút khỏi hạn dùng.</p>}
    </article>)}</div>
    <Pager page={page} setPage={setPage} size={10} total={q.data?.totalCount} />
  </QueryState></section>;
}
function OrderPayment({ order, payOsAvailable }) {
  const [proof, setProof] = useState('');
  const [reference, setReference] = useState('');
  const [uploading, setUploading] = useState(false);
  const [confirmed, setConfirmed] = useState(false);
  const [method, setMethod] = useState(order.payOsStarted || payOsAvailable ? 'payos' : 'manual');
  const account = useDepotQuery('subscription/payment-account');
  const accountReady = !!(account.data?.bankName && account.data?.accountNumber && account.data?.accountHolder);
  const link = useDepotMutation('post', `subscription/orders/${order.id}/payos-link`);
  const sync = useDepotMutation('post', `subscription/orders/${order.id}/payos-sync`);
  const submit = useDepotMutation('post', `subscription/orders/${order.id}/proof`);
  const busy = link.isPending || sync.isPending || submit.isPending || uploading;
  const checkout = link.data?.data?.data?.checkoutUrl ?? order.checkoutUrl;
  const safeCheckout = (() => { try { return new URL(checkout).origin === 'https://pay.payos.vn'; } catch { return false; } })();
  return <div className="space-y-3 rounded-2xl border border-d-border-subtle bg-white p-4">
    <h3 className="font-semibold">Thanh toán một tháng: {money(order.amount)}</h3>
    <p className="text-sm text-d-on-surface-variant">Mã đơn: {order.id}. Thanh toán chỉ áp dụng cho kho này. Giá được giữ theo lúc lập đơn; không tự động trừ tiền tháng sau.</p>
    {order.reviewNote && <p role="status" className="rounded-xl bg-d-surface-accent p-3 text-sm">Cần bổ sung / đối soát: {order.reviewNote}</p>}
    {order.proofUrl ? <p role="status">Đã gửi chứng từ, chờ Admin kiểm tra tiền thực nhận. Chưa cần trả thêm tiền.</p> : <>
      <label className="block text-sm">Hình thức thanh toán gói<Select className={inputClass} disabled={busy || order.payOsStarted || !!checkout} value={method} onChange={e => setMethod(e.target.value)}><option value="payos">QR qua PayOS</option><option value="manual">Chuyển khoản và gửi chứng từ</option></Select></label>
      {method === 'payos' ? <div className="space-y-3">
        {!payOsAvailable && <p role="status" className="text-sm">PayOS chưa được cấu hình. Chọn chuyển khoản và gửi chứng từ nếu Admin đã cung cấp tài khoản nhận tiền.</p>}
        {!safeCheckout && <button type="button" className={buttonClass} disabled={busy || !payOsAvailable} onClick={() => link.mutate()}>{link.isPending ? 'Đang tạo…' : 'Tạo mã thanh toán gói'}</button>}
        {safeCheckout && <a className={buttonClass} href={checkout} target="_blank" rel="noopener noreferrer">Mở trang thanh toán PayOS</a>}
        {(order.payOsStarted || safeCheckout || link.isError) && <button type="button" className="inline-flex items-center justify-center gap-2 rounded-full font-semibold transition active:scale-[.98] disabled:pointer-events-none disabled:opacity-50 outline-none focus-visible:ring-2 focus-visible:ring-d-primary/30 focus-visible:ring-offset-1 min-h-11 px-5 py-2 text-sm border border-d-border-subtle bg-white text-d-on-surface hover:bg-d-surface-container" disabled={busy} onClick={() => sync.mutate()}>Kiểm tra đã thanh toán</button>}
        {sync.isSuccess && <p role="status">Đã kiểm tra: {orderLabels[sync.data?.data?.data] ?? 'Đang chờ kết quả thanh toán'}. Hạn gói chỉ cập nhật khi đã nhận đủ tiền.</p>}
      </div> : <ManualPaymentAccount account={account} />}
      {method === 'manual' && <Form className="space-y-3" onSubmit={e => { e.preventDefault(); submit.mutate({ body: { proofUrl: proof, reference } }); }}>
        <ProofUpload onUploaded={setProof} onBusyChange={setUploading} disabled={busy} />
        <label className="block text-sm">Đường dẫn ảnh chứng từ<Input required type="url" pattern="https://.+" data-pattern-message="Chứng từ mua gói phải dùng đường dẫn HTTPS, bắt đầu bằng https://." className={inputClass} value={proof} onChange={e => setProof(e.target.value)} disabled={busy} /></label>
        <label className="block text-sm">Mã giao dịch / nội dung chuyển khoản<Input required maxLength={255} className={inputClass} value={reference} onChange={e => setReference(e.target.value)} disabled={busy} /></label>
        <label className="flex gap-2 text-sm"><Input required data-field-label="Xác nhận đã chuyển tiền gói" type="checkbox" checked={confirmed} onChange={e => setConfirmed(e.target.checked)} disabled={busy} />Tôi đã chuyển đúng {money(order.amount)} cho đơn gói này và kiểm tra chứng từ.</label>
        <button disabled={busy || !accountReady} className={buttonClass}>Gửi chứng từ để đối soát</button>
      </Form>}
    </>}
    <MutationError mutation={link} /><MutationError mutation={sync} /><MutationError mutation={submit} />
  </div>;
}
function ManualPaymentAccount({ account }) {
  const a = account.data;
  return <QueryState query={account}>{a?.bankName && a?.accountNumber && a?.accountHolder
    ? <div className="rounded-xl bg-d-surface-accent p-3 text-sm"><p>Ngân hàng: <strong>{a.bankName}</strong></p><p>Số tài khoản: <strong>{a.accountNumber}</strong></p><p>Chủ tài khoản: <strong>{a.accountHolder}</strong></p><p>Ghi mã đơn gói vào nội dung chuyển khoản.</p></div>
    : <p role="status" className="text-sm text-d-error">Chưa có tài khoản nhận tiền dịch vụ. Liên hệ Admin trước khi chuyển khoản.</p>}</QueryState>;
}
export default function Subscription() {
  const [legacy, setLegacy] = useState(false);
  const [page, setPage] = useState(1);
  const [operationId, setOperationId] = useState(() => crypto.randomUUID());
  const [refundOrder, setRefundOrder] = useState(null);
  const q = useDepotQuery('subscription');
  const orders = useDepotQuery('subscription/orders', { page, pageSize: 20 });
  const trial = useDepotMutation('post', 'subscription/trial');
  const buy = useDepotMutation('post', 'subscription/orders', () => { setPage(1); setOperationId(crypto.randomUUID()); });
  const d = q.data;
  const pending = orders.data?.items.find(x => x.status === 'PENDING');
  const busy = trial.isPending || buy.isPending;
  if (legacy) return <><button type="button" className={kitButton({ variant: 'secondary', className: 'm-4' })} onClick={() => setLegacy(false)}>← Quay lại gói dịch vụ</button><PlatformFees /></>;
  return <div className="mx-auto w-full max-w-7xl space-y-6 p-4 md:p-6">
    <PageHeader title="Gói quản lý kho" description="Thanh toán cho phần mềm quản lý của kho đang chọn. RETRACK không thu hoa hồng giao dịch mới; tiền hàng do các bên tự thanh toán." />
    <QueryState query={q}>{d && <section className="rounded-2xl border border-d-border-subtle bg-white p-5 shadow-sm space-y-4">
      <div className="flex flex-wrap justify-between gap-4"><div><h2 className="text-lg font-bold">RETRACK Quản lý kho</h2><p className="mt-2 text-2xl font-bold text-d-primary">{money(d.monthlyPrice)} <span className="text-base font-normal">/ kho / tháng</span></p></div><span className="self-start rounded-full bg-d-surface-accent px-4 py-2 font-semibold">{labels[d.status]}</span></div>
      <p>Thu mua, nhập–xuất–tồn, đồ tái sử dụng, chi phí, báo cáo và quản lý đội ngũ. Seller, Factory, Employee và Driver không mua gói riêng.</p>
      <p className="text-sm">Hạn trả phí: <strong>{d.paidUntil ? date(d.paidUntil) : 'Chưa có'}</strong>{d.trialEndsAt && <> · Hạn dùng thử: <strong>{date(d.trialEndsAt)}</strong></>}</p>
      <p className="text-sm text-d-on-surface-variant">Gia hạn cộng một tháng lịch vào hạn còn lại; gói hết hạn thì tính từ ngày kích hoạt. Không tự gia hạn. Hết hạn vẫn xem dữ liệu, đối soát và hoàn tất giao dịch đang có; cần gói để mở nghiệp vụ mới. Hồ sơ kho vẫn cần Admin xác minh.</p>
      <div className="flex flex-wrap gap-3">{d.canStartTrial && <button className={buttonClass} disabled={busy} onClick={() => trial.mutate()}>Dùng thử {d.trialDays} ngày</button>}
        {d.status !== 'DISABLED' && <button className={buttonClass} disabled={busy || orders.isPending || orders.isError || !!pending} onClick={() => buy.mutate({ body: { operationId } })}>{buy.isPending ? 'Đang lập đơn…' : pending ? (pending.proofUrl ? 'Đang chờ đối soát gói' : 'Đang có đơn chờ thanh toán') : d.hasAccess ? 'Gia hạn một tháng' : 'Mua gói một tháng'}</button>}</div>
      <MutationError mutation={trial} /><MutationError mutation={buy} />
    </section>}</QueryState>
    <QueryState query={orders}>
      {pending && <OrderPayment key={pending.id} order={pending} payOsAvailable={d?.payOsAvailable} />}
      <section className="space-y-3"><h2 className="text-lg font-bold">Lịch sử mua và gia hạn</h2><div className="overflow-x-auto rounded-2xl border border-d-border-subtle bg-white shadow-sm"><table className="w-full min-w-[560px] text-left text-sm"><thead><tr>{['Mã đơn', 'Ngày lập', 'Số tiền', 'Trạng thái', 'Thời hạn được cộng', 'Thao tác'].map(x => <th key={x} className="p-4">{x}</th>)}</tr></thead><tbody>{orders.data?.items.map(o => <tr key={o.id} className="border-t border-d-border-subtle"><td className="p-4">#{o.orderCode}</td><td className="p-4">{date(o.createdAt)}</td><td className="p-4">{money(o.amount)}</td><td className="p-4">{o.status === 'PENDING' && o.proofUrl ? 'Chờ đối soát' : orderLabels[o.status] ?? o.status}</td><td className="p-4">{o.periodRevokedAt ? 'Đã hoàn kỳ này' : o.periodStart && o.periodEnd ? `${date(o.periodStart)} – ${date(o.periodEnd)}` : 'Chưa kích hoạt'}</td><td className="p-4">{refundKindsFor(o).length > 0 && <button type="button" className="min-h-11 text-sm font-semibold text-d-primary underline" onClick={() => setRefundOrder(o)}>Yêu cầu hoàn tiền</button>}</td></tr>)}</tbody></table>{orders.data?.items.length === 0 && <p className="p-5">Chưa có đơn mua gói.</p>}</div><Pager page={page} setPage={setPage} total={orders.data?.totalCount} /></section>
    </QueryState>
    <Refunds />
    {refundOrder && <RefundRequest order={refundOrder} onClose={() => setRefundOrder(null)} />}
    <p className="text-sm text-d-on-surface-variant">Lịch sử mua gói là ghi nhận dịch vụ, chưa thay thế hóa đơn điện tử hợp pháp. Hoàn tiền gói tách riêng với tiền hàng; tiền hàng do các bên tự thanh toán và hoàn trả.</p>
    <button className="min-h-11 text-sm underline text-d-primary" onClick={() => setLegacy(true)}>Xem phí giao dịch và hóa đơn cũ</button>
    <p><Link className="min-h-11 inline-flex items-center text-sm underline text-d-primary" to="/depot/earnings">Đối soát tiền hàng đã thu/chi qua sàn trước đây</Link></p>
  </div>;
}
