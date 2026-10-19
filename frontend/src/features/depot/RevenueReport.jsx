import { useState } from 'react';
import { useDepotQuery, money, date } from './depotApi';
import { Page, Cards, QueryState, GridTable, PeriodFilter, cellClass } from './components/DepotUI';
import CostReportPanel from './components/CostReportPanel';
export default function RevenueReport(){
 const [period,setPeriod]=useState({groupBy:'day'}); const [tab,setTab]=useState('cash'); const query=useDepotQuery('reports/revenue',period,tab==='cash'); const report=query.data;
 const max=Math.max(1,...(report?.points??[]).flatMap((p)=>[p.revenue,p.purchaseCost]));
 return <Page title="Báo cáo doanh thu" description="Đối chiếu quyết toán, thu mua và giá vốn các giao dịch theo giờ Việt Nam.">
  <div role="tablist" aria-label="Loại báo cáo" className="flex gap-2 border-b border-d-border-subtle overflow-x-auto">{[['cash','Quyết toán và thu mua'],['costs','Giá vốn và chênh lệch']].map(([key,label])=><button key={key} type="button" role="tab" aria-selected={tab===key} className={`whitespace-nowrap px-4 py-3 border-b-2 font-semibold ${tab===key?'border-d-primary text-d-primary':'border-transparent text-d-on-surface-variant'}`} onClick={()=>setTab(key)}>{label}</button>)}</div>
  <PeriodFilter period={period} setPeriod={setPeriod} showGroupBy={tab==='cash'}/>
  {tab==='costs'?<CostReportPanel period={period}/>:<QueryState query={query}><Cards items={[
    {label:'Giá trị lô đã quyết toán',value:money(report?.revenue),icon:'payments',description:'Tiền ròng theo quyết toán nhà máy trong kỳ; chưa xác nhận tiền đã về tài khoản kho.'},
    {label:'Tiền thu mua đã ghi nhận',value:money(report?.purchaseCost),icon:'shopping_cart',description:'Thực trả cho người bán theo các giao dịch đã ghi nhận chuyển tiền trong kỳ.'}
  ]}/>
   <p className="text-sm text-d-on-surface-variant">Hai số liệu không phải lợi nhuận: chưa trừ vận chuyển, lương và chi phí vận hành; người bán hoặc chủ kho có thể chưa xác nhận đã nhận tiền.</p>
   <section className="bg-white border border-d-border-subtle rounded-[20px] p-6"><h2 className="text-xl font-semibold mb-4">Quyết toán lô xuất và thu mua</h2><p className="text-sm mb-4">Xanh: lô đã quyết toán · Xám: tiền thu mua đã ghi nhận</p>
    <div className="space-y-3 max-h-96 overflow-y-auto">{report?.points.map((p)=><div key={p.date} className="grid grid-cols-[6rem_1fr] gap-4 text-sm"><span>{date(p.date)}</span><div><div className="bg-d-primary rounded h-4 mb-1" style={{width:`${p.revenue/max*100}%`}} title={money(p.revenue)}/><div className="bg-d-outline-variant rounded h-4" style={{width:`${p.purchaseCost/max*100}%`}} title={money(p.purchaseCost)}/></div></div>)}</div>
    {!report?.points.length&&<p role="status">Chưa có giao dịch trong kỳ.</p>}</section>
   <GridTable headers={['Kỳ','Giá trị quyết toán','Tiền thu mua']} empty={!report?.points.length}>{report?.points.map((p)=><tr key={p.date}><td className={cellClass}>{date(p.date)}</td><td className={cellClass}>{money(p.revenue)}</td><td className={cellClass}>{money(p.purchaseCost)}</td></tr>)}</GridTable>
  </QueryState>}
 </Page>;
}
