import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { AlertTriangle, Undo2, HandCoins, CheckCheck } from 'lucide-react';
import { Modal, ConfirmModal, Notice } from '@/components/liquidation/LiquidationUI';
import { Field, inputClass, labelClass, GhostButton } from '@/features/admin/components/AdminUI';
import { moneyAdminApi } from '../moneyAdminApi';
import { vnd, apiError } from '../moneyLabels';
import {
  validateAnomalyRefundForm, validateCollectionRefundForm, buildAnomalyRefundBody, buildCollectionRefundBody, validateMarkPaid, validateReason,
  isPayerBankProblem, refundStatusMeta, refundActions,
} from '../refundLabels';
import { FieldError } from '../moneyUi';

// Các hộp thoại dùng chung cho Bất thường, Hoàn tiền và Khoản thu.

export function invalidateRefundQueries(qc) {
  for (const k of ['refunds', 'anomalies', 'collections', 'ledger', 'payouts', 'payees', 'balance']) qc.invalidateQueries({ queryKey: ['money-admin', k] });
}

/** Nhà cung cấp hoàn tiền hiện hành (dùng chung bộ nhớ đệm với màn Cấu hình). */
export function useIsRealMoney() {
  const q = useQuery({ queryKey: ['money-admin', 'settings'], queryFn: moneyAdminApi.getSettings });
  return q.data?.payoutProvider === 'PAYOS';
}

export function RealMoneyNotice() {
  return <Notice tone="red" icon={AlertTriangle}><strong>Đang dùng payOS thật: lệnh hoàn sẽ chuyển tiền thật từ tài khoản chi của sàn về người trả.</strong></Notice>;
}

/** Lỗi từ máy chủ; nếu liên quan tài khoản ngân hàng của người trả thì kèm lối tắt sang tab Người nhận. */
export function ServerError({ message, onGoPayees }) {
  if (!message) return null;
  const bank = isPayerBankProblem(message);
  return (
    <Notice tone="red" icon={AlertTriangle}>
      <div className="flex flex-col gap-2">
        <span className={bank ? 'font-bold' : ''}>{message}</span>
        {bank && onGoPayees && (
          <button type="button" onClick={onGoPayees} className="self-start rounded-full bg-red-600 px-4 py-1.5 text-xs font-bold text-white">Mở tab Người nhận để duyệt tài khoản</button>
        )}
      </div>
    </Notice>
  );
}

function SubmitFooter({ formId, onClose, busy, label, busyLabel, tone = 'bg-d-primary' }) {
  return (
    <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
      <GhostButton onClick={onClose} disabled={busy}>Đóng</GhostButton>
      <button type="submit" form={formId} disabled={busy} className={`rounded-full ${tone} px-6 py-2.5 text-sm font-bold text-white disabled:opacity-60`}>{busy ? busyLabel : label}</button>
    </div>
  );
}

function ReasonField({ value, onChange, error, disabled, id }) {
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className={labelClass}>Lý do (5 đến 255 ký tự, người trả sẽ thấy)</label>
      <textarea id={id} value={value} onChange={(e) => onChange(e.target.value)} maxLength={255} rows={3} disabled={disabled} aria-invalid={!!error} className={`${inputClass} !h-auto py-2`} />
      <FieldError>{error}</FieldError>
    </div>
  );
}

function ConfirmRefund({ total, payerName, real, busy, onConfirm, onCancel }) {
  return (
    <ConfirmModal tone={real ? 'red' : 'primary'} icon={Undo2} title="Xác nhận hoàn tiền?" confirmLabel="Hoàn tiền" busy={busy} onConfirm={onConfirm} onCancel={onCancel}>
      <div className="flex flex-col gap-3 text-sm">
        <p>Hoàn <strong>{vnd(total)}</strong> cho người trả <strong>{payerName || '—'}</strong>.</p>
        {real && <RealMoneyNotice />}
      </div>
    </ConfirmModal>
  );
}

