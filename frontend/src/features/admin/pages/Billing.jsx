import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import AdminLayout from '@/components/layout/AdminLayout';
import { MaterialIcon } from '@/components/ui/MaterialIcon';
import Pagination from '@/components/ui/Pagination';
import { adminApi, apiErrorMessage } from '../api';
import { formatCurrency, formatDate } from '@/lib/utils';
import RevenueAreaChart from '../components/RevenueAreaChart';

const PAGE_SIZE = 10;
const TABS = [
  { key: 'revenue', label: 'Doanh thu', icon: 'trending_up' },
  { key: 'transactions', label: 'Giao dịch', icon: 'receipt_long' },
  { key: 'invoices', label: 'Hóa đơn phí', icon: 'request_quote' },
  { key: 'fee-config', label: 'Cấu hình phí', icon: 'tune' },
];

const inputClass = 'h-11 px-4 bg-white border border-d-outline-variant rounded-lg font-d-body-md text-d-body-md text-d-on-surface focus:border-d-primary focus:ring-1 focus:ring-d-primary transition-all shadow-sm outline-none';

function todayISO() { return new Date().toISOString().slice(0, 10); }
function monthAgoISO() { const d = new Date(); d.setMonth(d.getMonth() - 1); return d.toISOString().slice(0, 10); }
function endOfDayISO(dateOnly) { return new Date(`${dateOnly}T23:59:59.999Z`).toISOString(); }

function TabBar({ tab, setTab }) {
  return (
    <div className="flex gap-6 border-b border-d-border-subtle">
      {TABS.map(({ key, label, icon }) => (
        <button
          key={key}
          onClick={() => setTab(key)}
          className={`pb-3 px-1 font-d-label-md text-d-label-md border-b-2 transition-colors flex items-center gap-1.5 ${
            tab === key ? 'border-d-primary text-d-primary' : 'border-transparent text-d-on-surface-variant hover:text-d-on-surface'
          }`}
        >
          <MaterialIcon name={icon} className="text-[18px]" />
          {label}
        </button>
      ))}
    </div>
  );
}

// ── Revenue Tab ──────────────────────────────────────────────
function RevenueTab() {
  const [range, setRange] = useState({ from: monthAgoISO(), to: todayISO(), groupBy: 'day' });
  const { data, isLoading, isError } = useQuery({
    queryKey: ['admin', 'billing', 'revenue', range],
    queryFn: () => adminApi.getRevenue({ from: new Date(range.from).toISOString(), to: endOfDayISO(range.to), groupBy: range.groupBy }),
  });

  return (
    <div className="flex flex-col gap-6">
      <div className="bg-white border border-d-border-subtle rounded-[20px] shadow-sm p-6 flex flex-wrap items-end gap-4">
        <div className="flex flex-col gap-1.5">
          <label className="font-d-body-sm text-d-body-sm font-medium text-d-on-surface">Từ ngày</label>
          <input type="date" value={range.from} onChange={(e) => setRange((r) => ({ ...r, from: e.target.value }))} className={inputClass} />
        </div>
        <div className="flex flex-col gap-1.5">
          <label className="font-d-body-sm text-d-body-sm font-medium text-d-on-surface">Đến ngày</label>
          <input type="date" value={range.to} onChange={(e) => setRange((r) => ({ ...r, to: e.target.value }))} className={inputClass} />
        </div>
        <div className="flex flex-col gap-1.5">
          <label className="font-d-body-sm text-d-body-sm font-medium text-d-on-surface">Gộp theo</label>
          <select value={range.groupBy} onChange={(e) => setRange((r) => ({ ...r, groupBy: e.target.value }))} className={`${inputClass} cursor-pointer`}>
            <option value="day">Ngày</option>
            <option value="week">Tuần</option>
            <option value="month">Tháng</option>
          </select>
        </div>
      </div>

      <div className="bg-white border border-d-border-subtle rounded-[20px] shadow-sm p-6">
        <h3 className="font-d-headline-md text-d-headline-md text-d-on-background mb-1">Tổng doanh thu nền tảng</h3>
        {isLoading ? (
          <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant mt-4">Đang tải...</p>
        ) : isError || !data ? (
          <p className="font-d-body-sm text-d-body-sm text-d-error mt-4">Không tải được báo cáo doanh thu.</p>
        ) : (
          <>
            <p className="font-d-headline-xl text-d-headline-xl text-d-secondary my-3">{formatCurrency(data.totalRevenue)}</p>
            {data.points.length === 0 ? (
              <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant">Không có dữ liệu trong khoảng thời gian này.</p>
            ) : (
              <RevenueAreaChart points={data.points} height={280} />
            )}
          </>
        )}
      </div>
    </div>
  );
}

