import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Page, Cards, QueryState, GridTable, Pager, inputClass, buttonClass, cellClass } from '../components/DepotUI';
import { useDispatchQuery, dispatchTime, availability, vehicleLabels } from './dispatchApi';
import { AreaForm, ShiftForm, StopShiftForm } from './DispatchForms';
import DispatchDetail from './DispatchDetail';
export default function DispatchPage() {
  const [params, setParams] = useSearchParams();
  const tab = ['areas', 'shifts'].includes(params.get('tab')) ? params.get('tab') : 'pickups';
  const [page, setPage] = useState(1), [search, setSearch] = useState(''), [pickupStatus, setPickupStatus] = useState('PENDING'), [shiftStatus, setShiftStatus] = useState('CURRENT');
  const [area, setArea] = useState(undefined), [shift, setShift] = useState(false);
  const [stopping, setStopping] = useState(null);
  const query = useDispatchQuery(tab, { page, search, status: tab === 'pickups' ? pickupStatus : tab === 'shifts' ? shiftStatus : undefined });
  const summary = useDispatchQuery('summary');
  const open = id => setParams(previous => { previous.set('pickupId', id); return previous; });
  const close = () => setParams(previous => { previous.delete('pickupId'); return previous; }, { replace: true });
  const switchTab = key => { setPage(1); setSearch(''); setParams({ tab: key }); };
  return <Page title="Điều phối thu gom" description="Bố trí khu vực, ca và xử lý đơn chưa có nhân viên phù hợp."
    action={<div className="flex flex-wrap gap-3"><button type="button" className="min-h-11 rounded-full border border-d-border-subtle bg-white px-5 py-2.5 font-medium" onClick={() => setArea(null)}>＋ Thêm khu vực</button><button type="button" className={buttonClass} onClick={() => setShift(true)}>＋ Bố trí ca</button></div>}>
    <QueryState query={summary}><Cards items={[{ label: 'Khu vực đang hoạt động', value: summary.data?.areaCount, icon: 'warehouse' }, { label: 'Ca hiện tại chưa ngừng', value: summary.data?.activeShiftCount, icon: 'badge' }, { label: 'Đơn chờ phân công', value: summary.data?.waitingCount, icon: 'schedule' }]} /></QueryState>
    <p className="rounded-xl bg-white border border-d-border-subtle p-4 text-sm text-d-on-surface-variant">Tự điều phối theo khu vực, ca và số đơn; cập nhật mỗi 15 giây. Tải kg khai báo chưa phải tải thực tế, vị trí hiển thị là tâm khu vực. Chủ kho có thể phân công thủ công để xử lý ngoại lệ.</p>
    <div role="tablist" aria-label="Nội dung điều phối" className="flex gap-2 overflow-x-auto border-b border-d-border-subtle">{[['pickups', 'Đơn thu gom'], ['shifts', 'Ca thu gom'], ['areas', 'Khu vực']].map(([key, label]) => <button key={key} role="tab" aria-selected={tab === key} className={`px-5 py-3 whitespace-nowrap rounded-t-xl ${tab === key ? 'text-d-primary font-semibold border-b-2 border-d-primary bg-d-surface-accent' : 'text-d-on-surface-variant'}`} onClick={() => switchTab(key)}>{label}</button>)}</div>
    <div className="grid sm:grid-cols-2 gap-4 rounded-2xl border border-d-border-subtle bg-white p-4"><label>Tìm {tab === 'areas' ? 'khu vực' : tab === 'shifts' ? 'nhân viên / khu vực' : 'người bán / địa chỉ'}<input type="search" maxLength={255} className={inputClass} value={search} onChange={e => { setSearch(e.target.value); setPage(1); }} /></label>
      {tab === 'pickups' && <label>Trạng thái đơn<select className={inputClass} value={pickupStatus} onChange={e => { setPickupStatus(e.target.value); setPage(1); }}><option value="PENDING">Chưa phân công</option><option value="SCHEDULED">Đã phân công, chưa thu gom</option></select></label>}
      {tab === 'shifts' && <label>Thời gian ca<select className={inputClass} value={shiftStatus} onChange={e => { setShiftStatus(e.target.value); setPage(1); }}><option value="CURRENT">Đang trong giờ ca</option><option value="UPCOMING">Sắp tới</option><option value="ENDED">Đã hết giờ</option><option value="">Tất cả</option></select></label>}
    </div>
    <QueryState query={query}>
      {tab === 'areas' && <GridTable headers={['Khu vực', 'Tâm vùng', 'Bán kính', 'Trạng thái', 'Thao tác']} empty={!query.data?.items.length}>{query.data?.items.map(a => <tr key={a.id}><td className={cellClass}>{a.name}</td><td className={cellClass}>{a.latitude}, {a.longitude}</td><td className={cellClass}>{a.radiusMeters.toLocaleString('vi-VN')} m</td><td className={cellClass}>{a.isActive ? 'Đang hoạt động' : 'Đã tắt'}</td><td className={cellClass}><button type="button" className={buttonClass} onClick={() => setArea(a)}>Chỉnh sửa vùng</button></td></tr>)}</GridTable>}
      {tab === 'shifts' && <GridTable headers={['Nhân viên / khu vực', 'Thời gian Việt Nam', 'Trạng thái', 'Phương tiện / tải khai báo', 'Số đơn đang phụ trách', 'Thao tác']} empty={!query.data?.items.length}>{query.data?.items.map(s => <tr key={s.id}><td className={cellClass}><strong>{s.employeeName}</strong><p className="text-sm mt-1">{s.areaName}</p></td><td className={cellClass}>{dispatchTime(s.startsAt)}<br />→ {dispatchTime(s.endsAt)}</td><td className={cellClass}>{availability[s.availability] ?? s.availability}{s.reason && <p className="text-sm mt-1">{s.reason}</p>}</td><td className={cellClass}>{vehicleLabels[s.vehicle]} · {s.capacityKg} kg</td><td className={cellClass}>{s.assignedCount}/{s.maxPendingOrders}</td><td className={cellClass}>{s.availability !== 'STOPPED' && new Date(s.endsAt) > new Date() && <button type="button" className="min-h-11 rounded-full border border-d-border-subtle px-4 py-2 whitespace-nowrap" onClick={() => setStopping(s)}>Ngừng ca</button>}</td></tr>)}</GridTable>}
      {tab === 'pickups' && <div className="space-y-3">{query.data?.items.map(p => <article key={p.id} className="rounded-2xl border border-d-border-subtle bg-white p-4 flex flex-wrap justify-between items-center gap-4"><div className="min-w-0 space-y-2"><h2 className="font-semibold">{p.sellerName}</h2><p className="[overflow-wrap:anywhere]">{p.address}</p><p className="text-sm text-d-on-surface-variant">{dispatchTime(p.preferredDatetime)} · {p.employeeName || 'Chưa có nhân viên'}</p><p className="text-sm">{['ASSIGNED','ASSIGNED_MANUAL'].includes(p.queueReason) ? 'Đã phân công' : p.queueReason || 'Đang chờ điều phối'}{p.hasCheckIn && ' · Đã check-in, không chuyển người'}</p></div><button type="button" className={buttonClass} onClick={() => open(p.id)}>Chi tiết / phân công</button></article>)}{query.data?.totalCount === 0 && <p role="status" className="rounded-2xl bg-white border border-d-border-subtle text-center p-8">Chưa có đơn phù hợp.</p>}</div>}
      <Pager page={page} setPage={setPage} total={query.data?.totalCount} />
    </QueryState>
    {area !== undefined && <AreaForm area={area} onClose={() => setArea(undefined)} />}{shift && <ShiftForm onClose={() => setShift(false)} />}
    {stopping && <StopShiftForm shift={stopping} onClose={() => setStopping(null)} />}
    {params.get('pickupId') && <DispatchDetail key={params.get('pickupId')} id={params.get('pickupId')} onClose={close} />}
  </Page>;
}