/** Hoàn tiền từ một bất thường: số tiền mặc định bằng số được hoàn, không vượt quá số đó. */
export function AnomalyRefundDialog({ anomaly, onClose, onDone, onGoPayees }) {
  const qc = useQueryClient();
  const real = useIsRealMoney();
  const max = Number(anomaly.refundableAmount ?? 0);
  const [amount, setAmount] = useState(String(max));
  const [reason, setReason] = useState('');
  const [errors, setErrors] = useState({});
  const [confirming, setConfirming] = useState(false);
  const [err, setErr] = useState('');
  const bankOk = anomaly.payerBank?.status === 'VERIFIED';

  const m = useMutation({
    mutationFn: () => moneyAdminApi.refundAnomaly(anomaly.id, buildAnomalyRefundBody({ amount, reason })),
    onSuccess: (data) => { toast.success('Đã tạo lệnh hoàn tiền'); invalidateRefundQueries(qc); onDone?.(data); onClose(); },
    onError: (e) => { setConfirming(false); setErr(apiError(e, 'Không tạo được lệnh hoàn tiền.')); },
  });
  const submit = (e) => {
    e.preventDefault();
    setErr('');
    const errs = validateAnomalyRefundForm({ amount, reason }, max);
    setErrors(errs);
    if (Object.keys(errs).length === 0) setConfirming(true);
  };
  return (
    <Modal size="md" icon={Undo2} title="Hoàn tiền cho người trả" subtitle={anomaly.payer?.name} onClose={onClose} busy={m.isPending}
      footer={<SubmitFooter formId="anomaly-refund-form" onClose={onClose} busy={m.isPending} label="Tiếp tục" busyLabel="Đang hoàn…" />}>
      <form id="anomaly-refund-form" onSubmit={submit} className="flex flex-col gap-4" noValidate>
        {!bankOk && (
          <Notice tone="amber" icon={AlertTriangle}>
            <div className="flex flex-col gap-2">
              <span>Người trả chưa có tài khoản ngân hàng được duyệt, máy chủ sẽ từ chối hoàn tiền cho đến khi Admin duyệt tài khoản.</span>
              {onGoPayees && <button type="button" onClick={onGoPayees} className="self-start rounded-full bg-amber-500 px-4 py-1.5 text-xs font-bold text-white">Mở tab Người nhận</button>}
            </div>
          </Notice>
        )}
        <p className="text-sm">Số tiền được hoàn tối đa: <strong>{vnd(max)}</strong>.</p>
        <Field label="Số tiền hoàn (đ)" hint="Mặc định là toàn bộ số được hoàn; có thể hoàn ít hơn.">
          <input type="number" inputMode="numeric" min="1" max={max} step="1" value={amount} onChange={(e) => setAmount(e.target.value)} disabled={m.isPending} aria-invalid={!!errors.amount} className={inputClass} />
          <FieldError>{errors.amount}</FieldError>
        </Field>
        <ReasonField id="anomaly-refund-reason" value={reason} onChange={setReason} error={errors.reason} disabled={m.isPending} />
        <ServerError message={err} onGoPayees={onGoPayees} />
      </form>
      {confirming && <ConfirmRefund total={Number(amount)} payerName={anomaly.payer?.name} real={real} busy={m.isPending} onConfirm={() => m.mutate()} onCancel={() => setConfirming(false)} />}
    </Modal>
  );
}

