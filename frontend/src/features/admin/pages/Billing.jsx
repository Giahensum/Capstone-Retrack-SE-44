import { Form, Input, Select } from '@/components/forms/ValidatedForm';
import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import {
  Banknote, TrendingUp, Receipt, FileText, SlidersHorizontal, CalendarRange, CheckCircle2, AlertTriangle, BellRing, BadgeCheck,
  Info, Percent, Save, ExternalLink, FilePlus2, Wallet,
} from 'lucide-react';
import AdminLayout from '@/components/layout/AdminLayout';
import { adminApi, apiErrorMessage } from '../api';
import { previousClosedBillingMonth, isClosedBillingPeriod } from '../billingPeriod';
import { formatCurrency, formatDate } from '@/lib/utils';
import RevenueAreaChart from '../components/RevenueAreaChart';
import SubscriptionBilling from './SubscriptionBilling';
import {
  PageHeader, FilterTabs, Panel, StatCard, DataTable, Th, Td, TableMessage, Field, Badge, ConfirmModal, PrimaryButton, Pagination, inputClass,
} from '../components/AdminUI';

const PAGE_SIZE = 10;
const TABS = [
  { key: 'revenue', label: 'Doanh thu', icon: TrendingUp },
  { key: 'transactions', label: 'Giao dịch', icon: Receipt },
  { key: 'invoices', label: 'Hóa đơn phí', icon: FileText },
  { key: 'fee-config', label: 'Cấu hình phí', icon: SlidersHorizontal },
];

function todayISO() { return new Date().toISOString().slice(0, 10); }
function monthAgoISO() { const d = new Date(); d.setMonth(d.getMonth() - 1); return d.toISOString().slice(0, 10); }
function endOfDayISO(dateOnly) { return new Date(`${dateOnly}T23:59:59.999Z`).toISOString(); }

// ── Doanh thu ──────────────────────────────────────────────
function RevenueTab() {
  const [range, setRange] = useState({ from: monthAgoISO(), to: todayISO(), groupBy: 'day' });
  const { data, isLoading, isError } = useQuery({
    queryKey: ['admin', 'billing', 'revenue', range],
    queryFn: () => adminApi.getRevenue({ from: new Date(range.from).toISOString(), to: endOfDayISO(range.to), groupBy: range.groupBy }),
  });

  return (
    <div className="flex flex-col gap-6">
      <Panel icon={CalendarRange} title="Khoảng thời gian">
        <div className="flex flex-wrap items-end gap-4 p-5">
          <Field label="Từ ngày"><Input type="date" value={range.from} onChange={(e) => setRange((r) => ({ ...r, from: e.target.value }))} className={`${inputClass} w-44`} /></Field>
          <Field label="Đến ngày"><Input type="date" value={range.to} onChange={(e) => setRange((r) => ({ ...r, to: e.target.value }))} className={`${inputClass} w-44`} /></Field>
          <Field label="Gộp theo">
            <Select value={range.groupBy} onChange={(e) => setRange((r) => ({ ...r, groupBy: e.target.value }))} className={`${inputClass} w-36 cursor-pointer`}>
              <option value="day">Ngày</option><option value="week">Tuần</option><option value="month">Tháng</option>
            </Select>
          </Field>
        </div>
      </Panel>

      <StatCard icon={Wallet} tone="lime" label="Tổng doanh thu nền tảng" value={data ? formatCurrency(data.totalRevenue) : '—'} loading={isLoading} hint={data ? `${data.points.length} mốc dữ liệu` : undefined} />

      <Panel icon={TrendingUp} title="Biểu đồ doanh thu">
        <div className="p-5">
          {isLoading ? <div className="h-[280px] animate-pulse rounded-2xl bg-d-surface-container" />
            : isError || !data ? <p className="text-sm text-d-error">Không tải được báo cáo doanh thu.</p>
              : data.points.length === 0 ? <p className="py-10 text-center text-sm text-d-on-surface-variant">Không có dữ liệu trong khoảng thời gian này.</p>
                : <RevenueAreaChart points={data.points} height={280} />}
        </div>
      </Panel>
    </div>
  );
}

