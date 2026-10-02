import { useDepotQuery, number, money, batchLabels } from '../depotApi';
import { Dialog, QueryState } from './DepotUI';
import { materialLabel } from '../materialLabels';
import { proofExtension } from '../proofDownload';
const missing = 'Chưa cập nhật';
const kg = (v) => v == null ? missing : `${number(v)} kg`;
const percent = (v) => v == null ? missing : `${number(v)}%`;
const cash = (v) => v == null ? missing : money(v);
const time = (v) => v ? new Date(v).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' }) : missing;
function Rows({ title, items }) {
  return <section className="rounded-2xl border border-d-border-subtle bg-white p-4"><h3 className="font-semibold text-d-primary mb-3">{title}</h3><dl className="grid grid-cols-1 sm:grid-cols-2 gap-4">{items.map(([label, value]) => <div key={label}><dt className="text-sm text-d-on-surface-variant">{label}</dt><dd className="mt-1 font-medium break-words">{value ?? missing}</dd></div>)}</dl></section>;
}
function Proof({ title, url }) {
  const extension = proofExtension(url);
  if (extension) return <p className="text-sm">{title}: <a className="text-d-primary underline" href={url} download={`chung-tu.${extension}`}>Tải {title.toLocaleLowerCase('vi-VN')}</a></p>;
  let safe = false;
  try { safe = ['https:', 'http:'].includes(new URL(url).protocol); } catch { /* Không mở URL chứng từ không hợp lệ. */ }
  return <p className="text-sm">{title}: {safe ? <a className="text-d-primary underline" href={url} target="_blank" rel="noopener noreferrer">Mở chứng từ</a> : missing}</p>;
}
export default function BatchDetailModal({ id, onClose }) {
  const query = useDepotQuery(`batches/${id}`);
  const d = query.data, b = d?.batch, q = d?.quality, s = d?.settlement;
  const transportLabels = { PENDING: 'Chờ phân công', ASSIGNED: 'Đã phân công', PICKED_UP: 'Đã lấy hàng', IN_TRANSIT: 'Đang vận chuyển', DELIVERED: 'Đã giao' };
  return <Dialog title={`Chi tiết lô ${b?.code ?? ''}`} onClose={onClose}><QueryState query={query}>{d && <div className="space-y-4">
    <div className="rounded-xl bg-d-surface-accent p-4"><p className="font-bold text-d-primary">{batchLabels[b.status] ?? b.status}</p><p className="text-sm mt-2">Thời gian hiển thị theo giờ Việt Nam.</p></div>
    {b.status === 'REJECTED' && <p role="status" className="rounded-xl border border-d-error p-3 text-d-error">Lô bị từ chối — cần kiểm tra lý do và hướng xử lý. Trạng thái này không xác nhận hàng đã quay về kho.</p>}
    <Rows title="Thông tin lô" items={[["Mã lô", b.code ?? b.id], ["Vật liệu", materialLabel(b.materialType)], ["Khối lượng khai báo", kg(b.weightKg)], ["Nhà máy", b.factoryName ?? 'Đăng công khai'], ["Ngày tạo", time(b.createdAt)], ["Ghi chú kho", b.description]]}/>
    <section className="rounded-2xl border border-d-border-subtle bg-white p-4"><h3 className="font-semibold text-d-primary mb-3">Ảnh vật liệu khi tạo lô</h3>
      {b.imageUrls?.length ? <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">{b.imageUrls.filter((url) => { try { return new URL(url).protocol === 'https:'; } catch { return false; } }).map((url, index) =>
        <a key={url} href={url} target="_blank" rel="noopener noreferrer"><img src={url} alt={`Ảnh vật liệu ${index + 1}`} className="h-32 w-full rounded-xl object-cover" /></a>)}</div>
        : <p className="text-sm text-d-on-surface-variant">Lô này chưa có ảnh vật liệu.</p>}</section>
    <Rows title="Vận chuyển và nhận hàng" items={[["Vận chuyển", transportLabels[d.transportStatus] ?? d.transportStatus], ["Tài xế", d.driverName], ["Nhà máy nhận hàng", time(d.receivedAt)], ["Quyết định chất lượng", time(d.decidedAt)]]}/>
    <Rows title="Cân và kiểm tra chất lượng (KCS)" items={[["Khối lượng thực tế", kg(q?.actualWeightKg)], ["Khối lượng tổng", kg(q?.grossWeightKg)], ["Khối lượng bì", kg(q?.tareWeightKg)], ["Độ lệch", percent(q?.differencePercentage)], ["Phân hạng", q?.grade === 'PENDING' ? 'Chờ kiểm tra' : q?.grade], ["Kết quả", q?.isAccepted == null ? 'Chưa chốt' : q.isAccepted ? 'Chấp nhận' : 'Từ chối'], ["Độ tinh khiết", percent(q?.purityPercent)], ["Độ ẩm", percent(q?.moisturePercent)], ["Tạp chất", percent(q?.contaminationPercent)], ["Biên bản cân", q?.ticketNumber], ["Lý do từ chối", d.rejectionReason], ["Hướng xử lý", q?.resolution], ["Ghi chú nhà máy", q?.note]]}/>
    <Proof title="Phiếu cân" url={q?.ticketImageUrl}/>
    <Rows title="Quyết toán do nhà máy ghi nhận" items={[["Đơn giá / kg", cash(s?.pricePerKg)], ["Tổng tiền", cash(s?.grossAmount)], ["Phí nền tảng", cash(s?.feeAmount)], ["Tiền thực nhận theo quyết toán", cash(s?.netAmount)], ["Tham chiếu thanh toán", s?.paymentReference], ["Ngày quyết toán", time(s?.settledAt)], ["Số hóa đơn", q?.invoiceNumber]]}/>
    <p className="text-sm text-d-on-surface-variant">Số liệu do nhà máy ghi nhận; chưa phải xác nhận nhận tiền từ phía chủ kho.</p>
    <Proof title="Hóa đơn nhà máy" url={q?.invoiceFileUrl}/><Proof title="Chứng từ thanh toán" url={s?.paymentProofUrl}/>
  </div>}</QueryState></Dialog>;
}