/** Hoàn tiền từ khoản thu đã thu (PAID): toàn bộ hoặc một phần. 409 khi tiền đã chi cho người nhận. */
export function CollectionRefundDialog({ collection, onClose, onGoPayees }) {
  const qc = useQueryClient();
  const real = useIsRealMoney();
  const max = Number(collection.amount ?? 0);
  const [kind, setKind] = useState('FULL');
  const [amount, setAmount] = useState('');
  const [reason, setReason] = useState('');
  const [errors, setErrors] = useState({});
  const [confirming, setConfirming] = useState(false);
  const [err, setErr] = useState('');
  const m = useMutation({
    mutationFn: () => moneyAdminApi.refundCollection(collection.id, buildCollectionRefundBody({ kind, amount, reason })),
    onSuccess: () => { toast.success('Đã tạo lệnh hoàn tiền'); invalidateRefundQueries(qc); onClose(); },
    onError: (e) => { setConfirming(false); setErr(apiError(e, 'Không tạo được lệnh hoàn tiền.')); },
  });
  const submit = (e) => {
    e.preventDefault();
    setErr('');
    const errs = validateCollectionRefundForm({ kind, amount, reason }, max);
    setErrors(errs);
    if (Object.keys(errs).length === 0) setConfirming(true);
  };
  const total = kind === 'FULL' ? max : Number(amount);
  return (
    <Modal size="md" icon={Undo2} title="Hoàn tiền khoản thu" subtitle={`${collection.payerName} · ${vnd(collection.amount)}`} onClose={onClose} busy={m.isPending}
      footer={<SubmitFooter formId="collection-refund-form" onClose={onClose} busy={m.isPending} label="Tiếp tục" busyLabel="Đang hoàn…" />}>
      <form id="collection-refund-form" onSubmit={submit} className="flex flex-col gap-4" noValidate>
        <p className="text-sm text-d-on-surface-variant">Chỉ hoàn được khi tiền của người nhận chưa được chi. Nếu đã chi, máy chủ sẽ từ chối và nêu lý do.</p>
        <fieldset className="flex flex-col gap-2" disabled={m.isPending}>
          <legend className={`${labelClass} mb-1`}>Hình thức hoàn</legend>
          <label className="flex items-center gap-2 text-sm"><input type="radio" name="refund-kind" checked={kind === 'FULL'} onChange={() => setKind('FULL')} className="size-4 accent-[#446900]" />Hoàn toàn bộ ({vnd(max)})</label>
          <label className="flex items-center gap-2 text-sm"><input type="radio" name="refund-kind" checked={kind === 'PARTIAL'} onChange={() => setKind('PARTIAL')} className="size-4 accent-[#446900]" />Hoàn một phần</label>
        </fieldset>
        {kind === 'PARTIAL' && (
          <Field label="Số tiền hoàn (đ)" hint={`Từ 1 đến dưới ${max.toLocaleString('vi-VN')} đ.`}>
            <input type="number" inputMode="numeric" min="1" max={max} step="1" value={amount} onChange={(e) => setAmount(e.target.value)} disabled={m.isPending} aria-invalid={!!errors.amount} className={inputClass} />
            <FieldError>{errors.amount}</FieldError>
          </Field>
        )}
        <ReasonField id="collection-refund-reason" value={reason} onChange={setReason} error={errors.reason} disabled={m.isPending} />
        <ServerError message={err} onGoPayees={onGoPayees} />
      </form>
      {confirming && <ConfirmRefund total={total} payerName={collection.payerName} real={real} busy={m.isPending} onConfirm={() => m.mutate()} onCancel={() => setConfirming(false)} />}
    </Modal>
  );
}