// ── Giao dịch ──────────────────────────────────────────────
function TransactionsTab() {
  const [page, setPage] = useState(1);
  const { data, isLoading } = useQuery({
    queryKey: ['admin', 'billing', 'transactions', page],
    queryFn: () => adminApi.getTransactions({ page, pageSize: PAGE_SIZE }),
    placeholderData: (prev) => prev,
  });
  const items = data?.items ?? [];

  return (
    <Panel icon={Receipt} title="Lịch sử giao dịch phí">
      <DataTable minWidth={700} head={<><Th>Loại</Th><Th>Người nộp phí</Th><Th className="text-right">Phí</Th><Th>Mô tả</Th><Th>Thời gian</Th></>}>
        <TableMessage cols={5} loading={isLoading && !data} empty={!isLoading && items.length === 0} icon={Receipt}>Chưa có giao dịch nào.</TableMessage>
        {items.map((t) => (
          <tr key={t.id} className="transition-colors hover:bg-d-surface-accent/40">
            <Td><Badge tone={t.sourceType === 'PICKUP_REQUEST' ? 'teal' : 'blue'}>{t.sourceType === 'PICKUP_REQUEST' ? 'Thu gom' : 'Xuất lô'}</Badge></Td>
            <Td className="font-semibold">{t.payerName ?? <span className="italic text-d-on-surface-variant">Chưa xác định</span>}</Td>
            <Td className="text-right font-black text-d-error">{formatCurrency(t.feeAmount)}<p className="text-xs font-normal text-d-on-surface-variant">{t.feePolicy === "BUYER_SEPARATE_V1" ? "Bên mua chịu riêng" : "Khấu trừ bên bán — đơn cũ"}</p></Td>
            <Td className="text-d-on-surface-variant">{t.description ?? '—'}</Td>
            <Td className="whitespace-nowrap text-d-on-surface-variant">{formatDate(t.createdAt)}</Td>
          </tr>
        ))}
      </DataTable>
      <Pagination page={page} pageSize={PAGE_SIZE} totalCount={data?.totalCount ?? 0} onPageChange={setPage} />
    </Panel>
  );
}

// ── Hóa đơn ────────────────────────────────────────────────
const INVOICE_FILTERS = [
  { key: '', label: 'Tất cả' }, { key: 'PENDING', label: 'Chưa thanh toán' }, { key: 'SUBMITTED', label: 'Chờ đối soát' }, { key: 'PAID', label: 'Đã thanh toán' },
];

