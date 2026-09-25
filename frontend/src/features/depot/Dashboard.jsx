import { Link } from 'react-router-dom';
import { useDepotQuery, number, money } from './depotApi';
import { Page, Cards, QueryState, GridTable, buttonClass, cellClass } from './components/DepotUI';
export default function Dashboard(){
 const query=useDepotQuery('dashboard'),stock=useDepotQuery('inventory'),history=useDepotQuery('inventory/receipts',{pageSize:5}); const d=query.data;
 const total=stock.data?.reduce((n,i)=>n+Math.max(0,i.onHandKg),0)??0;
 return <Page title="Bảng điều khiển" description="Theo dõi hoạt động kho vựa hôm nay.">
  <QueryState query={query}><Cards items={[{label:'Đơn mới hôm nay',value:d?.newRequestsToday??0,icon:'receipt_long'},{label:'Tồn kho khả dụng',value:number(d?.availableKg)+' kg',icon:'inventory'},{label:'Lô đang xử lý',value:d?.activeBatches??0,icon:'local_shipping'},{label:'Doanh thu tháng này',value:money(d?.revenue),icon:'payments'}]}/></QueryState>
  <nav className="flex flex-wrap gap-3">{[['/depot/staff','＋ Thêm nhân sự'],['/depot/payments','Duyệt thanh toán'],['/depot/batches','Tạo lô xuất']].map(([to,label])=><Link key={to} to={to} className={buttonClass}>{label}</Link>)}</nav>
  <div className="grid xl:grid-cols-3 gap-6"><section className="xl:col-span-2 min-w-0"><div className="flex justify-between mb-4"><h2 className="text-xl font-bold">Nhập kho gần đây</h2><Link to="/depot/inventory" className="text-d-primary">Xem tất cả</Link></div><QueryState query={history}><GridTable headers={['Mã đơn','Người bán','Khối lượng']} empty={!history.data?.items.length}>{history.data?.items.map((p)=><tr key={p.id}><td className={cellClass}>#{p.id.slice(0,8)}</td><td className={cellClass}>{p.sellerName}</td><td className={cellClass}>{number(p.items.reduce((n,i)=>n+i.weightKg,0))} kg</td></tr>)}</GridTable></QueryState></section>
  <section className="bg-white rounded-[20px] p-6 border border-d-border-subtle"><h2 className="text-xl font-bold mb-6">Cơ cấu tồn kho</h2><QueryState query={stock}>{stock.data?.map((i)=><div key={i.materialType} className="mb-4"><p className="flex justify-between gap-3 text-sm"><span>{i.materialType}</span><span>{number(i.onHandKg)} kg</span></p><div className="bg-d-surface-accent rounded h-3 mt-2"><div className="bg-d-primary rounded h-3" style={{width:`${total?Math.max(0,i.onHandKg)/total*100:0}%`}}/></div></div>)}{!stock.data?.length&&<p role="status">Chưa có hàng nhập kho.</p>}</QueryState></section></div>
 </Page>;
}