/** Đánh dấu bất thường đã xử lý, kèm ghi chú. */
export function ResolveAnomalyDialog({ anomaly, onClose, onDone }) {
  const qc = useQueryClient();
  const [note, setNote] = useState('');
  const [error, setError] = useState('');
  const [err, setErr] = useState('');
  const m = useMutation({
    mutationFn: () => moneyAdminApi.resolveAnomaly(anomaly.id, note.trim()),
    onSuccess: (data) => { toast.success('Đã đánh dấu đã xử lý'); invalidateRefundQueries(qc); onDone?.(data); onClose(); },
    onError: (e) => setErr(apiError(e)),
  });
  const submit = (e) => {
    e.preventDefault();
    setErr('');
    const r = validateReason(note, 'Ghi chú');
    setError(r ?? '');
    if (!r) m.mutate();
  };
  return (
    <Modal size="md" icon={CheckCheck} title="Đánh dấu đã xử lý" subtitle={anomaly.reference} onClose={onClose} busy={m.isPending}
      footer={<SubmitFooter formId="resolve-anomaly-form" onClose={onClose} busy={m.isPending} label="Đánh dấu đã xử lý" busyLabel="Đang lưu…" />}>
      <form id="resolve-anomaly-form" onSubmit={submit} className="flex flex-col gap-4" noValidate>
        <p className="text-sm text-d-on-surface-variant">Dùng khi bạn đã xử lý bất thường này ngoài hệ thống hoặc không cần hoàn tiền. Hành động này không chuyển tiền.</p>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="resolve-note" className={labelClass}>Ghi chú xử lý (5 đến 255 ký tự)</label>
          <textarea id="resolve-note" value={note} onChange={(e) => setNote(e.target.value)} maxLength={255} rows={3} disabled={m.isPending} aria-invalid={!!error} className={`${inputClass} !h-auto py-2`} />
          <FieldError>{error}</FieldError>
        </div>
        <FieldError>{err}</FieldError>
      </form>
    </Modal>
  );
}

/** Đánh dấu lệnh hoàn đã hoàn tay. PROCESSING/UNKNOWN bắt buộc tick xác nhận. */
export function MarkRefundPaidDialog({ refund, onClose, onDone }) {
  const needsConfirm = refundActions(refund.status).markPaidNeedsConfirm;
  const [reference, setReference] = useState('');
  const [note, setNote] = useState('');
  const [confirm, setConfirm] = useState(false);
  const [err, setErr] = useState('');
  const m = useMutation({
    mutationFn: () => moneyAdminApi.markRefundPaid(refund.id, { reference: reference.trim(), note: note.trim() || null, confirm }),
    onSuccess: (data) => { toast.success('Đã đánh dấu đã hoàn tay'); onDone(data); },
    onError: (e) => setErr(apiError(e)),
  });
  const submit = (e) => {
    e.preventDefault();
    setErr('');
    const v = validateMarkPaid({ reference, confirm }, needsConfirm);
    if (v) return setErr(v);
    m.mutate();
  };
  return (
    <Modal size="md" icon={HandCoins} title={`Đánh dấu đã hoàn tay: ${refund.code}`} onClose={onClose} busy={m.isPending}
      footer={<SubmitFooter formId="markrefund-form" onClose={onClose} busy={m.isPending} label="Đánh dấu đã hoàn" busyLabel="Đang lưu…" />}>
      <form id="markrefund-form" onSubmit={submit} className="flex flex-col gap-4" noValidate>
        <p className="text-sm">Số tiền hoàn: <strong>{vnd(refund.amount)}</strong>. Chỉ dùng khi bạn đã chuyển tiền cho người trả bằng cách khác.</p>
        {needsConfirm && <Notice tone="red" icon={AlertTriangle}>Lệnh đang ở trạng thái <strong>{refundStatusMeta(refund.status).label}</strong>: lệnh qua nhà cung cấp có thể vẫn đang chạy. Chỉ đánh dấu khi chắc chắn không bị hoàn trùng.</Notice>}
        <Field label="Mã tham chiếu"><input value={reference} onChange={(e) => setReference(e.target.value)} maxLength={100} disabled={m.isPending} className={inputClass} /></Field>
        <Field label="Ghi chú (không bắt buộc)"><input value={note} onChange={(e) => setNote(e.target.value)} maxLength={500} disabled={m.isPending} className={inputClass} /></Field>
        {needsConfirm && (
          <label className="flex items-start gap-2 text-sm">
            <input type="checkbox" checked={confirm} onChange={(e) => setConfirm(e.target.checked)} disabled={m.isPending} className="mt-0.5 size-4 accent-[#446900]" />
            <span>Tôi xác nhận đã chuyển tiền ngoài hệ thống và lệnh của nhà cung cấp sẽ không gây hoàn trùng.</span>
          </label>
        )}
        <FieldError>{err}</FieldError>
      </form>
    </Modal>
  );
}
