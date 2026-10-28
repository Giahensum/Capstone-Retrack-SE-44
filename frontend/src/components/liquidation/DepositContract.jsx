import { formatCurrency, formatDate } from '@/lib/utils';

// Hợp đồng đặt cọc điện tử lập từ snapshot thỏa thuận (giá, cọc, hạn). Không tự thêm điều khoản ngoài dữ liệu server.
// Ký = mỗi bên bấm xác nhận, hệ thống ghi thời điểm; không phải chữ ký số có giá trị pháp lý riêng.
export default function DepositContract({ title, depotName, amount, depositPercent, depositAmount, remainingAmount, collectionDeadline, sellerSignedAt, buyerSignedAt }) {
  const rows = [
    ['Bên bán (Seller)', sellerSignedAt ? `Đã ký lúc ${formatDate(sellerSignedAt)}` : 'Chưa ký'],
    [`Bên mua${depotName ? ` (${depotName})` : ' (Chủ kho trúng giá)'}`, buyerSignedAt ? `Đã ký lúc ${formatDate(buyerSignedAt)}` : 'Chưa ký'],
  ];
  return (
    <article aria-label="Hợp đồng đặt cọc" className="rounded-2xl border border-d-border-subtle bg-d-surface-container-lowest p-4 text-sm space-y-3">
      <h3 className="font-bold text-d-on-surface text-base">Hợp đồng đặt cọc mua trọn lô</h3>
      <p>
        Hai bên thỏa thuận mua bán trọn lô <strong>{title}</strong> với giá trúng{' '}
        <strong>{formatCurrency(amount)}</strong> (giá cố định, không tính lại theo kg cân sau).
      </p>
      <ul className="list-disc pl-5 space-y-1">
        <li>Tiền đặt cọc: <strong>{formatCurrency(depositAmount)}</strong> ({depositPercent}% giá trúng), bên mua chuyển cho bên bán trong vòng thực hiện hợp đồng.</li>
        <li>Phần còn lại: <strong>{formatCurrency(remainingAmount)}</strong>, chuyển sau khi bên bán xác nhận đã nhận cọc.</li>
        <li>Hạn bên mua thu gom hết hàng: <strong>{formatDate(collectionDeadline)}</strong>.</li>
        <li>Tiền cọc và tiền hàng chuyển ngoài hệ thống; RETRACK chỉ ghi nhận xác nhận của hai bên, không giữ hộ tiền và không xác minh giao dịch ngân hàng.</li>
        <li>Việc hoàn/giữ cọc khi một bên không thực hiện do hai bên tự thỏa thuận hoặc nhờ Admin hỗ trợ; hệ thống chưa tự xử lý.</li>
      </ul>
      <dl className="grid sm:grid-cols-2 gap-3 border-t border-d-border-subtle pt-3">
        {rows.map(([label, value]) => (
          <div key={label}>
            <dt className="text-xs text-d-on-surface-variant">{label}</dt>
            <dd className="font-semibold">{value}</dd>
          </div>
        ))}
      </dl>
    </article>
  );
}
