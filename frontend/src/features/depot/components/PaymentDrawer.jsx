import { useEffect, useRef, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import api from '@/lib/axios';
import ProofUpload from './ProofUpload';
import { depotError } from '../DepotContext';

const money = (value) => Number(value).toLocaleString('vi-VN') + ' đ';
export default function PaymentDrawer({ payment, onClose }) {
  const dialog = useRef(null);
  const [proof, setProof] = useState(payment.paymentProofUrl ?? '');
  const [uploading, setUploading] = useState(false);
  const client = useQueryClient();
  useEffect(() => { dialog.current.showModal(); }, []);
  const save = useMutation({
    mutationFn: () => api.patch(`/depot/pickup-requests/${payment.id}/payment-sent`, { paymentProofUrl: proof }),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: ['depot'] });
      onClose();
    },
  });
  return <dialog ref={dialog} onCancel={(e) => { if (save.isPending || uploading) e.preventDefault(); else onClose(); }}
    className="m-auto w-full max-w-xl max-h-[90vh] rounded-2xl border border-d-border-subtle bg-white p-6 backdrop:bg-black/40" aria-labelledby="payment-title">
    <div className="flex justify-between gap-4"><h2 id="payment-title" className="text-xl font-bold">Chi tiết thanh toán</h2>
      <button aria-label="Đóng chi tiết" disabled={save.isPending || uploading} onClick={onClose}>✕</button></div>
    <p className="my-4 break-words">{payment.sellerName} · {payment.sellerPhone}<br />{payment.address}</p>
    <p>Nhân viên: {payment.collectorName ?? 'Chưa có thông tin'}</p>
    <div className="overflow-x-auto my-4"><table className="w-full text-left"><thead><tr><th>Vật liệu</th><th>Kg</th><th>Đơn giá</th><th>Thành tiền</th></tr></thead>
      <tbody>{payment.items.map((item, i) => <tr key={i}><td className="py-2">{item.materialType}</td><td>{item.weightKg}</td><td>{money(item.pricePerKg)}</td><td>{money(item.subTotal)}</td></tr>)}</tbody></table></div>
    <p>Tiền gốc: {money(payment.grossAmount)}</p>
    <p>Phí ({payment.platformFeePercentage}%): {money(payment.platformFeeAmount)}</p>
    <p className="font-bold text-d-primary my-3">Thực trả: {money(payment.netAmount)}</p>
    {payment.checkinImageUrl && /^https?:\/\//i.test(payment.checkinImageUrl) && <a href={payment.checkinImageUrl} target="_blank" rel="noreferrer">Xem ảnh check-in</a>}
    <form className="mt-4 space-y-4" onSubmit={(e) => { e.preventDefault(); if (!uploading && !save.isPending) save.mutate(); }}>
      {payment.status === 'AWAITING_PAYMENT' && <ProofUpload onUploaded={setProof} onBusyChange={setUploading} disabled={save.isPending} />}
      <label className="block">Đường dẫn chứng từ chuyển khoản
        <input required type="url" pattern="https?://.+" value={proof} onChange={(e) => setProof(e.target.value)}
          readOnly={payment.status !== 'AWAITING_PAYMENT' || uploading || save.isPending} className="block w-full border rounded-xl p-3 mt-2" /></label>
      {save.isError && <p role="alert" className="text-d-error">{depotError(save.error)}</p>}
      {payment.status === 'AWAITING_PAYMENT' && <><p className="text-sm">Chỉ xác nhận sau khi bạn đã chuyển tiền. Thao tác này ghi nhận chứng từ và phí nền tảng.</p>
        <button disabled={save.isPending || uploading} className="bg-d-primary text-white rounded-full px-6 py-3 disabled:opacity-50">{save.isPending ? 'Đang ghi nhận…' : 'Xác nhận đã chuyển tiền'}</button></>}
    </form>
  </dialog>;
}
