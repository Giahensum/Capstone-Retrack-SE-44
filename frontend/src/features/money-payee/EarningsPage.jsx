import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { Wallet, Undo2, Hourglass, CircleDollarSign, Send, BadgeCheck, MinusCircle, CalendarClock, Landmark, ReceiptText, Info, Save, Pencil } from 'lucide-react';
import { Panel, StatCard, DataTable, Th, Td, TableMessage, Pagination, Field, inputClass, PrimaryButton, GhostButton } from '@/features/admin/components/AdminUI';
import { Badge, Notice } from '@/components/liquidation/LiquidationUI';
import { moneyPayeeApi } from './moneyPayeeApi';
import { vnd, fmtDateTime, fmtDate, apiError } from '@/features/money-admin/moneyLabels';
import { PayoutStatusChip, BankStatusChip, BankSelect, FieldError } from '@/features/money-admin/moneyUi';
import { payerRefundStatusMeta } from '@/features/money-admin/refundLabels';
import { validateBankForm, buildBankBody, bankHelpText, BANK_POLICY_TEXT, BANK_SAVED_TEXT } from './payeeBank';

const PAGE_SIZE = 10;

function Summary() {
  const q = useQuery({ queryKey: ['money-payee', 'earnings'], queryFn: moneyPayeeApi.getEarnings });
  const d = q.data;
  const v = (n) => (d ? vnd(n) : '—');
  return (
    <section aria-label="Tóm tắt thu nhập" className="flex flex-col gap-3">
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
        <StatCard icon={Hourglass} tone="amber" label="Đang giữ" value={v(d?.held)} loading={q.isLoading} hint="Chưa đến hạn chi" />
        <StatCard icon={CircleDollarSign} tone="teal" label="Sẵn sàng chi" value={v(d?.available)} loading={q.isLoading} hint="Sẽ vào kỳ chi tới" />
        <StatCard icon={Send} tone="blue" label="Đang chi" value={v(d?.inPayout)} loading={q.isLoading} hint="Đã vào kỳ chi, chờ hoàn tất" />
        <StatCard icon={BadgeCheck} tone="primary" label="Đã nhận" value={v(d?.paid)} loading={q.isLoading} hint="Tổng các kỳ đã chi" />
        <StatCard icon={MinusCircle} tone="red" label="Nợ mang sang" value={v(d?.carriedDebt)} loading={q.isLoading} hint="Được trừ vào kỳ chi sau" />
        <StatCard icon={CalendarClock} tone="slate" label="Lần mở chi tiếp theo" value={d ? (d.nextReleaseAt ? fmtDateTime(d.nextReleaseAt) : 'Chưa có') : '—'} loading={q.isLoading} />
      </div>
      {q.isError && <p role="alert" className="text-sm font-semibold text-d-error">{apiError(q.error, 'Không tải được thu nhập.')}</p>}
    </section>
  );
}

function History() {
  const [page, setPage] = useState(1);
  const q = useQuery({
    queryKey: ['money-payee', 'payouts', page],
    queryFn: () => moneyPayeeApi.listPayouts({ page, pageSize: PAGE_SIZE }),
    placeholderData: (p) => p,
  });
  const items = q.data?.items ?? [];
  return (
    <Panel icon={ReceiptText} title="Lịch sử chi trả">
      <DataTable minWidth={900} head={<><Th>Mã kỳ</Th><Th>Kỳ</Th><Th className="text-right">Tổng gộp</Th><Th className="text-right">Hoa hồng</Th><Th className="text-right">Hoàn tiền</Th><Th className="text-right">Nợ trừ</Th><Th className="text-right">Thực nhận</Th><Th>Trạng thái</Th></>}>
        <TableMessage cols={8} loading={q.isLoading} error={q.isError} empty={!q.isLoading && !q.isError && items.length === 0}>
          {q.isError ? apiError(q.error, 'Không tải được lịch sử chi trả.') : 'Chưa có kỳ chi trả nào.'}
        </TableMessage>
        {items.map((r) => (
          <tr key={r.id} className="align-top">
            <Td className="font-mono text-xs">{r.code}</Td>
            <Td>{fmtDate(r.periodStart)} đến {fmtDate(r.periodEnd)}</Td>
            <Td className="text-right">{vnd(r.gross)}</Td>
            <Td className="text-right">{vnd(r.commission)}</Td>
            <Td className="text-right">{vnd(r.refundAmount)}</Td>
            <Td className="text-right">{vnd(r.carriedDebt)}</Td>
            <Td className="text-right font-bold">{vnd(r.net)}</Td>
            <Td>
              <PayoutStatusChip status={r.status} />
              {r.statusNote && <p className="mt-1 max-w-xs text-xs text-d-on-surface-variant">{r.statusNote}</p>}
              {r.paidAt && <p className="mt-1 text-xs text-d-on-surface-variant">Chi lúc {fmtDateTime(r.paidAt)}</p>}
            </Td>
          </tr>
        ))}
      </DataTable>
      <p className="border-t border-d-border-subtle px-5 py-3 text-xs text-d-on-surface-variant">Thực nhận = tổng gộp − hoa hồng − hoàn tiền − nợ mang sang.</p>
      {q.data && <Pagination page={page} pageSize={q.data.pageSize} totalCount={q.data.totalCount} onPageChange={setPage} />}
    </Panel>
  );
}