function InvoicesTab() {
  const queryClient = useQueryClient();
  const [status, setStatus] = useState('');
  const [page, setPage] = useState(1);
  const [payTarget, setPayTarget] = useState(null);
  const [period, setPeriod] = useState(previousClosedBillingMonth);
  const canIssue = isClosedBillingPeriod(period);

  const { data, isLoading } = useQuery({
    queryKey: ['admin', 'billing', 'invoices', status, page],
    queryFn: () => adminApi.getInvoices({ status: status || undefined, page, pageSize: PAGE_SIZE }),
    placeholderData: (prev) => prev,
  });
  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['admin', 'billing', 'invoices'] });

  const generateMutation = useMutation({
    mutationFn: () => adminApi.generateInvoices(period),
    onSuccess: (created) => { toast.success(`Đã phát hành/đối soát ${created.length} hóa đơn cho ${period.month}/${period.year}.`); invalidate(); },
    onError: (err) => toast.error(apiErrorMessage(err, 'Không thể tạo hóa đơn.')),
  });
  const markPaidMutation = useMutation({
    mutationFn: (id) => adminApi.markInvoicePaid(id),
    onSuccess: () => { toast.success('Đã ghi nhận thanh toán.'); setPayTarget(null); invalidate(); },
    onError: (err) => { toast.error(apiErrorMessage(err, 'Không thể cập nhật.')); setPayTarget(null); },
  });
  const remindMutation = useMutation({
    mutationFn: (id) => adminApi.remindInvoice(id),
    onSuccess: () => toast.success('Đã gửi nhắc nhở thanh toán.'),
    onError: (err) => toast.error(apiErrorMessage(err, 'Không thể gửi nhắc nhở.')),
  });
  const items = data?.items ?? [];

  return (
    <div className="flex flex-col gap-6">
      <Panel icon={FilePlus2} title="Phát hành và đối soát tháng đã chốt">
        <div className="flex flex-wrap items-end gap-4 p-5">
          <Field label="Tháng"><Input type="number" min="1" max="12" value={period.month} onChange={(e) => setPeriod((p) => ({ ...p, month: Number(e.target.value) }))} className={`${inputClass} w-24`} /></Field>
          <Field label="Năm"><Input type="number" value={period.year} onChange={(e) => setPeriod((p) => ({ ...p, year: Number(e.target.value) }))} className={`${inputClass} w-28`} /></Field>
          <PrimaryButton icon={FilePlus2} disabled={generateMutation.isPending || !canIssue} onClick={() => generateMutation.mutate()}>{generateMutation.isPending ? 'Đang đối soát…' : 'Phát hành / đối soát'}</PrimaryButton>
          <p className="flex max-w-md items-start gap-2 text-xs text-d-on-surface-variant"><Info size={15} className="mt-0.5 shrink-0 text-d-secondary" />Chỉ phát hành khi tháng đã kết thúc theo giờ Việt Nam. Phí tháng hiện tại là tạm tính. Chạy lại sẽ đối soát hóa đơn chưa có giao dịch thanh toán; hóa đơn đã gửi chứng từ/đã trả được giữ nguyên, khoản thiếu phát hành bổ sung.</p>
          {!canIssue && <p role="status" className="text-sm text-d-error">Kỳ chưa kết thúc hoặc không hợp lệ; chưa thể phát hành hóa đơn.</p>}
        </div>
      </Panel>

      <FilterTabs tabs={INVOICE_FILTERS} value={status} onChange={(v) => { setStatus(v); setPage(1); }} />

      <Panel icon={FileText} title="Danh sách hóa đơn">
        <DataTable minWidth={820} head={<><Th>Người nhận hóa đơn</Th><Th>Kỳ</Th><Th className="text-right">Số tiền</Th><Th>Trạng thái</Th><Th>Ngày thanh toán</Th><Th className="text-right">Thao tác</Th></>}>
          <TableMessage cols={6} loading={isLoading && !data} empty={!isLoading && items.length === 0} icon={FileText}>Chưa có hóa đơn nào.</TableMessage>
          {items.map((inv) => {
            const paid = inv.status === 'PAID';
            return (
              <tr key={inv.id} className={`transition-colors hover:bg-d-surface-accent/40 ${paid ? '' : 'bg-amber-50/40'}`}>
                <Td className="font-semibold">{inv.payerName}</Td>
                <Td className="text-d-on-surface-variant">{inv.periodMonth}/{inv.periodYear}<p className="text-xs">{inv.revision > 0 ? `Bổ sung lần ${inv.revision}` : 'Hóa đơn gốc'}</p></Td>
                <Td className={`text-right font-black ${paid ? '' : 'text-d-error'}`}>{formatCurrency(inv.totalFeeAmount)}</Td>
                <Td>
                  {paid ? <Badge tone="primary" icon={CheckCircle2}>Đã thanh toán</Badge>
                    : <Badge tone="amber" icon={AlertTriangle}>{inv.status === 'SUBMITTED' ? 'Chờ đối soát' : 'Chờ thanh toán'}</Badge>}
                </Td>
                <Td className="text-d-on-surface-variant">
                  {inv.paidAt ? formatDate(inv.paidAt) : '—'}
                  {/^https?:\/\//i.test(inv.paymentProofUrl || '') && (
                    <a href={inv.paymentProofUrl} target="_blank" rel="noopener noreferrer" className="mt-0.5 flex items-center gap-1 text-xs font-semibold text-d-secondary underline"><ExternalLink size={12} />Xem chứng từ</a>
                  )}
                  {inv.paymentProofUrl?.startsWith('/api/factory/attachments/') && (
                    <button type="button" onClick={() => adminApi.downloadInvoiceProof(inv.paymentProofUrl).catch((error) => toast.error(apiErrorMessage(error, 'Không tải được chứng từ.')))} className="mt-0.5 flex items-center gap-1 text-xs font-semibold text-d-secondary underline"><ExternalLink size={12} />Tải chứng từ Factory</button>
                  )}
                </Td>
                <Td className="text-right">
                  {paid ? <span className="text-d-on-surface-variant">—</span> : (
                    <div className="flex items-center justify-end gap-2">
                      <button type="button" title="Gửi nhắc nhở" aria-label="Gửi nhắc nhở" disabled={remindMutation.isPending} onClick={() => remindMutation.mutate(inv.id)}
                        className="flex size-9 items-center justify-center rounded-full text-d-on-surface-variant transition hover:bg-amber-50 hover:text-amber-600 disabled:opacity-40"><BellRing size={18} /></button>
                      <button type="button" disabled={markPaidMutation.isPending} onClick={() => setPayTarget(inv)}
                        className="inline-flex items-center gap-1.5 rounded-full bg-d-secondary px-4 py-2 text-sm font-bold text-white shadow-sm transition hover:opacity-90 disabled:opacity-60"><BadgeCheck size={16} />Đã thanh toán</button>
                    </div>
                  )}
                </Td>
              </tr>
            );
          })}
        </DataTable>
        <Pagination page={page} pageSize={PAGE_SIZE} totalCount={data?.totalCount ?? 0} onPageChange={setPage} />
      </Panel>

      {payTarget && (
        <ConfirmModal icon={BadgeCheck} tone="teal" title="Ghi nhận hóa đơn đã thanh toán?" confirmLabel="Xác nhận đã thanh toán" cancelLabel="Chưa"
          description={`${payTarget.revision > 0 ? `Hóa đơn bổ sung lần ${payTarget.revision}` : 'Hóa đơn gốc'} kỳ ${payTarget.periodMonth}/${payTarget.periodYear} của ${payTarget.payerName} (${formatCurrency(payTarget.totalFeeAmount)}) sẽ được đánh dấu đã thanh toán. Chỉ xác nhận khi đã đối soát tiền thực nhận.`}
          busy={markPaidMutation.isPending} onCancel={() => setPayTarget(null)} onConfirm={() => markPaidMutation.mutate(payTarget.id)} />
      )}
    </div>
  );
}

