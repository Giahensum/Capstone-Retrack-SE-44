import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useDepotQuery } from './depotApi';
import AddStaffModal from './components/AddStaffModal';
import { Page, QueryState, GridTable, Pager, inputClass, buttonClass, cellClass } from './components/DepotUI';
export default function Staff(){
  const [page,setPage]=useState(1),[search,setSearch]=useState(''),[status,setStatus]=useState(''),[editing,setEditing]=useState(undefined);
  const query=useDepotQuery('staff',{page,search,status});
  return <Page title="Quản Lý Nhân Sự" description="Quản lý danh sách nhân viên và trạng thái làm việc." action={<button className={buttonClass} onClick={()=>setEditing(null)}>＋ Thêm nhân viên</button>}>
    {editing!==undefined && <AddStaffModal staff={editing} onClose={()=>setEditing(undefined)}/>}
    <div className="flex flex-wrap gap-4"><label>Tìm nhân viên<input className={inputClass} value={search} onChange={(e)=>{setSearch(e.target.value);setPage(1);}}/></label><label>Trạng thái<select className={inputClass} value={status} onChange={(e)=>{setStatus(e.target.value);setPage(1);}}><option value="">Tất cả</option><option value="ACTIVE">Đang hoạt động</option><option value="INACTIVE">Đã vô hiệu hóa</option></select></label></div>
    <QueryState query={query}><GridTable headers={['Nhân viên','Liên hệ','Vai trò','Trạng thái','Hành động']} empty={!query.data?.items.length}>{query.data?.items.map((s)=><tr key={s.id}><td className={cellClass}>{s.fullName}</td><td className={cellClass}>{s.email}<br/>{s.phone}</td><td className={cellClass}>{s.role==='DRIVER'?'Tài xế':'Nhân viên thu gom'}</td><td className={cellClass}>{s.isActive?'Đang hoạt động':'Đã vô hiệu hóa'}</td><td className={cellClass}><button className={buttonClass} onClick={()=>setEditing(s)}>Chỉnh sửa</button></td></tr>)}</GridTable><Pager page={page} setPage={setPage} total={query.data?.totalCount}/></QueryState>
    <Link to="/depot/staff/performance" className="text-d-primary underline">Xem hiệu suất và lịch sử nhân sự →</Link>
  </Page>;
}