// ── Transactions Tab ─────────────────────────────────────────
function TransactionsTab() {
  const [page, setPage] = useState(1);
  const { data, isLoading } = useQuery({
    queryKey: ['admin', 'billing', 'transactions', page],
    queryFn: () => adminApi.getTransactions({ page, pageSize: PAGE_SIZE }),
    placeholderData: (prev) => prev,
  });

  return (
    <div className="bg-white border border-d-border-subtle rounded-[20px] shadow-sm overflow-hidden flex flex-col">
      <div className="overflow-x-auto w-full">
        <table className="w-full text-left border-collapse min-w-[700px]">
          <thead>
            <tr className="bg-d-surface-container-low text-d-on-surface-variant font-d-label-md text-d-label-md uppercase tracking-wider">
              <th className="px-6 py-4 font-medium">Loại</th>
              <th className="px-6 py-4 font-medium">Người trả phí</th>
              <th className="px-6 py-4 font-medium text-right">Phí</th>
              <th className="px-6 py-4 font-medium">Mô tả</th>
              <th className="px-6 py-4 font-medium">Thời gian</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-d-border-subtle font-d-body-sm text-d-body-sm text-d-on-surface">
            {isLoading && <tr><td colSpan={5} className="px-6 py-10 text-center text-d-on-surface-variant">Đang tải...</td></tr>}
            {!isLoading && data?.items.length === 0 && <tr><td colSpan={5} className="px-6 py-10 text-center text-d-on-surface-variant">Chưa có giao dịch nào.</td></tr>}
            {data?.items.map((t) => (
              <tr key={t.id} className="hover:bg-d-surface-accent/20 transition-colors">
                <td className="px-6 py-4">
                  <span className="bg-d-surface-variant/50 text-d-on-surface-variant px-2.5 py-1 rounded-md font-d-label-sm text-d-label-sm">
                    {t.sourceType === 'PICKUP_REQUEST' ? 'Thu gom' : 'Xuất lô'}
                  </span>
                </td>
                <td className="px-6 py-4 font-medium text-d-on-surface">
                  {t.payerName ?? <span className="text-d-on-surface-variant italic">Chưa xác định</span>}
                </td>
                <td className="px-6 py-4 text-right font-bold text-d-error">{formatCurrency(t.feeAmount)}</td>
                <td className="px-6 py-4 text-d-on-surface-variant">{t.description ?? '—'}</td>
                <td className="px-6 py-4 text-d-on-surface-variant">{formatDate(t.createdAt)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <Pagination page={page} pageSize={PAGE_SIZE} totalCount={data?.totalCount ?? 0} onPageChange={setPage} />
    </div>
  );
}

// ── Invoices Tab ─────────────────────────────────────────────
function InvoicesTab() {
  const queryClient = useQueryClient();
  const [status, setStatus] = useState('');
  const [page, setPage] = useState(1);
  const now = new Date();
  const [period, setPeriod] = useState({ year: now.getFullYear(), month: now.getMonth() + 1 });

  const { data, isLoading } = useQuery({
    queryKey: ['admin', 'billing', 'invoices', status, page],
    queryFn: () => adminApi.getInvoices({ status: status || undefined, page, pageSize: PAGE_SIZE }),
    placeholderData: (prev) => prev,
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['admin', 'billing', 'invoices'] });

  const generateMutation = useMutation({
    mutationFn: () => adminApi.generateInvoices(period),
    onSuccess: (created) => { toast.success(`Đã tạo ${created.length} hóa đơn cho ${period.month}/${period.year}.`); invalidate(); },
    onError: (err) => toast.error(apiErrorMessage(err, 'Không thể tạo hóa đơn.')),
  });

  const markPaidMutation = useMutation({
    mutationFn: (id) => adminApi.markInvoicePaid(id),
    onSuccess: () => { toast.success('Đã ghi nhận thanh toán.'); invalidate(); },
    onError: (err) => toast.error(apiErrorMessage(err, 'Không thể cập nhật.')),
  });

  const remindMutation = useMutation({
    mutationFn: (id) => adminApi.remindInvoice(id),
    onSuccess: () => toast.success('Đã gửi nhắc nhở thanh toán.'),
    onError: (err) => toast.error(apiErrorMessage(err, 'Không thể gửi nhắc nhở.')),
  });

  return (
    <div className="flex flex-col gap-6">
      <div className="bg-d-surface-container-high rounded-xl p-4 flex items-start gap-4 border border-d-border-subtle">
        <MaterialIcon name="info" className="text-d-secondary shrink-0 mt-0.5" />
        <div className="flex flex-wrap items-end gap-3">
          <div className="flex flex-col gap-1">
            <label className="font-d-label-sm text-d-label-sm text-d-on-surface-variant">Tháng</label>
            <input type="number" min="1" max="12" value={period.month} onChange={(e) => setPeriod((p) => ({ ...p, month: Number(e.target.value) }))} className={`${inputClass} w-20 h-9`} />
          </div>
          <div className="flex flex-col gap-1">
            <label className="font-d-label-sm text-d-label-sm text-d-on-surface-variant">Năm</label>
            <input type="number" value={period.year} onChange={(e) => setPeriod((p) => ({ ...p, year: Number(e.target.value) }))} className={`${inputClass} w-24 h-9`} />
          </div>
          <button onClick={() => generateMutation.mutate()} disabled={generateMutation.isPending} className="h-9 px-5 rounded-full bg-d-secondary text-white font-d-label-md text-d-label-md font-medium hover:opacity-90 transition-opacity shadow-sm disabled:opacity-60">
            Tạo hóa đơn
          </button>
          <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant">Gom phí theo từng chủ kho / nhà máy có phát sinh giao dịch trong tháng đã chọn.</p>
        </div>
      </div>

      <div className="bg-white border border-d-border-subtle rounded-[20px] shadow-sm overflow-hidden flex flex-col">
        <div className="p-6 border-b border-d-border-subtle flex items-center justify-between bg-d-surface-container-lowest">
          <h3 className="font-d-headline-md text-d-headline-md text-d-on-background">Danh sách hóa đơn</h3>
          <select value={status} onChange={(e) => { setStatus(e.target.value); setPage(1); }} className={`${inputClass} h-9 cursor-pointer`}>
            <option value="">Tất cả</option>
            <option value="PENDING">Chưa thanh toán</option>
            <option value="PAID">Đã thanh toán</option>
          </select>
        </div>
        <div className="overflow-x-auto w-full">
          <table className="w-full text-left border-collapse min-w-[800px]">
            <thead>
              <tr className="bg-d-surface-container-low text-d-on-surface-variant font-d-label-md text-d-label-md uppercase tracking-wider">
                <th className="px-6 py-4 font-medium">Người nhận hóa đơn</th>
                <th className="px-6 py-4 font-medium">Kỳ</th>
                <th className="px-6 py-4 font-medium text-right">Số tiền</th>
                <th className="px-6 py-4 font-medium">Trạng thái</th>
                <th className="px-6 py-4 font-medium">Ngày thanh toán</th>
                <th className="px-6 py-4 font-medium text-right">Thao tác</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-d-border-subtle font-d-body-sm text-d-body-sm text-d-on-surface">
              {isLoading && <tr><td colSpan={6} className="px-6 py-10 text-center text-d-on-surface-variant">Đang tải...</td></tr>}
              {!isLoading && data?.items.length === 0 && <tr><td colSpan={6} className="px-6 py-10 text-center text-d-on-surface-variant">Chưa có hóa đơn nào.</td></tr>}
              {data?.items.map((inv) => (
                <tr key={inv.id} className={`hover:bg-d-surface-container-lowest transition-colors ${inv.status !== 'PAID' ? 'bg-d-error-container/10' : ''}`}>
                  <td className="px-6 py-4 font-medium text-d-on-background">{inv.payerName}</td>
                  <td className="px-6 py-4 text-d-on-surface-variant">{inv.periodMonth}/{inv.periodYear}</td>
                  <td className={`px-6 py-4 text-right font-bold ${inv.status !== 'PAID' ? 'text-d-error' : 'text-d-on-background'}`}>{formatCurrency(inv.totalFeeAmount)}</td>
                  <td className="px-6 py-4">
                    {inv.status === 'PAID' ? (
                      <span className="bg-d-surface-accent text-d-secondary border border-d-secondary-container px-3 py-1 rounded-full font-d-label-sm text-d-label-sm flex items-center w-max">
                        <MaterialIcon name="check_circle" className="text-[14px] mr-1" />Đã thanh toán
                      </span>
                    ) : (
                      <span className="bg-[#FFF8E1] text-[#F57F17] border border-[#FFECB3] px-3 py-1 rounded-full font-d-label-sm text-d-label-sm flex items-center w-max">
                        <MaterialIcon name="warning" className="text-[14px] mr-1" />Chờ thanh toán
                      </span>
                    )}
                  </td>
                  <td className="px-6 py-4 text-d-on-surface-variant">{inv.paidAt ? formatDate(inv.paidAt) : '-'}</td>
                  <td className="px-6 py-4 text-right">
                    {inv.status !== 'PAID' ? (
                      <div className="flex items-center justify-end gap-2">
                        <button onClick={() => remindMutation.mutate(inv.id)} title="Gửi nhắc nhở" className="p-1.5 text-d-on-surface-variant hover:text-[#F57F17] rounded-full hover:bg-d-surface-variant transition-colors">
                          <MaterialIcon name="notifications_active" className="text-[20px]" />
                        </button>
                        <button onClick={() => markPaidMutation.mutate(inv.id)} className="bg-d-secondary text-white px-4 py-2 rounded-full font-d-body-sm text-d-body-sm font-medium hover:opacity-90 transition-opacity shadow-sm inline-flex items-center gap-1">
                          <MaterialIcon name="payment" className="text-[16px]" />
                          Đã thanh toán
                        </button>
                      </div>
                    ) : (
                      <span className="text-d-on-surface-variant font-d-label-sm text-d-label-sm">—</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <Pagination page={page} pageSize={PAGE_SIZE} totalCount={data?.totalCount ?? 0} onPageChange={setPage} />
      </div>
    </div>
  );
}

// ── Fee Config Tab ───────────────────────────────────────────
function FeeConfigTab() {
  const queryClient = useQueryClient();
  const { data, isLoading } = useQuery({ queryKey: ['admin', 'billing', 'fee-config'], queryFn: adminApi.getFeeConfig });
  const [value, setValue] = useState(null);

  const mutation = useMutation({
    mutationFn: (dto) => adminApi.updateFeeConfig(dto),
    onSuccess: () => { toast.success('Đã cập nhật phí nền tảng.'); queryClient.invalidateQueries({ queryKey: ['admin', 'billing', 'fee-config'] }); },
    onError: (err) => toast.error(apiErrorMessage(err, 'Không thể cập nhật.')),
  });

  const current = value ?? data?.platformFeePercentage ?? '';

  return (
    <div className="bg-white border border-d-border-subtle rounded-[20px] shadow-sm p-6 max-w-md">
      <h3 className="font-d-headline-md text-d-headline-md text-d-on-background mb-4">Cấu hình phí nền tảng</h3>
      {isLoading ? (
        <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant">Đang tải...</p>
      ) : (
        <form className="flex flex-col gap-4" onSubmit={(e) => { e.preventDefault(); mutation.mutate({ platformFeePercentage: Number(current) }); }}>
          <div className="flex flex-col gap-1.5">
            <label className="font-d-body-sm text-d-body-sm font-medium text-d-on-surface">Phí nền tảng (%)</label>
            <input type="number" min="0" max="100" step="0.1" value={current} onChange={(e) => setValue(e.target.value)} className={inputClass} />
            <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant">Áp dụng cho cả giao dịch thu gom (Seller↔Depot) và xuất lô (Depot↔Factory).</p>
          </div>
          <button type="submit" disabled={mutation.isPending} className="h-10 px-6 rounded-full bg-d-primary-fixed text-d-on-primary-fixed font-d-label-md text-d-label-md font-bold hover:-translate-y-0.5 transition-all shadow-sm self-start disabled:opacity-60">
            Lưu cấu hình
          </button>
        </form>
      )}
    </div>
  );
}

export default function Billing() {
  const [tab, setTab] = useState('revenue');

  return (
    <AdminLayout crumb="Phí & Doanh thu">
      <div className="p-4 md:p-6 w-full flex flex-col gap-8">
        <section>
          <h2 className="font-d-headline-xl text-d-headline-xl text-d-on-surface">Phí & Doanh thu</h2>
          <p className="font-d-body-md text-d-body-md text-d-on-surface-variant mt-2">Doanh thu nền tảng, lịch sử giao dịch, hóa đơn phí và cấu hình phí.</p>
        </section>

        <TabBar tab={tab} setTab={setTab} />

        {tab === 'revenue' && <RevenueTab />}
        {tab === 'transactions' && <TransactionsTab />}
        {tab === 'invoices' && <InvoicesTab />}
        {tab === 'fee-config' && <FeeConfigTab />}
      </div>
    </AdminLayout>
  );
}
