import React, { useState } from 'react';
import { MaterialIcon } from '../../components/ui/MaterialIcon';
import CreateBatchModal from './components/CreateBatchModal';

const Inventory = () => {
  const [activeTab, setActiveTab] = useState('current');
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);

  return (
    <div className="flex flex-col p-4 md:p-6 w-full h-[calc(100vh-4rem)] gap-4 md:gap-6 overflow-hidden bg-d-surface">
      <CreateBatchModal isOpen={isCreateModalOpen} onClose={() => setIsCreateModalOpen(false)} />
      {/* Page Header */}
      <div className="flex justify-between items-end shrink-0">
        <div>
          <h2 className="font-d-headline-lg text-d-headline-lg text-d-on-surface mb-2">Quản lý Tồn Kho</h2>
          <p className="font-d-body-md text-d-on-surface-variant">Theo dõi hàng tồn kho và lịch sử nhập phế liệu.</p>
        </div>
        <button 
          onClick={() => setIsCreateModalOpen(true)}
          className="bg-d-primary-fixed text-d-on-primary-fixed font-d-label-md py-2.5 px-6 rounded-full hover:bg-d-primary-container transition-transform transform hover:-translate-y-0.5 duration-200 flex items-center gap-2 shadow-sm"
        >
          <MaterialIcon name="add" className="text-[18px]" /> Tạo lô xuất hàng
        </button>
      </div>

      {/* Tabs */}
      <div className="flex border-b border-d-border-subtle shrink-0">
        <button 
          onClick={() => setActiveTab('current')}
          className={`pb-3 px-6 font-d-label-md transition-colors ${activeTab === 'current' ? 'text-d-primary font-bold border-b-2 border-d-primary' : 'text-d-on-surface-variant hover:text-d-primary'}`}
        >
          Tồn kho hiện tại
        </button>
        <button 
          onClick={() => setActiveTab('history')}
          className={`pb-3 px-6 font-d-label-md transition-colors ${activeTab === 'history' ? 'text-d-primary font-bold border-b-2 border-d-primary' : 'text-d-on-surface-variant hover:text-d-primary'}`}
        >
          Lịch sử nhập kho
        </button>
      </div>

      {/* Scrollable Content */}
      <div className="flex-1 overflow-y-auto min-h-0 flex flex-col gap-6 pr-2">
        
        {activeTab === 'current' ? (
          <>
            {/* KPI Cards Grid */}
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 md:gap-6 shrink-0">
              <div className="bg-d-surface-container-lowest border border-d-border-subtle p-6 rounded-2xl shadow-sm">
                <div className="flex justify-between items-start mb-4">
                  <div className="w-10 h-10 rounded-full bg-d-surface-accent text-d-secondary flex items-center justify-center">
                    <MaterialIcon name="scale" />
                  </div>
                </div>
                <p className="font-d-body-sm text-d-on-surface-variant mb-1">Tổng tồn kho khả dụng</p>
                <p className="font-d-headline-md text-d-headline-md text-d-on-surface">1.245,5 <span className="font-d-body-sm text-d-on-surface-variant">Tấn</span></p>
              </div>
              
              <div className="bg-d-surface-container-lowest border border-d-border-subtle p-6 rounded-2xl shadow-sm">
                <div className="flex justify-between items-start mb-4">
                  <div className="w-10 h-10 rounded-full bg-d-surface-accent text-d-secondary flex items-center justify-center">
                    <MaterialIcon name="payments" />
                  </div>
                </div>
                <p className="font-d-body-sm text-d-on-surface-variant mb-1">Giá trị tồn kho ước tính</p>
                <p className="font-d-headline-md text-d-headline-md text-d-on-surface">3.8 <span className="font-d-body-sm text-d-on-surface-variant">Tỷ đ</span></p>
              </div>
              
              <div className="bg-d-surface-container-lowest border border-d-border-subtle p-6 rounded-2xl shadow-sm">
                <div className="flex justify-between items-start mb-4">
                  <div className="w-10 h-10 rounded-full bg-d-surface-accent text-d-secondary flex items-center justify-center">
                    <MaterialIcon name="category" />
                  </div>
                </div>
                <p className="font-d-body-sm text-d-on-surface-variant mb-1">Loại phế liệu đang có</p>
                <p className="font-d-headline-md text-d-headline-md text-d-on-surface">6</p>
              </div>
              
              <div className="bg-d-error-container/10 border border-d-error/30 p-6 rounded-2xl shadow-sm">
                <div className="flex justify-between items-start mb-4">
                  <div className="w-10 h-10 rounded-full bg-d-error-container text-d-error flex items-center justify-center">
                    <MaterialIcon name="warning" />
                  </div>
                </div>
                <p className="font-d-body-sm text-d-on-surface-variant mb-1">Loại sắp hết</p>
                <p className="font-d-headline-md text-d-headline-md text-d-error">1</p>
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
                    
                    {/* Row 1 */}
                    <tr className="hover:bg-d-surface-container-lowest/50 transition-colors">
                      <td className="py-4 px-6 font-medium">
                        <div className="flex items-center gap-3">
                          <div className="w-8 h-8 rounded-lg bg-d-surface-accent text-d-secondary flex items-center justify-center">
                            <MaterialIcon name="recycling" className="text-[18px]" />
                          </div>
                          <span className="whitespace-nowrap">Nhựa PET</span>
                        </div>
                      </td>
                      <td className="py-4 px-6">
                        <span className="inline-flex items-center gap-1.5 text-d-secondary bg-d-surface-accent px-3 py-1 rounded-full font-d-label-sm border border-d-secondary/10 whitespace-nowrap">
                          <span className="w-1.5 h-1.5 rounded-full bg-d-secondary"></span> 450 kg
                        </span>
                      </td>
                      <td className="py-4 px-6 text-d-on-surface-variant whitespace-nowrap">
                        <div className="flex items-center gap-1">
                          <MaterialIcon name="lock" className="text-[16px]" /> 50 kg
                        </div>
                      </td>
                      <td className="py-4 px-6 font-medium whitespace-nowrap">500 kg</td>
                      <td className="py-4 px-6">
                        <button className="text-d-secondary border border-d-secondary/20 px-4 py-1.5 rounded-lg hover:bg-d-secondary hover:text-white transition-colors font-d-label-sm whitespace-nowrap">
                          Tạo lô
                        </button>
                      </td>
                    </tr>

                    {/* Row 2: Orange Warning */}
                    <tr className="hover:bg-d-surface-container-lowest/50 transition-colors">
                      <td className="py-4 px-6 font-medium">
                        <div className="flex items-center gap-3">
                          <div className="w-8 h-8 rounded-lg bg-d-surface-accent text-d-secondary flex items-center justify-center">
                            <MaterialIcon name="cable" className="text-[18px]" />
                          </div>
                          <span className="whitespace-nowrap">Đồng</span>
                        </div>
                      </td>
                      <td className="py-4 px-6">
                        <span className="inline-flex items-center gap-1.5 text-[#b45309] bg-[#fef3c7] px-3 py-1 rounded-full font-d-label-sm border border-[#b45309]/10 whitespace-nowrap">
                          <span className="w-1.5 h-1.5 rounded-full bg-[#b45309]"></span> 85 kg
                        </span>
                      </td>
                      <td className="py-4 px-6 text-d-on-surface-variant whitespace-nowrap">
                        <div className="flex items-center gap-1">
                          <MaterialIcon name="lock" className="text-[16px]" /> 0 kg
                        </div>
                      </td>
                      <td className="py-4 px-6 font-medium whitespace-nowrap">85 kg</td>
                      <td className="py-4 px-6">
                        <button className="text-d-secondary border border-d-secondary/20 px-4 py-1.5 rounded-lg hover:bg-d-secondary hover:text-white transition-colors font-d-label-sm whitespace-nowrap">
                          Tạo lô
                        </button>
                      </td>
                    </tr>

                    {/* Row 3: Critical Warning */}
                    <tr className="bg-d-error-container/10 hover:bg-d-error-container/20 transition-colors">
                      <td className="py-4 px-6 font-medium">
                        <div className="flex items-center gap-3">
                          <div className="w-8 h-8 rounded-lg bg-d-error-container text-d-error flex items-center justify-center">
                            <MaterialIcon name="wine_bar" className="text-[18px]" />
                          </div>
                          <span className="flex items-center gap-1 whitespace-nowrap">
                            Thủy tinh <MaterialIcon name="warning" className="text-d-error text-[16px]" />
                          </span>
                        </div>
                      </td>
                      <td className="py-4 px-6">
                        <span className="inline-flex items-center gap-1.5 text-d-error bg-d-error-container px-3 py-1 rounded-full font-d-label-sm border border-d-error/10 whitespace-nowrap">
                          <span className="w-1.5 h-1.5 rounded-full bg-d-error"></span> 45 kg
                        </span>
                      </td>
                      <td className="py-4 px-6 text-d-on-surface-variant whitespace-nowrap">
                        <div className="flex items-center gap-1">
                          <MaterialIcon name="lock" className="text-[16px]" /> 0 kg
                        </div>
                      </td>
                      <td className="py-4 px-6 font-medium whitespace-nowrap">45 kg</td>
                      <td className="py-4 px-6">
                        <button className="text-d-outline border border-d-border-subtle px-4 py-1.5 rounded-lg opacity-50 cursor-not-allowed font-d-label-sm whitespace-nowrap" disabled>
                          Tạo lô
                        </button>
                      </td>
                    </tr>

                  </tbody>
                </table>
              </div>
            </div>

            {/* Footer Note */}
            <div className="flex items-start gap-2 text-d-on-surface-variant font-d-body-sm bg-d-surface-container-low p-4 rounded-lg shrink-0 mb-4">
              <MaterialIcon name="lightbulb" className="text-d-secondary" />
              <p>Tồn kho được cập nhật tự động dựa trên các giao dịch nhập và xuất mới nhất trong hệ thống.</p>
            </div>
          </>
        ) : (
          <div className="flex-1 flex flex-col min-h-0 bg-d-surface-container-lowest border border-d-border-subtle rounded-[20px] overflow-hidden shadow-sm">
            {/* Filter Bar */}
            <div className="p-6 border-b border-d-border-subtle shrink-0">
              <div className="flex flex-col lg:flex-row gap-4 items-center">
                <div className="relative flex-1 w-full">
                  <MaterialIcon name="search" className="absolute left-4 top-1/2 -translate-y-1/2 text-d-on-surface-variant" />
                  <input className="w-full bg-white border border-d-border-subtle rounded-lg py-3 pl-12 pr-4 font-d-body-sm text-d-body-sm focus:outline-none focus:border-d-secondary focus:ring-1 focus:ring-d-secondary transition-colors" placeholder="Tìm kiếm theo mã đơn, người bán, nhân viên..." type="text" />
                </div>
                <div className="flex flex-wrap gap-3 w-full lg:w-auto">
                  <div className="relative min-w-[200px]">
                    <MaterialIcon name="calendar_today" className="absolute left-3 top-1/2 -translate-y-1/2 text-d-on-surface-variant text-sm" />
                    <input className="w-full bg-white border border-d-border-subtle rounded-lg py-3 pl-10 pr-4 font-d-body-sm text-d-body-sm text-d-on-surface focus:outline-none focus:border-d-secondary transition-colors cursor-pointer" readOnly type="text" value="01/08/2026 - 26/08/2026" />
                  </div>
                  <div className="relative min-w-[180px]">
                    <select className="w-full appearance-none bg-white border border-d-border-subtle rounded-lg py-3 pl-4 pr-10 font-d-body-sm text-d-body-sm text-d-on-surface focus:outline-none focus:border-d-secondary transition-colors">
                      <option>Tất cả vật liệu</option>
                      <option>Nhựa PET</option>
                      <option>Sắt thép</option>
                      <option>Đồng cáp</option>
                    </select>
                    <MaterialIcon name="arrow_drop_down" className="absolute right-3 top-1/2 -translate-y-1/2 text-d-on-surface-variant pointer-events-none" />
                  </div>
                  <button className="px-4 py-3 text-d-secondary font-d-label-sm text-d-label-sm hover:bg-d-secondary/5 rounded-lg transition-colors whitespace-nowrap">Xóa bộ lọc</button>
                </div>
              </div>
              <div className="mt-4">
                <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant">Tìm thấy <span className="font-bold text-d-on-surface">48</span> đơn nhập kho trong khoảng thời gian đã chọn</p>
              </div>
            </div>

            {/* Data Table */}
            <div className="flex-1 overflow-y-auto">
              <table className="w-full text-left border-collapse min-w-[900px]">
                <thead className="sticky top-0 bg-d-surface-container-lowest z-10">
                  <tr className="border-b border-d-border-subtle">
                    <th className="py-4 px-6 font-d-label-sm text-d-label-sm text-d-on-surface-variant uppercase tracking-wider">Mã đơn</th>
                    <th className="py-4 px-6 font-d-label-sm text-d-label-sm text-d-on-surface-variant uppercase tracking-wider">Ngày nhập</th>
                    <th className="py-4 px-6 font-d-label-sm text-d-label-sm text-d-on-surface-variant uppercase tracking-wider">Người bán</th>
                    <th className="py-4 px-6 font-d-label-sm text-d-label-sm text-d-on-surface-variant uppercase tracking-wider">Vật liệu</th>
                    <th className="py-4 px-6 font-d-label-sm text-d-label-sm text-d-on-surface-variant uppercase tracking-wider text-right">KG</th>
                    <th className="py-4 px-6 font-d-label-sm text-d-label-sm text-d-on-surface-variant uppercase tracking-wider text-right">Đơn giá</th>
                    <th className="py-4 px-6 font-d-label-sm text-d-label-sm text-d-on-surface-variant uppercase tracking-wider text-right">Tổng tiền</th>
                    <th className="py-4 px-6 font-d-label-sm text-d-label-sm text-d-on-surface-variant uppercase tracking-wider">Nhân viên</th>
                    <th className="py-4 px-6 font-d-label-sm text-d-label-sm text-d-on-surface-variant uppercase tracking-wider">Trạng thái</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-d-border-subtle bg-white/50">
                  {/* Row 1 */}
                  <tr className="hover:bg-d-surface-container-lowest transition-colors">
                    <td className="py-4 px-6 font-d-label-md text-d-label-md text-d-on-surface font-bold">#INB-0421</td>
                    <td className="py-4 px-6 font-d-body-sm text-d-body-sm text-d-on-surface-variant">25/08 09:30</td>
                    <td className="py-4 px-6 font-d-body-sm text-d-body-sm text-d-on-surface">Trần Hữu H.</td>
                    <td className="py-4 px-6">
                      <span className="inline-flex items-center px-2.5 py-1 rounded-full bg-d-surface-container text-d-on-surface font-d-label-sm text-d-label-sm">3 loại</span>
                    </td>
                    <td className="py-4 px-6 font-d-label-md text-d-label-md text-d-on-surface text-right">150</td>
                    <td className="py-4 px-6 font-d-body-sm text-d-body-sm text-d-on-surface-variant text-right">—</td>
                    <td className="py-4 px-6 font-d-label-md text-d-label-md text-d-on-surface font-semibold text-right">1.400.000 đ</td>
                    <td className="py-4 px-6 font-d-body-sm text-d-body-sm text-d-on-surface-variant">NV: Long</td>
                    <td className="py-4 px-6">
                      <span className="inline-flex items-center px-2.5 py-1 rounded-full bg-d-surface-accent text-d-secondary font-d-label-sm text-d-label-sm gap-1 border border-d-secondary/20">
                        <span className="w-1.5 h-1.5 rounded-full bg-d-secondary"></span> Đã nhập
                      </span>
                    </td>
                  </tr>
                  
                  {/* Row 2 */}
                  <tr className="hover:bg-d-surface-container-lowest transition-colors">
                    <td className="py-4 px-6 font-d-label-md text-d-label-md text-d-on-surface font-bold">#INB-0420</td>
                    <td className="py-4 px-6 font-d-body-sm text-d-body-sm text-d-on-surface-variant">25/08 08:15</td>
                    <td className="py-4 px-6 font-d-body-sm text-d-body-sm text-d-on-surface">Lê Thị P.</td>
                    <td className="py-4 px-6">
                      <span className="inline-flex items-center px-2.5 py-1 rounded-full bg-d-inverse-on-surface text-d-secondary font-d-label-sm text-d-label-sm">Nhựa PET</span>
                    </td>
                    <td className="py-4 px-6 font-d-label-md text-d-label-md text-d-on-surface text-right">125</td>
                    <td className="py-4 px-6 font-d-body-sm text-d-body-sm text-d-on-surface-variant text-right">18.500 đ</td>
                    <td className="py-4 px-6 font-d-label-md text-d-label-md text-d-on-surface font-semibold text-right">2.312.500 đ</td>
                    <td className="py-4 px-6 font-d-body-sm text-d-body-sm text-d-on-surface-variant">NV: Minh</td>
                    <td className="py-4 px-6">
                      <span className="inline-flex items-center px-2.5 py-1 rounded-full bg-yellow-50 text-yellow-700 font-d-label-sm text-d-label-sm gap-1 border border-yellow-200">
                        <span className="w-1.5 h-1.5 rounded-full bg-yellow-500"></span> Chờ phân loại
                      </span>
                    </td>
                  </tr>

                  {/* Row 3 */}
                  <tr className="hover:bg-d-surface-container-lowest transition-colors">
                    <td className="py-4 px-6 font-d-label-md text-d-label-md text-d-on-surface font-bold">#INB-0419</td>
                    <td className="py-4 px-6 font-d-body-sm text-d-body-sm text-d-on-surface-variant">24/08 15:40</td>
                    <td className="py-4 px-6 font-d-body-sm text-d-body-sm text-d-on-surface">Công ty TNHH T.</td>
                    <td className="py-4 px-6">
                      <span className="inline-flex items-center px-2.5 py-1 rounded-full bg-d-inverse-on-surface text-d-secondary font-d-label-sm text-d-label-sm">Sắt thép</span>
                    </td>
                    <td className="py-4 px-6 font-d-label-md text-d-label-md text-d-on-surface text-right">1.200</td>
                    <td className="py-4 px-6 font-d-body-sm text-d-body-sm text-d-on-surface-variant text-right">8.000 đ</td>
                    <td className="py-4 px-6 font-d-label-md text-d-label-md text-d-on-surface font-semibold text-right">9.600.000 đ</td>
                    <td className="py-4 px-6 font-d-body-sm text-d-body-sm text-d-on-surface-variant">NV: Dũng</td>
                    <td className="py-4 px-6">
                      <span className="inline-flex items-center px-2.5 py-1 rounded-full bg-d-surface-accent text-d-secondary font-d-label-sm text-d-label-sm gap-1 border border-d-secondary/20">
                        <span className="w-1.5 h-1.5 rounded-full bg-d-secondary"></span> Đã nhập
                      </span>
                    </td>
                  </tr>

                </tbody>
                <tfoot className="bg-d-surface-container-high border-t border-d-border-subtle sticky bottom-0">
                  <tr>
                    <td className="py-4 px-6 font-d-label-md text-d-label-md text-d-on-surface font-bold" colSpan="4">Tổng cộng (48 đơn):</td>
                    <td className="py-4 px-6 font-d-label-md text-d-label-md text-d-on-surface font-bold text-right">8.450 kg</td>
                    <td></td>
                    <td className="py-4 px-6 font-d-label-md text-d-label-md text-d-primary font-bold text-right">45.250.000 đ</td>
                    <td colSpan="2"></td>
                  </tr>
                </tfoot>
              </table>
            </div>

            {/* Pagination */}
            <div className="bg-d-surface-container-lowest px-6 py-4 border-t border-d-border-subtle flex items-center justify-between shrink-0">
              <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant">Hiển thị 1 - 5 của 48</p>
              <div className="flex gap-2">
                <button className="w-8 h-8 rounded border border-d-border-subtle flex items-center justify-center text-d-on-surface-variant opacity-50 cursor-not-allowed">
                  <MaterialIcon name="chevron_left" className="text-sm" />
                </button>
                <button className="w-8 h-8 rounded bg-d-primary text-d-on-primary flex items-center justify-center font-d-label-sm text-d-label-sm">1</button>
                <button className="w-8 h-8 rounded border border-d-border-subtle flex items-center justify-center text-d-on-surface hover:bg-d-surface-container transition-colors font-d-label-sm text-d-label-sm">2</button>
                <button className="w-8 h-8 rounded border border-d-border-subtle flex items-center justify-center text-d-on-surface hover:bg-d-surface-container transition-colors font-d-label-sm text-d-label-sm">3</button>
                <span className="w-8 h-8 flex items-center justify-center text-d-on-surface-variant">...</span>
                <button className="w-8 h-8 rounded border border-d-border-subtle flex items-center justify-center text-d-on-surface hover:bg-d-surface-container transition-colors font-d-label-sm text-d-label-sm">10</button>
                <button className="w-8 h-8 rounded border border-d-border-subtle flex items-center justify-center text-d-on-surface hover:bg-d-surface-container transition-colors">
                  <MaterialIcon name="chevron_right" className="text-sm" />
                </button>
              </div>
            </div>
          </div>
        )}

      </div>
    </div>
  );
};

export default Inventory;
