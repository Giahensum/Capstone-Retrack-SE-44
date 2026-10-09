import { useEffect, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import api from '@/lib/axios';
import ProofUpload from './ProofUpload';
import { MaterialIcon } from './DepotIcon';
import { buttonClass, inputClass } from './DepotUI';
import { depotError, useDepot } from '../DepotContext';

const money = (value) => Number(value ?? 0).toLocaleString('vi-VN') + ' đ';
const safeUrl = (value) => /^https?:\/\/\S+$/i.test(value ?? '');

export default function PaymentDrawer({ payment, onClose }) {
  const dialog = useRef(null);
  const { depotId, userId } = useDepot();
  const [proof, setProof] = useState('');
  const [uploading, setUploading] = useState(false);
  const [confirmed, setConfirmed] = useState(false);
  const client = useQueryClient();
  const detail = useQuery({
    queryKey: ['depot', userId, depotId, 'payment-detail', payment.id],
    queryFn: async () => (await api.get(`/depot/payments/${payment.id}`, { params: { depotId } })).data.data,
    enabled: !!depotId,
  });
  const item = detail.data;
  const canSubmit = item?.status === 'AWAITING_PAYMENT' && safeUrl(proof.trim()) && confirmed && !uploading && item.items.length > 0;
  useEffect(() => { dialog.current.showModal(); }, []);
  const save = useMutation({
    mutationFn: () => api.patch(`/depot/pickup-requests/${payment.id}/payment-sent`, { paymentProofUrl: proof.trim() }),
    onSuccess: async () => { await client.invalidateQueries({ queryKey: ['depot', userId, depotId] }); onClose(); },
    onError: () => detail.refetch(),
  });
  const busy = save.isPending || uploading;

  return <dialog ref={dialog} aria-labelledby="payment-title"
    onCancel={(event) => { if (busy) event.preventDefault(); else onClose(); }}
    className="m-auto w-[calc(100%-2rem)] max-w-2xl max-h-[90dvh] overflow-y-auto rounded-[20px] border border-d-border-subtle bg-d-surface p-0 shadow-xl backdrop:bg-black/40">
    <header className="sticky top-0 z-10 flex items-start justify-between gap-4 border-b border-d-border-subtle bg-d-surface p-5 sm:p-6">
      <div><p className="text-xs font-semibold uppercase tracking-wide text-d-primary">Thanh toán cho người bán</p>
        <h2 id="payment-title" className="mt-1 text-xl font-bold text-d-on-surface">Đối soát đơn #{payment.id.slice(0, 8)}</h2></div>
      <button type="button" aria-label="Đóng chi tiết thanh toán" disabled={busy} onClick={onClose}
        className="flex size-11 shrink-0 items-center justify-center rounded-full text-d-on-surface-variant hover:bg-d-surface-container-high focus-visible:outline-2 focus-visible:outline-d-primary disabled:opacity-40">✕</button>
    </header>
    {detail.isPending ? <p role="status" className="p-6">Đang tải dữ liệu thanh toán mới nhất…</p>
      : detail.isError ? <div role="alert" className="p-6 text-d-error">{depotError(detail.error)}
        <button type="button" onClick={() => detail.refetch()} className={`${buttonClass} ml-3`}>Thử lại</button></div>
      : <div className="space-y-5 p-5 sm:p-6">
        <div className="rounded-2xl border border-d-border-subtle bg-d-surface-container-low p-4">
          <div className="flex flex-wrap items-center justify-between gap-2"><div>
            <p className="font-semibold text-d-on-surface">{item.sellerName}</p>
            <p className="text-sm text-d-on-surface-variant">{item.sellerPhone || 'Chưa có số điện thoại'} · {item.address}</p></div>
            <span className="rounded-full bg-d-surface-accent px-3 py-1 text-xs font-semibold text-d-primary">{item.status === 'AWAITING_PAYMENT' ? 'Chờ chuyển tiền' : item.status === 'PAYMENT_SENT' ? 'Chờ người bán xác nhận' : 'Hoàn tất'}</span></div>
          <p className="mt-3 text-sm text-d-on-surface-variant">Nhân viên thu gom: {item.collectorName || 'Chưa có thông tin'}</p>
        </div>
        <section aria-label="Chi tiết cân và giá" className="overflow-hidden rounded-2xl border border-d-border-subtle">
          <div className="border-b border-d-border-subtle px-4 py-3 font-semibold">Vật liệu đã cân</div>
          <div className="overflow-x-auto"><table className="w-full min-w-[450px] text-left text-sm"><thead className="bg-d-surface-container-low text-d-on-surface-variant"><tr>
            <th className="px-4 py-3">Vật liệu</th><th className="px-4 py-3 text-right">Khối lượng</th><th className="px-4 py-3 text-right">Đơn giá</th><th className="px-4 py-3 text-right">Thành tiền</th>
          </tr></thead><tbody className="divide-y divide-d-border-subtle">{item.items.map((row, index) => <tr key={`${row.materialType}-${index}`}>
            <td className="px-4 py-3">{row.materialType}</td><td className="px-4 py-3 text-right">{Number(row.weightKg).toLocaleString('vi-VN')} kg</td>
            <td className="px-4 py-3 text-right">{money(row.pricePerKg)}</td><td className="px-4 py-3 text-right font-medium">{money(row.subTotal)}</td>
          </tr>)}</tbody></table></div>
          {item.items.length === 0 && <p role="alert" className="px-4 py-3 text-d-error">Đơn chưa có kết quả cân. Hãy kiểm tra lại trước khi chuyển tiền.</p>}
        </section>
        <section aria-label="Số tiền cần thanh toán" className="space-y-2 rounded-2xl bg-d-surface-accent p-4">
          <div className="flex justify-between gap-4"><span>Giá trị vật liệu</span><strong>{money(item.grossAmount)}</strong></div>
          <div className="flex justify-between gap-4 text-d-on-surface-variant"><span>Phí nền tảng ({item.platformFeePercentage}%)</span><span>− {money(item.platformFeeAmount)}</span></div>
          <div className="flex justify-between gap-4 border-t border-d-border-subtle pt-3 text-lg font-bold text-d-primary"><span>Chuyển cho người bán</span><span>{money(item.netAmount)}</span></div>
        </section>
        {safeUrl(item.checkinImageUrl) && <a className="inline-flex items-center gap-2 rounded-full border border-d-border-subtle px-4 py-2 text-d-primary hover:bg-d-surface-container-low" href={item.checkinImageUrl} target="_blank" rel="noreferrer"><MaterialIcon name="visibility" />Xem ảnh check-in</a>}
        {item.status === 'AWAITING_PAYMENT' ? <form className="space-y-4" onSubmit={(event) => { event.preventDefault(); if (canSubmit && !save.isPending) save.mutate(); }}>
          <div className="rounded-2xl border border-d-border-subtle p-4"><h3 className="font-semibold">Chứng từ chuyển khoản</h3>
            <p className="mt-1 text-sm text-d-on-surface-variant">Chuyển đúng số tiền thực trả ở trên, rồi tải ảnh chứng từ hoặc nhập đường dẫn HTTPS.</p>
            <div className="mt-4"><ProofUpload onUploaded={setProof} onBusyChange={setUploading} disabled={save.isPending} /></div>
            <label className="mt-4 block text-sm font-medium">Đường dẫn chứng từ
              <input required type="url" value={proof} onChange={(event) => setProof(event.target.value)} disabled={busy}
                placeholder="https://..." className={inputClass} /></label></div>
          <label className="flex cursor-pointer items-start gap-3 rounded-xl border border-d-border-subtle p-3 text-sm">
            <input type="checkbox" checked={confirmed} onChange={(event) => setConfirmed(event.target.checked)} disabled={busy} className="mt-1 size-4 accent-d-primary" />
            <span>Tôi đã chuyển <strong>{money(item.netAmount)}</strong> cho người bán và đã kiểm tra chứng từ.</span></label>
          {save.isError && <p role="alert" className="text-d-error">{depotError(save.error)}</p>}
          <div className="flex flex-wrap justify-end gap-3 border-t border-d-border-subtle pt-4">
            <button type="button" onClick={onClose} disabled={busy} className="rounded-full border border-d-border-subtle px-5 py-2.5 font-semibold hover:bg-d-surface-container-low disabled:opacity-40">Đóng</button>
            <button type="submit" disabled={!canSubmit || save.isPending} className={buttonClass}>{save.isPending ? 'Đang ghi nhận…' : 'Xác nhận đã chuyển tiền'}</button>
          </div></form> : <div className="rounded-2xl border border-d-border-subtle p-4">
          <h3 className="font-semibold">Chứng từ đã ghi nhận</h3>
          {safeUrl(item.paymentProofUrl) ? <a className="mt-3 inline-flex items-center gap-2 rounded-full border border-d-border-subtle px-4 py-2 font-semibold text-d-primary hover:bg-d-surface-container-low" href={item.paymentProofUrl} target="_blank" rel="noreferrer"><MaterialIcon name="visibility" />Xem chứng từ</a>
            : <p className="mt-2 text-sm text-d-on-surface-variant">Chưa có đường dẫn chứng từ hợp lệ. Vui lòng liên hệ quản trị viên.</p>}
          {item.status === 'PAYMENT_SENT' && <p className="mt-3 text-sm text-d-on-surface-variant">Đang chờ người bán xác nhận đã nhận tiền. Tồn kho chỉ tăng khi đơn hoàn tất.</p>}
        </div>}
      </div>}
  </dialog>;
}
