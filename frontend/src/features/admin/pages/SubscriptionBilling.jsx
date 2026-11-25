import { Form, Input, Select, Textarea } from '@/components/forms/ValidatedForm';
import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import api from '@/lib/axios';
import AdminLayout from '@/components/layout/AdminLayout';
import { adminApi, apiErrorMessage } from '../api';
import { formatCurrency, formatDate } from '@/lib/utils';
import { inputClass, Panel, PageHeader } from '../components/AdminUI';
import { Banknote, Settings } from 'lucide-react';
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
      <details><summary className="cursor-pointer min-h-11 py-3 text-d-primary">Chứng từ chưa hợp lệ / chưa nhận tiền</summary><label className="block text-sm">Lý do yêu cầu bổ sung<Textarea maxLength={1000} disabled={save.isPending || reject.isPending} className={inputClass} value={reason} onChange={e => setReason(e.target.value)} /></label><button type="button" className="mt-3 min-h-11 rounded-full border border-d-border-subtle px-4" disabled={save.isPending || reject.isPending || reason.trim().length < 5} onClick={() => reject.mutate()}>Yêu cầu bổ sung chứng từ</button><MutationError mutation={reject} /></details>
    </Form>
  </Dialog>;
}
export default function SubscriptionBilling({ onLegacy }) {
  const client = useQueryClient();
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState('');
  const [price, setPrice] = useState('');
  const [selected, setSelected] = useState(null);
  const summary = useQuery({ queryKey: ['admin', 'subscriptions', 'summary'], queryFn: async () => (await api.get('/admin/subscriptions/summary')).data.data });
  const orders = useQuery({ queryKey: ['admin', 'subscriptions', 'orders', page, status], queryFn: async () => (await api.get('/admin/subscriptions/orders', { params: { page, pageSize: 20, status } })).data.data });
  const save = useMutation({ mutationFn: () => api.put('/admin/subscriptions/price', { monthlyPrice: Number(price) }), onSuccess: () => client.invalidateQueries({ queryKey: ['admin', 'subscriptions'] }) });
  const sync = useMutation({ mutationFn: id => api.post(`/admin/subscriptions/orders/${id}/payos-sync`), onSuccess: () => client.invalidateQueries({ queryKey: ['admin', 'subscriptions'] }) });
  return <AdminLayout crumb="Gói dịch vụ Depot"><div className="w-full max-w-7xl mx-auto space-y-6 p-4 md:p-6">
    <PageHeader icon={Banknote} title="Gói dịch vụ Depot" description="Chỉ thu tiền phần mềm quản lý kho. Tiền mua bán hàng do các bên thanh toán trực tiếp, không tính vào doanh thu nền tảng." />
    <QueryState query={summary}>{summary.data && <div className="grid gap-4 sm:grid-cols-3">{[['Giá một kho / tháng', formatCurrency(summary.data.monthlyPrice)], ['Tiền gói đã thu', formatCurrency(summary.data.paidRevenue)], ['Chứng từ cần đối soát', summary.data.pendingProofCount]].map(([label, value]) => <section key={label} className="rounded-2xl border border-d-border-subtle bg-white p-5"><h2 className="text-sm text-d-on-surface-variant">{label}</h2><p className="mt-2 text-2xl font-bold">{value}</p></section>)}</div>}</QueryState>
    <Panel title="Giá dịch vụ" icon={Settings}><Form className="space-y-3 p-4" onSubmit={e => { e.preventDefault(); save.mutate(); }}><p className="text-sm">Một gói đầy đủ cho một kho, tháng lịch, không tự gia hạn. Thay đổi giá chỉ áp dụng cho đơn mới; các đơn đang chờ giữ nguyên số tiền.</p><label className="block text-sm">Giá tháng mới (đ)<Input required type="number" min="1000" max="10000000" step="1" disabled={save.isPending} className={inputClass} value={price} onChange={e => setPrice(e.target.value)} /></label><button disabled={save.isPending} className={buttonClass}>Lưu giá cho đơn mới</button><MutationError mutation={save} />{save.isSuccess && <p role="status">Đã lưu giá gói.</p>}</Form></Panel>
    <ReceivingAccount />
    <section className="space-y-4"><div className="flex flex-wrap justify-between gap-3"><h2 className="text-xl font-bold">Đơn mua và gia hạn</h2><label className="text-sm">Trạng thái<Select className={inputClass} value={status} onChange={e => { setStatus(e.target.value); setPage(1); }}><option value="">Tất cả</option><option value="PENDING">Chờ thanh toán</option><option value="PAID">Đã kích hoạt</option><option value="CANCELLED">Đã đóng</option></Select></label></div>
      <QueryState query={orders}><div className="overflow-x-auto rounded-2xl border border-d-border-subtle bg-white"><table className="min-w-[700px] w-full text-left text-sm"><thead><tr>{['Kho', 'Ngày lập', 'Số tiền', 'Trạng thái', 'Hạn được cộng', 'Thao tác'].map(x => <th key={x} className="p-4">{x}</th>)}</tr></thead><tbody>{orders.data?.items.map(o => <tr key={o.id} className="border-t border-d-border-subtle"><td className="p-4">{o.depotName}</td><td className="p-4">{formatDate(o.createdAt)}</td><td className="p-4">{formatCurrency(o.amount)}</td><td className="p-4">{({ PENDING: o.proofUrl ? 'Chờ đối soát' : 'Chờ thanh toán', PAID: 'Đã kích hoạt', CANCELLED: 'Đã đóng' })[o.status]}</td><td className="p-4">{o.periodEnd ? formatDate(o.periodEnd) : 'Chưa có'}</td><td className="p-4">{o.status === 'PENDING' && <div className="flex flex-wrap gap-2">{o.payOsStarted ? <button disabled={sync.isPending} className="min-h-11 rounded-full border border-d-border-subtle px-3" onClick={() => sync.mutate(o.id)}>Kiểm tra PayOS</button> : o.proofUrl ? <button className={buttonClass} onClick={() => setSelected(o)}>Đối soát chứng từ</button> : <span>Chờ chủ kho thanh toán</span>}</div>}</td></tr>)}</tbody></table>{orders.data?.items.length === 0 && <p className="p-5">Chưa có đơn mua gói.</p>}</div><Pager page={page} setPage={setPage} total={orders.data?.totalCount} /></QueryState>
      {sync.isError && <p role="alert" className="text-sm text-d-error">{apiErrorMessage(sync.error)}</p>}
      {sync.isSuccess && <p role="status">Đã kiểm tra PayOS và cập nhật khi có đủ tiền.</p>}
    </section>
    <p className="text-sm text-d-on-surface-variant">Phiếu mua gói chưa thay thế hóa đơn điện tử hợp pháp. Dữ liệu hoa hồng cũ giữ riêng để đối soát.</p>
    <button className="min-h-11 text-d-primary underline" onClick={onLegacy}>Xem lịch sử phí và hóa đơn cũ</button>
    {selected && <ApproveOrder order={selected} onClose={() => setSelected(null)} />}
  </div></AdminLayout>;
}
