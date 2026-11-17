import { useEffect, useRef, useState } from 'react';
import toast from 'react-hot-toast';
import { downloadSignedContract, esignErrorMessage, getEsignStatus, refreshEsign, startEsignSigning } from './esignApi';

const STATE_TEXT = {
  NOT_STARTED: 'Chưa gửi ký',
  SENT: 'Đã gửi ký, chưa bên nào ký',
  FACTORY_SIGNED: 'Nhà máy đã ký, chờ chủ vựa ký',
  DEPOT_SIGNED: 'Chủ vựa đã ký, chờ nhà máy ký',
  COMPLETED: 'Hai bên đã ký xong',
  DECLINED: 'Một bên đã từ chối ký',
  VOIDED: 'Hợp đồng ký điện tử đã bị huỷ',
};

/**
 * Khi DocuSign đưa người dùng quay lại hệ thống (?esign=return&partnership=<id>), tự đọc lại trạng thái ký, báo kết quả rồi dọn tham số khỏi địa chỉ.
 * `onDone` chạy sau khi cập nhật xong (dùng để tải lại danh sách đối tác).
 */
export function useEsignReturn(onDone) {
  const doneRef = useRef(onDone);
  doneRef.current = onDone;
  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    if (params.get('esign') !== 'return') return;
    const id = params.get('partnership');
    const event = params.get('event');
    window.history.replaceState({}, '', window.location.pathname);
    if (!id) return;
    if (event && event !== 'signing_complete') toast('Bạn chưa hoàn tất việc ký hợp đồng.');
    refreshEsign(id)
      .then((status) => {
        if (status.state === 'COMPLETED') toast.success('Hai bên đã ký xong, hợp đồng có hiệu lực.');
        else if (status.state === 'DECLINED') toast.error('Hợp đồng đã bị từ chối ký.');
        else if (event === 'signing_complete') toast.success('Đã ghi nhận chữ ký của bạn, đang chờ bên còn lại.');
        doneRef.current?.(status);
      })
      .catch((error) => toast.error(esignErrorMessage(error)));
  }, []);
}

/**
 * Khối ký điện tử DocuSign trong hộp thoại hợp đồng của cả Nhà máy và Chủ vựa.
 * - contractStatus: trạng thái hợp đồng hiện tại (chỉ PENDING_DEPOT mới được ký).
 * - onChanged: gọi khi trạng thái ký vừa đổi để trang cha tải lại dữ liệu.
 */
export default function EsignPanel({ partnershipId, contractStatus, onChanged }) {
  const [status, setStatus] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    if (!partnershipId) return undefined;
    let alive = true;
    getEsignStatus(partnershipId)
      .then((value) => { if (alive) setStatus(value); })
      .catch((e) => { if (alive) setError(esignErrorMessage(e)); });
    return () => { alive = false; };
  }, [partnershipId, contractStatus]);

  async function run(action) {
    setBusy(true);
    setError('');
    try { await action(); } catch (e) { setError(esignErrorMessage(e)); } finally { setBusy(false); }
  }

  const sign = () => run(async () => {
    const { url } = await startEsignSigning(partnershipId);
    window.location.href = url; // sang trang ký DocuSign; ký xong DocuSign đưa người dùng quay lại hệ thống
  });
  const refresh = () => run(async () => {
    const next = await refreshEsign(partnershipId);
    setStatus(next);
    onChanged?.(next);
  });
  const download = () => run(() => downloadSignedContract(partnershipId, `${status?.contractCode || 'hop-dong'}-da-ky.pdf`));

  if (!partnershipId) return null;
  const state = status?.state || 'NOT_STARTED';
  const canSign = status?.configured && contractStatus === 'PENDING_DEPOT' && !status.mySideSigned && state !== 'DECLINED' && state !== 'COMPLETED';
  const button = 'rounded-full px-4 py-2 text-sm font-medium disabled:opacity-50';

  return (
    <section className="mt-4 rounded-xl border border-slate-300 bg-slate-50 p-4 text-sm text-slate-800" aria-label="Ký điện tử DocuSign">
      <h4 className="font-semibold">Ký điện tử qua DocuSign (môi trường thử nghiệm)</h4>
      {status && !status.configured && <p className="mt-2 text-amber-700">Máy chủ chưa cấu hình DocuSign nên chưa dùng được ký điện tử.</p>}
      {status && (
        <ul className="mt-2 space-y-1">
          <li>Trạng thái: <strong>{STATE_TEXT[state] || state}</strong></li>
          <li>Nhà máy: {status.factorySigned ? '✓ đã ký' : 'chưa ký'} · Chủ vựa: {status.depotSigned ? '✓ đã ký' : 'chưa ký'}</li>
        </ul>
      )}
      {error && <p role="alert" className="mt-2 text-red-700">{error}</p>}
      <div className="mt-3 flex flex-wrap gap-2">
        {canSign && <button type="button" className={`${button} bg-emerald-600 text-white`} disabled={busy} onClick={sign}>{busy ? 'Đang mở trang ký…' : 'Ký bằng DocuSign'}</button>}
        {status?.configured && status.state !== 'NOT_STARTED' && <button type="button" className={`${button} border border-slate-400`} disabled={busy} onClick={refresh}>Cập nhật trạng thái</button>}
        {state === 'COMPLETED' && <button type="button" className={`${button} border border-emerald-600 text-emerald-700`} disabled={busy} onClick={download}>Tải bản đã ký (PDF)</button>}
      </div>
      <p className="mt-2 text-xs text-slate-500">Bản ký thử nghiệm dùng sandbox của DocuSign, chỉ để minh hoạ đồ án, không có giá trị pháp lý.</p>
    </section>
  );
}
