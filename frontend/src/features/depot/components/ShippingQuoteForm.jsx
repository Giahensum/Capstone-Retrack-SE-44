import { useState } from 'react';
import { useDepotMutation, money, number } from '../depotApi';
import { MutationError, secondaryButtonClass } from './DepotUI';

export default function ShippingQuoteForm({ factoryId, materialType, weightKg, method, onQuote }) {
  const [quote, setQuote] = useState(null);
  const request = useDepotMutation('post', 'shipping/quotes');
  return <section className="rounded-xl border border-d-border-subtle p-4 space-y-2 sm:col-span-2" aria-label="Báo giá vận chuyển">
    <p className="font-semibold text-d-primary">Báo giá vận chuyển</p>
    <p className="text-sm text-d-on-surface-variant">Giá tham khảo theo tuyến kho → nhà máy và loại xe được cấu hình. Lấy lại báo giá trước khi đặt xe nếu đã hết hạn.</p>
    <button type="button" className={secondaryButtonClass} disabled={!factoryId || !(Number(weightKg) > 0) || !materialType || request.isPending}
      onClick={async () => { try {
        const response = await request.mutateAsync({ body: { factoryId, materialType, weightKg: Number(weightKg), method } });
        const value = response.data.data; setQuote(value); onQuote?.(value);
      } catch { /* MutationError hiển thị lỗi từ backend. */ } }}>{request.isPending ? 'Đang lấy báo giá…' : 'Lấy báo giá vận chuyển'}</button>
    {!factoryId && <p className="text-sm">Chọn nhà máy để xác định tuyến giao hàng. Lô công khai có thể báo giá sau khi nhà máy nhận.</p>}
    <MutationError mutation={request} />
    {quote && <dl className="grid sm:grid-cols-2 gap-2 text-sm" aria-live="polite">
      <div><dt>Giá thuê ngoài tham khảo</dt><dd className="font-bold">{money(quote.amount)}</dd></div>
      <div><dt>Khoảng cách tham khảo</dt><dd>{number(quote.distanceKm)} km</dd></div>
      <div><dt>Hệ số {method === 'INTERNAL' ? 'xe kho' : 'thuê ngoài'}</dt><dd>{number(quote.factor * 100)}%</dd></div>
      <div><dt>Chi phí vận chuyển dự kiến</dt><dd className="font-bold">{money(quote.estimatedCost)}</dd></div>
      <div className="sm:col-span-2"><dt>Hiệu lực báo giá</dt><dd>{new Date(quote.expiresAt).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' })}</dd></div>
    </dl>}
  </section>;
}
