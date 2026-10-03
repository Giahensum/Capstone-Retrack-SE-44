import { useState } from 'react';
import CreateBatchModal from './components/CreateBatchModal';
import BatchDetailModal from './components/BatchDetailModal';
import { materialLabel } from './materialLabels';
import { useDepotQuery, useDepotMutation, number, batchLabels } from './depotApi';
import { QueryState, Pager, Dialog, MutationError, inputClass, buttonClass } from './components/DepotUI';
import { MaterialIcon } from './components/DepotIcon';

export default function Batches() {
  const [create, setCreate] = useState(false);
  const [detail, setDetail] = useState(null);
  const [cancel, setCancel] = useState(null);
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState('');
  const [search, setSearch] = useState('');

  const query = useDepotQuery('batches', { page, status, search });
  const dashboard = useDepotQuery('dashboard');
  const remove = useDepotMutation('patch', (id) => `batches/${id}/cancel`, () => setCancel(null));

  return (
    <div className="p-4 sm:p-8 max-w-7xl mx-auto space-y-6">
      {/* Page Header */}
      <div className="flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
        <div>
          <h2 className="text-3xl font-bold font-d-headline-lg text-d-on-surface mb-2">Danh sách lô xuất hàng</h2>
          <p className="text-d-on-surface-variant font-d-body-lg">Quản lý và theo dõi trạng thái các lô vật liệu tái chế đang xuất kho.</p>
        </div>
        <button
          onClick={() => setCreate(true)}
          className="bg-d-primary text-white font-bold px-6 py-3 rounded-full hover:bg-d-primary-hover transition-all shadow-lg hover:-translate-y-0.5 flex items-center gap-2"
        >
          <MaterialIcon name="add" className="text-[20px]" /> Tạo lô xuất hàng mới
        </button>
      </div>

      <CreateBatchModal isOpen={create} onClose={() => setCreate(false)} />
      {detail && <BatchDetailModal id={detail} onClose={() => setDetail(null)}/>}
      {cancel && (
        <Dialog title={["PENDING_APPROVAL", "PENDING_FACTORY"].includes(cancel.status) ? "Xác nhận rút đề nghị" : "Xác nhận hủy lô hàng"} onClose={() => setCancel(null)} busy={remove.isPending}>
          <div className="bg-red-50 text-red-700 p-4 rounded-xl mb-4">
            <p>Hủy lô <strong>{cancel.code ?? cancel.id.slice(0, 8)}</strong> sẽ trả <strong>{number(cancel.weightKg)} kg</strong> về tồn khả dụng.</p>
          </div>
          <MutationError mutation={remove} />
          <div className="flex justify-end gap-3 mt-2">
            <button className={`${buttonClass} bg-transparent border border-d-border text-d-on-surface`} disabled={remove.isPending} onClick={() => setCancel(null)}>Đóng</button>
            <button className={`${buttonClass} bg-d-error text-white`} disabled={remove.isPending} onClick={() => remove.mutate({ id: cancel.id })}>
              {["PENDING_APPROVAL", "PENDING_FACTORY"].includes(cancel.status) ? "Xác nhận rút" : "Xác nhận hủy"}
            </button>
          </div>
        </Dialog>
      )}

      {/* KPI Bento Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <div className="bg-white border border-d-border-subtle p-5 rounded-2xl flex flex-col justify-between shadow-sm hover:shadow-md transition-shadow group">
          <div className="flex justify-between items-start mb-4">
            <div className="w-10 h-10 rounded-xl bg-blue-50 text-blue-600 flex items-center justify-center">
              <MaterialIcon name="publish" className="text-[20px]" />
            </div>
          </div>
          <div>
            <p className="text-sm font-medium text-d-on-surface-variant mb-1">Tổng Lô Đang Xử Lý</p>
            <p className="text-3xl font-bold text-d-on-surface group-hover:text-blue-600 transition-colors">{dashboard.data?.activeBatches ?? 0}</p>
          </div>
        </div>

        <div className="bg-white border border-d-border-subtle p-5 rounded-2xl flex flex-col justify-between shadow-sm hover:shadow-md transition-shadow group">
          <div className="flex justify-between items-start mb-4">
            <div className="w-10 h-10 rounded-xl bg-purple-50 text-purple-600 flex items-center justify-center">
              <MaterialIcon name="local_shipping" className="text-[20px]" />
            </div>
          </div>
          <div>
            <p className="text-sm font-medium text-d-on-surface-variant mb-1">Cảnh Báo Chất Lượng</p>
            <p className="text-3xl font-bold text-d-on-surface group-hover:text-purple-600 transition-colors">{dashboard.data?.rejectedQualityBatches ?? 0}</p>
          </div>
        </div>

        <div className="bg-white border border-d-border-subtle p-5 rounded-2xl flex flex-col justify-between shadow-sm hover:shadow-md transition-shadow group">
          <div className="flex justify-between items-start mb-4">
            <div className="w-10 h-10 rounded-xl bg-green-50 text-green-600 flex items-center justify-center">
              <MaterialIcon name="task_alt" className="text-[20px]" />
            </div>
          </div>
          <div>
            <p className="text-sm font-medium text-d-on-surface-variant mb-1">Đơn Nhập Kho Mới</p>
            <p className="text-3xl font-bold text-d-on-surface group-hover:text-green-600 transition-colors">{dashboard.data?.newRequestsToday ?? 0}</p>
          </div>
        </div>

        <div className="bg-gradient-to-br from-d-primary to-d-secondary p-5 rounded-2xl flex flex-col justify-between shadow-sm hover:shadow-md transition-shadow text-white group">
          <div className="flex justify-between items-start mb-4">
            <div className="w-10 h-10 rounded-xl bg-white/20 flex items-center justify-center text-white">
              <MaterialIcon name="auto_graph" className="text-[20px]" />
            </div>
          </div>
          <div>
            <p className="text-sm font-medium text-white/80 mb-1">Khả Dụng Tồn Kho</p>
            <p className="text-2xl font-bold truncate">{number(dashboard.data?.availableKg ?? 0)} kg</p>
          </div>
        </div>
      </div>

      {/* Main List */}
      <div className="bg-white border border-d-border-subtle rounded-2xl overflow-hidden shadow-sm">
        <div className="p-4 border-b border-d-border-subtle bg-d-surface-accent/30 flex flex-col sm:flex-row gap-4 items-center justify-between">
          <div className="relative flex-1 w-full max-w-sm">
            <MaterialIcon name="search" className="absolute left-3 top-1/2 -translate-y-1/2 text-d-on-surface-variant" />
            <input
              aria-label="Tìm mã lô hoặc vật liệu"
              className={`${inputClass} pl-10 w-full bg-white`}
              placeholder="Tìm mã lô hoặc vật liệu..."
              value={search}
              onChange={(e) => { setSearch(e.target.value); setPage(1); }}
            />
          </div>
          <div className="flex items-center gap-3 w-full sm:w-auto">
            <MaterialIcon name="filter_list" className="text-d-on-surface-variant" />
            <select aria-label="Lọc trạng thái lô xuất" className={`${inputClass} bg-white min-w-[200px]`} value={status} onChange={(e) => { setStatus(e.target.value); setPage(1); }}>
              <option value="">Tất cả trạng thái</option>
              {Object.entries(batchLabels).map(([s, label]) => <option key={s} value={s}>{label}</option>)}
            </select>
          </div>
        </div>

        <QueryState query={query}>
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse min-w-[1000px]">
              <thead>
                <tr className="bg-d-surface-container-low border-b border-d-border-subtle">
                  <th className="p-4 font-medium text-d-on-surface-variant">Mã lô</th>
                  <th className="p-4 font-medium text-d-on-surface-variant">Vật liệu</th>
                  <th className="p-4 font-medium text-d-on-surface-variant text-right">Khối lượng</th>
                  <th className="p-4 font-medium text-d-on-surface-variant">Nhà máy / Đích đến</th>
                  <th className="p-4 font-medium text-d-on-surface-variant text-center">Trạng thái</th>
                  <th className="p-4 font-medium text-d-on-surface-variant text-right">Hành động</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-d-border-subtle">
                {query.data?.items.map((b) => {
                  const labelStatus = batchLabels[b.status] ?? b.status;
                  const isPending = ['PENDING_APPROVAL', 'PENDING_FACTORY'].includes(b.status);
                  const isCancelable = isPending || (['DRAFT', 'LISTED', 'MARKETPLACE'].includes(b.status) && !b.targetFactoryId);

                  return (
                    <tr key={b.id} className="hover:bg-d-surface transition-colors">
                      <td className="p-4 font-bold text-d-on-surface" title={b.id}>{b.code ?? `#${b.id.slice(0, 8)}`}</td>
                      <td className="p-4">
                        <div className="font-medium text-d-on-surface flex items-center gap-2">
                          <div className="w-2 h-2 rounded-full bg-d-primary"></div>
                          {materialLabel(b.materialType)}
                        </div>
                        {b.description && <p className="text-xs text-d-on-surface-variant mt-1 max-w-[200px] truncate">{b.description}</p>}
                      </td>
                      <td className="p-4 text-right font-medium text-d-on-surface">{number(b.weightKg)} kg</td>
                      <td className="p-4">
                        {b.factoryName ? (
                          <div className="flex items-center gap-2 font-medium text-d-on-surface">
                            <MaterialIcon name="factory" className="text-d-on-surface-variant text-[16px]" />
                            {b.factoryName}
                          </div>
                        ) : (
                          <span className="text-d-primary font-medium flex items-center gap-1"><MaterialIcon name="public" className="text-[16px]" /> Đăng công khai</span>
                        )}
                      </td>
                      <td className="p-4 text-center">
                        <span className={`inline-flex items-center px-3 py-1 rounded-full text-xs font-bold ${
                          ['COMPLETED', 'DELIVERED'].includes(b.status) ? 'bg-green-100 text-green-700' :
                          ['REJECTED', 'CANCELLED'].includes(b.status) ? 'bg-red-100 text-red-700' :
                          'bg-blue-50 text-blue-700'
                        }`}>
                          {labelStatus}
                        </span>
                      </td>
                      <td className="p-4 text-right">
                        <div className="flex justify-end gap-2">
                          <button className="text-d-primary hover:bg-blue-50 h-11 w-11 inline-flex items-center justify-center rounded-full transition-colors" onClick={() => setDetail(b.id)} title="Chi tiết lô hàng" aria-label={`Xem chi tiết lô ${b.code ?? b.id.slice(0, 8)}`}>
                            <MaterialIcon name="visibility" className="text-[20px]" />
                          </button>
                          {isCancelable && (
                            <button className="text-red-500 hover:bg-red-50 h-11 w-11 inline-flex items-center justify-center rounded-full transition-colors" onClick={() => { remove.reset(); setCancel(b); }} title={isPending ? 'Rút đề nghị' : 'Hủy lô'} aria-label={`${isPending ? 'Rút đề nghị' : 'Hủy'} lô ${b.code ?? b.id.slice(0, 8)}`}>
                              <MaterialIcon name={isPending ? 'undo' : 'cancel'} className="text-[20px]" />
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  );
                })}
                {!query.data?.items.length && (
                  <tr><td colSpan="6" className="p-8 text-center text-d-on-surface-variant">Không tìm thấy lô hàng nào phù hợp.</td></tr>
                )}
              </tbody>
            </table>
          </div>
          <div className="p-4 border-t border-d-border-subtle bg-gray-50 flex justify-center">
            <Pager page={page} setPage={setPage} total={query.data?.totalCount} />
          </div>
        </QueryState>
      </div>
    </div>
  );
}
