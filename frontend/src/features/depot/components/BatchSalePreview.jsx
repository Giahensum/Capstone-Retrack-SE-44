import { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import api from '@/lib/axios';
import { useDepot, depotError } from '../DepotContext';
import { money, number } from '../depotApi';
import { inputClass } from './DepotUI';

export default function BatchSalePreview({ materialType, weightKg, demand }) {
  const { depotId, userId } = useDepot();
  const [price, setPrice] = useState(demand?.minPricePerKg ?? '');
  const [sellingCost, setSellingCost] = useState('');
  const [open, setOpen] = useState(!!demand);
  const body = JSON.stringify({ materialType, weightKg: Number(weightKg), pricePerKg: price === '' ? null : Number(price), sellingCost: Number(sellingCost || 0) });
  const [debounced, setDebounced] = useState(null);
  useEffect(() => { const timer = setTimeout(() => setDebounced(body), 350); return () => clearTimeout(timer); }, [body]);
  const valid = materialType && Number(weightKg) > 0 && Number.isFinite(Number(weightKg)) && (price === '' || Number(price) >= 0) && Number(sellingCost || 0) >= 0;
  const q = useQuery({ queryKey: ['depot', userId, depotId, 'sale-preview', debounced], enabled: !!depotId && open && !!valid && debounced === body,
    queryFn: async ({ signal }) => (await api.post('/depot/stock/sale-preview', JSON.parse(debounced), { params: { depotId }, signal })).data.data,
    retry: false, staleTime: 10000 });
  const d = debounced === body ? q.data : null;
  return <details open={open} onToggle={e => setOpen(e.currentTarget.open)} className="rounded-2xl border border-d-border-subtle bg-white sm:col-span-2">
    <summary className="cursor-pointer min-h-11 p-4 font-semibold text-d-primary">Ước tính lãi trước khi bán (tùy chọn)</summary>
    <div className="space-y-3 border-t border-d-border-subtle p-4">
      <p className="text-sm">Giá vốn lấy từ nguồn hàng khả dụng theo thứ tự nhập trước, xuất trước. Giá dưới đây chỉ để cân nhắc, không gửi thành giá thỏa thuận cho nhà máy.</p>
      {demand && <p className="rounded-xl bg-d-surface-accent p-3 text-sm">Khoảng Factory đang tham khảo: {demand.minPricePerKg == null ? 'Chưa có giá tối thiểu' : money(demand.minPricePerKg)} – {demand.maxPricePerKg == null ? 'Chưa có giá tối đa' : money(demand.maxPricePerKg)} / kg. Có thể thay giá để so sánh từng phương án.</p>}
      <div className="grid sm:grid-cols-2 gap-3"><label className="block text-sm">Giá bán dự kiến / kg (đ)<input type="number" min="0" max="1000000000" step="0.01" className={inputClass} value={price} onChange={e => setPrice(e.target.value)} /></label>
        <label className="block text-sm">Chi phí giao/bán dự kiến (đ)<input type="number" min="0" max="1000000000000" step="1" className={inputClass} value={sellingCost} onChange={e => setSellingCost(e.target.value)} placeholder="Xăng xe, bốc dỡ… chưa nằm trong giá vốn" /></label></div>
      {!valid ? <p role="status" className="text-sm">Chọn vật liệu và nhập khối lượng hợp lệ để xem dự kiến.</p> : q.isError ? <p role="alert" className="text-sm text-d-error">{depotError(q.error)}</p> : !d ? <p role="status" className="text-sm">Đang tính dự kiến…</p> : <>
        <dl aria-live="polite" className="grid sm:grid-cols-2 gap-3 rounded-xl bg-d-surface-accent p-3 text-sm">
          <div><dt>Giá vốn {number(d.weightKg)} kg chuẩn bị xuất</dt><dd className="font-bold">{d.costOfGoods == null ? 'Chưa đủ dữ liệu' : money(d.costOfGoods)}</dd></div>
          <div><dt>Giá hòa vốn tham khảo / kg</dt><dd className="font-bold">{d.breakEvenPricePerKg == null ? 'Chưa xác định' : money(d.breakEvenPricePerKg)}</dd></div>
          <div><dt>Tiền bán dự kiến</dt><dd>{d.revenue == null ? 'Nhập giá bán để tính' : money(d.revenue)}</dd></div>
          <div><dt>{d.estimatedDifference < 0 ? 'Lỗ dự kiến' : 'Lãi dự kiến trước thuế thu nhập'}</dt><dd className="font-bold">{d.estimatedDifference == null ? 'Chưa đủ dữ liệu để kết luận' : money(d.estimatedDifference)}</dd></div>
        </dl>
        {!d.hasCompleteCost && <p role="status" className="text-sm">Có nguồn nhập chưa xác định giá vốn. Hệ thống không lấy giá vốn thiếu bằng 0 để báo lãi.</p>}
      </>}
      <details className="text-sm"><summary className="cursor-pointer min-h-11 py-2 text-d-primary">Cách tính và giới hạn dự kiến</summary><p>Lãi dự kiến = khối lượng × giá bán dự kiến − giá vốn phần xuất − chi phí giao/bán dự kiến. Giá hòa vốn = (giá vốn + chi phí giao/bán) ÷ khối lượng, làm tròn lên 2 số lẻ. Chi phí đầu vào đã phân bổ vào giá vốn không nhập lại.</p><p className="mt-2">Chưa gồm khoản chưa ghi, phân bổ tiền gói tháng, thuế thu nhập hoặc thay đổi sau cân/KCS. Tiền gói là chi phí vận hành theo kỳ, không tự trừ toàn bộ vào từng lô. Xem trước chưa giữ hàng; khi tạo lô hệ thống kiểm tra tồn lại.</p></details>
    </div>
  </details>;
}
