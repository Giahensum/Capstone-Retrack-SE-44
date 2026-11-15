import { FileSignature, BadgePercent, Banknote, CalendarClock, ShieldAlert, CheckCircle2, Clock, Printer } from 'lucide-react';
import { formatCurrency, formatDate } from '@/lib/utils';

function Signature({ label, signedAt }) {
  return (
    <div className={`flex items-center gap-3 rounded-xl border p-3 ${signedAt ? 'border-d-primary/30 bg-d-surface-accent' : 'border-dashed border-d-border-subtle bg-white'}`}>
      <span className={`flex size-9 shrink-0 items-center justify-center rounded-full ${signedAt ? 'bg-d-primary text-white' : 'bg-d-surface-container text-d-outline'}`}>
        {signedAt ? <CheckCircle2 size={18} /> : <Clock size={18} />}
      </span>
      <div className="min-w-0">
        <p className="text-xs text-d-on-surface-variant">{label}</p>
        <p className={`text-sm font-bold ${signedAt ? 'text-d-primary' : 'text-d-on-surface-variant'}`}>{signedAt ? `Đã ký • ${formatDate(signedAt)}` : 'Chưa ký'}</p>
      </div>
    </div>
  );
}

// Hợp đồng đặt cọc điện tử lập từ snapshot thỏa thuận (giá, cọc, hạn). Không tự thêm điều khoản ngoài dữ liệu server.
// Ký = mỗi bên bấm xác nhận, hệ thống ghi thời điểm; không phải chữ ký số có giá trị pháp lý riêng.
export default function DepositContract({ title, depotName, amount, depositPercent, depositAmount, remainingAmount, collectionDeadline, sellerSignedAt, buyerSignedAt, onOpenDocument }) {
  return (
    <article aria-label="Hợp đồng đặt cọc" className="overflow-hidden rounded-2xl border border-d-border-subtle bg-white">
      <div className="flex items-center gap-3 bg-gradient-to-r from-[#446900] to-[#5c8a00] px-4 py-3 text-white">
        <span className="flex size-9 items-center justify-center rounded-xl bg-white/15"><FileSignature size={19} /></span>
        <div>
          <h3 className="text-sm font-black">Hợp đồng đặt cọc mua trọn lô</h3>
          <p className="text-xs text-white/80">Xác nhận điện tử của hai bên</p>
        </div>
        {onOpenDocument && <button type="button" onClick={onOpenDocument} className="ml-auto inline-flex shrink-0 items-center gap-2 rounded-full bg-white px-4 py-2 text-xs font-black text-[#446900] shadow-sm transition hover:bg-[#a3e635] active:scale-95"><Printer size={15} />Xem / In / Tải PDF</button>}
      </div>
      <div className="space-y-4 p-4 text-sm">
        <p className="leading-relaxed">
          Hai bên thỏa thuận mua bán trọn lô <strong>{title}</strong> với giá trúng <strong className="text-d-primary">{formatCurrency(amount)}</strong>{' '}
          (giá cố định, không tính lại theo kg cân sau).
        </p>
        <div className="grid gap-2.5 sm:grid-cols-3">
          <div className="rounded-xl bg-amber-50 p-3"><p className="flex items-center gap-1.5 text-xs font-semibold text-amber-700"><BadgePercent size={14} />Tiền đặt cọc ({depositPercent}%)</p><p className="mt-0.5 text-base font-black text-amber-800">{formatCurrency(depositAmount)}</p></div>
          <div className="rounded-xl bg-teal-50 p-3"><p className="flex items-center gap-1.5 text-xs font-semibold text-teal-700"><Banknote size={14} />Phần còn lại</p><p className="mt-0.5 text-base font-black text-teal-800">{formatCurrency(remainingAmount)}</p></div>
          <div className="rounded-xl bg-blue-50 p-3"><p className="flex items-center gap-1.5 text-xs font-semibold text-blue-700"><CalendarClock size={14} />Hạn thu gom hết</p><p className="mt-0.5 text-sm font-black text-blue-800">{formatDate(collectionDeadline)}</p></div>
        </div>
        <ul className="list-disc space-y-1 pl-5 text-d-on-surface-variant">
          <li>Bên mua chuyển tiền cọc cho bên bán sau khi hai bên đã ký; phần còn lại chuyển sau khi bên bán xác nhận đã nhận cọc.</li>
          <li>Tiền cọc và tiền hàng chuyển ngoài hệ thống; RETRACK chỉ ghi nhận xác nhận của hai bên, không giữ hộ tiền và không xác minh giao dịch ngân hàng.</li>
        </ul>
        <p className="flex items-start gap-2 rounded-xl bg-red-50 px-3 py-2 text-xs text-red-700"><ShieldAlert size={15} className="mt-0.5 shrink-0" />Việc hoàn/giữ cọc khi một bên không thực hiện do hai bên tự thỏa thuận hoặc nhờ Admin hỗ trợ; hệ thống chưa tự xử lý.</p>
        <div className="grid gap-2.5 sm:grid-cols-2">
          <Signature label="Bên bán (Seller)" signedAt={sellerSignedAt} />
          <Signature label={`Bên mua${depotName ? ` (${depotName})` : ' (Chủ kho trúng giá)'}`} signedAt={buyerSignedAt} />
        </div>
      </div>
    </article>
  );
}
