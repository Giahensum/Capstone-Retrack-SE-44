import { useState } from 'react';
import { MaterialIcon } from './components/DepotIcon';
import CreateBatchModal from './components/CreateBatchModal';
import { materialLabel } from './materialLabels';
import { useDepotQuery, number } from './depotApi';
import { QueryState } from './components/DepotUI';
import StockHistory from './StockHistory';
import ReusableInventory from './ReusableInventory';
import { useDepot } from './DepotContext';
import { useSearchParams } from 'react-router-dom';
import { PageHeader, StatCard, buttonClass as kitButton } from '@/components/ui/kit';

const Inventory = () => {
  const { depotId } = useDepot();
  const [searchParams, setSearchParams] = useSearchParams();
  const activeTab = ['receipts', 'reusable', 'history'].includes(searchParams.get('tab')) ? searchParams.get('tab') : 'current';
  const setActiveTab = tab => setSearchParams(previous => { previous.set('tab', tab); previous.delete('receiptId'); previous.delete('newReceipt'); return previous; });
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);

  const [batchMaterial, setBatchMaterial] = useState('');

  const stock = useDepotQuery('inventory');


  const sum = (key) => stock.data?.reduce((n, i) => n + i[key], 0) ?? 0;

  return (
    <div className="flex flex-col p-4 md:p-6 w-full max-w-7xl mx-auto min-h-0 gap-4 md:gap-6 bg-d-surface">
      <CreateBatchModal isOpen={isCreateModalOpen} onClose={() => setIsCreateModalOpen(false)} initialMaterial={batchMaterial} />
      <PageHeader title="Quản lý tồn kho" description="Theo dõi lượng hàng đã ghi nhận trên RETRACK, nguồn nhập và lượng còn có thể xuất.">
        <button type="button" className={kitButton({ variant: 'secondary' })} onClick={() => setSearchParams({ tab: 'history', newReceipt: '1' })}>Nhập hàng</button>
        <button type="button" className={kitButton()} onClick={() => { setBatchMaterial(''); setIsCreateModalOpen(true); }}><MaterialIcon name="add" className="text-[18px]" />Tạo lô xuất hàng</button>
      </PageHeader>

      {/* Tabs */}
      <div className="flex overflow-x-auto border-b border-d-border-subtle shrink-0 [&>button]:whitespace-nowrap [&>button]:min-h-11">
        <button
          onClick={() => { setActiveTab('current'); }}
          className={`pb-3 px-6 font-d-label-md transition-colors ${activeTab === 'current' ? 'text-d-primary font-bold border-b-2 border-d-primary' : 'text-d-on-surface-variant hover:text-d-primary'}`}
        >
          Tồn kho hiện tại
        </button>
        <button
          onClick={() => { setActiveTab('history'); }}
          className={`pb-3 px-6 font-d-label-md transition-colors ${['history', 'receipts'].includes(activeTab) ? 'text-d-primary font-bold border-b-2 border-d-primary' : 'text-d-on-surface-variant hover:text-d-primary'}`}
        >
          Lịch sử nhập hàng
        </button>
        {[['reusable', 'Đồ tái sử dụng']].map(([key, label]) => <button type="button" key={key} onClick={() => { setActiveTab(key); }} className={`pb-3 px-6 font-d-label-md transition-colors ${activeTab === key ? 'text-d-primary font-bold border-b-2 border-d-primary' : 'text-d-on-surface-variant hover:text-d-primary'}`}>{label}</button>)}
      </div>

      {/* Scrollable Content */}
      <div className="min-h-0 flex flex-col gap-6">
        {activeTab === 'receipts' || activeTab === 'history' ? <StockHistory key={`${depotId}-${activeTab}`} initialSource={activeTab === 'receipts' ? 'RECEIPT' : ''} /> : activeTab === 'reusable' ? <ReusableInventory key={depotId} /> : activeTab === 'current' ? (
          <QueryState query={stock}>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
              <StatCard label="Tổng tồn kho khả dụng" value={`${number(sum('availableKg'))} kg`} icon={<MaterialIcon name="scale" className="text-xl" />} />
              <StatCard label="Đang giữ chờ xuất" value={`${number(sum('reservedKg'))} kg`} icon={<MaterialIcon name="inventory" className="text-xl" />} />
              <StatCard label="Tổng tồn đã ghi nhận" value={`${number(sum('onHandKg'))} kg`} icon={<MaterialIcon name="inventory_2" className="text-xl" />} />
              <StatCard label="Số loại vật liệu" value={stock.data?.length ?? 0} icon={<MaterialIcon name="category" className="text-xl" />} />
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
              <p>Số liệu theo các nguồn đã ghi nhận trên RETRACK, không thay kiểm kê vật lý. Đơn thu gom chờ hoàn tất và phiếu nháp chưa cộng tồn.</p>
            </div>
          </QueryState>
        ) : null}
      </div>
    </div>
  );
};

export default Inventory;