function BankCard() {
  const qc = useQueryClient();
  const bankQ = useQuery({ queryKey: ['money-payee', 'bank'], queryFn: moneyPayeeApi.getBank });
  const banksQ = useQuery({ queryKey: ['money-payee', 'banks'], queryFn: moneyPayeeApi.getBanks, staleTime: 3600_000 });
  const [editing, setEditing] = useState(false);
  const [form, setForm] = useState({ bankBin: '', accountNumber: '', accountName: '' });
  const [errors, setErrors] = useState({});
  const [err, setErr] = useState('');
  const bank = bankQ.data;
  const hasBank = !!bank?.accountNumberMasked;

  const save = useMutation({
    mutationFn: () => moneyPayeeApi.updateBank(buildBankBody(form, banksQ.data ?? [])),
    onSuccess: (data) => {
      qc.setQueryData(['money-payee', 'bank'], data);
      setEditing(false); setErr('');
      toast.success(BANK_SAVED_TEXT);
    },
    onError: (e) => setErr(apiError(e, 'Không lưu được tài khoản.')),
  });
  const startEdit = () => {
    setForm({ bankBin: bank?.bankBin ?? '', accountNumber: '', accountName: bank?.accountName ?? '' });
    setErrors({}); setErr(''); setEditing(true);
  };
  const submit = (e) => {
    e.preventDefault();
    const errs = validateBankForm(form);
    setErrors(errs);
    if (Object.keys(errs).length === 0) save.mutate();
  };

  return (
    <Panel icon={Landmark} title="Tài khoản nhận tiền"
      toolbar={!editing && !bankQ.isLoading && !bankQ.isError && <GhostButton onClick={startEdit}><Pencil size={14} className="mr-1 inline" aria-hidden="true" />{hasBank ? 'Đổi tài khoản' : 'Nhập tài khoản'}</GhostButton>}>
      <div className="flex flex-col gap-4 p-5">
        {bankQ.isLoading && <p role="status" className="text-sm text-d-on-surface-variant">Đang tải…</p>}
        {bankQ.isError && <p role="alert" className="text-sm font-semibold text-d-error">{apiError(bankQ.error, 'Không tải được tài khoản ngân hàng.')}</p>}
        {bank && !editing && (
          <>
            <div className="flex flex-wrap items-center gap-3">
              <BankStatusChip status={hasBank ? bank.status : null} />
              <span className="text-sm text-d-on-surface-variant">{bankHelpText(bank)}</span>
            </div>
            {hasBank && (
              <dl className="grid gap-3 text-sm sm:grid-cols-3">
                <div><dt className="text-xs font-semibold text-d-on-surface-variant">Ngân hàng</dt><dd className="font-semibold">{bank.bankName ?? '—'}</dd></div>
                <div><dt className="text-xs font-semibold text-d-on-surface-variant">Số tài khoản</dt><dd className="font-mono font-semibold">{bank.accountNumberMasked}</dd></div>
                <div><dt className="text-xs font-semibold text-d-on-surface-variant">Chủ tài khoản</dt><dd className="font-semibold">{bank.accountName ?? '—'}</dd></div>
              </dl>
            )}
            {bank.status === 'REJECTED' && bank.rejectReason && <Notice tone="red" icon={Info}>Lý do từ chối: {bank.rejectReason}</Notice>}
            <p className="text-xs text-d-on-surface-variant">{BANK_POLICY_TEXT}</p>
          </>
        )}
        {editing && (
          <form onSubmit={submit} className="grid gap-5 md:grid-cols-2" noValidate>
            <BankSelect banks={banksQ.data ?? []} loading={banksQ.isLoading} value={form.bankBin} disabled={save.isPending}
              error={errors.bankBin || (banksQ.isError ? 'Không tải được danh sách ngân hàng.' : '')} onChange={(bankBin) => setForm((f) => ({ ...f, bankBin }))} />
            <div className="flex flex-col gap-5">
              <Field label="Số tài khoản">
                <input value={form.accountNumber} onChange={(e) => setForm((f) => ({ ...f, accountNumber: e.target.value }))} inputMode="numeric" autoComplete="off" disabled={save.isPending} aria-invalid={!!errors.accountNumber} className={inputClass} />
                <FieldError>{errors.accountNumber}</FieldError>
              </Field>
              <Field label="Tên chủ tài khoản">
                <input value={form.accountName} onChange={(e) => setForm((f) => ({ ...f, accountName: e.target.value }))} autoComplete="off" disabled={save.isPending} aria-invalid={!!errors.accountName} className={inputClass} />
                <FieldError>{errors.accountName}</FieldError>
              </Field>
            </div>
            <div className="md:col-span-2"><Notice icon={Info} tone="blue">
              {BANK_POLICY_TEXT} Hãy nhập đúng tên chủ tài khoản như trên ngân hàng để Admin duyệt nhanh hơn.
            </Notice></div>
            {err && <div className="md:col-span-2"><p role="alert" className="text-sm font-semibold text-d-error">{err}</p></div>}
            <div className="flex flex-wrap gap-2 md:col-span-2">
              <PrimaryButton type="submit" icon={Save} disabled={save.isPending}>{save.isPending ? 'Đang lưu…' : 'Lưu tài khoản'}</PrimaryButton>
              <GhostButton onClick={() => setEditing(false)} disabled={save.isPending}>Hủy</GhostButton>
            </div>
          </form>
        )}
      </div>
    </Panel>
  );
}

