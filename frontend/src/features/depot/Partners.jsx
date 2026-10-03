import { useRef, useState } from 'react';
import { useDepotQuery, useDepotMutation, number, money, date } from './depotApi';
import { QueryState, Pager, Dialog, MutationError, inputClass } from './components/DepotUI';
import { MaterialIcon } from './components/DepotIcon';
import { materialLabel } from './materialLabels';
import CreateBatchModal from './components/CreateBatchModal';

const labels = { APPROVED: 'Đang hợp tác', PENDING: 'Chờ duyệt', BLOCKED: 'Đã chặn' };

export default function Partners() {
  const [activeTab, setActiveTab] = useState('list');
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('');
  const [createFor, setCreateFor] = useState(null);
  const [decision, setDecision] = useState(null);
  const searchRef = useRef(null);

  const query = useDepotQuery('partners', { page, search, status }, activeTab === 'list');
  const demands = useDepotQuery('partners/demands', { page, search }, activeTab === 'demand');
  const requests = useDepotQuery('partnerships', { page }, activeTab === 'requests');
  const update = useDepotMutation('put', (id) => `partnerships/${id}/status`, () => setDecision(null));

  return (
    <div className="flex flex-col p-4 md:p-6 w-full h-[calc(100vh-4rem)] gap-6 overflow-hidden bg-d-surface">
      <CreateBatchModal isOpen={!!createFor} onClose={() => setCreateFor(null)} initialMaterial={createFor?.materialType} initialFactoryId={createFor?.factoryId} />
      {decision && <Dialog title="Xác nhận quan hệ nhà máy" onClose={() => setDecision(null)} busy={update.isPending}>
        <p className="mb-4">{decision.status === 'APPROVED' ? 'Duyệt' : 'Chặn'} quan hệ với <strong>{decision.factoryName}</strong>?</p>
        <MutationError mutation={update} />
        <div className="flex justify-end gap-3"><button onClick={() => setDecision(null)} disabled={update.isPending}>Đóng</button>
          <button className="bg-d-primary text-white px-4 py-2 rounded-full" disabled={update.isPending} onClick={() => update.mutate({ id: decision.factoryId, body: { status: decision.status } })}>{update.isPending ? 'Đang lưu…' : 'Xác nhận'}</button></div>
      </Dialog>}

      {/* Page Header */}
      <div className="flex flex-col sm:flex-row sm:items-end justify-between gap-6 shrink-0">
        <div>
          <h2 className="font-d-headline-lg text-d-headline-lg text-d-on-surface mb-2">Đối tác nhà máy</h2>
          <p className="font-d-body-md text-d-body-md text-d-on-surface-variant max-w-2xl">Quản lý danh sách các nhà máy tái chế đã liên kết.</p>
        </div>
        <button onClick={() => { setActiveTab('list'); setPage(1); setSearch(''); requestAnimationFrame(() => searchRef.current?.focus()); }} className="bg-transparent border border-d-primary text-d-primary hover:bg-d-surface-variant hover:border-d-primary-fixed px-6 py-2.5 rounded-full font-d-label-md text-d-label-md font-medium transition-all flex items-center justify-center gap-2 self-start sm:self-auto shrink-0">
          <MaterialIcon name="search" className="text-[18px]" /> Tìm đối tác mới
        </button>
      </div>

      {/* Tabs */}
      <div className="flex border-b border-d-border-subtle overflow-x-auto no-scrollbar space-x-6 shrink-0">
        <button
          onClick={() => { setActiveTab('list'); setPage(1); setSearch(''); }}
          className={`pb-3 px-1 font-d-body-md text-d-body-md font-bold whitespace-nowrap transition-colors ${
            activeTab === 'list' ? 'text-d-primary border-b-2 border-d-primary' : 'text-d-on-surface-variant hover:text-d-primary'
          }`}
        >
          Danh sách nhà máy
        </button>
        <button
          onClick={() => { setActiveTab('demand'); setPage(1); setSearch(''); }}
          className={`pb-3 px-1 font-d-body-md text-d-body-md font-bold whitespace-nowrap transition-colors ${
            activeTab === 'demand' ? 'text-d-primary border-b-2 border-d-primary' : 'text-d-on-surface-variant hover:text-d-primary'
          }`}
        >
          Nhu cầu thu mua
        </button>
        <button onClick={() => { setActiveTab('requests'); setPage(1); setSearch(''); }} className={`pb-3 px-1 font-d-body-md text-d-body-md font-bold whitespace-nowrap transition-colors ${activeTab === 'requests' ? 'text-d-primary border-b-2 border-d-primary' : 'text-d-on-surface-variant hover:text-d-primary'}`}>Yêu cầu hợp tác</button>
      </div>

      {/* Scrollable Content */}
      <div className="flex-1 overflow-y-auto min-h-0 flex flex-col gap-6 pr-2">
        {activeTab === 'list' ? (
          <>
            {/* KPI Cards (Bento style grid) */}
            <div className="grid grid-cols-1 md:grid-cols-3 gap-6 shrink-0">
              {/* Total Partners */}
              <div className="bg-d-surface-container-low border border-d-border-subtle rounded-[20px] p-6 relative overflow-hidden group hover:shadow-md transition-shadow">
                <div className="absolute top-0 right-0 p-4 opacity-10 group-hover:opacity-20 transition-opacity">
                  <MaterialIcon name="domain" className="text-6xl text-d-primary" />
                </div>
                <p className="font-d-label-sm text-d-label-sm text-d-on-surface-variant uppercase tracking-wider mb-2">Tổng đối tác</p>
                <div className="flex items-end gap-3">
                  <h3 className="font-d-headline-xl text-d-headline-xl text-d-on-surface">{query.data?.totalCount ?? 0}</h3>
                </div>
              </div>

              {/* Active Partners */}
              <div className="bg-d-surface-accent border border-d-secondary-container rounded-[20px] p-6 relative overflow-hidden group hover:shadow-md transition-shadow">
                <div className="absolute top-0 right-0 p-4 opacity-10 group-hover:opacity-20 transition-opacity">
                  <MaterialIcon name="handshake" className="text-6xl text-d-secondary" />
                </div>
                <p className="font-d-label-sm text-d-label-sm text-d-secondary uppercase tracking-wider mb-2">Đang hợp tác</p>
                <div className="flex items-end gap-3">
                  <h3 className="font-d-headline-xl text-d-headline-xl text-d-secondary">{query.data?.items.filter((f) => f.partnershipStatus === 'APPROVED').length ?? 0}</h3>
                  <span className="font-d-body-sm text-d-body-sm text-d-secondary opacity-80 mb-2 font-medium">trong trang này</span>
                </div>
              </div>

              {/* Pending Approval */}
              <div className="bg-d-surface-container-low border border-d-border-subtle rounded-[20px] p-6 relative overflow-hidden group hover:shadow-md transition-shadow">
                <div className="absolute top-0 right-0 p-4 opacity-10 group-hover:opacity-20 transition-opacity">
                  <MaterialIcon name="hourglass_empty" className="text-6xl text-d-outline" />
                </div>
                <p className="font-d-label-sm text-d-label-sm text-d-on-surface-variant uppercase tracking-wider mb-2">Chờ nhà máy duyệt</p>
                <div className="flex items-end gap-3">
                  <h3 className="font-d-headline-xl text-d-headline-xl text-d-on-surface">{query.data?.items.filter((f) => f.partnershipStatus === 'PENDING').length ?? 0}</h3>
                  <span className="font-d-body-sm text-d-body-sm text-d-on-surface-variant opacity-80 mb-2 font-medium">trong trang này</span>
                </div>
              </div>
            </div>

            {/* Data Table Section */}
            <div className="bg-d-surface-container-lowest border border-d-border-subtle rounded-[20px] overflow-hidden flex flex-col flex-1 shadow-sm shrink-0 min-h-[500px]">
              {/* Table Toolbar */}
              <div className="p-6 border-b border-d-border-subtle flex flex-col sm:flex-row justify-between items-center gap-4 bg-d-surface-container-lowest shrink-0">
                <div className="relative w-full sm:w-80">
                  <MaterialIcon name="search" className="absolute left-3 top-1/2 -translate-y-1/2 text-d-outline text-[20px]" />
                  <input
                    ref={searchRef}
                    aria-label="Tìm tên nhà máy"
                    className="w-full bg-white border border-d-border-subtle rounded-full py-2 pl-10 pr-4 font-d-body-sm text-d-body-sm focus:border-d-primary focus:ring-1 focus:ring-d-primary transition-all text-d-on-surface outline-none"
                    placeholder="Tìm tên nhà máy..."
                    type="text"
                    value={search}
                    onChange={(e) => { setSearch(e.target.value); setPage(1); }}
                  />
                </div>
                <div className="flex gap-3 w-full sm:w-auto">
                  <select aria-label="Lọc quan hệ đối tác" value={status} onChange={(e) => { setStatus(e.target.value); setPage(1); }} className={inputClass}><option value="">Tất cả quan hệ</option>{Object.entries(labels).map(([key, label]) => <option key={key} value={key}>{label}</option>)}</select>
                </div>
              </div>

              {/* Table Container */}
              <QueryState query={query}>
                <div className="overflow-x-auto flex-1">
                  <table className="w-full text-left border-collapse min-w-[1000px]">
                    <thead className="sticky top-0 bg-d-surface-container-low z-10">
                      <tr className="border-b border-d-border-subtle shadow-sm">
                        <th className="py-4 px-6 font-d-label-sm text-d-label-sm text-d-on-surface-variant uppercase tracking-wider w-[35%]">Nhà máy</th>
                        <th className="py-4 px-6 font-d-label-sm text-d-label-sm text-d-on-surface-variant uppercase tracking-wider w-[25%]">Vật liệu thu mua</th>
                        <th className="py-4 px-6 font-d-label-sm text-d-label-sm text-d-on-surface-variant uppercase tracking-wider text-center w-[15%]">Đánh giá</th>
                        <th className="py-4 px-6 font-d-label-sm text-d-label-sm text-d-on-surface-variant uppercase tracking-wider w-[15%]">Trạng thái</th>
                        <th className="py-4 px-6 font-d-label-sm text-d-label-sm text-d-on-surface-variant uppercase tracking-wider text-center w-[10%]">Hành động</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-d-border-subtle font-d-body-sm text-d-body-sm text-d-on-surface bg-white/50">
                      {query.data?.items.map(f => (
                        <tr key={f.id} className="hover:bg-d-surface transition-colors group">
                          <td className="py-4 px-6">
                            <div className="flex items-center gap-3">
                              <div className="w-10 h-10 rounded-lg bg-d-surface-variant flex items-center justify-center shrink-0">
                                <MaterialIcon name="factory" className="text-d-on-surface-variant" />
                              </div>
                              <div>
                                <p className="font-d-label-md text-d-label-md text-d-on-surface font-medium mb-0.5">{f.name}</p>
                                <p className="font-d-label-sm text-d-label-sm text-d-on-surface-variant opacity-70">{f.address || 'Đang cập nhật địa chỉ'}</p>
                              </div>
                            </div>
                          </td>
                          <td className="py-4 px-6">
                            <div className="flex flex-wrap gap-1">
                              <span className="text-d-on-surface-variant italic text-sm">Xem bảng nhu cầu thu mua</span>
                            </div>
                          </td>
                          <td className="py-4 px-6">
                            <div className="flex items-center justify-center gap-1 text-d-secondary">
                              <MaterialIcon name="star" className="text-[16px]" />
                              <span className="font-medium">{f.rating > 0 ? number(f.rating) : 'Chưa có'}</span>
                            </div>
                          </td>
                          <td className="py-4 px-6">
                            {f.partnershipStatus === 'APPROVED' ? (
                              <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-d-surface-accent text-d-secondary font-d-label-sm text-d-label-sm">
                                <span className="w-1.5 h-1.5 rounded-full bg-d-secondary"></span> Đang hợp tác
                              </span>
                            ) : (
                              <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-d-surface-container-high text-d-on-surface-variant font-d-label-sm text-d-label-sm border border-d-border-subtle">
                                <MaterialIcon name="hourglass_empty" className="text-[14px]" /> {labels[f.partnershipStatus] ?? 'Chưa hợp tác'}
                              </span>
                            )}
                          </td>
                          <td className="py-4 px-6 text-center">
                            <button aria-label={`Tạo lô chỉ định cho ${f.name}`} onClick={() => setCreateFor({ factoryId: f.id })} className="text-d-primary underline">Tạo lô</button>
                          </td>
                        </tr>
                      ))}
                      {!query.data?.items.length && (
                        <tr><td colSpan="5" className="py-8 text-center text-d-on-surface-variant">Không tìm thấy đối tác.</td></tr>
                      )}
                    </tbody>
                  </table>
                </div>

                {/* Pagination */}
                <div className="p-4 border-t border-d-border-subtle bg-d-surface-container-lowest shrink-0">
                  <Pager page={page} setPage={setPage} total={query.data?.totalCount} />
                </div>
              </QueryState>
            </div>
          </>
        ) : activeTab === 'requests' ? (
          <QueryState query={requests}><div className="bg-white rounded-[20px] border border-d-border-subtle overflow-x-auto"><table className="w-full min-w-[650px] text-left"><thead><tr className="border-b border-d-border-subtle"><th className="p-4">Nhà máy</th><th className="p-4">Liên hệ</th><th className="p-4">Ngày gửi</th><th className="p-4">Trạng thái</th><th className="p-4">Thao tác</th></tr></thead><tbody>
            {requests.data?.items.map((p) => <tr key={p.id} className="border-b border-d-border-subtle"><td className="p-4">{p.factoryName}</td><td className="p-4">{p.contactPhone || 'Chưa cập nhật'}</td><td className="p-4">{date(p.createdAt)}</td><td className="p-4">{labels[p.status] ?? p.status}</td><td className="p-4">{p.status === 'PENDING' && <div className="flex gap-3"><button className="text-d-primary underline" onClick={() => setDecision({ factoryId: p.factoryId, factoryName: p.factoryName, status: 'APPROVED' })}>Duyệt</button><button className="text-d-error underline" onClick={() => setDecision({ factoryId: p.factoryId, factoryName: p.factoryName, status: 'BLOCKED' })}>Chặn</button></div>}</td></tr>)}
            {!requests.data?.items.length && <tr><td colSpan="5" className="p-6 text-center">Chưa có yêu cầu hợp tác.</td></tr>}
          </tbody></table><Pager page={page} setPage={setPage} total={requests.data?.totalCount} /></div></QueryState>
        ) : (
          <div className="flex flex-col gap-6">
            {/* Filter Bar */}
            <div className="flex flex-col md:flex-row gap-4 mb-2">
              <div className="flex-1 flex items-center bg-white rounded-full px-4 py-3 border border-d-border-subtle focus-within:border-d-secondary transition-colors shadow-sm">
                <MaterialIcon name="search" className="text-d-outline mr-3" />
                <input
                  className="bg-transparent border-none outline-none w-full font-d-body-md text-d-body-md text-d-on-surface placeholder-d-on-surface-variant/50 focus:ring-0"
                  placeholder="Tìm kiếm theo loại phế liệu..."
                  aria-label="Tìm nhu cầu theo loại phế liệu"
                  type="text"
                  value={search}
                  onChange={(e) => { setSearch(e.target.value); setPage(1); }}
                />
              </div>
            </div>

            {/* Demand Grid */}
            <QueryState query={demands}>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                {demands.data?.items.map(d => {
                  const urgent = new Date(d.deadline) < new Date(Date.now() + 7 * 86400000);
                  return (
                    <div key={d.id} className="bg-white border border-d-border-subtle rounded-[20px] p-6 hover:shadow-xl transition-shadow duration-300 flex flex-col group relative overflow-hidden">
                      {urgent ? (
                        <div className="absolute top-0 left-0 w-full h-1 bg-gradient-to-r from-d-error to-d-tertiary"></div>
                      ) : (
                        <div className="absolute top-0 left-0 w-full h-1 bg-d-surface-variant"></div>
                      )}

                      <div className="flex justify-between items-start mb-4">
                        <h3 className="font-d-headline-md text-d-headline-md text-d-on-surface">{d.factoryName}</h3>
                        {urgent ? (
                          <span className="bg-d-error-container/30 text-d-error font-d-label-sm text-d-label-sm px-3 py-1 rounded-full flex items-center">
                            🔥 Cần gấp
                          </span>
                        ) : (
                          <span className="bg-d-surface-container-high text-d-on-surface font-d-label-sm text-d-label-sm px-3 py-1 rounded-full">
                            Nhu cầu thường xuyên
                          </span>
                        )}
                      </div>

                      <p className="font-d-body-lg text-d-body-lg font-medium text-d-secondary mb-4 flex-grow">
                        Thu mua {number(d.requiredWeightKg)} kg {materialLabel(d.materialType)}
                      </p>

                      <div className={urgent ? "bg-d-surface-accent rounded-lg p-4 mb-6" : "bg-d-surface-container-low rounded-lg p-4 mb-6"}>
                        <div className="flex justify-between items-center mb-2">
                          <span className="font-d-body-sm text-d-body-sm text-d-on-surface-variant">Đơn giá đề xuất:</span>
                          <span className="font-d-label-md text-d-label-md font-bold text-d-on-surface">{d.minPricePerKg != null ? money(d.minPricePerKg) : 'Thỏa thuận'} — {d.maxPricePerKg != null ? money(d.maxPricePerKg) : 'Thỏa thuận'} / kg</span>
                        </div>
                        <div className="flex justify-between items-center">
                          <span className="font-d-body-sm text-d-body-sm text-d-on-surface-variant">Hạn chót:</span>
                          <span className={`font-d-label-md text-d-label-md font-medium ${urgent ? 'text-d-error' : 'text-d-on-surface-variant'}`}>{date(d.deadline)}</span>
                        </div>
                      </div>

                      <button onClick={() => setCreateFor({ materialType: d.materialType, factoryId: d.factoryId })} className={urgent ?
                        "w-full bg-d-on-surface text-d-surface py-3 rounded-full font-d-body-md text-d-body-md font-bold hover:bg-d-secondary transition-colors flex justify-center items-center gap-2 group-hover:bg-d-primary" :
                        "w-full bg-transparent border border-d-on-surface text-d-on-surface py-3 rounded-full font-d-body-md text-d-body-md font-bold hover:bg-d-surface-container transition-colors flex justify-center items-center gap-2 group-hover:bg-d-surface-variant"
                      }>
                        Tạo lô bán ngay
                        <MaterialIcon name="arrow_forward" className="text-[20px] group-hover:translate-x-1 transition-transform" />
                      </button>
                    </div>
                  );
                })}
                {!demands.data?.items.length && (
                  <div className="col-span-full py-8 text-center text-d-on-surface-variant bg-white rounded-[20px]">Không có nhu cầu nào đang mở.</div>
                )}
              </div>
              <div className="mt-4"><Pager page={page} setPage={setPage} total={demands.data?.totalCount} /></div>
            </QueryState>
          </div>
        )}
      </div>
    </div>
  );
}
