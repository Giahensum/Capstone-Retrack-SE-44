import { useState } from 'react';
import { useDepotQuery, money, date } from './depotApi';
import { Page, Cards, QueryState, GridTable, PeriodFilter, cellClass } from './components/DepotUI';
export default function RevenueReport(){
 const [period,setPeriod]=useState({groupBy:'day'}); const query=useDepotQuery('reports/revenue',period); const report=query.data;
 const max=Math.max(1,...(report?.points??[]).flatMap((p)=>[p.revenue,p.purchaseCost]));
 return <Page title="Báo cáo doanh thu" description="Doanh thu đã ghi nhận và chi phí thu mua theo thời gian (giờ Việt Nam)."><PeriodFilter period={period} setPeriod={setPeriod}/>
  <QueryState query={query}><Cards items={[{label:'Doanh thu thực nhận',value:money(report?.revenue),icon:'payments'},{label:'Chi phí trả người bán',value:money(report?.purchaseCost),icon:'shopping_cart'}]}/>
   <section className="bg-white border border-d-border-subtle rounded-[20px] p-6"><h2 className="text-xl font-semibold mb-4">Doanh thu và chi phí thu mua</h2><p className="text-sm mb-4">Xanh: doanh thu · Xám: chi phí thu mua</p>
    <div className="space-y-3 max-h-96 overflow-y-auto">{report?.points.map((p)=><div key={p.date} className="grid grid-cols-[6rem_1fr] gap-4 text-sm"><span>{date(p.date)}</span><div><div className="bg-d-primary rounded h-4 mb-1 min-w-px" style={{width:`${p.revenue/max*100}%`}} title={money(p.revenue)}/><div className="bg-d-outline-variant rounded h-4 min-w-px" style={{width:`${p.purchaseCost/max*100}%`}} title={money(p.purchaseCost)}/></div></div>)}</div>
    {!report?.points.length&&<p role="status">Chưa có giao dịch trong kỳ.</p>}</section>
   <GridTable headers={['Kỳ','Doanh thu','Chi phí thu mua']} empty={!report?.points.length}>{report?.points.map((p)=><tr key={p.date}><td className={cellClass}>{date(p.date)}</td><td className={cellClass}>{money(p.revenue)}</td><td className={cellClass}>{money(p.purchaseCost)}</td></tr>)}</GridTable>
  </QueryState>
 </Page>;
}