// ── Cấu hình phí ───────────────────────────────────────────
function FeeConfigTab() {
  const queryClient = useQueryClient();
  const { data, isLoading } = useQuery({ queryKey: ['admin', 'billing', 'fee-config'], queryFn: adminApi.getFeeConfig });
  const account = useQuery({ queryKey: ['admin', 'billing', 'payment-account'], queryFn: adminApi.getPaymentAccount });
  const [value, setValue] = useState(null);
  const [accountDraft, setAccountDraft] = useState(null);

  const mutation = useMutation({
    mutationFn: (dto) => adminApi.updateFeeConfig(dto),
    onSuccess: () => { toast.success('Đã cập nhật phí nền tảng.'); queryClient.invalidateQueries({ queryKey: ['admin', 'billing', 'fee-config'] }); },
    onError: (err) => toast.error(apiErrorMessage(err, 'Không thể cập nhật.')),
  });
  const current = value ?? data?.platformFeePercentage ?? '';
  const paymentAccount = accountDraft ?? account.data ?? { bankName: '', accountNumber: '', accountHolder: '' };
  const accountMutation = useMutation({
    mutationFn: adminApi.updatePaymentAccount,
    onSuccess: () => { toast.success('Đã lưu tài khoản nhận phí.'); setAccountDraft(null); queryClient.invalidateQueries({ queryKey: ['admin', 'billing', 'payment-account'] }); },
    onError: (err) => toast.error(apiErrorMessage(err, 'Không thể lưu tài khoản nhận phí.')),
  });

  return (
    <div className="flex flex-wrap items-start gap-5">
    <Panel icon={Percent} title="Cấu hình phí nền tảng" className="max-w-lg">
      <div className="p-5">
        {isLoading ? <div className="h-24 animate-pulse rounded-2xl bg-d-surface-container" /> : (
          <Form className="flex flex-col gap-4" onSubmit={(e) => { e.preventDefault(); mutation.mutate({ platformFeePercentage: Number(current) }); }}>
            <Field label="Phí nền tảng (%)" hint="Đơn/lô mới chốt mức phí khi tạo. Bên mua chịu phí riêng: Depot mua Seller, Factory mua Depot. Không đổi mức phí trên giao dịch đã tạo.">
              <Input type="number" min="0" max="100" step="0.1" value={current} onChange={(e) => setValue(e.target.value)} className={inputClass} />
            </Field>
            <PrimaryButton icon={Save} type="submit" disabled={mutation.isPending} className="self-start">{mutation.isPending ? 'Đang lưu…' : 'Lưu cấu hình'}</PrimaryButton>
          </Form>
        )}
      </div>
    </Panel>
    <Panel icon={Banknote} title="Tài khoản nhận phí từ Factory" className="max-w-lg">
      <div className="p-5">
        {account.isLoading ? <div className="h-24 animate-pulse rounded-2xl bg-d-surface-container" /> : <Form className="flex flex-col gap-4" onSubmit={(event) => { event.preventDefault(); accountMutation.mutate(paymentAccount); }}>
          <p className="text-sm text-d-on-surface-variant">Factory sẽ thấy thông tin này trên hóa đơn phí tháng. Chỉ nhập tài khoản của đơn vị vận hành RETRACK.</p>
          <Field label="Ngân hàng"><Input required maxLength={100} className={inputClass} value={paymentAccount.bankName} onChange={(event) => setAccountDraft({ ...paymentAccount, bankName: event.target.value })} /></Field>
          <Field label="Số tài khoản"><Input required inputMode="numeric" pattern="[0-9]{4,30}" maxLength={30} className={inputClass} value={paymentAccount.accountNumber} onChange={(event) => setAccountDraft({ ...paymentAccount, accountNumber: event.target.value })} /></Field>
          <Field label="Tên chủ tài khoản"><Input required maxLength={120} className={inputClass} value={paymentAccount.accountHolder} onChange={(event) => setAccountDraft({ ...paymentAccount, accountHolder: event.target.value })} /></Field>
          <PrimaryButton icon={Save} type="submit" disabled={accountMutation.isPending} className="self-start">{accountMutation.isPending ? 'Đang lưu…' : 'Lưu tài khoản nhận phí'}</PrimaryButton>
        </Form>}
      </div>
    </Panel>
    </div>
  );
}

