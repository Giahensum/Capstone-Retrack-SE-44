import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { AlertTriangle, Wallet, RefreshCw, Send, ListChecks, Plus } from 'lucide-react';
import { ConfirmModal, Notice } from '@/components/liquidation/LiquidationUI';
import { Panel, Field, inputClass, PrimaryButton, GhostButton, DetailList } from '@/features/admin/components/AdminUI';
import { moneyAdminApi } from '../moneyAdminApi';
import { vnd, apiError, newClientKey, validateTestTransfer, PROVIDER_LABEL } from '../moneyLabels';
import { BankSelect, FieldError } from '../moneyUi';

const KEY_STORE = 'retrack_money_test_key';
const readStore = () => { try { return JSON.parse(sessionStorage.getItem(KEY_STORE) ?? 'null'); } catch { return null; } };
const writeStore = (v) => { try { if (v) sessionStorage.setItem(KEY_STORE, JSON.stringify(v)); else sessionStorage.removeItem(KEY_STORE); } catch { /* không có storage */ } };

const OUTCOME_TEXT = {
  ACCEPTED: { tone: 'blue', text: 'Nhà cung cấp đã nhận lệnh. Đang theo dõi trạng thái.' },
  FAILED: { tone: 'red', text: 'Lệnh bị từ chối rõ ràng, tiền chưa đi.' },
  UNKNOWN: { tone: 'amber', text: 'Chưa rõ lệnh đã đến ngân hàng chưa. KHÔNG tạo lệnh mới: bấm Gửi lại (cùng khóa) hoặc chờ theo dõi trạng thái.' },
};
const STATUS_TEXT = {
  PROCESSING: { tone: 'blue', text: 'Đang xử lý (kiểm tra lại mỗi 5 giây).' },
  SUCCEEDED: { tone: 'teal', text: 'Thành công: tiền đã chuyển.' },
  FAILED: { tone: 'red', text: 'Thất bại.' },
  NOT_FOUND: { tone: 'amber', text: 'Nhà cung cấp chưa biết khóa này (có thể lệnh chưa tới nơi).' },
};

