import { useState } from 'react';
import { RotateCcw } from 'lucide-react';
import { Form, Input, Select, Textarea } from '@/components/forms/ValidatedForm';
import { Dialog, buttonClass, secondaryButtonClass, inputClass } from './DepotUI';
import ProofUpload from './ProofUpload';
import { money, date } from '../depotApi';
import { REFUND_KINDS, REFUND_HINTS, refundKindsFor } from '../subscriptionRefunds';

const newId = () => (globalThis.crypto?.randomUUID ? globalThis.crypto.randomUUID()
  : 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => { const r = (Math.random() * 16) | 0; return (c === 'x' ? r : (r & 0x3) | 0x8).toString(16); }));

/**
 * Form yêu cầu hoàn tiền một đơn gói. Dùng chung cho Chủ kho và Admin (Admin lập thay khi đối soát tiền thiếu/thừa/về muộn).
 * mutation nhận { body } theo useDepotMutation, hoặc body trực tiếp với useMutation phía Admin (adapter qua prop submit).
 */
export default function SubscriptionRefundForm({ order, onClose, submit, pending, error, allowUpload = true }) {
  const kinds = refundKindsFor(order);
  const [operationId] = useState(newId);
  const [kind, setKind] = useState(kinds[0] ?? '');
  const [amount, setAmount] = useState(kinds[0] === 'UNUSED_PERIOD' ? String(order.amount) : '');
  const [reason, setReason] = useState('');
  const [evidenceUrl, setEvidenceUrl] = useState('');
  const [evidenceReference, setEvidenceReference] = useState('');
  const [uploading, setUploading] = useState(false);
  const busy = pending || uploading;
  const fixed = kind === 'UNUSED_PERIOD';
  const value = Number(fixed ? order.amount : amount);
  const needsEvidence = ['DUPLICATE_OR_EXCESS', 'SHORT_PAYMENT', 'LATE_PAYMENT'].includes(kind);
  const problem = !kind ? 'Đơn này không có loại hoàn tiền phù hợp.'
    : !Number.isSafeInteger(value) || value <= 0 || value > 10_000_000 ? 'Số tiền là số nguyên từ 1 đến 10.000.000 đ.'
    : reason.trim().length < 10 ? 'Mô tả lý do tối thiểu 10 ký tự.'
    : needsEvidence && !evidenceUrl && !evidenceReference.trim() ? 'Cần chứng từ hoặc mã giao dịch ngân hàng để đối soát.' : null;
  return <Dialog icon={RotateCcw} title={`Yêu cầu hoàn tiền đơn gói #${order.orderCode || order.id.slice(0, 8)}`} busy={busy} onClose={onClose}>
    <p className="text-sm">Đơn {money(order.amount)} · {order.periodStart ? `kỳ ${date(order.periodStart)} – ${date(order.periodEnd)}` : 'chưa kích hoạt'}.</p>
    {!kinds.length ? <p role="status" className="mt-3 text-sm">Đơn ở trạng thái này không có loại hoàn tiền phù hợp. Kỳ đang sử dụng được dùng đến hết hạn.</p> :
      <Form className="mt-3 space-y-3" validate={() => (problem ? { refundReason: problem } : {})}
        onSubmit={(e) => { e.preventDefault(); if (problem || busy) return; submit({ operationId, orderId: order.id, kind, amount: value, reason: reason.trim(), evidenceUrl: evidenceUrl || null, evidenceReference: evidenceReference.trim() || null }); }}>
        <label className="block text-sm">Loại hoàn tiền
          <Select className={inputClass} value={kind} disabled={busy} onChange={(e) => { setKind(e.target.value); if (e.target.value === 'UNUSED_PERIOD') setAmount(String(order.amount)); }}>
            {kinds.map((k) => <option key={k} value={k}>{REFUND_KINDS[k]}</option>)}
          </Select>
        </label>
        <p className="rounded-xl bg-d-surface-accent p-3 text-xs">{REFUND_HINTS[kind]}</p>
        <label className="block text-sm">Số tiền đề nghị hoàn (đ)
          <Input inputMode="numeric" className={inputClass} disabled={busy || fixed} value={fixed ? String(order.amount) : amount} onChange={(e) => setAmount(e.target.value.replace(/\D/g, ''))} />
        </label>
        <label className="block text-sm">Lý do (10–1000 ký tự)
          <Textarea name="refundReason" data-field-label="Lý do hoàn tiền" rows={3} maxLength={1000} className={inputClass} disabled={busy} value={reason} onChange={(e) => setReason(e.target.value)} />
        </label>
        {allowUpload ? <>
          <ProofUpload onUploaded={setEvidenceUrl} onBusyChange={setUploading} disabled={busy} />
          {evidenceUrl && <p className="text-xs">Đã tải chứng từ.</p>}
        </> : <label className="block text-sm">Đường dẫn chứng từ (HTTPS, nếu có)
          <Input type="url" pattern="https://.+" className={inputClass} disabled={busy} value={evidenceUrl} onChange={(e) => setEvidenceUrl(e.target.value.trim())} />
        </label>}
        <label className="block text-sm">Mã giao dịch ngân hàng (nếu có)
          <Input maxLength={255} className={inputClass} disabled={busy} value={evidenceReference} onChange={(e) => setEvidenceReference(e.target.value)} />
        </label>
        <p className="text-xs text-d-on-surface-variant">Duyệt yêu cầu chưa có nghĩa là tiền đã về tài khoản; trạng thái chỉ chuyển "Đã chuyển tiền hoàn" khi Admin ghi nhận giao dịch thật.</p>
        {error}
        <div className="flex flex-col-reverse gap-2 border-t border-d-border-subtle pt-3 sm:flex-row sm:justify-end">
          <button type="button" className={secondaryButtonClass} disabled={busy} onClick={onClose}>Hủy</button>
          <button className={buttonClass} disabled={busy || !!problem}>{pending ? 'Đang gửi…' : 'Gửi yêu cầu hoàn tiền'}</button>
        </div>
      </Form>}
  </Dialog>;
}

