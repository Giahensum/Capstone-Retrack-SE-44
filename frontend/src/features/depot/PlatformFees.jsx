import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import ProofUpload from './components/ProofUpload';
import { useDepotQuery, useDepotMutation, money, date } from './depotApi';
import { Page, Cards, QueryState, GridTable, Pager, PeriodFilter, Dialog, MutationError, inputClass, buttonClass, cellClass } from './components/DepotUI';
const labels={UNPAID:'Chưa thanh toán',SUBMITTED:'Chờ đối soát',PAID:'Đã thanh toán'};
function ConfirmInvoice({invoice,onClose}){
 const [proof,setProof]=useState(''); const [uploading,setUploading]=useState(false); const save=useDepotMutation('patch',`reports/invoices/${invoice.id}/confirmation`,onClose);
 return <Dialog title="Xác nhận chuyển khoản phí nền tảng" busy={save.isPending || uploading} onClose={onClose}><p>Hóa đơn tháng {date(invoice.periodStart)}: {money(invoice.amount)}</p><form className="mt-4 space-y-4" onSubmit={(e)=>{e.preventDefault();if (!uploading && !save.isPending) save.mutate({body:{paymentProofUrl:proof}});}}><ProofUpload onUploaded={setProof} onBusyChange={setUploading} disabled={save.isPending}/><label className="block">Đường dẫn chứng từ<input className={inputClass} disabled={uploading || save.isPending} required type="url" pattern="https?://.+" value={proof} onChange={(e)=>setProof(e.target.value)}/></label><p>Gửi chứng từ sau khi chuyển khoản. Hóa đơn sẽ chờ Admin đối soát.</p><MutationError mutation={save}/><button disabled={save.isPending || uploading} className={buttonClass}>Gửi xác nhận</button></form></Dialog>;
}
function PayOsPay({invoice}){
 const pay=useDepotMutation('post',`reports/invoices/${invoice.id}/payos-link`,undefined);
 return <button disabled={pay.isPending} className={buttonClass} onClick={()=>pay.mutate({},{onSuccess:(res)=>{window.location.assign(res.data.data.checkoutUrl);}})}>{pay.isPending?'Đang tạo link…':'Thanh toán PayOS'}</button>;
}
function SimulatePayment({invoice,onClose}){
 const save=useDepotMutation('post',`reports/invoices/${invoice.id}/simulate-payment`,onClose);
 return <Dialog title="Giả lập thanh toán PayOS" busy={save.isPending} onClose={onClose}>
  <p>Hóa đơn tháng {date(invoice.periodStart)}: {money(invoice.amount)}</p>
  <p className="mt-3 rounded-xl border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900">Chế độ thử nghiệm: không gọi PayOS và không chuyển tiền thật. Hệ thống chỉ lưu chứng từ giả lập, hóa đơn sẽ chờ Admin đối soát.</p>
  <MutationError mutation={save}/>
  <div className="mt-4 flex flex-wrap gap-3"><button disabled={save.isPending} className={buttonClass} onClick={()=>save.mutate({})}>{save.isPending?'Đang giả lập…':'Mô phỏng thanh toán thành công'}</button><button disabled={save.isPending} className={buttonClass} type="button" onClick={onClose}>Đóng</button></div>
 </Dialog>;
}
const orderCodeOf=(url)=>{try{return new URL(url).searchParams.get('orderCode');}catch{return null;}};
function PayOsReturn({orderCode,invoices}){
 // Sau khi PayOS chuyển hướng về: đối soát trực tiếp với PayOS (không tin tham số trên URL).
 const sync=useDepotMutation('post',(id)=>`reports/invoices/${id}/payos-sync`);
 const target=invoices?.find((i)=>orderCodeOf(i.paymentProofUrl)===orderCode);
 useEffect(()=>{if(target&&target.status==='UNPAID'&&sync.isIdle)sync.mutate({id:target.id});},[target,sync]);
 if(!target)return null;
 if(sync.isPending)return <p role="status" className="rounded-xl border border-d-border-subtle p-3 text-sm">Đang xác nhận thanh toán với PayOS…</p>;
 if(sync.isError)return <MutationError mutation={sync}/>;
 return <p role="status" className="rounded-xl border border-emerald-300 bg-emerald-50 p-3 text-sm text-emerald-900">{target.status==='PAID'||sync.data?.data?.data==='PAID'?'Thanh toán PayOS thành công, hóa đơn đã được ghi nhận.':'PayOS chưa ghi nhận thanh toán. Nếu bạn đã chuyển tiền, hệ thống sẽ tự cập nhật khi nhận thông báo từ PayOS.'}</p>;
}
export default function PlatformFees(){
 const [params]=useSearchParams(); const returned=params.get('payos')==='return'?params.get('orderCode'):null;
 const [period,setPeriod]=useState({}),[tab,setTab]=useState(returned?'invoices':'fees'),[page,setPage]=useState(1),[invoice,setInvoice]=useState(null),[simulatedInvoice,setSimulatedInvoice]=useState(null);
 const summary=useDepotQuery('reports/fees/summary',period); const query=useDepotQuery(`reports/${tab}`,{...period,page});
 const client=useQueryClient();
 // Có hóa đơn đã tạo link PayOS mà chưa thanh toán: tải lại mỗi 10 giây vì backend tự đối soát với PayOS.
 const waitingPayos=tab==='invoices'&&query.data?.items?.some((i)=>i.status==='UNPAID'&&orderCodeOf(i.paymentProofUrl));
 useEffect(()=>{if(!waitingPayos)return undefined;const t=setInterval(()=>client.invalidateQueries({queryKey:['depot']}),10000);return ()=>clearInterval(t);},[waitingPayos,client]);
 return <Page title="Phí nền tảng" description="Phí từ đơn thu gom và trạng thái hóa đơn do nền tảng phát hành."><PeriodFilter period={period} setPeriod={(p)=>{setPeriod(p);setPage(1);}} showGroupBy={false}/>
  <p className="text-sm text-d-on-surface-variant">Khoảng ngày chỉ lọc phí của kho đang chọn. Hóa đơn và hai số dư hóa đơn bên dưới hiển thị tất cả kỳ, trên mọi kho của chủ tài khoản.</p>
  <QueryState query={summary}><Cards items={[
    {label:'Phí đơn thu gom tại kho trong kỳ',value:money(summary.data?.accruedAmount),icon:'receipt',description:'Chỉ tính kho đang chọn và khoảng ngày lọc; phát sinh khi ghi nhận chuyển tiền cho người bán.'},
    {label:'Hóa đơn chưa gửi chứng từ',value:money(summary.data?.unpaidInvoiceAmount),icon:'hourglass_empty',description:'Tổng hóa đơn đang chờ thanh toán của mọi kho thuộc tài khoản; không theo khoảng ngày lọc.'},
    {label:'Đã gửi chứng từ, chờ đối soát',value:money(summary.data?.submittedInvoiceAmount),icon:'fact_check',description:'Tổng hóa đơn đã gửi chứng từ của mọi kho; Admin chưa xác nhận đã thanh toán.'}
  ]}/></QueryState>
  <nav aria-label="Nội dung phí nền tảng" className="flex flex-wrap gap-2 border-b border-d-border-subtle"><button type="button" aria-current={tab==='fees'?'page':undefined} className={`min-h-11 px-4 border-b-2 font-semibold ${tab==='fees'?'border-d-primary text-d-primary':'border-transparent text-d-on-surface-variant hover:text-d-primary'}`} onClick={()=>{setTab('fees');setPage(1);}}>Chi tiết phí</button><button type="button" aria-current={tab==='invoices'?'page':undefined} className={`min-h-11 px-4 border-b-2 font-semibold ${tab==='invoices'?'border-d-primary text-d-primary':'border-transparent text-d-on-surface-variant hover:text-d-primary'}`} onClick={()=>{setTab('invoices');setPage(1);}}>Hóa đơn hàng tháng</button></nav>
  {returned&&tab==='invoices'&&<PayOsReturn orderCode={returned} invoices={query.data?.items}/>}
  {params.get('payos')==='cancel'&&<p role="status" className="rounded-xl border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900">Bạn đã hủy thanh toán PayOS. Hóa đơn vẫn chờ thanh toán.</p>}
  {invoice&&<ConfirmInvoice invoice={invoice} onClose={()=>setInvoice(null)}/>}
  {simulatedInvoice&&<SimulatePayment invoice={simulatedInvoice} onClose={()=>setSimulatedInvoice(null)}/>}
  <QueryState query={query}>{tab==='fees'?<GridTable headers={['Đơn thu gom','Ngày ghi nhận','Phí']} empty={!query.data?.items.length}>{query.data?.items.map((f)=><tr key={f.id}><td className={cellClass}>#{f.sourceId.slice(0,8)}</td><td className={cellClass}>{date(f.date)}</td><td className={cellClass}>{money(f.feeAmount)}</td></tr>)}</GridTable>:<><p className="text-sm">Hóa đơn do nền tảng phát hành, tổng hợp tất cả kho của chủ tài khoản.</p><GridTable headers={['Kỳ hóa đơn','Số tiền','Trạng thái','Hành động']} empty={!query.data?.items.length}>{query.data?.items.map((i)=><tr key={i.id}><td className={cellClass}>{date(i.periodStart)}</td><td className={cellClass}>{money(i.amount)}</td><td className={cellClass}>{labels[i.status]}</td><td className={cellClass}>{i.status==='UNPAID'&&<div className="flex flex-wrap gap-2"><PayOsPay invoice={i}/><button className={buttonClass} onClick={()=>setInvoice(i)}>Xác nhận chuyển khoản</button>{import.meta.env.DEV&&<button className={buttonClass} onClick={()=>setSimulatedInvoice(i)}>PayOS giả lập</button>}</div>}</td></tr>)}</GridTable></>}<Pager page={page} setPage={setPage} total={query.data?.totalCount}/></QueryState>
 </Page>;
}
