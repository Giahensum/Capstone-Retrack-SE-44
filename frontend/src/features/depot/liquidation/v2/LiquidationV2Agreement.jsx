import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useIsMutating, useQueryClient } from '@tanstack/react-query';
import { Handshake, Banknote, PackageCheck, Warehouse, ReceiptText, ArrowRight, AlertTriangle, Flag, MapPin, Phone, Timer, Info, Scale } from 'lucide-react';
import { Form, Input, Textarea } from '@/components/forms/ValidatedForm';
import { QueryState, MutationError, inputClass } from '@/features/depot/components/DepotUI';
import { money } from '@/features/depot/depotApi';
import { usesConfirmedQuantity } from '@/components/liquidation/handoverQuantity';
import LiquidationHandoverTable from '@/components/liquidation/LiquidationHandoverTable';
import LiquidationQuantityForm from './LiquidationQuantityForm';
import StockReceiptForm from '@/features/depot/components/StockReceiptForm';
import { Modal, Section, Tile, Notice, Steps, Badge } from '@/components/liquidation/LiquidationUI';
import { localTime } from '../liquidationApi';
import { useV2Query, useV2Mutation, useV2Scope, AGREEMENT_STAGE } from './liquidationV2Api';

const primaryBtn = 'inline-flex items-center justify-center gap-2 min-h-11 rounded-full bg-d-primary px-5 py-2.5 text-sm font-semibold text-white shadow-sm transition hover:bg-[#385500] active:scale-[.98] disabled:pointer-events-none disabled:opacity-50';
const ghostBtn = 'inline-flex items-center justify-center gap-2 min-h-11 rounded-full border border-d-border-subtle bg-white px-5 py-2.5 text-sm font-semibold text-d-on-surface transition hover:bg-d-surface-container disabled:opacity-50';
const stageTone = { CANCELLED: 'red', DISPUTED: 'amber', COMPLETED: 'primary' };