/** Hoàn tiền cho tôi: chỉ hiện khi người dùng có ít nhất một lệnh hoàn. */
function MyRefunds() {
  const [page, setPage] = useState(1);
  const q = useQuery({
    queryKey: ['money-payee', 'refunds', page],
    queryFn: () => moneyPayeeApi.listMyRefunds({ page, pageSize: PAGE_SIZE }),
    placeholderData: (p) => p,
  });
  const items = q.data?.items ?? [];
  if (q.isLoading || q.isError || (q.data?.totalCount ?? 0) === 0) return null;
  return (
    <Panel icon={Undo2} title="Hoàn tiền cho tôi">
      <DataTable minWidth={640} head={<><Th>Mã</Th><Th className="text-right">Số tiền</Th><Th>Trạng thái</Th><Th>Tạo lúc</Th></>}>
        <TableMessage cols={4} empty={items.length === 0}>Chưa có khoản hoàn tiền nào.</TableMessage>
        {items.map((r) => {
          const m = payerRefundStatusMeta(r.status);
          return (
            <tr key={r.code} className="align-top">
              <Td className="font-mono text-xs">{r.code}</Td>
              <Td className="text-right font-bold">{vnd(r.amount)}</Td>
              <Td>
                <Badge tone={m.tone}>{m.label}</Badge>
                {r.statusNote && <p className="mt-1 max-w-xs text-xs text-d-on-surface-variant">{r.statusNote}</p>}
                {r.paidAt && <p className="mt-1 text-xs text-d-on-surface-variant">Hoàn lúc {fmtDateTime(r.paidAt)}</p>}
              </Td>
              <Td>{fmtDateTime(r.createdAt)}</Td>
            </tr>
          );
        })}
      </DataTable>
      {q.data && <Pagination page={page} pageSize={q.data.pageSize} totalCount={q.data.totalCount} onPageChange={setPage} />}
    </Panel>
  );
}

/** Trang Thu nhập dùng chung cho Seller, Chủ kho, Nhà máy. */
export default function EarningsPage() {
  return (
    <div className="mx-auto flex w-full max-w-6xl flex-col gap-6 p-4 md:p-6">
      <header className="flex items-start gap-3.5">
        <span className="flex size-12 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-[#446900] to-[#5c8a00] text-white shadow-md"><Wallet size={23} aria-hidden="true" /></span>
        <div>
          <h1 className="text-2xl font-black tracking-tight text-d-on-surface">Thu nhập</h1>
          <p className="mt-1 text-sm text-d-on-surface-variant">Tiền sàn đã thu hộ bạn, hoa hồng đã trừ và các kỳ chi trả về tài khoản ngân hàng.</p>
        </div>
      </header>
      <Summary />
      <BankCard />
      <History />
      <MyRefunds />
    </div>
  );
}
