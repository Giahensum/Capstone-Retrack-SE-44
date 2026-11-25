import { useDepotQuery, useDepotMutation, number, money, batchLabels } from '../depotApi';
import { Dialog, QueryState } from './DepotUI';
import { materialLabel } from '../materialLabels';
import { factoryProofPath, proofExtension } from '../proofDownload';
import { useState } from 'react';
import api from '@/lib/axios';
import { depotError } from '../DepotContext';
import BatchCostPanel from './BatchCostPanel';
import DriverEventsPanel from './DriverEventsPanel';
import BatchSettlementPanel from './BatchSettlementPanel';
import BatchReturnPanel from './BatchReturnPanel';
import BatchDetailSection from './BatchDetailSection';
import { paymentMethodLabel } from '@/lib/paymentMethods';
const missing = 'Chưa cập nhật';
const kg = (v) => v == null ? missing : `${number(v)} kg`;
const percent = (v) => v == null ? missing : `${number(v)}%`;
const cash = (v) => v == null ? missing : money(v);
const time = (v) => v ? new Date(v).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' }) : missing;
const nextSteps = {
  DRAFT: 'Lô đang ở bản nháp, chưa gửi cho nhà máy.',
  LISTED: 'Chờ nhà máy nhận lô đang đăng công khai.', MARKETPLACE: 'Chờ nhà máy nhận lô đang đăng công khai.',
  PENDING_APPROVAL: 'Chờ nhà máy duyệt đề nghị nhận lô.', PENDING_FACTORY: 'Chờ nhà máy duyệt đề nghị nhận lô.',
  TRANSPORT_READY: 'Chờ phân công vận chuyển. Sau khi giao hàng, nhà máy sẽ cân và kiểm tra chất lượng.',
  ACCEPTED: 'Nhà máy đã nhận đề nghị. Theo dõi phân công và giao hàng ở mục vận chuyển.',
  READY_FOR_PICKUP: 'Chờ tài xế lấy hàng tại kho.',
  IN_PROGRESS: 'Hàng đang vận chuyển. Theo dõi giao nhận và liên hệ khi có sự cố.',
  IN_TRANSIT: 'Hàng đang vận chuyển. Theo dõi giao nhận và liên hệ khi có sự cố.',
  DELIVERED: 'Tài xế báo đã giao. Chờ nhà máy xác nhận nhận hàng, cân và kiểm tra chất lượng.',
  RECEIVED: 'Nhà máy đã nhận hàng; chờ cân và kiểm tra chất lượng.',
  WEIGHED: 'Nhà máy đã cân; chờ kết quả kiểm tra chất lượng (KCS).',
  VERIFIED: 'Đã có kết quả KCS. Chờ nhà máy đề xuất giá và số tiền thanh toán.',
  SETTLEMENT_PENDING: 'Đối chiếu cân, KCS và số tiền bên dưới, sau đó đồng ý hoặc yêu cầu điều chỉnh giá.',
  SETTLEMENT_DISPUTED: 'Đã yêu cầu điều chỉnh; chờ nhà máy gửi đề xuất giá mới.',
  SETTLEMENT_ACCEPTED: 'Đã đồng ý giá. Chờ nhà máy chuyển tiền và gửi chứng từ.',
  COMPLETED: 'Nhà máy báo đã chuyển tiền. Kiểm tra tài khoản ngân hàng trước khi xác nhận nhận đủ tiền.',
  PAID: 'Kho đã xác nhận nhận tiền. Có thể xem lại chứng từ và đối chiếu chi phí nội bộ.',
  NEGOTIATING: 'KCS không đạt; chờ nhà máy đề xuất giá mua theo hiện trạng.',
  RETURN_REQUESTED: 'Chờ nhà máy bàn giao trả hàng; chưa hoàn tồn kho.',
  RETURNING: 'Kiểm tra hàng trả thực tế và xác nhận nhận đủ toàn bộ lô bên dưới.',
  RETURNED: 'Đã xác nhận nhận đủ lô trả và hoàn tồn; đây không phải giao dịch bán đã thanh toán.',
  REJECTED: 'Kiểm tra lý do từ chối và phối hợp nhà máy xử lý; chưa tự hoàn tồn.',
  CANCELLED: 'Lô đã hủy; không cần xác nhận giao hàng hoặc nhận tiền cho lô này.',
};
function Rows({ title, items }) {
  return <section className="rounded-2xl border border-d-border-subtle bg-white p-4"><h3 className="font-semibold text-d-primary mb-3">{title}</h3><dl className="grid grid-cols-1 sm:grid-cols-2 gap-4">{items.map(([label, value]) => <div key={label}><dt className="text-sm text-d-on-surface-variant">{label}</dt><dd className="mt-1 font-medium break-words">{value ?? missing}</dd></div>)}</dl></section>;
}
function Proof({ title, url }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const path = factoryProofPath(url);
  if (path) return <div className="text-sm"><span>{title}: </span><button type="button" disabled={busy}
    className="rounded-full border border-d-border-subtle px-4 py-2 text-d-primary disabled:opacity-40"
    onClick={async () => {
      setBusy(true); setError('');
      try {
        const response = await api.get(path, { responseType: 'blob' });
        const extensions = { 'image/png': 'png', 'image/jpeg': 'jpg', 'application/pdf': 'pdf' };
        const extension = extensions[response.data.type];
        if (!extension || response.data.size > 700 * 1024) throw new Error('Chứng từ có định dạng hoặc dung lượng không hợp lệ.');
        const objectUrl = URL.createObjectURL(response.data);
        const link = document.createElement('a');
        link.href = objectUrl; link.download = `chung-tu.${extension}`; link.click();
        setTimeout(() => URL.revokeObjectURL(objectUrl), 1000);
      } catch (e) { setError(e.response?.data?.message || (e.response ? depotError(e) : e.message)); }
      finally { setBusy(false); }
    }}>{busy ? 'Đang tải…' : `Tải ${title.toLocaleLowerCase('vi-VN')}`}</button>
    {error && <p role="alert" className="text-d-error mt-2">{error}</p>}</div>;
  const extension = proofExtension(url);
  if (extension) return <p className="text-sm">{title}: <a className="text-d-primary underline" href={url} download={`chung-tu.${extension}`}>Tải {title.toLocaleLowerCase('vi-VN')}</a></p>;
  let safe = false;
  try { safe = ['https:', 'http:'].includes(new URL(url).protocol); } catch { /* Không mở URL chứng từ không hợp lệ. */ }
  return <p className="text-sm">{title}: {safe ? <a className="text-d-primary underline" href={url} target="_blank" rel="noopener noreferrer">Mở chứng từ</a> : missing}</p>;
}
export default function BatchDetailModal({ id, onClose }) {
  const query = useDepotQuery(`batches/${id}`);
  const confirmPayment = useDepotMutation('patch', `batches/${id}/confirm-payment`);
  const [confirming, setConfirming] = useState(false);
  const d = query.data, b = d?.batch, q = d?.quality, s = d?.settlement;
  const transportLabels = { PENDING: 'Chờ phân công', ASSIGNED: 'Đã phân công', ACCEPTED: 'Tài xế đã nhận chuyến', PICKED_UP: 'Đã lấy hàng', IN_TRANSIT: 'Đang vận chuyển', ON_THE_WAY: 'Đang vận chuyển', DELIVERED: 'Đã giao' };
  return <Dialog title={`Chi tiết lô ${b?.code ?? ''}`} onClose={onClose} wide><QueryState query={query}>{d && <div className="space-y-4">
    <div className="rounded-xl bg-d-surface-accent p-4 space-y-2"><p className="font-bold text-d-primary">{batchLabels[b.status] ?? b.status}</p><p className="text-sm">{nextSteps[b.status] ?? 'Theo dõi thông tin lô và phản hồi của nhà máy.'}</p><p className="text-xs text-d-on-surface-variant">Thời gian hiển thị theo giờ Việt Nam. Giá vốn và chi phí là thông tin tùy chọn của kho.</p></div>
    {b.status === 'REJECTED' && <p role="status" className="rounded-xl border border-d-error p-3 text-d-error">Lô bị từ chối — cần kiểm tra lý do và hướng xử lý. Trạng thái này không xác nhận hàng đã quay về kho.</p>}
    <Rows title="Thông tin lô" items={[["Mã lô", b.code ?? b.id], ["Vật liệu", materialLabel(b.materialType)], ["Khối lượng khai báo", kg(b.weightKg)], ["Nhà máy", b.factoryName ?? 'Đăng công khai'], ["Ngày tạo", time(b.createdAt)], ["Ghi chú kho", b.description]]}/>
    {b.imageUrls?.length > 0 && <BatchDetailSection title="Ảnh vật liệu khi tạo lô">
      {b.imageUrls?.length ? <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">{b.imageUrls.filter((url) => { try { return new URL(url).protocol === 'https:'; } catch { return false; } }).map((url, index) =>
        <a key={url} href={url} target="_blank" rel="noopener noreferrer"><img src={url} alt={`Ảnh vật liệu ${index + 1}`} className="h-32 w-full rounded-xl object-cover" /></a>)}</div>
        : <p className="text-sm text-d-on-surface-variant">Lô này chưa có ảnh vật liệu.</p>}</BatchDetailSection>}
    <Rows title="Vận chuyển và nhận hàng" items={[["Vận chuyển", transportLabels[d.transportStatus] ?? d.transportStatus], ["Tài xế", d.driverName], ["Nhà máy nhận hàng", time(d.receivedAt)], ["Quyết định chất lượng", time(d.decidedAt)]]}/>
    {q ? <><Rows title="Cân và kiểm tra chất lượng (KCS)" items={[["Khối lượng thực tế", kg(q.actualWeightKg)], ["Khối lượng tổng", kg(q.grossWeightKg)], ["Khối lượng bì", kg(q.tareWeightKg)], ["Độ lệch", percent(q.differencePercentage)], ["Phân hạng", q.grade === 'PENDING' ? 'Chờ kiểm tra' : q.grade], ["Kết quả", q.isAccepted == null ? 'Chưa chốt' : q.isAccepted ? 'Chấp nhận' : 'Từ chối'], ["Độ tinh khiết", percent(q.purityPercent)], ["Độ ẩm", percent(q.moisturePercent)], ["Tạp chất", percent(q.contaminationPercent)], ["Biên bản cân", q.ticketNumber], ["Lý do từ chối", d.rejectionReason], ["Hướng xử lý", ({ RETURN: 'Trả toàn bộ lô', RENEGOTIATE: 'Thương lượng mua theo hiện trạng' })[q.resolution] ?? q.resolution], ["Ghi chú nhà máy", q.note?.replace(/^(RENEGOTIATE|RETURN):\s*/, '')]]}/>
    {q.ticketImageUrl && <Proof title="Phiếu cân" url={q.ticketImageUrl}/>}</> : <p className="text-sm text-d-on-surface-variant">Chưa có phiếu cân/KCS từ nhà máy.</p>}
    {s && <Rows title={d.workflow?.version && b.status !== 'PAID' ? 'Đề xuất giá và số tiền' : 'Quyết toán do nhà máy ghi nhận'} items={[["Đơn giá thỏa thuận / kg", cash(s?.pricePerKg)],
      ["Cơ sở đơn giá", s?.priceIncludesVat == null ? missing : s.priceIncludesVat ? 'Đã gồm VAT' : 'Chưa gồm VAT'],
      ["Tiền hàng trước VAT", cash(s?.grossAmount)],
      ["Cách khai VAT", ({ NOT_APPLICABLE: 'Không áp dụng VAT', ZERO_RATE: 'VAT 0%', TAXABLE: 'Có VAT theo hóa đơn' })[s?.vatTreatment] ?? missing],
      ["Thuế suất VAT", percent(s?.vatPercentage)], ["Tiền VAT theo hóa đơn", cash(s?.vatAmount)],
      ["Tổng tiền theo hóa đơn", cash(s?.invoiceTotalAmount)], [s?.feePolicy === "BUYER_SEPARATE_V1" ? "Phí do Factory trả riêng" : "Phí khấu trừ — lô cũ", cash(s?.feeAmount)],
      ["Tiền thanh toán theo quyết toán, gồm VAT đã khai", cash(s?.netAmount)],
      ["Hình thức thanh toán", paymentMethodLabel(s?.paymentMethod)], ["Tham chiếu thanh toán", s?.paymentReference], [d.workflow?.version && b.status !== 'PAID' ? "Ngày đề xuất giá" : "Ngày quyết toán", time(s?.settledAt)], ["Vựa xác nhận nhận tiền", time(s?.paymentConfirmedAt)], ["Số hóa đơn", q?.invoiceNumber]]}/> }
    <BatchReturnPanel key={id} detail={d} id={id} handoverProof={<Proof title="Biên bản bàn giao trả" url={d.return?.handoverProofUrl}/>} />
    {(s || ['SETTLEMENT_PENDING', 'SETTLEMENT_ACCEPTED', 'SETTLEMENT_DISPUTED', 'COMPLETED', 'PAID'].includes(b.status)) && <BatchSettlementPanel key={`${id}-${d.workflow?.revision ?? 0}`} detail={d} id={id} />}
    {b.status === 'COMPLETED' && (s?.paymentProofUrl || ['CASH', 'OTHER'].includes(s?.paymentMethod)) && <section className="rounded-2xl border border-d-border-subtle bg-white p-4 space-y-3">
      <h3 className="font-semibold text-d-primary">Xác nhận tiền thực nhận</h3>
      <p className="text-sm text-d-on-surface-variant">Hình thức {paymentMethodLabel(s.paymentMethod)}. Đối chiếu đã nhận đủ {cash(s.netAmount)} và căn cứ {s.paymentReference} trước khi xác nhận.</p>
      <label className="flex items-start gap-2 text-sm"><input type="checkbox" checked={confirming} onChange={(e) => setConfirming(e.target.checked)} />Tôi đã kiểm tra và nhận đủ số tiền trên.</label>
      <button type="button" disabled={!confirming || confirmPayment.isPending} className="rounded-xl bg-d-primary px-4 py-2 text-white disabled:opacity-40"
        onClick={() => confirmPayment.mutate({ body: { amount: s.netAmount, paymentReference: s.paymentReference } })}>
        {confirmPayment.isPending ? 'Đang xác nhận…' : 'Xác nhận đã nhận tiền'}
      </button>
      {confirmPayment.isError && <p role="alert" className="text-d-error text-sm">{depotError(confirmPayment.error)}</p>}
      {confirmPayment.isSuccess && <p role="status" className="text-d-primary text-sm">Đã xác nhận nhận tiền.</p>}
    </section>}
    {s && !['PAID', 'CANCELLED', 'RETURNED'].includes(b.status) && <p className="text-sm text-d-on-surface-variant">Số liệu do nhà máy ghi nhận; vựa chưa xác nhận nhận tiền.</p>}
    {(q?.invoiceFileUrl || s?.paymentProofUrl) && <section className="rounded-2xl border border-d-border-subtle bg-white p-4 space-y-3"><h3 className="font-semibold text-d-primary">Chứng từ giao dịch</h3>{q?.invoiceFileUrl && <Proof title="Hóa đơn nhà máy" url={q.invoiceFileUrl}/ >}{s?.paymentProofUrl && <Proof title="Chứng từ thanh toán" url={s.paymentProofUrl}/>}</section>}
    <BatchDetailSection title="Giá vốn và chi phí của kho (tùy chọn)"><BatchCostPanel id={id} quantity={b.weightKg} /></BatchDetailSection>
    <BatchDetailSection title="Nhật ký vận chuyển và sự cố"><DriverEventsPanel id={id} /></BatchDetailSection>
  </div>}</QueryState></Dialog>;
}
