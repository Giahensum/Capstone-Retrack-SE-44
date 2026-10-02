import { useState } from 'react';
import ProofUpload from './components/ProofUpload';
import { useDepotQuery, useDepotMutation, money, date } from './depotApi';
import { Page, Cards, QueryState, GridTable, Pager, PeriodFilter, Dialog, MutationError, inputClass, buttonClass, cellClass } from './components/DepotUI';
const labels={UNPAID:'Chưa thanh toán',SUBMITTED:'Chờ đối soát',PAID:'Đã thanh toán'};
function ConfirmInvoice({invoice,onClose}){
 const [proof,setProof]=useState(''); const [uploading,setUploading]=useState(false); const save=useDepotMutation('patch',`reports/invoices/${invoice.id}/confirmation`,onClose);
 return <Dialog title="Xác nhận chuyển khoản phí nền tảng" busy={save.isPending || uploading} onClose={onClose}><p>Hóa đơn tháng {date(invoice.periodStart)}: {money(invoice.amount)}</p><form className="mt-4 space-y-4" onSubmit={(e)=>{e.preventDefault();if (!uploading && !save.isPending) save.mutate({body:{paymentProofUrl:proof}});}}><ProofUpload onUploaded={setProof} onBusyChange={setUploading} disabled={save.isPending}/><label className="block">Đường dẫn chứng từ<input className={inputClass} disabled={uploading || save.isPending} required type="url" pattern="https?://.+" value={proof} onChange={(e)=>setProof(e.target.value)}/></label><p>Gửi chứng từ sau khi chuyển khoản. Hóa đơn sẽ chờ Admin đối soát.</p><MutationError mutation={save}/><button disabled={save.isPending || uploading} className={buttonClass}>Gửi xác nhận</button></form></Dialog>;
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
export default function PlatformFees(){
 const [period,setPeriod]=useState({}),[tab,setTab]=useState('fees'),[page,setPage]=useState(1),[invoice,setInvoice]=useState(null),[simulatedInvoice,setSimulatedInvoice]=useState(null);
 const summary=useDepotQuery('reports/fees/summary',period); const query=useDepotQuery(`reports/${tab}`,{...period,page});
 return <Page title="Phí nền tảng" description="Theo dõi phí tích lũy và hóa đơn phí hàng tháng."><PeriodFilter period={period} setPeriod={(p)=>{setPeriod(p);setPage(1);}}/>
  <QueryState query={summary}><Cards items={[{label:'Phí phát sinh tại kho trong kỳ',value:money(summary.data?.accruedAmount),icon:'receipt'},{label:'Hóa đơn chưa trả — toàn bộ kho của bạn',value:money(summary.data?.unpaidInvoiceAmount)},{label:'Đã gửi chứng từ — chờ đối soát',value:money(summary.data?.submittedInvoiceAmount)}]}/></QueryState>
  <nav className="flex gap-3"><button className={buttonClass} onClick={()=>{setTab('fees');setPage(1);}}>Chi tiết phí</button><button className={buttonClass} onClick={()=>{setTab('invoices');setPage(1);}}>Hóa đơn hàng tháng</button></nav>
  {invoice&&<ConfirmInvoice invoice={invoice} onClose={()=>setInvoice(null)}/>}
  {simulatedInvoice&&<SimulatePayment invoice={simulatedInvoice} onClose={()=>setSimulatedInvoice(null)}/>}
  <QueryState query={query}>{tab==='fees'?<GridTable headers={['Đơn thu gom','Ngày ghi nhận','Phí']} empty={!query.data?.items.length}>{query.data?.items.map((f)=><tr key={f.id}><td className={cellClass}>#{f.sourceId.slice(0,8)}</td><td className={cellClass}>{date(f.date)}</td><td className={cellClass}>{money(f.feeAmount)}</td></tr>)}</GridTable>:<><p className="text-sm">Hóa đơn do nền tảng phát hành, tổng hợp tất cả kho của chủ tài khoản.</p><GridTable headers={['Kỳ hóa đơn','Số tiền','Trạng thái','Hành động']} empty={!query.data?.items.length}>{query.data?.items.map((i)=><tr key={i.id}><td className={cellClass}>{date(i.periodStart)}</td><td className={cellClass}>{money(i.amount)}</td><td className={cellClass}>{labels[i.status]}</td><td className={cellClass}>{i.status==='UNPAID'&&<div className="flex flex-wrap gap-2"><button className={buttonClass} onClick={()=>setInvoice(i)}>Xác nhận chuyển khoản</button>{import.meta.env.DEV&&<button className={buttonClass} onClick={()=>setSimulatedInvoice(i)}>PayOS giả lập</button>}</div>}</td></tr>)}</GridTable></>}<Pager page={page} setPage={setPage} total={query.data?.totalCount}/></QueryState>
 </Page>;
}
