import { useState } from 'react';
import CreateBatchModal from './components/CreateBatchModal';
import { useDepotQuery, useDepotMutation, number, batchLabels } from './depotApi';
import { Page, QueryState, GridTable, Pager, Dialog, MutationError, inputClass, buttonClass, cellClass } from './components/DepotUI';

export default function Batches() {
  const [create, setCreate] = useState(false);
  const [cancel, setCancel] = useState(null);
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState('');
  const [search, setSearch] = useState('');
  const query = useDepotQuery('batches', { page, status, search });
  const remove = useDepotMutation('patch', (id) => `batches/${id}/cancel`, () => setCancel(null));
  return <Page title="Danh sách Lô Xuất Hàng" description="Quản lý và theo dõi trạng thái các lô vật liệu tái chế đang xuất kho."
    action={<button className={buttonClass} onClick={() => setCreate(true)}>＋ Tạo lô xuất hàng mới</button>}>
    <CreateBatchModal isOpen={create} onClose={() => setCreate(false)} />
    {cancel && <Dialog title="Xác nhận hủy lô hàng" onClose={() => setCancel(null)} busy={remove.isPending}><p>Hủy lô {cancel.code ?? cancel.id.slice(0, 8)} sẽ trả {number(cancel.weightKg)} kg về tồn khả dụng.</p><MutationError mutation={remove} />
      <button className={buttonClass + ' mt-4'} disabled={remove.isPending} onClick={() => remove.mutate({ id: cancel.id })}>Xác nhận hủy</button></Dialog>}
    <div className="flex flex-wrap gap-4"><label>Tìm mã lô / vật liệu<input className={inputClass} value={search} onChange={(e) => { setSearch(e.target.value); setPage(1); }} /></label>
      <label>Trạng thái<select className={inputClass} value={status} onChange={(e) => { setStatus(e.target.value); setPage(1); }}><option value="">Tất cả</option>{Object.entries(batchLabels).map(([s, label]) => <option key={s} value={s}>{label}</option>)}</select></label></div>
    <QueryState query={query}><GridTable headers={['Mã lô', 'Loại vật liệu', 'Khối lượng', 'Nhà máy', 'Trạng thái', 'Hành động']} empty={!query.data?.items.length}>
      {query.data?.items.map((b) => <tr key={b.id} className="hover:bg-d-surface-container-low"><td className={cellClass} title={b.id}>{b.code ?? `#${b.id.slice(0, 8)}`}</td><td className={cellClass}>{b.materialType}<p className="text-sm text-d-on-surface-variant break-words">{b.description}</p></td>
        <td className={cellClass}>{number(b.weightKg)} kg</td><td className={cellClass}>{b.factoryName ?? 'Đăng công khai'}</td><td className={cellClass}><span className="bg-d-surface-accent text-d-primary rounded-full px-3 py-1 text-sm">{batchLabels[b.status] ?? b.status}</span></td>
        <td className={cellClass}>{['DRAFT', 'LISTED', 'MARKETPLACE'].includes(b.status) && !b.targetFactoryId && <button className={buttonClass} onClick={() => { remove.reset(); setCancel(b); }}>Hủy lô</button>}</td></tr>)}
    </GridTable><Pager page={page} setPage={setPage} total={query.data?.totalCount} /></QueryState>
  </Page>;
}
