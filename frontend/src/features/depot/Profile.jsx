import { useState } from 'react';
import { MaterialIcon } from './components/DepotIcon';
import { useDepotQuery } from './depotApi';
import { QueryState, buttonClass } from './components/DepotUI';
import EditProfileModal from './components/EditProfileModal';
export default function Profile() {
  const query=useDepotQuery('profile'); const [edit,setEdit]=useState(false); const p=query.data;
  return <div className="min-h-full bg-d-surface"><div className="hidden md:block h-60 d-pattern-overlay"/><div className="max-w-7xl mx-auto px-4 md:px-6 pt-4 md:pt-0 md:-mt-24 relative pb-12"><QueryState query={query}>
    {p && <><section className="d-profile-glass rounded-2xl p-6 md:p-8 mb-8 flex flex-wrap items-center justify-between gap-6"><div className="flex items-center gap-6"><span className="inline-flex bg-white p-4 rounded-2xl"><MaterialIcon name="warehouse" className="text-3xl text-d-primary"/></span><div><h1 className="text-3xl font-bold">{p.name}</h1><p className="text-d-on-surface-variant mt-2">{p.address}</p><p className="mt-2 text-sm">{p.rating > 0 ? `★ Điểm hồ sơ ${p.rating} / 5 (tham khảo)` : 'Chưa có điểm hồ sơ'}</p></div></div><button className={buttonClass} onClick={()=>setEdit(true)}>Chỉnh sửa hồ sơ</button></section>
    <section className="rounded-[20px] border border-d-border-subtle bg-white p-8"><h2 className="text-xl font-bold mb-6">Thông tin kho vựa</h2><dl className="grid md:grid-cols-2 gap-6">{[['Mã số thuế',p.taxCode],['Điện thoại',p.contactPhone],['Email chủ kho',p.email],['GPS',p.latitude != null ? `${p.latitude}, ${p.longitude}` : null]].map(([label,value])=><div key={label}><dt className="text-d-on-surface-variant">{label}</dt><dd className="mt-2 font-medium break-words">{value || 'Chưa cập nhật'}</dd></div>)}</dl><p className="mt-8 whitespace-pre-wrap break-words">{p.description}</p></section>
    {edit && <EditProfileModal profile={p} onClose={()=>setEdit(false)}/>}</>}
  </QueryState></div></div>;
}