/** Thỏa thuận của kho thắng trong phiên nhiều hạng mục: tiền theo policy đã công bố, hai bên tự thanh toán và bàn giao. */
export default function LiquidationV2Agreement({ id, onClose }) {
  const scope = useV2Scope();
  const client = useQueryClient();
  const query = useV2Query(`/agreements/${id}`);
  const d = query.data;
  const busy = useIsMutating({ mutationKey: ['liquidation-v2', scope.userId, scope.depotId] }) > 0;
  const [reference, setReference] = useState('');
  const [confirmed, setConfirmed] = useState(false);
  const [receiving, setReceiving] = useState(false);
  const [editingQuantity, setEditingQuantity] = useState(false);
  const [abandon, setAbandon] = useState(false);
  const intake = useV2Query(`/agreements/${id}/intake-source`, {}, { enabled: receiving });
  const closeIntake = () => { setReceiving(false); void client.invalidateQueries({ queryKey: ['liquidation-v2', scope.userId, scope.depotId] }); };
  const report = useV2Mutation('patch', `/agreements/${id}/report-payment`, () => { setReference(''); setConfirmed(false); });
  const received = useV2Mutation('patch', `/agreements/${id}/confirm-received`);

  if (receiving && d) {
    if (intake.data?.readyForIntake && !intake.isError) return <StockReceiptForm agreement={{ id: intake.data.agreementId, title: d.title, amount: intake.data.finalPurchaseAmount }}
      intakeSource={intake.data} confirmBulkReceived={!intake.data.ownerReceived} onClose={closeIntake} />;
    return <Modal title="Nhập kho từ hạng mục đã mua" icon={Warehouse} onClose={closeIntake}>
      <QueryState query={intake}>{intake.data && <Notice tone="amber" icon={AlertTriangle}>Thỏa thuận chưa đủ điều kiện nhập hoặc đã có phiếu. Hãy kiểm tra lại các mốc tiền, bàn giao và trạng thái xử lý.</Notice>}</QueryState>
      <button type="button" className={ghostBtn} onClick={closeIntake}>Quay lại thỏa thuận</button>
    </Modal>;
  }

  if (editingQuantity && d) return <LiquidationQuantityForm agreement={d} onClose={() => setEditingQuantity(false)} />;
  const measured = d && usesConfirmedQuantity(d);
  const steps = d && [
    ...(measured ? [{ key: 'quantity', label: 'Chốt cân bàn giao', icon: Scale, state: d.handoverStatus === 'CONFIRMED' ? 'done' : 'current' }] : []),
    { key: 'pay', label: 'Kho thanh toán', icon: Banknote, state: d.paymentReportedAt ? 'done' : !measured || d.handoverStatus === 'CONFIRMED' ? 'current' : 'todo' },
    { key: 'confirm', label: 'Seller xác nhận tiền', icon: Handshake, state: d.paymentConfirmedAt ? 'done' : d.paymentReportedAt ? 'current' : 'todo' },
    { key: 'handover', label: 'Seller bàn giao', icon: PackageCheck, state: d.sellerHandedOverAt ? 'done' : d.paymentConfirmedAt ? 'current' : 'todo' },
    { key: 'receive', label: 'Kho nhận & nhập kho', icon: Warehouse, state: d.receiptStatus === 'POSTED' ? 'done' : d.sellerHandedOverAt ? 'current' : 'todo' },
  ];

  let action = null;
  if (d && ['WAITING_BUYER_QUANTITY', 'WAITING_SELLER_QUANTITY'].includes(d.stage)) action = <div className="flex justify-end"><button type="button" className={primaryBtn} disabled={busy} onClick={() => setEditingQuantity(true)}><Scale size={16} />{d.stage === 'WAITING_SELLER_QUANTITY' ? 'Chỉnh và gửi lại bảng cân' : 'Nhập cân / kiểm đếm bàn giao'}</button></div>;
  else if (d && d.stage === 'WAITING_BUYER_PAYMENT') action = <Form className="space-y-2.5" onSubmit={(e) => { e.preventDefault(); if (confirmed && reference.trim()) report.mutate({ reference: reference.trim() }); }}>
    <label className="block text-xs font-semibold text-d-on-surface-variant">Tham chiếu / nội dung đã thanh toán đủ {money(d.totalAmount)} trực tiếp cho Seller
      <Input required disabled={busy} maxLength={255} className={inputClass} value={reference} onChange={(e) => setReference(e.target.value)} placeholder="VD: CK Vietcombank 12/11, tiền mặt có biên nhận…" />
    </label>
    <label className="flex cursor-pointer gap-2.5 text-sm"><Input required type="checkbox" className="mt-0.5 size-4 accent-[#446900]" disabled={busy} checked={confirmed} onChange={(e) => setConfirmed(e.target.checked)} /><span>Tôi đã thanh toán đủ tổng tiền đã chốt trong thỏa thuận; RETRACK không thu hộ khoản này.</span></label>
    <div className="flex justify-end"><button className={primaryBtn} disabled={busy || !confirmed}><Banknote size={16} />Báo đã thanh toán</button></div>
    <MutationError mutation={report} />
  </Form>;
  else if (d && d.stage === 'WAITING_OWNER_RECEIVE') action = <div className="flex flex-wrap justify-end gap-2">
    <button type="button" className={ghostBtn} disabled={busy} onClick={() => received.mutate()}><PackageCheck size={16} />Chỉ xác nhận đã nhận hàng</button>
    <button type="button" className={primaryBtn} disabled={busy} onClick={() => setReceiving(true)}><Warehouse size={16} />Nhận hàng và nhập kho</button>
    <MutationError mutation={received} />
  </div>;
  else if (d && d.stage === 'COMPLETED' && !d.receiptId) action = <div className="flex justify-end"><button type="button" className={primaryBtn} onClick={() => setReceiving(true)}><Warehouse size={16} />Nhập kho lượng thực tế sau phân loại</button></div>;
  else if (d?.receiptId) action = <div className="flex justify-end"><Link to={`/depot/inventory?tab=receipts&receiptId=${d.receiptId}`} className={primaryBtn}><ReceiptText size={16} />{d.receiptStatus === 'POSTED' ? 'Xem phiếu đã ghi sổ' : 'Đến phiếu nhập để kiểm tra'}<ArrowRight size={15} /></Link></div>;
  const waiting = d && ({ WAITING_SELLER_QUANTITY: 'Đã gửi bảng cân / kiểm đếm. Chờ Seller kiểm tra và xác nhận trước khi thanh toán.', WAITING_SELLER_CONFIRM: 'Đã báo thanh toán. Chờ Seller đối chiếu và xác nhận nhận đủ tiền.',
    WAITING_HANDOVER: 'Seller đã nhận tiền. Chờ Seller xác nhận đã bàn giao đủ các hạng mục.',
    DISPUTED: 'Thỏa thuận đang tạm dừng chờ Admin xử lý báo bỏ mua.', CANCELLED: 'Admin đã hủy thỏa thuận. Tiền đã giao (nếu có) do hai bên tự hoàn trả.' })[d.stage];

  return <Modal icon={Handshake} tone="teal" size="lg" title={d ? `Thỏa thuận: ${d.title}` : 'Thỏa thuận mua hạng mục'} onClose={onClose} busy={busy}
    badge={d && <Badge tone={stageTone[d.stage] ?? 'blue'}>{AGREEMENT_STAGE[d.stage] ?? d.stage}</Badge>}
    subtitle={d && `Kho ${d.depotName} · tạo ${localTime(d.createdAt)}`}
    footer={d && (action || waiting) ? <div className="space-y-2">{waiting && <p className="text-sm text-d-on-surface-variant">{waiting}</p>}{action}</div> : null}>
    <QueryState query={query}>{d && <>
      <Steps steps={steps} />
      {d.resolution && <Notice tone={d.resolution === 'CANCELLED' ? 'red' : 'primary'} icon={Info}>Admin: {d.resolution === 'CANCELLED' ? 'đã hủy thỏa thuận' : 'quyết định tiếp tục thực hiện'}. {d.resolutionNote}</Notice>}
      {d.abandonReportedAt && <Notice tone="amber" icon={AlertTriangle}>{d.abandonReporterRole === 'SELLER' ? 'Seller' : 'Kho'} báo bỏ mua lúc {localTime(d.abandonReportedAt)}: {d.abandonReason}</Notice>}
      <Section icon={Scale} title="Hạng mục đã thắng">
        <LiquidationHandoverTable agreement={d} />
      </Section>
      <div className="grid gap-3 sm:grid-cols-2">
        <Tile icon={Timer} tone="amber" label="Hạn thu gom" value={localTime(d.collectionDeadline)} />
        <Tile icon={Phone} tone="blue" label={`Liên hệ ${d.sellerName}`} value={d.sellerPhone || d.contactPhone} />
        <Tile icon={MapPin} tone="blue" label="Địa chỉ nhận hàng" value={d.address} className="sm:col-span-2" />
      </div>
      <Section icon={Info} title="Mốc đã ghi nhận">
        <dl className="grid gap-2 text-sm sm:grid-cols-2">{[
          ['Tham chiếu thanh toán', d.paymentReference ?? 'Chưa báo'], ['Kho báo đã thanh toán', localTime(d.paymentReportedAt)],
          ['Seller xác nhận đủ tiền', localTime(d.paymentConfirmedAt)], ['Seller bàn giao đủ', localTime(d.sellerHandedOverAt)],
          ['Kho xác nhận đã nhận', localTime(d.ownerCollectedAt)], ['Phiếu nhập kho', d.receiptId ? (d.receiptStatus === 'POSTED' ? 'Đã ghi sổ' : 'Đã lập') : 'Chưa lập'],
        ].map(([k, v]) => <div key={k}><dt className="text-xs text-d-on-surface-variant">{k}</dt><dd className="font-semibold">{v}</dd></div>)}</dl>
      </Section>
      {!d.abandonReportedAt && !d.sellerHandedOverAt && <AbandonBox id={id} open={abandon} setOpen={setAbandon} busy={busy} />}
    </>}</QueryState>
  </Modal>;
}

