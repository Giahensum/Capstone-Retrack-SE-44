import { useState } from 'react';
import CreateBatchModal from './components/CreateBatchModal';
import { useDepotQuery, number, date } from './depotApi';
import { Page, Cards, QueryState, GridTable, Pager, buttonClass, cellClass } from './components/DepotUI';
export default function Inventory() {
  const [tab, setTab] = useState('current');
  const [create, setCreate] = useState(false);
  const [page, setPage] = useState(1);
  const stock = useDepotQuery('inventory');
  const history = useDepotQuery('inventory/receipts', { page }, tab === 'history');
  const sum = (key) => stock.data?.reduce((n, i) => n + i[key], 0) ?? 0;
  return <Page title="Quản lý Tồn Kho" description="Theo dõi hàng tồn kho và lịch sử nhập phế liệu." action={<button className={buttonClass} onClick={() => setCreate(true)}>＋ Tạo lô xuất hàng</button>}>
    <CreateBatchModal isOpen={create} onClose={() => setCreate(false)} />
    <nav className="flex gap-3 border-b pb-3">{[['current', 'Tồn kho hiện tại'], ['history', 'Lịch sử nhập kho']].map(([key, label]) => <button key={key} className={buttonClass} aria-pressed={tab === key} onClick={() => setTab(key)}>{label}</button>)}</nav>
    {tab === 'current' ? <QueryState query={stock}><Cards items={[{label:'Tồn kho khả dụng',value:number(sum('availableKg'))+' kg',icon:'scale'}, {label:'Đang giữ cho lô xuất',value:number(sum('reservedKg'))+' kg',icon:'inventory'}, {label:'Tổng tồn tại kho',value:number(sum('onHandKg'))+' kg'}, {label:'Loại vật liệu',value:stock.data?.length ?? 0,icon:'category'}]} />
      <GridTable headers={['Loại vật liệu', 'Khả dụng', 'Đang giữ', 'Tổng tồn']} empty={!stock.data?.length}>{stock.data?.map((i) => <tr key={i.materialType}><td className={cellClass}>{i.materialType}</td>{['availableKg','reservedKg','onHandKg'].map((k) => <td className={cellClass} key={k}>{number(i[k])} kg</td>)}</tr>)}</GridTable></QueryState>
      : <QueryState query={history}><GridTable headers={['Mã đơn', 'Người bán', 'Vật liệu / Khối lượng', 'Ngày tạo đơn']} empty={!history.data?.items.length}>{history.data?.items.map((p) => <tr key={p.id}><td className={cellClass}>#{p.id.slice(0,8)}</td><td className={cellClass}>{p.sellerName}</td><td className={cellClass}>{p.items.map((i) => `${i.materialType}: ${number(i.weightKg)} kg`).join(', ')}</td><td className={cellClass}>{date(p.createdAt)}</td></tr>)}</GridTable><Pager page={page} setPage={setPage} total={history.data?.totalCount} /></QueryState>}
  </Page>;
}
