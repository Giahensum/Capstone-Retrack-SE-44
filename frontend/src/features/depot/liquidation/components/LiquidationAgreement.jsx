import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Dialog, QueryState, MutationError, inputClass, buttonClass } from '@/features/depot/components/DepotUI';
import { money } from '@/features/depot/depotApi';
import StockReceiptForm from '@/features/depot/components/StockReceiptForm';
import { useLiquidationQuery, useLiquidationMutation, localTime } from '../liquidationApi';

export default function LiquidationAgreement({ id, onClose }) {
  const query = useLiquidationQuery(`/agreements/${id}`, {}, true, true);
  const [reference, setReference] = useState('');
  const [confirmed, setConfirmed] = useState(false);
  const [receiving, setReceiving] = useState(false);
  const report = useLiquidationMutation('patch', `/agreements/${id}/report-payment`, () => setConfirmed(false));
  const handover = useLiquidationMutation('patch', `/agreements/${id}/confirm-collected`, () => setConfirmed(false));
  const d = query.data;
  const busy = report.isPending || handover.isPending;
  return <Dialog title="Thỏa thuận mua lô trúng" onClose={onClose} busy={busy} wide>
    <QueryState query={query}>{d && <div className="space-y-4">
      <h3 className="font-semibold text-lg">{d.title}</h3><p className="text-2xl font-bold text-d-primary">{money(d.amount)} / cả lô</p>
      <p className="rounded-xl bg-d-surface-accent p-3 text-sm">Tiền chuyển và giao nhận ngoài app; RETRACK ghi nhận xác nhận của hai bên, chưa tự xác minh giao dịch ngân hàng. Không tính lại giá trúng theo kg cân về.</p>
      <p className="whitespace-pre-wrap break-words">{d.scopeDetails}</p><p className="whitespace-pre-wrap break-words">{d.collectionTerms}</p>
      <dl className="grid sm:grid-cols-2 gap-4 bg-white border border-d-border-subtle rounded-xl p-4">{[
        ['Địa điểm', d.address], ['Liên hệ Seller', d.contactPhone], ['Hạn thu gom hết', localTime(d.collectionDeadline)],
        ['Tham chiếu chuyển tiền', d.paymentReference ?? 'Chưa báo chuyển'], ['Chủ kho báo chuyển tiền', localTime(d.paymentReportedAt)],
        ['Seller xác nhận đủ tiền', localTime(d.paymentConfirmedAt)], ['Seller bàn giao đủ', localTime(d.sellerHandedOverAt)],
        ['Kho xác nhận đã nhận đủ về kho', localTime(d.ownerCollectedAt)], ['Phiếu nhập', d.receiptStatus ?? 'Chưa lập']
      ].map(([label, value]) => <div key={label}><dt className="text-sm text-d-on-surface-variant">{label}</dt><dd className="font-medium mt-1 break-words">{value}</dd></div>)}</dl>
      {new Date(d.collectionDeadline).getTime() < Date.now() && !d.receiptId && <p role="status" className="text-d-error">Đã qua hạn thu gom. Hai bên cần đối soát việc còn lại; hệ thống không tự gán lô cho người trả giá thứ hai.</p>}
      {!d.paymentReportedAt && <form className="space-y-3" onSubmit={e => { e.preventDefault(); report.mutate({ reference }); }}><label className="block">Tham chiếu / nội dung đã chuyển đủ tiền<input required disabled={busy} maxLength={255} className={inputClass} value={reference} onChange={e => setReference(e.target.value)} /></label><label className="flex gap-3"><input required type="checkbox" disabled={busy} checked={confirmed} onChange={e => setConfirmed(e.target.checked)} />Tôi đã chuyển đủ tổng tiền của thỏa thuận cho Seller.</label><button className={buttonClass} disabled={busy || !confirmed}>Báo đã chuyển tiền ngoài hệ thống</button><MutationError mutation={report} /></form>}
      {d.paymentConfirmedAt && !d.ownerCollectedAt && <div className="space-y-3"><label className="flex gap-3"><input type="checkbox" disabled={busy} checked={confirmed} onChange={e => setConfirmed(e.target.checked)} />Tôi đã thu gom đủ và chở hàng thực tế về kho.</label><button type="button" className={buttonClass} disabled={busy || !confirmed} onClick={() => handover.mutate()}>Xác nhận đã nhận đủ hàng về kho</button><MutationError mutation={handover} /></div>}
      {d.paymentConfirmedAt && d.sellerHandedOverAt && d.ownerCollectedAt && !d.receiptId && <button type="button" className={buttonClass} onClick={() => setReceiving(true)}>Lập phiếu nhập từ lô trúng</button>}
      {d.receiptId && <Link to="/depot/inventory?tab=receipts" className={buttonClass + ' inline-block'}>Đến phiếu nhập để kiểm tra / duyệt</Link>}
      {receiving && <StockReceiptForm agreement={d} onClose={() => { setReceiving(false); query.refetch(); }} />}
    </div>}</QueryState>
  </Dialog>;
}
