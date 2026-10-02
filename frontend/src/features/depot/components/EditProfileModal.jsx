import { useState } from 'react';
import { useDepotMutation } from '../depotApi';
import { Dialog, MutationError, inputClass, buttonClass } from './DepotUI';
export default function EditProfileModal({ profile, onClose }) {
  const [form, setForm] = useState({ ...profile, latitude: profile.latitude ?? '', longitude: profile.longitude ?? '' });
  const save = useDepotMutation('put', 'profile', onClose);
  return <Dialog title="Chỉnh sửa hồ sơ kho vựa" busy={save.isPending} onClose={onClose}><form className="space-y-4" onSubmit={(e) => {e.preventDefault(); save.mutate({body:{...form, latitude:form.latitude === '' ? null : Number(form.latitude), longitude:form.longitude === '' ? null : Number(form.longitude)}});}}>
    {[["name","Tên kho vựa"],["address","Địa chỉ"],["contactPhone","Số điện thoại"],["taxCode","Mã số thuế"],["latitude","Vĩ độ"],["longitude","Kinh độ"]].map(([key,label]) => <label key={key} className="block">{label}<input className={inputClass} value={form[key] ?? ''} required={['name','address'].includes(key)} maxLength={key === 'name' ? 255 : key === 'contactPhone' ? 20 : undefined} type={['latitude','longitude'].includes(key) ? 'number' : 'text'} step="any" onChange={(e) => setForm({...form,[key]:e.target.value})}/></label>)}
    <label className="block">Mô tả kho vựa<textarea className={inputClass} value={form.description ?? ''} onChange={(e)=>setForm({...form,description:e.target.value})}/></label>
    <MutationError mutation={save}/><button className={buttonClass} disabled={save.isPending}>{save.isPending?'Đang lưu…':'Lưu thay đổi'}</button>
  </form></Dialog>;
}