export default function TestTransferTab({ settings }) {
  const qc = useQueryClient();
  const stored = readStore();
  const [clientKey, setClientKey] = useState(() => stored?.key ?? newClientKey());
  const [submitted, setSubmitted] = useState(() => !!stored?.submitted);
  const [form, setForm] = useState({ amount: '2000', bin: '', bankName: '', account: '' });
  const [errors, setErrors] = useState({});
  const [result, setResult] = useState(null);
  const [confirming, setConfirming] = useState(false);
  const [err, setErr] = useState('');

  const banksQ = useQuery({ queryKey: ['money-admin', 'banks'], queryFn: moneyAdminApi.getBanks, staleTime: 3600_000 });
  const balanceQ = useQuery({ queryKey: ['money-admin', 'balance'], queryFn: moneyAdminApi.getBalance, staleTime: 15_000 });

  const maxAmount = settings?.payoutTestMaxAmount ?? 5000;
  const enabled = !!settings?.payoutTestEnabled;
  const real = settings?.payoutProvider === 'PAYOS';

  const statusQ = useQuery({
    queryKey: ['money-admin', 'test-transfer', clientKey],
    queryFn: () => moneyAdminApi.testTransferStatus(clientKey),
    enabled: submitted,
    refetchInterval: (query) => (['SUCCEEDED', 'FAILED'].includes(query.state.data?.status) ? false : 5000),
  });
  const status = statusQ.data?.status;
  const terminal = status === 'SUCCEEDED' || status === 'FAILED';

  const send = useMutation({
    mutationFn: () => moneyAdminApi.testTransfer({ clientKey, amount: Number(form.amount), toBin: form.bin, toAccountNumber: form.account.trim() }),
    onSuccess: (data) => {
      setConfirming(false); setResult(data); setErr(''); setSubmitted(true); writeStore({ key: clientKey, submitted: true });
      qc.invalidateQueries({ queryKey: ['money-admin', 'test-transfer', clientKey] });
      qc.invalidateQueries({ queryKey: ['money-admin', 'balance'] });
      toast.success('Đã gửi lệnh chuyển thử');
    },
    onError: (e) => { setConfirming(false); setErr(apiError(e, 'Không gửi được lệnh chuyển thử.')); },
  });

  const submit = (e) => {
    e.preventDefault();
    const errs = validateTestTransfer({ amount: form.amount, bin: form.bin, account: form.account }, maxAmount);
    setErrors(errs);
    if (Object.keys(errs).length === 0) setConfirming(true);
  };
  const newTransfer = () => {
    const k = newClientKey();
    setClientKey(k); setSubmitted(false); setResult(null); setErr(''); writeStore(null);
  };
  const outcome = result?.outcome;
  // Chỉ cho tạo lệnh mới khi lệnh trước đã kết thúc rõ ràng; UNKNOWN thì chỉ gửi lại cùng khóa.
  const canStartNew = submitted && (terminal || outcome === 'FAILED') && outcome !== 'UNKNOWN';
  const locked = submitted;

  return (
    <div className="flex flex-col gap-6">
      <Panel icon={Wallet} title="Nhà cung cấp chi và số dư"
        toolbar={<GhostButton onClick={() => balanceQ.refetch()} disabled={balanceQ.isFetching}><RefreshCw size={14} className={`mr-1 inline ${balanceQ.isFetching ? 'animate-spin' : ''}`} aria-hidden="true" />Làm mới số dư</GhostButton>}>
        <div className="p-5">
          {balanceQ.isLoading && <p role="status" className="text-sm text-d-on-surface-variant">Đang tải số dư…</p>}
          {balanceQ.isError && <p role="alert" className="text-sm font-semibold text-d-error">{apiError(balanceQ.error, 'Không tải được số dư.')}</p>}
          {balanceQ.data && (
            <DetailList rows={[
              { label: 'Nhà cung cấp đang chọn', value: PROVIDER_LABEL[balanceQ.data.provider] ?? balanceQ.data.provider },
              { label: 'Đã cấu hình khóa', value: balanceQ.data.configured ? 'Có' : 'Chưa' },
              { label: 'Số dư tài khoản chi', value: balanceQ.data.balance == null ? '—' : (Number.isFinite(Number(balanceQ.data.balance)) ? vnd(balanceQ.data.balance) : balanceQ.data.balance) },
              { label: 'Lỗi từ nhà cung cấp', value: balanceQ.data.error },
            ]} />
          )}
        </div>
      </Panel>

      <Panel icon={ListChecks} title="Hướng dẫn thử chuyển tiền thật số nhỏ">
        <ol className="list-decimal space-y-1.5 p-5 pl-9 text-sm text-d-on-surface-variant">
          <li>Trên máy chủ, đặt đủ 3 khóa kênh chi: <code>PayOSPayout__ClientId</code>, <code>PayOSPayout__ApiKey</code>, <code>PayOSPayout__ChecksumKey</code>, rồi khởi động lại.</li>
          <li>Tab <strong>Cấu hình</strong>: chọn nhà cung cấp <strong>PAYOS</strong>, bật <strong>Cho phép chuyển thử</strong> và lưu.</li>
          <li>Bấm <strong>Làm mới số dư</strong> ở trên, chắc chắn tài khoản chi có đủ tiền.</li>
          <li>Điền ngân hàng, số tài khoản của <strong>chính bạn</strong> và số tiền <strong>2.000 đ</strong>, rồi gửi. Nếu kết quả chưa rõ, bấm Gửi lại (cùng khóa), đừng tạo lệnh mới.</li>
        </ol>
      </Panel>

      {!enabled && <Notice tone="amber" icon={AlertTriangle}>Chuyển thử đang tắt. Vào tab Cấu hình, bật “Cho phép chuyển thử” rồi lưu.</Notice>}
      {enabled && real && <Notice tone="red" icon={AlertTriangle}><strong>payOS thật:</strong> lệnh dưới đây chuyển tiền thật.</Notice>}
      {enabled && !real && <Notice tone="blue">Đang ở chế độ mô phỏng: không có tiền thật được chuyển.</Notice>}

      <Panel icon={Send} title="Chuyển thử">
        <form onSubmit={submit} className="grid gap-5 p-5 md:grid-cols-2" noValidate>
          <BankSelect banks={banksQ.data ?? []} loading={banksQ.isLoading} value={form.bin} disabled={locked || !enabled} error={errors.bin || (banksQ.isError ? 'Không tải được danh sách ngân hàng.' : '')}
            onChange={(bin, bankName) => setForm((f) => ({ ...f, bin, bankName }))} label="Ngân hàng nhận" />
          <div className="flex flex-col gap-5">
            <Field label="Số tài khoản nhận">
              <input value={form.account} onChange={(e) => setForm((f) => ({ ...f, account: e.target.value }))} inputMode="numeric" disabled={locked || !enabled} aria-invalid={!!errors.account} className={inputClass} />
              <FieldError>{errors.account}</FieldError>
            </Field>
            <Field label="Số tiền (đ)" hint={`Từ 1.000 đến ${maxAmount.toLocaleString('vi-VN')} đ.`}>
              <input type="number" inputMode="numeric" min={1000} max={maxAmount} value={form.amount} onChange={(e) => setForm((f) => ({ ...f, amount: e.target.value }))} disabled={locked || !enabled} aria-invalid={!!errors.amount} className={inputClass} />
              <FieldError>{errors.amount}</FieldError>
            </Field>
          </div>
          <p className="text-xs text-d-on-surface-variant md:col-span-2">Khóa lệnh (giữ nguyên khi gửi lại): <code>{clientKey}</code></p>
          {err && <div className="md:col-span-2"><Notice tone="red" icon={AlertTriangle}>{err}</Notice></div>}
          <div className="flex flex-wrap gap-2 md:col-span-2">
            {!submitted && <PrimaryButton type="submit" icon={Send} disabled={!enabled || send.isPending}>{send.isPending ? 'Đang gửi…' : 'Gửi chuyển thử'}</PrimaryButton>}
            {submitted && !terminal && <PrimaryButton type="submit" icon={Send} disabled={!enabled || send.isPending}>{send.isPending ? 'Đang gửi…' : 'Gửi lại (cùng khóa)'}</PrimaryButton>}
            {canStartNew && <GhostButton onClick={newTransfer}><Plus size={14} className="mr-1 inline" aria-hidden="true" />Tạo lệnh chuyển mới</GhostButton>}
          </div>
        </form>

        {(result || statusQ.data) && (
          <div className="flex flex-col gap-3 border-t border-d-border-subtle p-5" aria-live="polite">
            {result && <Notice tone={OUTCOME_TEXT[outcome]?.tone ?? 'slate'}><strong>Kết quả gửi:</strong> {OUTCOME_TEXT[outcome]?.text ?? outcome}{result.message ? ` (${result.message})` : ''}</Notice>}
            {statusQ.data && <Notice tone={STATUS_TEXT[status]?.tone ?? 'slate'}><strong>Trạng thái:</strong> {STATUS_TEXT[status]?.text ?? status}{statusQ.data.providerRef ? ` · mã nhà cung cấp ${statusQ.data.providerRef}` : ''}{statusQ.data.message ? ` · ${statusQ.data.message}` : ''}</Notice>}
            {statusQ.isError && <p role="alert" className="text-sm font-semibold text-d-error">{apiError(statusQ.error, 'Không kiểm tra được trạng thái.')}</p>}
          </div>
        )}
      </Panel>

      {confirming && (
        <ConfirmModal tone={real ? 'red' : 'primary'} icon={Send} title="Gửi lệnh chuyển thử?" confirmLabel="Gửi" cancelLabel="Quay lại" busy={send.isPending}
          onConfirm={() => send.mutate()} onCancel={() => setConfirming(false)}>
          <div className="flex flex-col gap-3 text-sm">
            {real && <Notice tone="red" icon={AlertTriangle}><strong>Sẽ chuyển tiền thật qua payOS.</strong></Notice>}
            <p>Chuyển <strong>{vnd(form.amount)}</strong> tới <strong>{form.bankName}</strong>, số tài khoản <strong>{form.account.trim()}</strong>.</p>
          </div>
        </ConfirmModal>
      )}
    </div>
  );
}
