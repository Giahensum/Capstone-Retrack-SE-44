import { useState } from 'react';
import { useDepotMutation, useDepotQuery, money, batchLabels } from '../depotApi';
import { buttonClass, inputClass, MutationError, QueryState, Pager } from './DepotUI';
import BatchDetailSection from './BatchDetailSection';

const actions = { PRICE_PROPOSED: 'Nhà máy gửi đề xuất giá', PRICE_ACCEPTED: 'Kho đồng ý giá', PRICE_DISPUTED: 'Kho yêu cầu điều chỉnh', PAYMENT_REPORTED: 'Nhà máy báo đã chuyển tiền', PAYMENT_CONFIRMED: 'Kho xác nhận nhận đủ tiền', QC_ACCEPTED: 'KCS đạt', QC_REJECTED: 'KCS không đạt', QC_RESOLUTION: 'Chọn hướng xử lý KCS', RETURN_HANDED_OVER: 'Nhà máy bàn giao trả', RETURN_RECEIVED: 'Kho nhận đủ lô trả' };
export default function BatchSettlementPanel({ detail, id }) {
  const { batch, workflow, settlement } = detail;
  const [checked, setChecked] = useState(false), [reason, setReason] = useState(''), [page, setPage] = useState(1);
  const decision = useDepotMutation('patch', `batches/${id}/settlement-decision`, () => setChecked(false));
  const history = useDepotQuery(`batches/${id}/workflow-events`, { page }, workflow?.version > 0);
  if (!workflow?.version) return null;
  const pending = batch.status === 'SETTLEMENT_PENDING';
  const decide = (accept) => decision.mutate({ body: { accept, revision: workflow.revision, amount: settlement.netAmount, reason } });
  return <section className="space-y-4 rounded-2xl border border-d-border-subtle bg-white p-4">
    <h3 className="font-semibold text-d-primary">Thỏa thuận giá và xác nhận tiền</h3>
    <p className="text-sm text-d-on-surface-variant">Nhà máy đề xuất → kho đồng ý → nhà máy chuyển tiền và gửi chứng từ → kho đối chiếu tài khoản và xác nhận nhận đủ tiền. RETRACK ghi nhận các xác nhận, không tự xác minh chuyển khoản ngân hàng.</p>
    <p className="text-sm font-medium">{batchLabels[batch.status] ?? batch.status}{workflow.revision > 0 ? ` · Đề xuất phiên bản ${workflow.revision}` : ''}</p>
    {workflow.decisionReason && <p className="text-sm whitespace-pre-wrap">Phản hồi của kho: {workflow.decisionReason}</p>}
    {pending && <div className="rounded-xl bg-d-surface-accent p-4 space-y-3">
      <p className="font-semibold">Số tiền nhà máy sẽ chuyển cho kho: {money(settlement.netAmount)}</p>
      <p className="text-sm">Đối chiếu khối lượng thực cân, KCS, đơn giá và VAT ở bảng phía trên. Phí bên Factory trả riêng không trừ vào số tiền này.</p>
      <label className="block text-sm">Lý do / nội dung đề nghị điều chỉnh<textarea maxLength={2000} className={inputClass} value={reason} disabled={decision.isPending} onChange={e => setReason(e.target.value)} /></label>
      <label className="flex gap-2 text-sm"><input type="checkbox" checked={checked} disabled={decision.isPending} onChange={e => setChecked(e.target.checked)} />{detail.quality?.isAccepted === false ? "Tôi đồng ý bán lô theo hiện trạng KCS không đạt, với khối lượng, đơn giá và số tiền của phiên bản này." : "Tôi đồng ý khối lượng, đơn giá và số tiền của phiên bản này."}</label>
      <div className="flex flex-wrap gap-3">
        <button type="button" className={buttonClass} disabled={!checked || decision.isPending} onClick={() => decide(true)}>Đồng ý giá và số tiền</button>
        <button type="button" className="rounded-full border border-d-border-subtle px-4 py-2 text-d-primary disabled:opacity-40" disabled={!reason.trim() || decision.isPending} onClick={() => decide(false)}>Yêu cầu điều chỉnh giá</button>
      </div><MutationError mutation={decision} />
    </div>}
    <BatchDetailSection title={<span role="heading" aria-level="4">Lịch sử xác nhận của hai bên</span>}>
    <QueryState query={history}><ol className="space-y-3">{history.data?.items.map(event => <li key={event.id} className="rounded-xl bg-d-surface-accent p-3 text-sm">
      <p className="font-semibold">{actions[event.action] ?? event.action}{event.revision ? ` · Phiên bản ${event.revision}` : ''}</p>
      {event.netAmount != null && <p>Đơn giá: {money(event.pricePerKg)}/kg · Tiền cho kho: {money(event.netAmount)}</p>}
      <p className="text-d-on-surface-variant">{new Date(event.createdAt).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' })}</p>
      {event.note && <p className="whitespace-pre-wrap">{event.note}</p>}
    </li>)}</ol>{!history.data?.totalCount && <p className="text-sm text-d-on-surface-variant">Chưa có xác nhận giá hoặc tiền.</p>}{history.data?.totalCount > 20 && <Pager page={page} setPage={setPage} total={history.data.totalCount} />}</QueryState>
    </BatchDetailSection>
  </section>;
}
