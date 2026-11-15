import { useState } from 'react';
import { useDepotMutation } from '../depotApi';
import { Dialog, MutationError, inputClass, buttonClass } from './DepotUI';
export default function AddStaffModal({ staff, onClose }) {
  const [form,setForm]=useState(staff ?? {fullName:'',email:'',phone:'',password:'',role:'DEPOT_EMPLOYEE',isActive:true});
  const save=useDepotMutation(staff ? 'put':'post',staff ? `staff/${staff.id}`:'staff',onClose);
  return <Dialog title={staff?'Cập nhật nhân viên':'Thêm nhân viên mới'} onClose={onClose} busy={save.isPending}><form className="space-y-4" onSubmit={(e)=>{e.preventDefault();save.mutate({body:form});}}>
    {[["fullName","Họ tên","text"],["phone","Số điện thoại","tel"],...(!staff?[["email","Email","email"],["password","Mật khẩu ban đầu","password"]]:[])].map(([key,label,type])=><label key={key} className="block">{label}<input required className={inputClass} type={type} autoComplete={type==='password'?'new-password':'off'} minLength={type==='password'?6:undefined} value={form[key]} onChange={(e)=>setForm({...form,[key]:e.target.value})}/></label>)}
    {!staff && <label className="block">Vai trò<select className={inputClass} value={form.role} onChange={(e)=>setForm({...form,role:e.target.value})}><option value="DEPOT_EMPLOYEE">Nhân viên thu gom</option><option value="DRIVER">Tài xế</option></select></label>}
    {staff && <label className="flex items-center gap-3"><input type="checkbox" checked={form.isActive} onChange={(e)=>setForm({...form,isActive:e.target.checked})}/>Đang hoạt động</label>}
    <MutationError mutation={save}/><button className={buttonClass} disabled={save.isPending}>{save.isPending?'Đang lưu…':'Lưu nhân viên'}</button>
  </form></Dialog>;
}
