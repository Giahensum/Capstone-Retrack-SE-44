import { useEffect, useRef } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useAuth } from '@/hooks/useAuth';
import { depotError } from '@/features/depot/depotError';
import { collectionKey, syncPayos } from './moneyApi';
import { FLOW_LABEL, MONEY_FLOWS, formatVnd, homePathFor, returnOutcome, returnPathFor } from './moneyStatus';

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const linkClass = 'inline-flex min-h-11 items-center justify-center rounded-full bg-emerald-700 px-6 py-2.5 text-sm font-bold text-white hover:opacity-90';

const OUTCOME = {
  paid: { tone: 'border-emerald-300 bg-emerald-50 text-emerald-900', title: 'Thanh toán thành công', text: 'PayOS đã xác nhận khoản thanh toán. Hệ thống đã ghi nhận và cập nhật đơn của bạn.' },
  pending: { tone: 'border-amber-300 bg-amber-50 text-amber-900', title: 'Chưa ghi nhận', text: 'PayOS chưa ghi nhận thanh toán. Nếu bạn đã chuyển tiền, hệ thống sẽ tự cập nhật khi nhận thông báo từ PayOS; bạn có thể kiểm tra lại sau ít phút.' },
  cancelled: { tone: 'border-slate-300 bg-slate-50 text-slate-800', title: 'Bạn đã hủy thanh toán', text: 'Khoản thanh toán chưa được ghi nhận. Bạn có thể quay lại và thanh toán khi sẵn sàng.' },
  expired: { tone: 'border-red-300 bg-red-50 text-red-800', title: 'Link thanh toán đã hết hạn', text: 'Khoản thanh toán chưa được ghi nhận. Hãy quay lại và tạo link thanh toán mới.' },
  refund: { tone: 'border-amber-300 bg-amber-50 text-amber-900', title: 'Khoản thanh toán đang được hoàn tiền', text: 'Khoản thu này đang hoặc đã được hoàn tiền. Vui lòng liên hệ quản trị viên nếu cần hỗ trợ.' },
};

/**
 * Trang PayOS đưa người trả quay về. Tham số URL (flow, source, cancel) chỉ dùng để biết cần đồng bộ khoản nào
 * và để chọn lời nhắn; kết luận thanh toán luôn lấy từ phản hồi của máy chủ sau payos-sync.
 */
export default function PayReturnPage() {
  const [params] = useSearchParams();
  const { role } = useAuth();
  const client = useQueryClient();
  const flow = String(params.get('flow') ?? '').toUpperCase();
  const source = params.get('source') ?? '';
  const cancelled = params.get('cancel') === 'true';
  const valid = MONEY_FLOWS.includes(flow) && GUID.test(source);
  const sync = useMutation({
    mutationFn: () => syncPayos(flow, source),
    onSuccess: (dto) => {
      client.setQueryData(collectionKey(flow, source), dto);
      for (const key of [['depot'], ['liquidation'], ['factory'], ['seller']]) client.invalidateQueries({ queryKey: key });
    },
  });
  const started = useRef(false);
  const { mutate } = sync;
  useEffect(() => {
    if (!valid || started.current) return;
    started.current = true;
    mutate();
  }, [valid, mutate]);

  const dto = sync.data;
  const kind = dto ? returnOutcome(dto, cancelled) : null;
  const outcome = kind ? OUTCOME[kind] : null;
  const back = returnPathFor(flow, role);
  const backLabel = flow === 'PICKUP' ? 'Về trang thanh toán' : flow === 'BATCH' ? 'Về nhà máy' : 'Về thanh lý';

  return <main className="mx-auto flex min-h-screen max-w-xl flex-col justify-center gap-4 bg-d-background p-4">
    <h1 className="text-2xl font-bold text-d-on-surface">Kết quả thanh toán PayOS</h1>
    {!valid && <p role="alert" className="rounded-xl border border-red-300 bg-red-50 p-4 text-red-800">Đường dẫn quay về không hợp lệ hoặc thiếu thông tin khoản thanh toán.</p>}
    {valid && <p className="text-sm text-d-on-surface-variant">{FLOW_LABEL[flow]}</p>}
    {valid && sync.isPending && <p role="status" className="rounded-xl border border-d-border-subtle bg-white p-4">Đang xác nhận thanh toán với PayOS…</p>}
    {valid && sync.isError && <div role="alert" className="space-y-3 rounded-xl border border-red-300 bg-red-50 p-4 text-red-800">
      <p>{depotError(sync.error)}</p>
      <button type="button" className="rounded-full border border-red-400 bg-white px-5 py-2 font-semibold" disabled={sync.isPending} onClick={() => sync.mutate()}>Thử lại</button></div>}
    {outcome && <div role="status" className={`space-y-2 rounded-xl border p-4 ${outcome.tone}`}>
      <p className="text-lg font-bold">{outcome.title}</p><p className="text-sm">{outcome.text}</p>
      <p className="text-sm font-semibold">Số tiền: {formatVnd(dto.amount)}</p>
      {dto.hasOpenAnomaly && <p className="text-sm">Khoản thu có bất thường đang được Admin xử lý; vui lòng chưa chuyển thêm tiền.</p>}</div>}
    {valid && dto && kind !== 'paid' && !sync.isPending && <button type="button" className="self-start rounded-full border border-d-border-subtle bg-white px-5 py-2 text-sm font-semibold" disabled={sync.isPending} onClick={() => sync.mutate()}>Kiểm tra lại với PayOS</button>}
    <div className="flex flex-wrap gap-3">
      {back && <Link className={linkClass} to={back}>{backLabel}</Link>}
      <Link className={back ? 'inline-flex min-h-11 items-center rounded-full border border-d-border-subtle bg-white px-5 py-2.5 text-sm font-semibold' : linkClass} to={homePathFor(role)}>Về trang chính</Link>
    </div>
  </main>;
}
