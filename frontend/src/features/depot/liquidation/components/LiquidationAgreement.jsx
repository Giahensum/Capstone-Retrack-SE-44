import { Form, Input } from '@/components/forms/ValidatedForm';
import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import {
  Handshake, FileSignature, Wallet, Banknote, Warehouse, MapPin, Phone, CalendarClock, FileText, Layers,
  AlertTriangle, CheckCircle2, Hourglass, ReceiptText, BadgePercent, Coins, ArrowRight,
} from 'lucide-react';
import { QueryState, MutationError, inputClass } from '@/features/depot/components/DepotUI';
import { money } from '@/features/depot/depotApi';
import StockReceiptForm from '@/features/depot/components/StockReceiptForm';
import BatchDetailSection from '@/features/depot/components/BatchDetailSection';
import DepositContract from '@/components/liquidation/DepositContract';
import ContractViewer from '@/components/liquidation/ContractViewer';
import { toContract } from '@/components/liquidation/ContractDocument';
import { Modal, Section, Tile, Notice, Steps, Badge } from '@/components/liquidation/LiquidationUI';
import { useLiquidationQuery, useLiquidationMutation, useLiquidationScope, localTime } from '../liquidationApi';
import PayCollectionButton, { usePlatformCollection } from '@/features/money/PayCollectionButton';
import { LiquidationEsignPanel, LiquidationEsignSignButton, isEsignActive, useLiquidationEsign } from '@/features/esign/LiquidationEsign';

const primaryBtn = 'inline-flex items-center justify-center gap-2 rounded-full bg-d-primary px-6 py-2.5 text-sm font-bold text-white shadow-sm transition active:scale-95 disabled:opacity-50';
const checkLabel = 'flex cursor-pointer gap-2.5 text-sm text-d-on-surface';
const checkbox = 'mt-0.5 size-4 accent-[#446900]';

