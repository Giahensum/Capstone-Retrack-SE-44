import { useState } from 'react';
import { useDepotQuery, number, money, date } from './depotApi';
import { Page, QueryState, GridTable, Pager, inputClass, buttonClass, cellClass } from './components/DepotUI';
const labels={APPROVED:'Đã hợp tác',PENDING:'Chờ duyệt',BLOCKED:'Đã chặn'};
export default function Partners(){
  const [tab,setTab]=useState('partners'),[page,setPage]=useState(1),[search,setSearch]=useState(''),[status,setStatus]=useState('');
  const query=useDepotQuery(tab==='partners'?'partners':'partners/demands',{page,search,...(tab==='partners'?{status}:{})});
  return <Page title="Nhà máy đối tác" description="Tìm nhà máy phù hợp và xem nhu cầu thu mua đang còn hiệu lực.">
    <nav className="flex gap-3">{[['partners','Danh sách nhà máy'],['demands','Nhu cầu thu mua']].map(([key,label])=><button key={key} aria-pressed={key===tab} className={buttonClass} onClick={()=>{setTab(key);setPage(1);}}>{label}</button>)}</nav>
    <div className="flex flex-wrap gap-4"><label>Tìm kiếm<input value={search} className={inputClass} onChange={(e)=>{setSearch(e.target.value);setPage(1);}}/></label>{tab==='partners'&&<label>Quan hệ đối tác<select value={status} className={inputClass} onChange={(e)=>{setStatus(e.target.value);setPage(1);}}><option value="">Tất cả nhà máy</option>{Object.entries(labels).map(([key,label])=><option key={key} value={key}>{label}</option>)}</select></label>}</div>
    <QueryState query={query}>{tab==='partners'?<GridTable headers={['Nhà máy','Địa chỉ','Đánh giá','Quan hệ']} empty={!query.data?.items.length}>{query.data?.items.map((f)=><tr key={f.id}><td className={cellClass}>{f.name}</td><td className={cellClass}>{f.address}</td><td className={cellClass}>★ {f.rating}</td><td className={cellClass}>{labels[f.partnershipStatus]??'Chưa hợp tác'}</td></tr>)}</GridTable>:<GridTable headers={['Nhà máy','Vật liệu','Nhu cầu','Giá tham khảo / kg','Hạn nhận']} empty={!query.data?.items.length}>{query.data?.items.map((d)=><tr key={d.id}><td className={cellClass}>{d.factoryName}</td><td className={cellClass}>{d.materialType}</td><td className={cellClass}>{number(d.requiredWeightKg)} kg</td><td className={cellClass}>{d.minPricePerKg!=null?money(d.minPricePerKg):'Thỏa thuận'} — {d.maxPricePerKg!=null?money(d.maxPricePerKg):'Thỏa thuận'}</td><td className={cellClass}>{date(d.deadline)}</td></tr>)}</GridTable>}<Pager page={page} setPage={setPage} total={query.data?.totalCount}/></QueryState>
  </Page>;
}
