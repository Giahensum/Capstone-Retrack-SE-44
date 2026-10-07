import React, { useState } from 'react';
import { MaterialIcon } from './components/DepotIcon';
import CreateBatchModal from './components/CreateBatchModal';
import { materialLabel } from './materialLabels';
import { useDepotQuery, number, date } from './depotApi';
import { QueryState, Pager } from './components/DepotUI';

const Inventory = () => {
  const [activeTab, setActiveTab] = useState('current');
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [batchMaterial, setBatchMaterial] = useState('');

  const stock = useDepotQuery('inventory');
  const history = useDepotQuery('inventory/receipts', { page, search }, activeTab === 'history');

  const sum = (key) => stock.data?.reduce((n, i) => n + i[key], 0) ?? 0;

  return (
    <div className="flex flex-col p-4 md:p-6 w-full max-w-7xl mx-auto h-[calc(100vh-4rem)] gap-4 md:gap-6 overflow-hidden bg-d-surface">
      <CreateBatchModal isOpen={isCreateModalOpen} onClose={() => setIsCreateModalOpen(false)} initialMaterial={batchMaterial} />
      {/* Page Header */}
      <div className="flex justify-between items-end shrink-0">
        <div>
          <h2 className="font-d-headline-lg text-d-headline-lg text-d-on-surface mb-2">Quản lý Tồn Kho</h2>
          <p className="font-d-body-md text-d-on-surface-variant">Theo dõi hàng tồn kho và lịch sử nhập phế liệu.</p>
        </div>
        <button
          onClick={() => { setBatchMaterial(''); setIsCreateModalOpen(true); }}
          className="bg-d-primary-fixed text-d-on-primary-fixed font-d-label-md py-2.5 px-6 rounded-full hover:bg-d-primary-container transition-transform transform hover:-translate-y-0.5 duration-200 flex items-center gap-2 shadow-sm"
        >
          <MaterialIcon name="add" className="text-[18px]" /> Tạo lô xuất hàng
        </button>
      </div>

      {/* Tabs */}
      <div className="flex border-b border-d-border-subtle shrink-0">
        <button
          onClick={() => { setActiveTab('current'); setPage(1); }}
          className={`pb-3 px-6 font-d-label-md transition-colors ${activeTab === 'current' ? 'text-d-primary font-bold border-b-2 border-d-primary' : 'text-d-on-surface-variant hover:text-d-primary'}`}
        >
          Tồn kho hiện tại
        </button>
        <button
          onClick={() => { setActiveTab('history'); setPage(1); }}
          className={`pb-3 px-6 font-d-label-md transition-colors ${activeTab === 'history' ? 'text-d-primary font-bold border-b-2 border-d-primary' : 'text-d-on-surface-variant hover:text-d-primary'}`}
        >
          Lịch sử nhập kho
        </button>
      </div>

      {/* Scrollable Content */}
      <div className="flex-1 overflow-y-auto min-h-0 flex flex-col gap-6 pr-2">
        {activeTab === 'current' ? (
          <QueryState query={stock}>
            {/* KPI Cards Grid */}
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 md:gap-6 shrink-0">
              <div className="bg-d-surface-container-lowest border border-d-border-subtle p-6 rounded-2xl shadow-sm">
                <div className="flex justify-between items-start mb-4">
                  <div className="w-10 h-10 rounded-full bg-d-surface-accent text-d-secondary flex items-center justify-center">
                    <MaterialIcon name="scale" />
                  </div>
                </div>
                <p className="font-d-body-sm text-d-on-surface-variant mb-1">Tổng tồn kho khả dụng</p>
                <p className="font-d-headline-md text-d-headline-md text-d-on-surface">{number(sum('availableKg'))} <span className="font-d-body-sm text-d-on-surface-variant">kg</span></p>
              </div>

              <div className="bg-d-surface-container-lowest border border-d-border-subtle p-6 rounded-2xl shadow-sm">
                <div className="flex justify-between items-start mb-4">
                  <div className="w-10 h-10 rounded-full bg-d-surface-accent text-d-secondary flex items-center justify-center">
                    <MaterialIcon name="inventory" />
                  </div>
                </div>
                <p className="font-d-body-sm text-d-on-surface-variant mb-1">Đang giữ chờ xuất</p>
                <p className="font-d-headline-md text-d-headline-md text-d-on-surface">{number(sum('reservedKg'))} <span className="font-d-body-sm text-d-on-surface-variant">kg</span></p>
              </div>

              <div className="bg-d-surface-container-lowest border border-d-border-subtle p-6 rounded-2xl shadow-sm">
                <div className="flex justify-between items-start mb-4">
                  <div className="w-10 h-10 rounded-full bg-d-surface-accent text-d-secondary flex items-center justify-center">
                    <MaterialIcon name="inventory_2" />
                  </div>
                </div>
                <p className="font-d-body-sm text-d-on-surface-variant mb-1">Tổng tồn tại kho</p>
                <p className="font-d-headline-md text-d-headline-md text-d-on-surface">{number(sum('onHandKg'))} <span className="font-d-body-sm text-d-on-surface-variant">kg</span></p>
              </div>

              <div className="bg-d-surface-container-lowest border border-d-border-subtle p-6 rounded-2xl shadow-sm">
                <div className="flex justify-between items-start mb-4">
                  <div className="w-10 h-10 rounded-full bg-d-surface-accent text-d-secondary flex items-center justify-center">
                    <MaterialIcon name="category" />
                  </div>
                </div>
                <p className="font-d-body-sm text-d-on-surface-variant mb-1">Số loại vật liệu</p>
                <p className="font-d-headline-md text-d-headline-md text-d-on-surface">{stock.data?.length ?? 0}</p>
              </div>
            </div>

            {/* Inventory Table */}
            <div className="bg-d-surface-container-lowest border border-d-border-subtle rounded-xl overflow-hidden shadow-sm flex-shrink-0">
              <div className="overflow-x-auto">
                <table className="w-full text-left border-collapse">
                  <thead>
                    <tr className="bg-d-surface-container-low font-d-label-md text-d-on-surface-variant border-b border-d-border-subtle">
                      <th className="py-4 px-6 font-medium whitespace-nowrap">LOẠI VẬT LIỆU</th>
                      <th className="py-4 px-6 font-medium whitespace-nowrap">KHỐI LƯỢNG KHẢ DỤNG</th>
                      <th className="py-4 px-6 font-medium whitespace-nowrap">ĐANG GIỮ (HOLD)</th>
                      <th className="py-4 px-6 font-medium whitespace-nowrap">TỔNG TỒN</th>
                      <th className="py-4 px-6 font-medium whitespace-nowrap">HÀNH ĐỘNG</th>
                    </tr>
                  </thead>
                  <tbody className="font-d-body-sm text-d-on-surface divide-y divide-d-border-subtle">
                    {stock.data?.map((i) => (
                      <tr key={materialLabel(i.materialType)} className="hover:bg-d-surface-container-lowest/50 transition-colors">
                        <td className="py-4 px-6 font-medium">
                          <div className="flex items-center gap-3">
                            <div className="w-8 h-8 rounded-lg bg-d-surface-accent text-d-secondary flex items-center justify-center">
                              <MaterialIcon name="recycling" className="text-[18px]" />
                            </div>
                            <span className="whitespace-nowrap">{materialLabel(i.materialType)}</span>
                          </div>
                        </td>
                        <td className="py-4 px-6">
                          <span className="inline-flex items-center gap-1.5 text-d-secondary bg-d-surface-accent px-3 py-1 rounded-full font-d-label-sm border border-d-secondary/10 whitespace-nowrap">
                            <span className="w-1.5 h-1.5 rounded-full bg-d-secondary"></span> {number(i.availableKg)} kg
                          </span>
                        </td>
                        <td className="py-4 px-6 text-d-on-surface-variant whitespace-nowrap">
                          <div className="flex items-center gap-1">
                            <MaterialIcon name="lock" className="text-[16px]" /> {number(i.reservedKg)} kg
                          </div>
                        </td>
                        <td className="py-4 px-6 font-medium whitespace-nowrap">{number(i.onHandKg)} kg</td>
                        <td className="py-4 px-6">
                          <button onClick={() => { setBatchMaterial(i.materialType); setIsCreateModalOpen(true); }} className="text-d-secondary border border-d-secondary/20 px-4 py-1.5 rounded-lg hover:bg-d-secondary hover:text-white transition-colors font-d-label-sm whitespace-nowrap">
                            Tạo lô
                          </button>
                        </td>
                      </tr>
                    ))}
                    {!stock.data?.length && (
                      <tr><td colSpan="5" className="py-8 text-center text-d-on-surface-variant">Kho đang trống.</td></tr>
                    )}
                  </tbody>
                </table>
              </div>
            </div>

            {/* Footer Note */}
            <div className="flex items-start gap-2 text-d-on-surface-variant font-d-body-sm bg-d-surface-container-low p-4 rounded-lg shrink-0 mb-4">
              <MaterialIcon name="lightbulb" className="text-d-secondary" />
              <p>Tồn kho được cập nhật tự động dựa trên các giao dịch nhập và xuất mới nhất trong hệ thống.</p>
            </div>
          </QueryState>
        ) : (
          <div className="flex-1 flex flex-col min-h-0 bg-d-surface-container-lowest border border-d-border-subtle rounded-[20px] overflow-hidden shadow-sm">
            {/* Filter Bar */}
            <div className="p-6 border-b border-d-border-subtle shrink-0">
              <div className="flex flex-col lg:flex-row gap-4 items-center">
                <div className="relative flex-1 w-full">
                  <MaterialIcon name="search" className="absolute left-4 top-1/2 -translate-y-1/2 text-d-on-surface-variant" />
                  <input
                    className="w-full bg-white border border-d-border-subtle rounded-lg py-3 pl-12 pr-4 font-d-body-sm text-d-body-sm focus:outline-none focus:border-d-secondary focus:ring-1 focus:ring-d-secondary transition-colors"
                    placeholder="Tìm kiếm theo mã đơn, người bán..."
                    type="text"
                    value={search}
                    onChange={(e) => { setSearch(e.target.value); setPage(1); }}
                  />
                </div>
              </div>
            </div>

            <QueryState query={history}>
              {/* Data Table */}
              <div className="flex-1 overflow-y-auto">
                <table className="w-full text-left border-collapse min-w-[900px]">
                  <thead className="sticky top-0 bg-d-surface-container-lowest z-10">
                    <tr className="border-b border-d-border-subtle">
                      <th className="py-4 px-6 font-d-label-sm text-d-label-sm text-d-on-surface-variant uppercase tracking-wider">Mã đơn</th>
                      <th className="py-4 px-6 font-d-label-sm text-d-label-sm text-d-on-surface-variant uppercase tracking-wider">Ngày nhập</th>
                      <th className="py-4 px-6 font-d-label-sm text-d-label-sm text-d-on-surface-variant uppercase tracking-wider">Người bán</th>
                      <th className="py-4 px-6 font-d-label-sm text-d-label-sm text-d-on-surface-variant uppercase tracking-wider">Chi tiết vật liệu</th>
                      <th className="py-4 px-6 font-d-label-sm text-d-label-sm text-d-on-surface-variant uppercase tracking-wider text-right">Tổng khối lượng</th>
                      <th className="py-4 px-6 font-d-label-sm text-d-label-sm text-d-on-surface-variant uppercase tracking-wider">Trạng thái</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-d-border-subtle bg-white/50">
                    {history.data?.items.map((p) => (
                      <tr key={p.id} className="hover:bg-d-surface-container-lowest transition-colors">
                        <td className="py-4 px-6 font-d-label-md text-d-label-md text-d-on-surface font-bold">#{p.id.slice(0,8)}</td>
                        <td className="py-4 px-6 font-d-body-sm text-d-body-sm text-d-on-surface-variant">{date(p.createdAt)}</td>
                        <td className="py-4 px-6 font-d-body-sm text-d-body-sm text-d-on-surface flex items-center gap-2">
                          <div className="w-6 h-6 rounded-full bg-d-surface-variant flex items-center justify-center text-[10px] font-bold text-d-on-surface-variant">
                            {p.sellerName.charAt(0).toUpperCase()}
                          </div>
                          {p.sellerName}
                        </td>
                        <td className="py-4 px-6">
                          <div className="flex flex-wrap gap-1">
                            {p.items.map((i, idx) => (
                              <span key={idx} className="inline-flex items-center px-2.5 py-1 rounded-full bg-d-surface-container text-d-on-surface font-d-label-sm text-d-label-sm whitespace-nowrap">
                                {materialLabel(i.materialType)}: <span className="font-bold ml-1 text-d-primary">{number(i.weightKg)}kg</span>
                              </span>
                            ))}
                          </div>
                        </td>
                        <td className="py-4 px-6 font-d-label-md text-d-label-md text-d-on-surface text-right">{number(p.items.reduce((n,i) => n+i.weightKg, 0))} kg</td>
                        <td className="py-4 px-6">
                          <span className="inline-flex items-center px-2.5 py-1 rounded-full bg-d-surface-accent text-d-secondary font-d-label-sm text-d-label-sm gap-1 border border-d-secondary/20">
                            <span className="w-1.5 h-1.5 rounded-full bg-d-secondary"></span> Đã nhập
                          </span>
                        </td>
                      </tr>
                    ))}
                    {!history.data?.items.length && (
                      <tr><td colSpan="6" className="py-8 text-center text-d-on-surface-variant">Không tìm thấy lịch sử nhập kho.</td></tr>
                    )}
                  </tbody>
                </table>
              </div>
              <div className="bg-d-surface-container-lowest px-6 py-4 border-t border-d-border-subtle shrink-0">
                <Pager page={page} setPage={setPage} total={history.data?.totalCount} />
              </div>
            </QueryState>
          </div>
        )}
      </div>
    </div>
  );
};

export default Inventory;
