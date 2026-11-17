import { useState } from 'react';
import { Button, Field, Pagination, useFactory } from '../components/FactoryUI';
import { downloadFactoryAttachment, uploadFactoryFeeProof } from '../data/factoryApi';
import { useFactoryWorkflowHistory } from '../data/useFactoryData';
import { labels } from '../data/factoryState';
const time = value => new Date(value).toLocaleString('vi-VN', { timeZone:'Asia/Ho_Chi_Minh' });
const actions = { QC_ACCEPTED:'KCS đạt', QC_REJECTED:'KCS không đạt', QC_RESOLUTION:'Chọn hướng xử lý KCS', PRICE_PROPOSED:'Nhà máy đề xuất giá', PRICE_ACCEPTED:'Kho đồng ý giá', PRICE_DISPUTED:'Kho yêu cầu điều chỉnh giá', PAYMENT_REPORTED:'Nhà máy báo chuyển khoản', PAYMENT_CONFIRMED:'Kho nhận đủ tiền', RETURN_HANDED_OVER:'Nhà máy bàn giao trả', RETURN_RECEIVED:'Kho nhận đủ lô trả' };
export function FactoryWorkflowHistory({ order }) {
  const [page,setPage]=useState(1);
  const query=useFactoryWorkflowHistory(order.id,page);
  return <>
    <p className="muted">Chỉ hiển thị mốc thời gian đã được ghi nhận. Không suy đoán giờ vận chuyển hoặc xác nhận của bên còn lại.</p>
    {query.isPending && <p role="status">Đang tải lịch sử xác nhận…</p>}
    {query.isError && <p role="alert">Không tải được lịch sử xác nhận. <button type="button" onClick={()=>query.refetch()}>Thử lại</button></p>}
    {!!query.data?.totalCount && <><ol className="timeline">{query.data.items.map(e=><li key={e.id}><span className="timeline-dot"/><div><strong>{actions[e.action] || e.action}</strong><small>{time(e.createdAt)}</small>{e.note && <p>{e.note}</p>}</div></li>)}</ol><Pagination page={page} totalPages={query.data.totalPages} totalCount={query.data.totalCount} onChange={setPage}/></>}
    {!query.data?.totalCount && <ol className="timeline">{order.history.map((h,i)=><li key={i}><span className="timeline-dot"/><div><strong>{h.label || labels[h.status] || h.status}</strong><small>{time(h.at)}</small></div></li>)}</ol>}
  </>;
}
export function QualityResolutionForm({ order, close }) {
  const {act}=useFactory();
  const [resolution,setResolution]=useState(order.qc?.resolution || 'RENEGOTIATE'),[note,setNote]=useState(''),[busy,setBusy]=useState(false);
  return <form onSubmit={async e=>{e.preventDefault();if(busy)return;setBusy(true);try{if(await act('QUALITY_RESOLUTION',{id:order.id,resolution,note},'Đã chọn hướng xử lý; kết quả KCS được giữ nguyên.'))close();}finally{setBusy(false);}}}>
    <p className="info-box">Giữ nguyên KCS không đạt. Nếu thương lượng, vựa cần đồng ý giá mua theo hiện trạng. Nếu trả hàng, cần bàn giao trả kèm chứng từ rồi vựa xác nhận nhận đủ; chưa tự hoàn tồn.</p>
    <Field label="Hướng xử lý KCS"><select disabled={busy} value={resolution} onChange={e=>setResolution(e.target.value)}><option value="RENEGOTIATE">Thương lượng mua theo hiện trạng</option><option value="RETURN">Trả toàn bộ lô về kho</option></select></Field>
    <Field label="Ghi chú lựa chọn hướng xử lý" required><textarea required disabled={busy} maxLength={2000} value={note} onChange={e=>setNote(e.target.value)}/></Field>
    <div className="form-actions"><Button secondary disabled={busy} onClick={close}>Hủy</Button><Button type="submit" disabled={busy || !note.trim()}>{busy?'Đang lưu…':'Lưu hướng xử lý'}</Button></div>
  </form>;
}
export function ReturnHandoverForm({ order, close }) {
  const {act}=useFactory();
  const [reference,setReference]=useState(''),[note,setNote]=useState(''),[file,setFile]=useState(null),[confirmed,setConfirmed]=useState(false),[busy,setBusy]=useState(false),[error,setError]=useState('');
  return <form onSubmit={async e=>{e.preventDefault();if(busy || !file || !confirmed)return;setBusy(true);setError('');try{const proofUrl=await uploadFactoryFeeProof(file);if(await act('RETURN_HANDOVER',{id:order.id,reference,note,proofUrl},'Đã bàn giao trả; chờ kho xác nhận nhận đủ.'))close();}catch(cause){setError(cause.message);}finally{setBusy(false);}}}>
    <p className="info-box">Xác nhận thực tế đã bàn giao trả toàn bộ lô. Chứng từ không thay cho xác nhận đã nhận hàng của kho; tồn chỉ được hoàn sau bước của kho.</p>
    <Field label="Tham chiếu bàn giao trả" required value={reference} maxLength={200} disabled={busy} onChange={e=>setReference(e.target.value)}/>
    <Field label="Ghi chú bàn giao trả" required><textarea required maxLength={2000} disabled={busy} value={note} onChange={e=>setNote(e.target.value)}/></Field>
    <Field label="Chứng từ bàn giao trả (PDF, JPG, PNG; tối đa 700 KB)" required><input type="file" required accept=".pdf,.jpg,.jpeg,.png" disabled={busy} onChange={e=>setFile(e.target.files?.[0] || null)}/></Field>
    <label className="checkbox-line"><input type="checkbox" checked={confirmed} disabled={busy} onChange={e=>setConfirmed(e.target.checked)}/>Tôi đã bàn giao trả toàn bộ lô thực tế và kiểm tra chứng từ.</label>
    {error && <p role="alert" className="text-danger">{error}</p>}
    <div className="form-actions"><Button secondary disabled={busy} onClick={close}>Hủy</Button><Button type="submit" disabled={busy || !confirmed || !file || !reference.trim() || !note.trim()}>{busy?'Đang gửi…':'Xác nhận đã bàn giao trả'}</Button></div>
  </form>;
}
export function ReturnSummary({ order }) {
  const {notify}=useFactory(),d=order.return;
  if(!d?.handedOverAt)return null;
  return <div className="info-box"><strong>{order.status==='RETURNED'?'Kho đã xác nhận nhận đủ lô trả':'Đang chờ kho nhận đủ lô trả'}</strong><p>Bàn giao: {time(d.handedOverAt)} · {d.handoverReference}</p><p>{d.handoverNote}</p>{d.handoverProofUrl && <button type="button" onClick={()=>void downloadFactoryAttachment(d.handoverProofUrl,'bien-ban-tra').catch(e=>notify(e.message,true))}>Tải chứng từ bàn giao trả</button>}{d.receivedAt && <p>Kho nhận: {time(d.receivedAt)} · {d.receiptReference}. Không phải giao dịch đã thanh toán.</p>}</div>;
}
