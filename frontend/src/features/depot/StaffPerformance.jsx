import { useState } from 'react';
import { useDepotQuery, number, money, date } from './depotApi';
import { Page, QueryState, GridTable, Pager, PeriodFilter, Dialog, buttonClass, cellClass, inputClass } from './components/DepotUI';
function History({staff,period,onClose}){
 const [page,setPage]=useState(1); const query=useDepotQuery(`reports/staff/${staff.id}`,{...period,page});
 return <Dialog title={`Lịch sử — ${staff.fullName}`} onClose={onClose}><QueryState query={query}><GridTable headers={['Mã đơn/chuyến','Khối lượng','Giá trị thu mua','Ngày hoàn tất']} empty={!query.data?.items.length}>{query.data?.items.map((r)=><tr key={r.id}><td className={cellClass}>#{r.id.slice(0,8)}</td><td className={cellClass}>{number(r.weightKg)} kg</td><td className={cellClass}>{r.type==='TRANSPORT'?'—':money(r.amount)}</td><td className={cellClass}>{date(r.date)}</td></tr>)}</GridTable><Pager page={page} setPage={setPage} total={query.data?.totalCount}/></QueryState></Dialog>;
}
export default function StaffPerformance(){
 const [period,setPeriod]=useState({}),[page,setPage]=useState(1),[selected,setSelected]=useState(null); const query=useDepotQuery('reports/staff',{...period,page});
 const [metric,setMetric]=useState('completedCount');
 const chartRows=(query.data?.items??[]).filter((s)=>metric!=='purchaseAmount'||s.role!=='DRIVER');
 const max=Math.max(1,...chartRows.map((s)=>s[metric]));
 const formatMetric=(value)=>metric==='purchaseAmount'?money(value):metric==='weightKg'?`${number(value)} kg`:number(value);
 return <Page title="Hiệu suất nhân sự" description="Xếp hạng theo đơn/chuyến hoàn tất. Chủ kho tự quyết định mức trả công."><PeriodFilter period={period} setPeriod={(p)=>{setPeriod(p);setPage(1);}}/>
  {selected&&<History staff={selected} period={period} onClose={()=>setSelected(null)}/>}
  <QueryState query={query}><section className="rounded-[20px] bg-white border border-d-border-subtle p-6"><h2 className="font-bold mb-4">Biểu đồ hiệu suất nhân sự</h2><label className="block max-w-xs mb-5">Chỉ số biểu đồ<select className={inputClass} value={metric} onChange={(e)=>setMetric(e.target.value)}><option value="completedCount">Đơn và chuyến hoàn tất</option><option value="weightKg">Khối lượng hoàn tất</option><option value="purchaseAmount">Giá trị thu mua của nhân viên gom</option></select></label><div className="space-y-3">{chartRows.map((s)=><div key={s.id}><div className="flex justify-between gap-3 text-sm mb-1"><span>{s.fullName}</span><span>{formatMetric(s[metric])}</span></div><div className="h-3 rounded bg-d-surface-accent"><div className="bg-d-primary h-3 rounded" style={{width:`${s[metric]/max*100}%`}}/></div></div>)}</div>{!chartRows.length&&<p role="status">Chưa có dữ liệu phù hợp.</p>}</section>
  <GridTable headers={['Hạng','Nhân viên','Vai trò','Hoàn tất','Khối lượng','Giá trị thu mua','Chi tiết']} empty={!query.data?.items.length}>{query.data?.items.map((s,index)=><tr key={s.id}><td className={cellClass}>{(page-1)*20+index+1}</td><td className={cellClass}>{s.fullName}</td><td className={cellClass}>{s.role==='DRIVER'?'Tài xế':'Thu gom'}</td><td className={cellClass}>{s.completedCount}</td><td className={cellClass}>{number(s.weightKg)} kg</td><td className={cellClass}>{s.role==='DRIVER'?'—':money(s.purchaseAmount)}</td><td className={cellClass}><button className={buttonClass} onClick={()=>setSelected(s)}>Lịch sử</button></td></tr>)}</GridTable><Pager page={page} setPage={setPage} total={query.data?.totalCount}/></QueryState>
 </Page>;
}