export default function Billing() {
  const [tab, setTab] = useState('revenue');
  const [legacy, setLegacy] = useState(false);
  if (!legacy) return <SubscriptionBilling onLegacy={() => setLegacy(true)} />;

  return (
    <AdminLayout crumb="Phí & Doanh thu">
      <div className="flex w-full flex-col gap-6 p-4 md:p-6">
        <button className="self-start inline-flex items-center justify-center gap-2 rounded-full font-semibold transition active:scale-[.98] disabled:pointer-events-none disabled:opacity-50 outline-none focus-visible:ring-2 focus-visible:ring-d-primary/30 focus-visible:ring-offset-1 min-h-11 px-5 py-2 text-sm border border-d-border-subtle bg-white text-d-on-surface hover:bg-d-surface-container" onClick={() => setLegacy(false)}>← Quay lại gói dịch vụ Depot</button>
        <PageHeader icon={Banknote} title="Phí & Doanh thu" description="Doanh thu nền tảng, lịch sử giao dịch, hóa đơn phí và cấu hình phí." />
        <FilterTabs tabs={TABS} value={tab} onChange={setTab} />
        {tab === 'revenue' && <RevenueTab />}
        {tab === 'transactions' && <TransactionsTab />}
        {tab === 'invoices' && <InvoicesTab />}
        {tab === 'fee-config' && <FeeConfigTab />}
      </div>
    </AdminLayout>
  );
}
