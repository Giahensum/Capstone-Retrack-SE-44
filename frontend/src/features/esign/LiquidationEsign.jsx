import { useEffect, useRef, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { FileSignature, Download, RefreshCw } from 'lucide-react';
import {
  downloadLiquidationContract, getLiquidationEsign, liquidationEsignError, refreshLiquidationEsign, startLiquidationSigning,
} from './liquidationEsignApi';

const STATE_TEXT = {
  NOT_STARTED: 'Chưa gửi ký',
  SENT: 'Đã gửi ký, chưa bên nào ký',
  SELLER_SIGNED: 'Seller đã ký, chờ chủ kho ký',
  BUYER_SIGNED: 'Chủ kho đã ký, chờ Seller ký',
  COMPLETED: 'Hai bên đã ký xong',
  DECLINED: 'Một bên đã từ chối ký, cần ký lại',
  VOIDED: 'Hợp đồng ký điện tử đã bị hủy',
};

/** Trạng thái ký điện tử của một thỏa thuận; `configured=false` nghĩa là máy chủ chưa bật DocuSign (dùng nút xác nhận cũ). */
export function useLiquidationEsign(agreementId) {
  return useQuery({
    queryKey: ['liquidation-esign', agreementId],
    queryFn: () => getLiquidationEsign(agreementId),
    enabled: !!agreementId,
    staleTime: 10_000,
  });
}

/**
 * Khi DocuSign đưa người dùng quay lại (?esign=return&agreement=<id>), đọc lại trạng thái ký, báo kết quả rồi chỉ xóa các tham số ký
 * khỏi địa chỉ (giữ lại các tham số khác như view/lotId). `onDone` chạy sau khi cập nhật xong để trang tải lại dữ liệu.
 */
export function useLiquidationEsignReturn(onDone) {
  const doneRef = useRef(onDone);
  doneRef.current = onDone;
  const client = useQueryClient();
  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    if (params.get('esign') !== 'return') return;
    const id = params.get('agreement');
    const event = params.get('event');
    ['esign', 'agreement', 'event'].forEach((key) => params.delete(key));
    const rest = params.toString();
    window.history.replaceState({}, '', window.location.pathname + (rest ? `?${rest}` : ''));
    if (!id) return;
    if (event && event !== 'signing_complete') toast('Bạn chưa hoàn tất việc ký hợp đồng.');
    refreshLiquidationEsign(id)
      .then((status) => {
        if (status.state === 'COMPLETED') toast.success('Hai bên đã ký xong hợp đồng đặt cọc.');
        else if (status.state === 'DECLINED') toast.error('Hợp đồng đã bị từ chối ký.');
        else if (event === 'signing_complete') toast.success('Đã ghi nhận chữ ký của bạn, đang chờ bên còn lại.');
        client.invalidateQueries({ queryKey: ['liquidation-esign', id] });
        doneRef.current?.(status);
      })
      .catch((error) => toast.error(liquidationEsignError(error)));
  }, [client]);
}

const primary = 'inline-flex items-center justify-center gap-2 rounded-full bg-d-primary px-6 py-3 text-sm font-bold text-white shadow-md transition active:scale-95 disabled:opacity-50';
const secondary = 'inline-flex items-center justify-center gap-2 rounded-full border border-d-border-subtle px-4 py-2 text-sm font-semibold text-d-on-surface transition disabled:opacity-50';

function useAction(agreementId, onChanged) {
  const client = useQueryClient();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  async function run(action) {
    setBusy(true); setError('');
    try { await action(); } catch (e) { setError(liquidationEsignError(e)); } finally { setBusy(false); }
  }
  const reload = (status) => {
    client.invalidateQueries({ queryKey: ['liquidation-esign', agreementId] });
    onChanged?.(status);
  };
  return { busy, error, run, reload };
}

/** Nút ký chính, đặt ở chân hộp thoại/khối hành động. Chuyển trình duyệt sang trang ký DocuSign. */
export function LiquidationEsignSignButton({ agreementId, status, onChanged, label = 'Ký điện tử (DocuSign)' }) {
  const { busy, error, run } = useAction(agreementId, onChanged);
  if (!status?.configured || status.mySideSigned || status.state === 'COMPLETED') return null;
  const sign = () => run(async () => {
    const { url } = await startLiquidationSigning(agreementId);
    window.location.href = url; // ký xong DocuSign đưa người dùng quay lại hệ thống
  });
  return (
    <div className="space-y-2">
      <div className="flex flex-wrap items-center justify-end gap-3">
        <span className="text-xs text-d-on-surface-variant">{STATE_TEXT[status.state] ?? status.state}</span>
        <button type="button" className={primary} disabled={busy} onClick={sign}><FileSignature size={17} />{busy ? 'Đang mở DocuSign…' : label}</button>
      </div>
      {error && <p role="alert" className="text-right text-xs font-semibold text-red-700">{error}</p>}
    </div>
  );
}

/** Khối thông tin ký điện tử trong thân hộp thoại: trạng thái, cập nhật từ DocuSign và tải bản đã ký. */
export function LiquidationEsignPanel({ agreementId, status, onChanged }) {
  const { busy, error, run, reload } = useAction(agreementId, onChanged);
  if (!status?.configured || status.state === 'NOT_STARTED') return null;
  const refresh = () => run(async () => { const next = await refreshLiquidationEsign(agreementId); reload(next); toast.success('Đã cập nhật trạng thái ký.'); });
  const download = () => run(() => downloadLiquidationContract(agreementId, `${status.contractNumber || 'hop-dong'}-da-ky.pdf`));
  return (
    <div className="rounded-2xl border border-d-border-subtle bg-d-surface-container-low p-4 text-sm" aria-label="Ký điện tử qua DocuSign">
      <p className="font-semibold text-d-on-surface">Ký điện tử qua DocuSign: {STATE_TEXT[status.state] ?? status.state}</p>
      <p className="mt-1 text-xs text-d-on-surface-variant">Seller: {status.sellerSigned ? 'đã ký' : 'chưa ký'} · Chủ kho: {status.buyerSigned ? 'đã ký' : 'chưa ký'}</p>
      <div className="mt-3 flex flex-wrap gap-2">
        {status.state !== 'COMPLETED' && <button type="button" className={secondary} disabled={busy} onClick={refresh}><RefreshCw size={15} />Cập nhật trạng thái</button>}
        {status.state === 'COMPLETED' && <button type="button" className={secondary} disabled={busy} onClick={download}><Download size={15} />Tải hợp đồng đã ký (PDF)</button>}
      </div>
      {error && <p role="alert" className="mt-2 text-xs font-semibold text-red-700">{error}</p>}
    </div>
  );
}
