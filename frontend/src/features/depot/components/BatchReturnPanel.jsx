import { Form, Input, Textarea } from '@/components/forms/ValidatedForm';
import { useState } from 'react';
import { useDepotMutation, number } from '../depotApi';
import { buttonClass, inputClass, MutationError } from './DepotUI';
const time = value => value ? new Date(value).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' }) : 'Chưa xác nhận';
export default function BatchReturnPanel({ detail, id, handoverProof }) {
  const { batch, quality, return: returned } = detail;
  const [form, setForm] = useState({ confirmed: false, receivedKg: '', reference: '', note: '' });
  const save = useDepotMutation('post', `batches/${id}/return-received`);
  if (quality?.isAccepted !== false || !['REJECTED','NEGOTIATING','RETURN_REQUESTED','RETURNING','RETURNED'].includes(batch.status)) return null;
  const mismatch = form.receivedKg !== '' && Number(form.receivedKg) !== batch.weightKg;
  return <section className="rounded-2xl border border-d-border-subtle bg-white p-4 space-y-4">
    <h3 className="font-semibold text-d-primary">Xử lý lô không đạt KCS</h3>
    <p className="text-sm">Kết quả KCS được giữ nguyên. Thương lượng giá không có nghĩa là vật liệu đã đạt chất lượng ban đầu.</p>
    {batch.status === 'REJECTED' && <p className="text-sm">Lô cũ đang từ chối. Nhà máy cần chọn rõ thương lượng hoặc trả hàng trước khi xử lý tiếp; hệ thống không tự sửa lịch sử tiền.</p>}
    {batch.status === 'NEGOTIATING' && <p className="rounded-xl bg-d-surface-accent p-4 text-sm">Chờ nhà máy đề xuất giá mua theo hiện trạng. Kho được đồng ý hoặc yêu cầu điều chỉnh, sau đó mới chuyển tiền.</p>}
    {batch.status === 'RETURN_REQUESTED' && <p className="rounded-xl bg-d-surface-accent p-4 text-sm">Đã chọn trả hàng; chờ nhà máy xác nhận bàn giao trả kèm chứng từ. Hàng chưa được cộng lại vào tồn khả dụng.</p>}
    {returned?.handedOverAt && <div className="rounded-xl bg-d-surface-accent p-4 text-sm space-y-2">
      <p>Nhà máy báo bàn giao trả: {time(returned.handedOverAt)}</p><p>Tham chiếu: {returned.handoverReference}</p><p className="whitespace-pre-wrap">{returned.handoverNote}</p>{handoverProof}
    </div>}
    {batch.status === 'RETURNING' && <Form className="space-y-4" onSubmit={e=>{e.preventDefault();save.mutate({body:{...form,receivedKg:Number(form.receivedKg)}});}}>
      <p className="text-sm font-medium">Chỉ hoàn tồn khi thực nhận đủ {number(batch.weightKg)} kg đã xuất. Nếu khác lượng, giữ chờ đối soát; không sửa số cân cho khớp giả.</p>
      <label className="block">Khối lượng thực nhận về kho (kg)<Input required type="number" min="0.001" step="0.001" className={inputClass} disabled={save.isPending} value={form.receivedKg} onChange={e=>setForm({...form,receivedKg:e.target.value})}/></label>
      {mismatch && <p role="alert" className="text-sm text-d-error">Lượng nhận chưa khớp toàn bộ lô. Chưa thể hoàn tồn; hãy liên hệ nhà máy để đối soát.</p>}
      <label className="block">Tham chiếu nhận hàng trả<Input required maxLength={200} className={inputClass} disabled={save.isPending} value={form.reference} onChange={e=>setForm({...form,reference:e.target.value})}/></label>
      <label className="block">Ghi chú cân, kiểm nhận<Textarea required maxLength={2000} className={inputClass} disabled={save.isPending} value={form.note} onChange={e=>setForm({...form,note:e.target.value})}/></label>
      <label className="flex gap-2 text-sm"><Input type="checkbox" checked={form.confirmed} disabled={save.isPending} onChange={e=>setForm({...form,confirmed:e.target.checked})}/>Tôi đã cân, kiểm và nhận đủ toàn bộ lô thực tế về kho.</label>
      <MutationError mutation={save}/><button className={buttonClass} disabled={save.isPending || !form.confirmed || !form.receivedKg || mismatch || !form.reference.trim() || !form.note.trim()}>{save.isPending?'Đang xác nhận…':'Xác nhận nhận đủ lô trả về'}</button>
    </Form>}
    {batch.status === 'RETURNED' && <div role="status" className="rounded-xl bg-d-surface-accent p-4 text-sm space-y-2"><p className="font-semibold">Kho đã nhận đủ lô trả về lúc {time(returned?.receivedAt)}.</p><p>Đã hoàn nguồn tồn đúng một lần, không tạo phiếu nhập trùng. Lô này không phải giao dịch đã thanh toán.</p><p>Tham chiếu: {returned?.receiptReference}</p><p className="whitespace-pre-wrap">{returned?.receiptNote}</p></div>}
  </section>;
}
