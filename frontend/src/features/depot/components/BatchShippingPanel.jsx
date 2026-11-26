import { useState } from 'react';
import { Input, Select, Textarea } from '@/components/forms/ValidatedForm';
import { useDepotQuery, useDepotMutation, money, number } from '../depotApi';
import { QueryState, MutationError, inputClass, buttonClass, secondaryButtonClass } from './DepotUI';
import ShippingQuoteForm from './ShippingQuoteForm';

const labels = { BOOKING: 'Đang gửi yêu cầu', UNKNOWN: 'Cần đối soát yêu cầu đặt xe', ASSIGNING_DRIVER: 'Đang tìm xe', ON_GOING: 'Tài xế đang đến lấy hàng', PICKED_UP: 'Đang vận chuyển', COMPLETED: 'Đã giao hàng', CANCELED: 'Đã hủy', REJECTED: 'Không bố trí được xe', EXPIRED: 'Hết thời gian tìm xe', CANCEL_REQUESTED: 'Đang xác nhận hủy' };
const terminal = ['COMPLETED', 'CANCELED', 'REJECTED', 'EXPIRED'];

export default function BatchShippingPanel({ batch }) {
  const q = useDepotQuery(`shipping/batches/${batch.id}`, {}, true, 5000);
  return <section className="rounded-2xl border border-d-border-subtle bg-white p-4 space-y-3"><h3 className="font-semibold text-d-primary">Phương án vận chuyển</h3>
    <QueryState query={q}>{q.data && <ShippingActions key={`${batch.id}-${q.data.method}-${q.data.shipment?.id ?? ''}`} batch={batch} data={q.data} />}</QueryState>
  </section>;
}
function ShippingActions({ batch, data }) {
  const [recoveryId, setRecoveryId] = useState('');
  const recover = useDepotMutation('post', `shipping/batches/${batch.id}/recover`);
  const [method, setMethod] = useState(data.method);
  const [quote, setQuote] = useState(null);
  const [operationId, setOperationId] = useState(() => crypto.randomUUID());
  const [contacts, setContacts] = useState({ senderName: '', senderPhone: '', recipientName: '', recipientPhone: '', note: '' });
  const [confirmed, setConfirmed] = useState(false);
  const save = useDepotMutation('put', `shipping/batches/${batch.id}/method`);
  const book = useDepotMutation('post', `shipping/batches/${batch.id}/book`);
  const refresh = useDepotMutation('post', `shipping/batches/${batch.id}/refresh`);
  const cancel = useDepotMutation('post', `shipping/batches/${batch.id}/cancel`);
  const shipment = data.shipment;
  const live = shipment && !terminal.includes(shipment.status);
  const ready = ['TRANSPORT_READY', 'ACCEPTED', 'READY_FOR_PICKUP'].includes(batch.status);
  const editable = ['DRAFT', 'LISTED', 'PENDING_APPROVAL', 'PENDING_FACTORY', 'TRANSPORT_READY', 'ACCEPTED', 'READY_FOR_PICKUP'].includes(batch.status) && !live;
  const busy = save.isPending || book.isPending || cancel.isPending || recover.isPending || refresh.isPending;
  const bookingQuote = quote ?? data.quote;
  const missingContacts = Object.entries(contacts).some(([key, value]) => key !== 'note' && !value.trim());
  const bookingHint = method !== 'EXTERNAL' ? 'Chọn phương thức thuê vận chuyển ngoài để gọi xe.'
    : !bookingQuote ? 'Chưa có báo giá. Bấm “Lấy báo giá vận chuyển” ở phía trên trước khi gọi xe.'
      : missingContacts ? 'Điền đủ tên và số điện thoại của người giao, người nhận.'
        : !confirmed ? 'Tích xác nhận đã kiểm tra loại xe, tải trọng và đồng ý chi phí.' : null;
  const safeLink = (url) => { try { return new URL(url).protocol === 'https:'; } catch { return false; } };
  return <div className="space-y-3">
    <p>Đang sử dụng: <strong>{data.method === 'EXTERNAL' ? 'Thuê vận chuyển ngoài' : 'Xe tải của kho'}</strong></p>
    {data.method === 'EXTERNAL' && !shipment && <div role="status" className="rounded-xl bg-d-surface-accent p-3 text-sm space-y-1"><p className="font-semibold">Chưa đặt xe vận chuyển</p><p>{ready ? 'Lô đã sẵn sàng. Lấy báo giá, lưu phương án và nhập thông tin giao nhận bên dưới để xác nhận gọi xe.' : 'Đợi nhà máy chấp thuận lô. Sau đó quay lại đây để nhập thông tin giao nhận và xác nhận gọi xe.'}</p><p>Chọn thuê ngoài khi tạo lô chưa tự phát sinh vận đơn.</p></div>}
    {data.quote && <p className="text-sm">Chi phí dự kiến đã lưu: <strong>{money(data.quote.estimatedCost)}</strong> · hệ số {number(data.quote.factor * 100)}%. Đây là ước tính, chưa tự ghi vào sổ chi phí thực tế.</p>}
    {editable && <>
      <label className="block text-sm">Phương thức vận chuyển<Select className={inputClass} value={method} disabled={busy} onChange={e => { setMethod(e.target.value); setQuote(null); setConfirmed(false); setOperationId(crypto.randomUUID()); }}><option value="INTERNAL">Xe tải của kho</option><option value="EXTERNAL">Thuê vận chuyển ngoài</option></Select></label>
      <ShippingQuoteForm key={`${method}-${batch.targetFactoryId}-${batch.weightKg}`} factoryId={batch.targetFactoryId} materialType={batch.materialType} weightKg={batch.weightKg} method={method} onQuote={value => { setQuote(value); setOperationId(crypto.randomUUID()); }} />
      <button type="button" disabled={busy} className={secondaryButtonClass} onClick={() => save.mutate({ body: { method, quoteId: quote?.id ?? null } })}>{save.isPending ? 'Đang lưu…' : 'Lưu phương án vận chuyển'}</button>
      <MutationError mutation={save} />
    </>}
    {data.method === 'EXTERNAL' && !live && ready && <div className="space-y-3 border-t pt-3">
      <h4 className="font-semibold text-d-primary">Thông tin giao nhận và đặt xe</h4>
      <p className="text-sm">Lấy báo giá, nhập thông tin giao nhận rồi xác nhận gọi xe. Báo giá đang chọn sẽ được lưu cùng yêu cầu đặt xe; không cần nhập các mục chi phí khác bên dưới.</p>
      <div className="grid sm:grid-cols-2 gap-3">{[['senderName', 'Tên người giao tại kho'], ['senderPhone', 'Điện thoại người giao'], ['recipientName', 'Tên người nhận tại nhà máy'], ['recipientPhone', 'Điện thoại người nhận']].map(([key, label]) => <label key={key} className="block text-sm">{label}<Input className={inputClass} maxLength={key.endsWith('Phone') ? 20 : 100} value={contacts[key]} onChange={e => { setContacts({ ...contacts, [key]: e.target.value }); setOperationId(crypto.randomUUID()); }} /></label>)}</div>
      <label className="block text-sm">Ghi chú bốc xếp / số kiện / kích thước<Textarea className={inputClass} maxLength={500} value={contacts.note} onChange={e => { setContacts({ ...contacts, note: e.target.value }); setOperationId(crypto.randomUUID()); }} /></label>
      <label className="flex gap-2 text-sm"><input type="checkbox" checked={confirmed} onChange={e => setConfirmed(e.target.checked)} />Tôi đã kiểm tra loại xe, tải trọng, đóng gói và đồng ý chi phí trong báo giá.</label>
      {bookingHint && <p role="status" className="text-sm text-d-on-surface-variant">{bookingHint}</p>}
      <button type="button" className={buttonClass} disabled={busy || !!bookingHint}
        onClick={() => book.mutate({ body: { operationId, quoteId: bookingQuote.id, contacts } })}>{book.isPending ? 'Đang gửi…' : 'Xác nhận gọi xe'}</button>
      <MutationError mutation={book} />
    </div>}
    {shipment && <div className="rounded-xl bg-d-surface-accent p-3 space-y-2 text-sm">
      <p className="font-bold">{labels[shipment.status] ?? shipment.status}</p>
      <p>Mã vận đơn: {shipment.providerOrderId ?? 'Đang chờ xác nhận'}</p><p>Cước vận chuyển: {money(shipment.amount)}</p>
      {safeLink(shipment.shareLink) && <a className="underline text-d-primary" href={shipment.shareLink} target="_blank" rel="noopener noreferrer">Theo dõi vận đơn</a>}
      <p>Kiểm tra gần nhất: {shipment.lastCheckedAt ? new Date(shipment.lastCheckedAt).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' }) : 'Chưa kiểm tra'}</p>
      {!terminal.includes(shipment.status) && shipment.providerOrderId && <button type="button" className={secondaryButtonClass} disabled={busy} onClick={() => refresh.mutate()}>{refresh.isPending ? 'Đang đồng bộ…' : 'Đồng bộ trạng thái vận chuyển'}</button>}
      {!terminal.includes(shipment.status) && <p className="text-xs">Tự động kiểm tra vận chuyển; hai lần đồng bộ thủ công cách nhau tối thiểu 10 giây.</p>}
      <MutationError mutation={refresh} />
      {shipment.lastError && <p role="alert">{shipment.lastError}</p>}
      {['UNKNOWN', 'BOOKING'].includes(shipment.status) && <div className="space-y-2"><p>Mã thao tác đối soát: {shipment.operationId}</p><label className="block">Mã vận đơn từ nhà vận chuyển<Input className={inputClass} maxLength={100} value={recoveryId} onChange={e => setRecoveryId(e.target.value)} /></label><button type="button" className={secondaryButtonClass} disabled={busy || !recoveryId.trim()} onClick={() => recover.mutate({ body: { orderId: recoveryId.trim() } })}>Đối soát vận đơn</button><MutationError mutation={recover} /></div>}
      {['ASSIGNING_DRIVER', 'ON_GOING'].includes(shipment.status) && <button type="button" disabled={busy} className={secondaryButtonClass} onClick={() => { if (window.confirm('Yêu cầu hủy vận chuyển? Chỉ chuyển phương thức sau khi đã xác nhận hủy.')) cancel.mutate(); }}>Yêu cầu hủy vận chuyển</button>}
      <MutationError mutation={cancel} />
      {shipment.status === 'COMPLETED' && <p>{data.receivedAt
        ? `Nhà máy đã xác nhận nhận hàng lúc ${new Date(data.receivedAt).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' })}. Tiếp tục theo dõi KCS và quyết toán ở các mục tương ứng.`
        : 'Đối tác đã báo giao hàng. Đang chờ nhà máy xác nhận tiếp nhận; đây là bước riêng với kết quả giao của đơn vị vận chuyển.'}</p>}
    </div>}
    {data.events?.length > 0 && <details><summary className="cursor-pointer py-2 text-sm">Lịch sử vận chuyển</summary><ol className="space-y-2 text-sm">{data.events.map((e, i) => <li key={`${e.createdAt}-${i}`}>{new Date(e.createdAt).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' })} — {labels[e.status] ?? e.status}</li>)}</ol></details>}
  </div>;
}