function AbandonBox({ id, open, setOpen, busy }) {
  const [reason, setReason] = useState('');
  const save = useV2Mutation('post', `/agreements/${id}/abandon-report`, () => { setReason(''); setOpen(false); });
  if (!open) return <button type="button" className="inline-flex items-center gap-1.5 text-sm font-semibold text-d-error" onClick={() => setOpen(true)}><Flag size={14} />Báo không thể thực hiện / bỏ mua</button>;
  return <Section icon={Flag} tone="red" title="Báo không thể thực hiện thỏa thuận">
    <Form className="space-y-2.5" onSubmit={(e) => { e.preventDefault(); if (reason.trim().length >= 5) save.mutate({ reason: reason.trim() }); }}>
      <p className="text-sm text-d-on-surface-variant">Thỏa thuận tạm dừng để Admin xem xét. Hệ thống không tự phạt tiền, tịch thu cọc hay chuyển hạng mục cho người trả giá thứ hai.</p>
      <label className="block text-xs font-semibold text-d-on-surface-variant">Lý do (5–1000 ký tự)
        <Textarea required minLength={5} maxLength={1000} rows={3} disabled={busy} className={inputClass} value={reason} onChange={(e) => setReason(e.target.value)} />
      </label>
      <div className="flex justify-end gap-2"><button type="button" className={ghostBtn} disabled={busy} onClick={() => setOpen(false)}>Hủy</button>
        <button className="inline-flex min-h-11 items-center gap-2 rounded-full bg-d-error px-5 py-2.5 text-sm font-semibold text-white disabled:opacity-50" disabled={busy || reason.trim().length < 5}><Flag size={15} />Gửi Admin xử lý</button></div>
      <MutationError mutation={save} />
    </Form>
  </Section>;
}
