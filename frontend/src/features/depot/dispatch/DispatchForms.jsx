import { useState } from 'react';
import { useDepotQuery } from '../depotApi';
import { Dialog, QueryState, MutationError, Pager, inputClass, buttonClass } from '../components/DepotUI';
import { useDispatchQuery, useDispatchMutation, initialLocalTime, vehicleLabels } from './dispatchApi';

export function AreaForm({ area, onClose }) {
  const [id] = useState(() => area?.id ?? crypto.randomUUID());
  const [form, setForm] = useState({ name: area?.name ?? '', latitude: area?.latitude ?? '', longitude: area?.longitude ?? '', radiusMeters: area?.radiusMeters ?? 3000, isActive: area?.isActive ?? true });
  const save = useDispatchMutation('put', `areas/${id}`, onClose);
  const field = (name, value) => setForm({ ...form, [name]: value });
  return <Dialog title={area ? 'Cập nhật khu vực thu gom' : 'Thêm khu vực thu gom'} onClose={onClose} busy={save.isPending}>
    <form className="space-y-4" onSubmit={e => { e.preventDefault(); save.mutate({ ...form, latitude: Number(form.latitude), longitude: Number(form.longitude), radiusMeters: Number(form.radiusMeters) }); }}>
      <fieldset disabled={save.isPending} className="space-y-4">
        <label className="block">Tên khu vực<input required maxLength={120} className={inputClass} value={form.name} onChange={e => field('name', e.target.value)} placeholder="Ví dụ: Ngũ Hành Sơn" /></label>
        <div className="grid sm:grid-cols-2 gap-4"><label>Vĩ độ tâm vùng<input required type="number" min="-90" max="90" step="any" className={inputClass} value={form.latitude} onChange={e => field('latitude', e.target.value)} /></label><label>Kinh độ tâm vùng<input required type="number" min="-180" max="180" step="any" className={inputClass} value={form.longitude} onChange={e => field('longitude', e.target.value)} /></label></div>
        <label className="block">Bán kính phục vụ (m)<input required type="number" min="100" max="20000" step="1" className={inputClass} value={form.radiusMeters} onChange={e => field('radiusMeters', e.target.value)} /></label>
        <label className="flex gap-3"><input type="checkbox" checked={form.isActive} onChange={e => field('isActive', e.target.checked)} />Cho phép dùng khu vực để phân công</label>
        <p className="text-sm text-d-on-surface-variant">Tâm vùng dùng để gợi ý phân công. Tắt vùng không thay đổi đơn đã check-in hoặc đang giữ hàng.</p>
      </fieldset><MutationError mutation={save} /><Footer busy={save.isPending} onClose={onClose} label="Lưu khu vực" />
    </form>
  </Dialog>;
}
export function ShiftForm({ onClose }) {
  const [areaPage, setAreaPage] = useState(1), [staffPage, setStaffPage] = useState(1);
  const areas = useDispatchQuery('areas', { page: areaPage });
  const staff = useDepotQuery('staff', { page: staffPage, status: 'ACTIVE' });
  const [form, setForm] = useState(() => ({ operationId: crypto.randomUUID(), areaId: '', employeeId: '', startsAt: initialLocalTime(0), endsAt: initialLocalTime(8), vehicle: 'motorcycle', capacityKg: '100', maxPendingOrders: '10' }));
  const save = useDispatchMutation('post', 'shifts', onClose), field = (key, value) => setForm({ ...form, [key]: value });
  return <Dialog title="Bố trí ca thu gom" onClose={onClose} busy={save.isPending} wide>
    <form className="space-y-4" onSubmit={e => { e.preventDefault(); save.mutate({ ...form, startsAt: new Date(`${form.startsAt}:00+07:00`).toISOString(), endsAt: new Date(`${form.endsAt}:00+07:00`).toISOString(), capacityKg: Number(form.capacityKg), maxPendingOrders: Number(form.maxPendingOrders) }); }}>
      <fieldset disabled={save.isPending} className="space-y-4">
        <QueryState query={areas}><label className="block">Khu vực thu gom<select required className={inputClass} value={form.areaId} onChange={e => field('areaId', e.target.value)}><option value="">Chọn khu vực đang hoạt động</option>{areas.data?.items.filter(a => a.isActive).map(a => <option key={a.id} value={a.id}>{a.name}</option>)}</select></label><Pager page={areaPage} setPage={value => { setAreaPage(value); field('areaId', ''); }} total={areas.data?.totalCount} /></QueryState>
        <QueryState query={staff}><label className="block">Nhân viên thu gom<select required className={inputClass} value={form.employeeId} onChange={e => field('employeeId', e.target.value)}><option value="">Chọn nhân viên kho đang hoạt động</option>{staff.data?.items.filter(s => s.role === 'DEPOT_EMPLOYEE').map(s => <option key={s.id} value={s.userId}>{s.fullName} — {s.phone}</option>)}</select></label><Pager page={staffPage} setPage={value => { setStaffPage(value); field('employeeId', ''); }} total={staff.data?.totalCount} /></QueryState>
        <div className="grid sm:grid-cols-2 gap-4"><label>Bắt đầu ca (giờ Việt Nam)<input required type="datetime-local" className={inputClass} value={form.startsAt} onChange={e => field('startsAt', e.target.value)} /></label><label>Kết thúc ca (giờ Việt Nam)<input required type="datetime-local" className={inputClass} value={form.endsAt} min={form.startsAt} onChange={e => field('endsAt', e.target.value)} /></label>
          <label>Phương tiện<select className={inputClass} value={form.vehicle} onChange={e => field('vehicle', e.target.value)}>{Object.entries(vehicleLabels).map(([key, label]) => <option key={key} value={key}>{label}</option>)}</select></label><label>Tải trọng khai báo (kg)<input required type="number" min="0.01" step="0.01" max="20000" className={inputClass} value={form.capacityKg} onChange={e => field('capacityKg', e.target.value)} /></label>
          <label>Giới hạn đơn đang phụ trách<input required type="number" min="1" max="50" step="1" className={inputClass} value={form.maxPendingOrders} onChange={e => field('maxPendingOrders', e.target.value)} /></label></div>
        <p className="text-sm text-d-on-surface-variant">Ca tối đa 24 giờ, không trùng ca khác của nhân viên. Tải trọng là thông tin khai báo; hệ thống hiện giới hạn theo số đơn, chưa biết tải kg thực tế trên xe.</p>
      </fieldset><MutationError mutation={save} /><Footer busy={save.isPending} onClose={onClose} label="Lưu ca thu gom" />
    </form>
  </Dialog>;
}
export function Footer({ busy, onClose, label }) {
  return <div className="flex flex-wrap justify-end gap-3 border-t border-d-border-subtle pt-4"><button type="button" disabled={busy} className="rounded-full border border-d-border-subtle min-h-11 px-5 py-2" onClick={onClose}>Hủy bỏ</button><button className={buttonClass + ' min-h-11'} disabled={busy}>{busy ? 'Đang lưu…' : label}</button></div>;
}
export function StopShiftForm({ shift, onClose }) {
  const [reason, setReason] = useState('');
  const save = useDispatchMutation('patch', `shifts/${shift.id}/stop`, onClose);
  return <Dialog title="Ngừng ca thu gom" onClose={onClose} busy={save.isPending}>
    <form className="space-y-4" onSubmit={e => { e.preventDefault(); save.mutate({ reason }); }}>
      <p>Ngừng ca của <strong>{shift.employeeName}</strong>. Đơn chưa check-in sẽ được hệ thống điều phối lại ở lượt kế tiếp (khoảng 15 giây). Đơn đang giữ hàng vẫn do nhân viên hiện tại xử lý.</p>
      <label className="block">Lý do ngừng ca<textarea required minLength={10} maxLength={1000} disabled={save.isPending} className={inputClass} value={reason} onChange={e => setReason(e.target.value)} /></label>
      <MutationError mutation={save} /><Footer busy={save.isPending} onClose={onClose} label="Xác nhận ngừng ca" />
    </form>
  </Dialog>;
}
