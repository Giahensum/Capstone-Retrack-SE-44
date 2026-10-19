import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import api from '@/lib/axios';
import { useDepot } from '../DepotContext';
import { Dialog, inputClass, buttonClass, MutationError } from './DepotUI';

export default function CreateDepotModal({ onClose }) {
  const [operationId] = useState(() => crypto.randomUUID());
  const [profile, setProfile] = useState({ name: '', address: '', contactPhone: '', taxCode: '' });
  const client = useQueryClient(); const { setSelected } = useDepot();
  const save = useMutation({ mutationFn: () => api.post('/depot/owned', { operationId, profile: { ...profile, contactPhone: profile.contactPhone.trim() || null, taxCode: profile.taxCode.trim() || null } }),
    onSuccess: async response => { await client.invalidateQueries({ queryKey: ['depot'] }); setSelected(response.data.data.id); onClose(); } });
  return <Dialog title="Tạo kho vựa" onClose={onClose} busy={save.isPending}><form className="space-y-4" onSubmit={e => { e.preventDefault(); save.mutate(); }}>
    <p className="text-sm text-d-on-surface-variant">Sau khi tạo kho, tải hồ sơ xác minh trong Hồ sơ kho vựa. Admin duyệt xong thì kho được tạo giao dịch mới.</p>
    {[["name", "Tên kho", 255], ["address", "Địa chỉ", 1000], ["contactPhone", "Điện thoại", 20], ["taxCode", "Mã số thuế", 50]].map(([key, label, max]) =>
      <label key={key} className="block">{label}<input className={inputClass} required={['name', 'address'].includes(key)} maxLength={max} value={profile[key]}
        onChange={e => setProfile({ ...profile, [key]: e.target.value })} /></label>)}
    <MutationError mutation={save} /><div className="flex justify-end gap-3"><button type="button" className={buttonClass} onClick={onClose} disabled={save.isPending}>Hủy</button>
      <button className={buttonClass} disabled={save.isPending}>{save.isPending ? 'Đang tạo…' : 'Tạo kho'}</button></div>
  </form></Dialog>;
}