export default function LiquidationAgreement({ id, onClose }) {
  const { userId, depotId } = useLiquidationScope();
  const client = useQueryClient();
  const query = useLiquidationQuery(`/agreements/${id}`, {}, true, true);
  const [reference, setReference] = useState('');
  const [depositRef, setDepositRef] = useState('');
  const [confirmed, setConfirmed] = useState(false);
  const [receiving, setReceiving] = useState(false);
  const [docOpen, setDocOpen] = useState(false);
  const reset = () => setConfirmed(false);
  const report = useLiquidationMutation('patch', `/agreements/${id}/report-payment`, reset);
  const sign = useLiquidationMutation('patch', `/agreements/${id}/sign`, reset);
  const reportDeposit = useLiquidationMutation('patch', `/agreements/${id}/report-deposit`, reset);
  const d = query.data;
  const esign = useLiquidationEsign(id);
  const esignOn = isEsignActive(esign.data);
  const onEsignChanged = () => client.invalidateQueries({ queryKey: ['liquidation', userId, depotId] });
  const busy = report.isPending || sign.isPending || reportDeposit.isPending;

  const hasDeposit = d?.depositAmount > 0;
  const bothSigned = !!(d?.sellerSignedAt && d?.buyerSignedAt);
  const depositOk = !hasDeposit || !!d?.depositConfirmedAt;
  const posted = d?.receiptStatus === 'POSTED';
  // Chế độ PLATFORM: trả cọc / phần còn lại qua PayOS vào sàn; luồng "báo đã chuyển" thủ công chỉ dùng khi LEGACY.
  const depositPay = usePlatformCollection('LIQUIDATION_DEPOSIT', id, { enabled: !!d && hasDeposit && bothSigned && !d.depositConfirmedAt });
  const balancePay = usePlatformCollection('LIQUIDATION_BALANCE', id, { enabled: !!d && depositOk && !d.paymentConfirmedAt });
  const platformMode = depositPay.platform || balancePay.platform;
  const receiptLabel = posted ? 'Đã ghi sổ' : d?.receiptStatus === 'SUBMITTED' ? 'Chờ duyệt phiếu nhập'
    : d?.receiptId ? 'Phiếu nháp · chờ ghi sổ' : 'Chưa lập';

  const flags = hasDeposit
    ? [bothSigned, !!d?.depositConfirmedAt, !!d?.paymentConfirmedAt, posted]
    : [!!d?.paymentConfirmedAt, posted];
  const defs = hasDeposit
    ? [['sign', 'Ký hợp đồng', FileSignature], ['deposit', 'Chuyển cọc', Wallet], ['pay', 'Thanh toán', Banknote], ['receipt', 'Nhận hàng và nhập kho', Warehouse]]
    : [['pay', 'Thanh toán', Banknote], ['receipt', 'Nhận hàng và nhập kho', Warehouse]];
  const firstOpen = flags.findIndex((f) => !f);
  const steps = defs.map(([key, label, icon], i) => ({ key, label, icon, state: flags[i] ? 'done' : i === firstOpen ? 'current' : 'todo' }));

  // Hành động tiếp theo của Chủ kho đặt ở chân popup để luôn nhìn thấy, không phải cuộn.
  let action = null;
  let waiting = null;
  if (d) {
    if (hasDeposit && !d.buyerSignedAt && esignOn) {
      // Máy chủ đã bật DocuSign: ký điện tử qua DocuSign thay cho nút xác nhận cũ.
      action = <LiquidationEsignSignButton agreementId={id} status={esign.data} onChanged={onEsignChanged} />;
    } else if (hasDeposit && !d.buyerSignedAt && !esign.isPending) {
      action = (
        <div className="space-y-2.5">
          <label className={checkLabel}><Input type="checkbox" className={checkbox} disabled={busy} checked={confirmed} onChange={(e) => setConfirmed(e.target.checked)} />Tôi đã đọc và đồng ý ký hợp đồng đặt cọc mua trọn lô này.</label>
          <div className="flex justify-end"><button type="button" className={primaryBtn} disabled={busy || !confirmed} onClick={() => sign.mutate()}><FileSignature size={16} />Ký hợp đồng đặt cọc</button></div>
          <MutationError mutation={sign} />
        </div>
      );
    } else if (hasDeposit && !d.sellerSignedAt) {
      waiting = 'Bạn đã ký. Đang chờ Seller ký hợp đồng.';
    } else if (hasDeposit && bothSigned && !d.depositReportedAt && depositPay.pending) {
      waiting = 'Đang kiểm tra hình thức thanh toán…';
    } else if (hasDeposit && bothSigned && !d.depositReportedAt && depositPay.platform) {
      action = <PayCollectionButton flow="LIQUIDATION_DEPOSIT" sourceId={id} title={`Đặt cọc ${money(d.depositAmount)} qua sàn (PayOS)`} />;
    } else if (hasDeposit && bothSigned && !d.depositReportedAt) {
      action = (
        <Form className="space-y-2.5" onSubmit={(e) => { e.preventDefault(); reportDeposit.mutate({ reference: depositRef }); }}>
          <label className="block text-xs font-semibold text-d-on-surface-variant">Tham chiếu / nội dung chuyển cọc {money(d.depositAmount)}
            <Input required disabled={busy} maxLength={255} className={`${inputClass} mt-1`} value={depositRef} onChange={(e) => setDepositRef(e.target.value)} placeholder="VD: COC-LO-001" />
          </label>
          <label className={checkLabel}><Input required type="checkbox" className={checkbox} disabled={busy} checked={confirmed} onChange={(e) => setConfirmed(e.target.checked)} />Tôi đã chuyển đủ tiền cọc cho Seller.</label>
          <div className="flex justify-end"><button className={primaryBtn} disabled={busy || !confirmed}><Wallet size={16} />Báo đã chuyển cọc</button></div>
          <MutationError mutation={reportDeposit} />
        </Form>
      );
    } else if (hasDeposit && d.depositReportedAt && !d.depositConfirmedAt) {
      waiting = `Đã báo chuyển cọc (${d.depositReference}). Chờ Seller xác nhận nhận cọc.`;
    } else if (depositOk && !d.paymentReportedAt && balancePay.pending) {
      waiting = 'Đang kiểm tra hình thức thanh toán…';
    } else if (depositOk && !d.paymentReportedAt && balancePay.platform) {
      action = <PayCollectionButton flow="LIQUIDATION_BALANCE" sourceId={id} title={`${hasDeposit ? `Phần còn lại ${money(d.remainingAmount)}` : `Thanh toán ${money(d.amount)}`} qua sàn (PayOS)`} />;
    } else if (depositOk && !d.paymentReportedAt) {
      action = (
        <Form className="space-y-2.5" onSubmit={(e) => { e.preventDefault(); report.mutate({ reference }); }}>
          <label className="block text-xs font-semibold text-d-on-surface-variant">Tham chiếu / nội dung đã chuyển {hasDeposit ? `phần còn lại ${money(d.remainingAmount)}` : `đủ ${money(d.amount)}`}
            <Input required disabled={busy} maxLength={255} className={`${inputClass} mt-1`} value={reference} onChange={(e) => setReference(e.target.value)} placeholder="VD: CK-LO-001" />
          </label>
          <label className={checkLabel}><Input required type="checkbox" className={checkbox} disabled={busy} checked={confirmed} onChange={(e) => setConfirmed(e.target.checked)} />
            {hasDeposit ? `Tôi đã chuyển đủ phần còn lại ${money(d.remainingAmount)} (sau khi trừ cọc) cho Seller.` : 'Tôi đã chuyển đủ tổng tiền của thỏa thuận cho Seller.'}
          </label>
          <div className="flex justify-end"><button className={primaryBtn} disabled={busy || !confirmed}><Banknote size={16} />Báo đã chuyển tiền</button></div>
          <MutationError mutation={report} />
        </Form>
      );
    } else if (d.paymentReportedAt && !d.paymentConfirmedAt) {
      waiting = 'Đã báo chuyển tiền. Chờ Seller xác nhận nhận đủ tiền.';
    } else if (d.paymentConfirmedAt && !d.sellerHandedOverAt && !d.receiptId) {
      waiting = 'Chờ Seller xác nhận đã bàn giao đủ toàn bộ lô. Sau đó bạn xác nhận thực nhận và nhập kho trong một lần.';
    } else if (d.paymentConfirmedAt && d.sellerHandedOverAt && !d.receiptId) {
      action = <div className="flex justify-end"><button type="button" className={primaryBtn} onClick={() => setReceiving(true)}><Warehouse size={16} />{d.ownerCollectedAt ? 'Nhập kho từ lô đã nhận' : 'Nhận hàng và nhập kho'}</button></div>;
    } else if (d.receiptId) {
      action = <div className="flex justify-end"><Link to={`/depot/inventory?tab=receipts&receiptId=${d.receiptId}`} className={primaryBtn}><ReceiptText size={16} />{posted ? 'Xem phiếu đã ghi sổ' : 'Đến phiếu nhập để kiểm tra / duyệt'}<ArrowRight size={15} /></Link></div>;
    }
  }
  const overdue = d && new Date(d.collectionDeadline).getTime() < Date.now() && !d.receiptId;

  const timeline = d && [
    ...(hasDeposit ? [
      ['Seller ký hợp đồng', localTime(d.sellerSignedAt)], ['Chủ kho ký hợp đồng', localTime(d.buyerSignedAt)],
      ['Tham chiếu chuyển cọc', d.depositReference ?? 'Chưa báo chuyển'], ['Chủ kho báo chuyển cọc', localTime(d.depositReportedAt)],
      ['Seller xác nhận nhận cọc', localTime(d.depositConfirmedAt)],
    ] : []),
    ['Tham chiếu chuyển tiền', d.paymentReference ?? 'Chưa báo chuyển'], ['Chủ kho báo chuyển tiền', localTime(d.paymentReportedAt)],
    ['Seller xác nhận đủ tiền', localTime(d.paymentConfirmedAt)], ['Seller bàn giao đủ', localTime(d.sellerHandedOverAt)],
    ['Kho xác nhận đã nhận đủ về kho', localTime(d.ownerCollectedAt)], ['Phiếu nhập', receiptLabel],
  ];

  return (
    <>
      {!receiving && <Modal
        icon={Handshake} tone="teal" title="Thỏa thuận mua lô trúng" size="lg" onClose={onClose} busy={busy}
        subtitle={d?.title}
        badge={d && <Badge tone={posted ? 'primary' : 'teal'} icon={posted ? CheckCircle2 : Hourglass}>
          {posted ? 'Hoàn thành · đã nhập kho' : d.receiptId ? receiptLabel : 'Đang thực hiện'}
        </Badge>}
        footer={d && (action ?? (waiting ? <p role="status" className="flex items-center gap-2 text-sm font-medium text-d-on-surface-variant"><Hourglass size={16} className="text-amber-500" />{waiting}</p> : null))}
      >
        <QueryState query={query}>{d && (
          <>
            <div className="grid gap-3 sm:grid-cols-3">
              <Tile icon={Coins} tone="primary" label="Giá trúng (cả lô, cố định)" value={money(d.amount)} />
              {hasDeposit && <Tile icon={BadgePercent} tone="amber" label={`Tiền cọc (${d.depositPercent}%)`} value={money(d.depositAmount)} hint={d.depositConfirmedAt ? 'Seller đã nhận cọc' : 'Chưa hoàn tất'} />}
              {hasDeposit && <Tile icon={Banknote} tone="teal" label="Phần còn lại" value={money(d.remainingAmount)} />}
            </div>

            <Section icon={Layers} title="Tiến trình thỏa thuận"><Steps steps={steps} /></Section>
            {overdue && <Notice tone="red" icon={AlertTriangle}>Đã qua hạn thu gom. Hai bên cần đối soát việc còn lại; hệ thống không tự gán lô cho người trả giá thứ hai.</Notice>}
            <Notice tone="blue" icon={FileText}>{platformMode
              ? 'Tiền cọc và phần còn lại được thanh toán vào nền tảng qua PayOS; hệ thống tự ghi nhận khi PayOS xác nhận. Giao nhận hàng vẫn do hai bên thực hiện. Không tính lại giá trúng theo kg cân về.'
              : 'Tiền chuyển và giao nhận ngoài app; RETRACK ghi nhận xác nhận của hai bên, chưa tự xác minh giao dịch ngân hàng. Không tính lại giá trúng theo kg cân về.'}</Notice>

            {hasDeposit && (
              <DepositContract
                title={d.title} amount={d.amount} depositPercent={d.depositPercent} depositAmount={d.depositAmount} remainingAmount={d.remainingAmount}
                collectionDeadline={d.collectionDeadline} sellerSignedAt={d.sellerSignedAt} buyerSignedAt={d.buyerSignedAt}
                onOpenDocument={() => setDocOpen(true)}
              />
            )}

            {hasDeposit && <LiquidationEsignPanel agreementId={id} status={esign.data} onChanged={onEsignChanged} />}

            <div className="grid gap-3 sm:grid-cols-2">
              <Tile icon={MapPin} tone="blue" label="Địa điểm" value={d.address} className="sm:col-span-2" />
              <Tile icon={Phone} tone="blue" label="Liên hệ Seller" value={d.contactPhone} />
              <Tile icon={CalendarClock} tone="amber" label="Hạn thu gom hết" value={localTime(d.collectionDeadline)} />
            </div>

            <BatchDetailSection title="Lịch sử xác nhận của hai bên">
              <dl className="grid gap-x-6 gap-y-2.5 text-sm sm:grid-cols-2">
                {timeline.map(([label, value]) => (
                  <div key={label} className="flex items-baseline justify-between gap-3 border-b border-d-border-subtle/60 pb-1.5">
                    <dt className="text-d-on-surface-variant">{label}</dt>
                    <dd className="break-words text-right font-semibold">{value}</dd>
                  </div>
                ))}
              </dl>
            </BatchDetailSection>

            <BatchDetailSection title="Xem lại phạm vi và điều kiện lô">
              <p className="whitespace-pre-wrap break-words text-sm">{d.scopeDetails}</p>
              <p className="mt-3 whitespace-pre-wrap break-words rounded-xl bg-d-surface-container-low p-3 text-sm">{d.collectionTerms}</p>
            </BatchDetailSection>
          </>
        )}</QueryState>
      </Modal>}
      {docOpen && d && <ContractViewer contract={toContract(d, d)} onClose={() => setDocOpen(false)} />}
      {receiving && <StockReceiptForm agreement={d} confirmBulkReceived={!d.ownerCollectedAt} onClose={() => {
        setReceiving(false);
        client.invalidateQueries({ queryKey: ['liquidation', userId, depotId] });
      }} />}
    </>
  );
}
