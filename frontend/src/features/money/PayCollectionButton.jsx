import { useCollection, useCollectionMutations, useInvalidateOnPaid } from './moneyApi';
import { FLOW_LABEL, formatVnd, isPaid, isPlatformCollection, payButtonLabel, statusLabel, statusTone } from './moneyStatus';
import { depotError } from '@/features/depot/depotError';

const toneClass = {
  neutral: 'border-slate-300 bg-slate-50 text-slate-700',
  warn: 'border-amber-300 bg-amber-50 text-amber-900',
  ok: 'border-emerald-300 bg-emerald-50 text-emerald-900',
  bad: 'border-red-300 bg-red-50 text-red-800',
};
const btn = 'inline-flex min-h-11 items-center justify-center gap-2 rounded-full bg-d-primary px-6 py-2.5 text-sm font-bold text-white shadow-sm transition active:scale-95 disabled:opacity-50';
const btnSecondary = 'inline-flex min-h-11 items-center justify-center rounded-full border border-d-border-subtle bg-white px-5 py-2.5 text-sm font-semibold text-d-primary hover:bg-d-surface-container-low disabled:opacity-50';

/**
 * Hook tiện dụng cho màn hình có luồng cũ: trả về { platform, pending } để quyết định ẩn/hiện form thủ công.
 * platform chỉ true khi máy chủ báo moneyMode === 'PLATFORM'; lỗi tải thì coi như luồng cũ.
 */
export function usePlatformCollection(flow, sourceId, options) {
  const query = useCollection(flow, sourceId, options);
  return { query, platform: isPlatformCollection(query.data), pending: query.isPending && query.fetchStatus !== 'idle' };
}

/**
 * Thanh toán một khoản thu qua sàn bằng PayOS. Chỉ hiển thị khi moneyMode === 'PLATFORM'.
 * Số liệu, trạng thái và quyền thanh toán đều lấy từ máy chủ.
 */
export default function PayCollectionButton({ flow, sourceId, title, enabled = true }) {
  const query = useCollection(flow, sourceId, { enabled });
  const dto = query.data;
  const { link, sync } = useCollectionMutations(flow, sourceId);
  useInvalidateOnPaid(dto);
  if (!enabled) return null;
  if (query.isPending) return <p role="status" className="text-sm text-d-on-surface-variant">Đang kiểm tra hình thức thanh toán…</p>;
  if (query.isError) {
    return <div role="alert" className="rounded-xl border border-red-300 bg-red-50 p-3 text-sm text-red-800">
      {depotError(query.error)} <button type="button" className={`${btnSecondary} ml-2`} onClick={() => query.refetch()}>Thử lại</button></div>;
  }
  if (!isPlatformCollection(dto)) return null;

  const label = payButtonLabel(dto);
  const busy = link.isPending || sync.isPending;
  const paid = isPaid(dto);
  const error = link.error ?? sync.error;
  const startPay = () => {
    if (busy) return;
    link.mutate(undefined, { onSuccess: (res) => { if (res?.checkoutUrl) window.location.assign(res.checkoutUrl); } });
  };
  const canSync = !paid && dto.role === 'PAYER' && dto.status === 'PENDING';
  const statusText = statusLabel(dto.status);

  return <section aria-label={title ?? FLOW_LABEL[flow] ?? 'Thanh toán qua sàn'} className="space-y-3 rounded-2xl border border-d-border-subtle bg-white p-4">
    <div className="flex flex-wrap items-center justify-between gap-2">
      <div>
        <h3 className="font-semibold text-d-on-surface">{title ?? FLOW_LABEL[flow] ?? 'Thanh toán qua sàn'}</h3>
        <p className="text-lg font-bold text-d-primary">{formatVnd(dto.amount)}</p>
      </div>
      <span className={`rounded-full border px-3 py-1 text-xs font-semibold ${toneClass[statusTone(dto.status)]}`}>{statusText}</span>
    </div>
    {dto.hasOpenAnomaly && <p role="alert" className="rounded-xl border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900">
      Khoản thu này có bất thường đang được Admin xử lý (ví dụ số tiền nhận chưa khớp). Vui lòng chưa chuyển thêm tiền; hệ thống sẽ cập nhật sau khi xử lý.</p>}
    {paid && <p role="status" className="text-sm text-emerald-900">
      Đã nhận {formatVnd(dto.receivedAmount ?? dto.amount)}{dto.paidAt ? ` lúc ${new Date(dto.paidAt).toLocaleString('vi-VN')}` : ''}. Tiền được nền tảng giữ và chi cho người nhận theo quy trình.</p>}
    {dto.status === 'PENDING' && <p className="text-sm text-d-on-surface-variant">Link PayOS đã được tạo. Nếu bạn đã chuyển tiền, hệ thống tự cập nhật sau ít giây; có thể bấm “Cập nhật trạng thái”.</p>}
    {dto.status === 'EXPIRED' && <p className="text-sm text-d-on-surface-variant">Link PayOS đã hết hạn. Bạn có thể tạo link mới.</p>}
    {!paid && !label && dto.message && <p role="status" className="text-sm text-d-on-surface-variant">{dto.message}</p>}
    {error && <p role="alert" className="text-sm text-d-error">{depotError(error)}</p>}
    {(label || canSync) && <div className="flex flex-wrap gap-3">
      {label && <button type="button" className={btn} disabled={busy} onClick={startPay}>{link.isPending ? 'Đang tạo link…' : label}</button>}
      {canSync && <button type="button" className={btnSecondary} disabled={busy} onClick={() => sync.mutate()}>{sync.isPending ? 'Đang cập nhật…' : 'Cập nhật trạng thái'}</button>}
    </div>}
  </section>;
}
