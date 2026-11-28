import { formatCurrency } from '@/lib/utils';

import { usesConfirmedQuantity } from './handoverQuantity';
const unit = u => ({ kg: 'kg', cai: 'cái', bo: 'bộ' })[u] ?? u;
const qty = (n, u) => Number(n).toLocaleString('vi-VN', { maximumFractionDigits: 3 }) + ' ' + unit(u);

// Cùng một bảng đối chiếu cho Seller và Depot; tiền chưa xác nhận chỉ là đề xuất.
export default function LiquidationHandoverTable({ agreement: a }) {
  const measured = usesConfirmedQuantity(a);
  const h = a.handover;
  const current = new Map(h?.lines.map(x => [x.agreementItemId, x]) ?? []);
  const status = { PENDING: 'Chờ Seller xác nhận', CONFIRMED: 'Hai bên đã chốt', REJECTED: 'Seller yêu cầu sửa' }[h?.status];
  return <div className="space-y-3">
    {measured && <p className="text-sm font-semibold text-d-primary">{status ?? 'Chưa nhập cân / kiểm đếm bàn giao'}{h && ' · lần ' + h.revision}</p>}
    <div className="overflow-x-auto"><table className="w-full min-w-[520px] text-left text-sm">
      <thead><tr><th className="p-2">Hạng mục</th><th className="p-2 text-right">Seller đăng</th>{measured && <th className="p-2 text-right">Bàn giao</th>}<th className="p-2 text-right">Đơn giá trúng</th><th className="p-2 text-right">Thành tiền</th></tr></thead>
      <tbody className="divide-y divide-d-border-subtle">{a.items.map(x => <tr key={x.id}>
        <td className="p-2 font-semibold">{x.name}</td><td className="p-2 text-right">{qty(x.quantity, x.unit)}</td>
        {measured && <td className="p-2 text-right">{current.has(x.id) ? qty(current.get(x.id).quantity, x.unit) : 'Chưa chốt'}</td>}
        <td className="p-2 text-right">{formatCurrency(x.unitPrice)}/{unit(x.unit)}</td>
        <td className="p-2 text-right font-semibold">{formatCurrency(current.get(x.id)?.amount ?? x.amount)}</td>
      </tr>)}</tbody>
      <tfoot><tr className="border-t-2 border-d-border-subtle"><td colSpan={measured ? 4 : 3} className="p-2 text-right font-bold">{measured && h?.status !== 'CONFIRMED' ? (h ? 'Tổng đề xuất · chưa chốt' : 'Tổng tạm tính') : 'Tổng tiền mua đã chốt'}</td><td className="p-2 text-right text-lg font-bold text-d-primary">{formatCurrency(h?.amount ?? a.totalAmount)}</td></tr></tfoot>
    </table></div>
    <p className="text-xs text-d-on-surface-variant">{measured ? 'Tiền mua = lượng bàn giao được Seller xác nhận × đơn giá trúng. Khi nhập kho, ghi lượng sau phân loại; hao hụt không làm giảm tiền trả Seller.' : 'Phiên cũ giữ điều kiện: tiền mua theo số lượng Seller đã đăng. Lượng sau phân loại chỉ dùng để nhập kho và tính vốn.'}</p>
    {h?.note && <p className="text-sm">Kho ghi chú: {h.note}</p>}
    {h?.decisionNote && <p className="text-sm text-d-error">Seller: {h.decisionNote}</p>}
  </div>;
}
