import { useState } from 'react';
import { Wallet } from 'lucide-react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import api from '@/lib/axios';
import ProofUpload from './ProofUpload';
import { MaterialIcon } from './DepotIcon';
import { Dialog, buttonClass, inputClass } from './DepotUI';
import { depotError, useDepot } from '../DepotContext';
import PayCollectionButton, { usePlatformCollection } from '@/features/money/PayCollectionButton';
import { PICKUP_STATUS_LABEL } from '@/lib/utils';
import { paymentMethods, paymentMethodLabel } from '@/lib/paymentMethods';

const money = (value) => Number(value ?? 0).toLocaleString('vi-VN') + ' đ';
const safeUrl = (value) => /^https?:\/\/\S+$/i.test(value ?? '');

export default function PaymentDrawer({ payment, onClose }) {
  const { depotId, userId } = useDepot();
  const [proof, setProof] = useState('');
  const [uploading, setUploading] = useState(false);
  const [confirmed, setConfirmed] = useState(false);
  const [paymentMethod, setPaymentMethod] = useState('BANK_TRANSFER');
  const [reference, setReference] = useState('');
  const client = useQueryClient();
  const detail = useQuery({
    queryKey: ['depot', userId, depotId, 'payment-detail', payment.id],
    queryFn: async () => (await api.get(`/depot/payments/${payment.id}`, { params: { depotId } })).data.data,
    enabled: !!depotId,
  });
  const item = detail.data;
  // Chế độ PLATFORM: thanh toán PayOS vào sàn, ẩn biểu mẫu chứng từ thủ công (backend từ chối với 409).
  const collection = usePlatformCollection('PICKUP', payment.id, { enabled: item?.status === 'AWAITING_PAYMENT' || item?.status === 'PAYMENT_SENT' });
  const canSubmit = item?.status === 'AWAITING_PAYMENT' && (paymentMethod === 'BANK_TRANSFER' ? safeUrl(proof.trim()) : !!reference.trim()) && confirmed && !uploading && item.items.length > 0;
  const save = useMutation({
    mutationFn: () => api.patch(`/depot/pickup-requests/${payment.id}/payment-sent`, { paymentProofUrl: proof.trim() || null, paymentMethod, paymentReference: reference.trim() || null }),
    onSuccess: async () => { await client.invalidateQueries({ queryKey: ['depot', userId, depotId] }); onClose(); },
    onError: () => detail.refetch(),
  });
  const busy = save.isPending || uploading;

  return <Dialog icon={Wallet} title={`${payment.status === "AWAITING_PAYMENT" ? "Đối soát" : "Chi tiết"} đơn #${payment.id.slice(0, 8)}`} subtitle="Thanh toán cho người bán" closeLabel="Đóng chi tiết thanh toán" onClose={onClose} busy={busy} wide>
    {detail.isPending ? <p role="status" className="p-6">Đang tải dữ liệu thanh toán mới nhất…</p>
      : detail.isError ? <div role="alert" className="p-6 text-d-error">{depotError(detail.error)}
        <button type="button" onClick={() => detail.refetch()} className={`${buttonClass} ml-3`}>Thử lại</button></div>
      : <div className="space-y-5">
        <div className="rounded-2xl border border-d-border-subtle bg-d-surface-container-low p-4">
          <div className="flex flex-wrap items-center justify-between gap-2"><div>
            <p className="font-semibold text-d-on-surface">{item.sellerName}</p>
            <p className="text-sm text-d-on-surface-variant">{item.sellerPhone || 'Chưa có số điện thoại'} · {item.address}</p></div>
            <span className="rounded-full bg-d-surface-accent px-3 py-1 text-xs font-semibold text-d-primary">{PICKUP_STATUS_LABEL[item.status] ?? item.status}</span></div>
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
          {!collection.platform && item.platformFeeAmount > 0 && <div className="flex justify-between gap-4 text-d-on-surface-variant"><span>{item.feePolicy === 'BUYER_SEPARATE_V1' ? 'Phí kho trả riêng — đơn cũ' : 'Phí khấu trừ — đơn cũ'} ({item.platformFeePercentage}%)</span><span>{item.feePolicy === 'BUYER_SEPARATE_V1' ? '+ ' : '− '}{money(item.platformFeeAmount)}</span></div>}
          <div className="flex justify-between gap-4 border-t border-d-border-subtle pt-3 text-lg font-bold text-d-primary"><span>{collection.platform ? 'Thanh toán vào sàn' : 'Trả người bán'}</span><span>{money(collection.platform ? item.grossAmount : item.netAmount)}</span></div>
          {item.feePolicy === 'BUYER_SEPARATE_V1' && item.platformFeeAmount > 0 && <p className="mt-3 text-sm text-d-on-surface-variant">Tổng chi của kho: <strong>{money(item.buyerTotalAmount)}</strong>. Phí cũ giữ theo chính sách của đơn, không trừ tiền Seller.</p>}
        </section>
        {safeUrl(item.checkinImageUrl) && <a className="inline-flex items-center gap-2 rounded-full border border-d-border-subtle px-4 py-2 text-d-primary hover:bg-d-surface-container-low" href={item.checkinImageUrl} target="_blank" rel="noreferrer"><MaterialIcon name="visibility" />Xem ảnh check-in</a>}
        {item.status === 'SELLER_CONFIRMED' ? <div role="status" className="rounded-2xl border border-d-border-subtle bg-d-surface-container-low p-4">
          <h3 className="font-semibold">Chờ nhân viên bàn giao</h3>
          <p className="mt-2 text-sm text-d-on-surface-variant">Người bán đã đồng ý kết quả cân. Nhân viên thu gom cần mở đơn và chọn “Bàn giao chủ kho” để gửi đơn sang bước chờ thanh toán.</p>
          <p className="mt-2 text-sm text-d-on-surface-variant">Chủ kho chỉ chuyển tiền sau khi hoàn tất bàn giao. Đơn này chưa được tính vào tổng nợ cần trả.</p>
        </div> : collection.platform && ['AWAITING_PAYMENT', 'PAYMENT_SENT'].includes(item.status) ? <div className="space-y-4">
          <PayCollectionButton flow="PICKUP" sourceId={payment.id} title="Thanh toán qua sàn (PayOS)" />
          <p className="text-sm text-d-on-surface-variant">Chủ kho thanh toán vào nền tảng; nền tảng giữ tiền và chi cho người bán sau khi đơn hoàn tất. Không cần tải chứng từ chuyển khoản.</p>
          <div className="flex justify-end border-t border-d-border-subtle pt-4"><button type="button" onClick={onClose} className="rounded-full border border-d-border-subtle px-5 py-2.5 font-semibold hover:bg-d-surface-container-low">Đóng</button></div>
        </div> : item.status === 'AWAITING_PAYMENT' ? <form className="space-y-4" onSubmit={(event) => { event.preventDefault(); if (canSubmit && !save.isPending) save.mutate(); }}>
          <label className="block text-sm font-medium">Hình thức đã thanh toán<select className={inputClass} value={paymentMethod} disabled={busy} onChange={e => { setPaymentMethod(e.target.value); setConfirmed(false); }}>{Object.entries(paymentMethods).map(([key, label]) => <option key={key} value={key}>{label}</option>)}</select></label>
          {paymentMethod !== 'BANK_TRANSFER' && <label className="block text-sm font-medium">Căn cứ giao tiền / ghi chú thỏa thuận<input required maxLength={255} disabled={busy} className={inputClass} value={reference} onChange={e => setReference(e.target.value)} placeholder="Ví dụ: giao tiền mặt trực tiếp cho người bán, mã biên nhận…" /></label>}
          {paymentMethod === 'BANK_TRANSFER' && <>
          <div className="rounded-2xl border border-d-border-subtle p-4"><h3 className="font-semibold">Chứng từ chuyển khoản</h3>
            <p className="mt-1 text-sm text-d-on-surface-variant">Chuyển đúng số tiền thực trả ở trên, rồi tải ảnh chứng từ hoặc nhập đường dẫn HTTPS.</p>
            <div className="mt-4"><ProofUpload onUploaded={setProof} onBusyChange={setUploading} disabled={save.isPending} /></div>
            <label className="mt-4 block text-sm font-medium">Đường dẫn chứng từ
              <input required type="url" value={proof} onChange={(event) => setProof(event.target.value)} disabled={busy}
                placeholder="https://..." className={inputClass} /></label></div></>}
          <label className="flex cursor-pointer items-start gap-3 rounded-xl border border-d-border-subtle p-3 text-sm">
            <input type="checkbox" checked={confirmed} onChange={(event) => setConfirmed(event.target.checked)} disabled={busy} className="mt-1 size-4 accent-d-primary" />
            <span>Tôi đã trả đủ <strong>{money(item.netAmount)}</strong> cho người bán theo hình thức {paymentMethodLabel(paymentMethod).toLocaleLowerCase('vi-VN')} và chịu trách nhiệm về xác nhận này.</span></label>
          {save.isError && <p role="alert" className="text-d-error">{depotError(save.error)}</p>}
          <div className="flex flex-wrap justify-end gap-3 border-t border-d-border-subtle pt-4">
            <button type="button" onClick={onClose} disabled={busy} className="rounded-full border border-d-border-subtle px-5 py-2.5 font-semibold hover:bg-d-surface-container-low disabled:opacity-40">Đóng</button>
            <button type="submit" disabled={!canSubmit || save.isPending} className={buttonClass}>{save.isPending ? 'Đang ghi nhận…' : 'Xác nhận đã trả tiền'}</button>
          </div></form> : <div className="rounded-2xl border border-d-border-subtle p-4">
          <h3 className="font-semibold">Chứng từ đã ghi nhận</h3>
          <p className="mt-2 text-sm">Hình thức: <strong>{paymentMethodLabel(item.paymentMethod)}</strong>{item.paymentReference && <> · Căn cứ: {item.paymentReference}</>}</p>
          {safeUrl(item.paymentProofUrl) ? <a className="mt-3 inline-flex items-center gap-2 rounded-full border border-d-border-subtle px-4 py-2 font-semibold text-d-primary hover:bg-d-surface-container-low" href={item.paymentProofUrl} target="_blank" rel="noreferrer"><MaterialIcon name="visibility" />Xem chứng từ</a>
            : <p className="mt-2 text-sm text-d-on-surface-variant">Không có tệp đính kèm. Người bán cần kiểm tra tiền thực nhận trước khi xác nhận.</p>}
          {item.status === 'PAYMENT_SENT' && <p className="mt-3 text-sm text-d-on-surface-variant">Đang chờ người bán xác nhận đã nhận tiền. Tồn kho chỉ tăng khi đơn hoàn tất.</p>}
        </div>}
      </div>}
  </Dialog>;
}
