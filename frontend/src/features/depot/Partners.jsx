import React, { useState } from 'react';
import { MaterialIcon } from '../../components/ui/MaterialIcon';

const Partners = () => {
  const [activeTab, setActiveTab] = useState('list');

  return (
    <div className="flex flex-col p-4 md:p-6 w-full h-[calc(100vh-4rem)] gap-6 overflow-hidden bg-d-surface">
      
      {/* Page Header */}
      <div className="flex flex-col sm:flex-row sm:items-end justify-between gap-6 shrink-0">
        <div>
          <h2 className="font-d-headline-lg text-d-headline-lg text-d-on-surface mb-2">Đối Tác Nhà Máy</h2>
          <p className="font-d-body-md text-d-body-md text-d-on-surface-variant max-w-2xl">Quản lý danh sách các nhà máy tái chế đã liên kết.</p>
        </div>
        <button className="bg-transparent border border-d-primary text-d-primary hover:bg-d-surface-variant hover:border-d-primary-fixed px-6 py-2.5 rounded-full font-d-label-md text-d-label-md font-medium transition-all flex items-center justify-center gap-2 self-start sm:self-auto shrink-0">
          <MaterialIcon name="search" className="text-[18px]" /> Tìm đối tác mới
        </button>
      </div>

      {/* Tabs */}
      <div className="flex border-b border-d-border-subtle overflow-x-auto no-scrollbar space-x-6 shrink-0">
        <button 
          onClick={() => setActiveTab('list')}
          className={`pb-3 px-1 font-d-body-md text-d-body-md font-bold whitespace-nowrap transition-colors ${
            activeTab === 'list' ? 'text-d-primary border-b-2 border-d-primary' : 'text-d-on-surface-variant hover:text-d-primary'
          }`}
        >
          Danh sách nhà máy
        </button>
        <button 
          onClick={() => setActiveTab('demand')}
          className={`pb-3 px-1 font-d-body-md text-d-body-md font-bold whitespace-nowrap transition-colors ${
            activeTab === 'demand' ? 'text-d-primary border-b-2 border-d-primary' : 'text-d-on-surface-variant hover:text-d-primary'
          }`}
        >
          Bảng nhu cầu (Demand Board)
        </button>
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
                  <h3 className="font-d-headline-xl text-d-headline-xl text-d-on-surface">15</h3>
                  <span className="font-d-label-sm text-d-label-sm text-d-secondary flex items-center bg-d-surface-accent px-2 py-1 rounded-full mb-2">
                    <MaterialIcon name="trending_up" className="text-[14px] mr-1" /> +2 tháng này
                  </span>
                </div>
              </div>
              
              {/* Active Partners */}
              <div className="bg-d-surface-accent border border-d-secondary-container rounded-[20px] p-6 relative overflow-hidden group hover:shadow-md transition-shadow">
                <div className="absolute top-0 right-0 p-4 opacity-10 group-hover:opacity-20 transition-opacity">
                  <MaterialIcon name="handshake" className="text-6xl text-d-secondary" />
                </div>
                <p className="font-d-label-sm text-d-label-sm text-d-secondary uppercase tracking-wider mb-2">Đang hợp tác</p>
                <div className="flex items-end gap-3">
                  <h3 className="font-d-headline-xl text-d-headline-xl text-d-secondary">12</h3>
                  <span className="font-d-body-sm text-d-body-sm text-d-secondary opacity-80 mb-2 font-medium">nhà máy</span>
                </div>
              </div>
              
              {/* Pending Approval */}
              <div className="bg-d-surface-container-low border border-d-border-subtle rounded-[20px] p-6 relative overflow-hidden group hover:shadow-md transition-shadow">
                <div className="absolute top-0 right-0 p-4 opacity-10 group-hover:opacity-20 transition-opacity">
                  <MaterialIcon name="hourglass_empty" className="text-6xl text-d-outline" />
                </div>
                <p className="font-d-label-sm text-d-label-sm text-d-on-surface-variant uppercase tracking-wider mb-2">Chờ nhà máy duyệt</p>
                <div className="flex items-end gap-3">
                  <h3 className="font-d-headline-xl text-d-headline-xl text-d-on-surface">3</h3>
                  <span className="font-d-body-sm text-d-body-sm text-d-on-surface-variant opacity-80 mb-2 font-medium">yêu cầu</span>
                </div>
              </div>
              
            </div>

            {/* Data Table Section */}
            <div className="bg-d-surface-container-lowest border border-d-border-subtle rounded-[20px] overflow-hidden flex flex-col flex-1 shadow-sm shrink-0 min-h-[500px]">
          {/* Table Toolbar */}
          <div className="p-6 border-b border-d-border-subtle flex flex-col sm:flex-row justify-between items-center gap-4 bg-d-surface-container-lowest shrink-0">
            <div className="relative w-full sm:w-80">
              <MaterialIcon name="search" className="absolute left-3 top-1/2 -translate-y-1/2 text-d-outline text-[20px]" />
              <input className="w-full bg-white border border-d-border-subtle rounded-full py-2 pl-10 pr-4 font-d-body-sm text-d-body-sm focus:border-d-primary focus:ring-1 focus:ring-d-primary transition-all text-d-on-surface outline-none" placeholder="Tìm tên nhà máy, vật liệu..." type="text" />
            </div>
            <div className="flex gap-3 w-full sm:w-auto">
              <button className="flex-1 sm:flex-none flex items-center justify-center gap-2 px-4 py-2 border border-d-outline-variant rounded-full font-d-label-sm text-d-label-sm text-d-on-surface-variant hover:bg-d-surface-variant transition-colors">
                <MaterialIcon name="filter_list" className="text-[18px]" /> Lọc
              </button>
              <button className="flex-1 sm:flex-none flex items-center justify-center gap-2 px-4 py-2 border border-d-outline-variant rounded-full font-d-label-sm text-d-label-sm text-d-on-surface-variant hover:bg-d-surface-variant transition-colors">
                <MaterialIcon name="download" className="text-[18px]" /> Xuất CSV
              </button>
            </div>
          </div>

          {/* Table Container */}
          <div className="overflow-x-auto flex-1">
            <table className="w-full text-left border-collapse min-w-[1000px]">
              <thead className="sticky top-0 bg-d-surface-container-low z-10">
                <tr className="border-b border-d-border-subtle shadow-sm">
                  <th className="py-4 px-6 font-d-label-sm text-d-label-sm text-d-on-surface-variant uppercase tracking-wider w-[25%]">Nhà máy</th>
                  <th className="py-4 px-6 font-d-label-sm text-d-label-sm text-d-on-surface-variant uppercase tracking-wider w-[15%]">Vật liệu thu mua</th>
                  <th className="py-4 px-6 font-d-label-sm text-d-label-sm text-d-on-surface-variant uppercase tracking-wider text-right w-[12%]">Số lô đã giao</th>
                  <th className="py-4 px-6 font-d-label-sm text-d-label-sm text-d-on-surface-variant uppercase tracking-wider text-right w-[15%]">Tổng khối lượng</th>
                  <th className="py-4 px-6 font-d-label-sm text-d-label-sm text-d-on-surface-variant uppercase tracking-wider text-center w-[13%]">Đánh giá</th>
                  <th className="py-4 px-6 font-d-label-sm text-d-label-sm text-d-on-surface-variant uppercase tracking-wider w-[10%]">Trạng thái</th>
                  <th className="py-4 px-6 font-d-label-sm text-d-label-sm text-d-on-surface-variant uppercase tracking-wider text-center w-[10%]">Hành động</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-d-border-subtle font-d-body-sm text-d-body-sm text-d-on-surface bg-white/50">
                
                {/* Row 1: Active */}
                <tr className="hover:bg-d-surface transition-colors group">
                  <td className="py-4 px-6">
                    <div className="flex items-center gap-3">
                      <div className="w-10 h-10 rounded-lg bg-d-surface-variant flex items-center justify-center shrink-0">
                        <MaterialIcon name="factory" className="text-d-on-surface-variant" />
                      </div>
                      <div>
                        <p className="font-d-label-md text-d-label-md text-d-on-surface font-medium mb-0.5">Công ty TNHH Nhựa Xanh</p>
                        <p className="font-d-label-sm text-d-label-sm text-d-on-surface-variant opacity-70">Bình Dương, Việt Nam</p>
                      </div>
                    </div>
                  </td>
                  <td className="py-4 px-6">
                    <div className="flex flex-wrap gap-1">
                      <span className="px-2 py-1 rounded bg-d-surface-container text-d-on-surface-variant font-d-label-sm text-[11px]">PET</span>
                      <span className="px-2 py-1 rounded bg-d-surface-container text-d-on-surface-variant font-d-label-sm text-[11px]">HDPE</span>
                    </div>
                  </td>
                  <td className="py-4 px-6 text-right font-medium">12</td>
                  <td className="py-4 px-6 text-right font-medium">45,500 kg</td>
                  <td className="py-4 px-6">
                    <div className="flex items-center justify-center gap-1 text-d-secondary">
                      <MaterialIcon name="star" className="text-[16px]" />
                      <span className="font-medium">4.8</span>
                    </div>
                  </td>
                  <td className="py-4 px-6">
                    <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-d-surface-accent text-d-secondary font-d-label-sm text-d-label-sm">
                      <span className="w-1.5 h-1.5 rounded-full bg-d-secondary"></span> Đang hợp tác
                    </span>
                  </td>
                  <td className="py-4 px-6 text-center">
                    <button className="w-8 h-8 rounded-full hover:bg-d-surface-variant flex items-center justify-center text-d-on-surface-variant transition-colors mx-auto">
                      <MaterialIcon name="more_horiz" />
                    </button>
                  </td>
                </tr>
                
                {/* Row 2: Active */}
                <tr className="hover:bg-d-surface transition-colors group">
                  <td className="py-4 px-6">
                    <div className="flex items-center gap-3">
                      <div className="w-10 h-10 rounded-lg bg-d-surface-variant flex items-center justify-center shrink-0">
                        <MaterialIcon name="domain" className="text-d-on-surface-variant" />
                      </div>
                      <div>
                        <p className="font-d-label-md text-d-label-md text-d-on-surface font-medium mb-0.5">Tập đoàn Tái chế ABC</p>
                        <p className="font-d-label-sm text-d-label-sm text-d-on-surface-variant opacity-70">Long An, Việt Nam</p>
                      </div>
                    </div>
                  </td>
                  <td className="py-4 px-6">
                    <div className="flex flex-wrap gap-1">
                      <span className="px-2 py-1 rounded bg-d-surface-container text-d-on-surface-variant font-d-label-sm text-[11px]">Giấy</span>
                      <span className="px-2 py-1 rounded bg-d-surface-container text-d-on-surface-variant font-d-label-sm text-[11px]">Carton</span>
                    </div>
                  </td>
                  <td className="py-4 px-6 text-right font-medium">8</td>
                  <td className="py-4 px-6 text-right font-medium">32,200 kg</td>
                  <td className="py-4 px-6">
                    <div className="flex items-center justify-center gap-1 text-d-secondary">
                      <MaterialIcon name="star" className="text-[16px]" />
                      <span className="font-medium">4.5</span>
                    </div>
                  </td>
                  <td className="py-4 px-6">
                    <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-d-surface-accent text-d-secondary font-d-label-sm text-d-label-sm">
                      <span className="w-1.5 h-1.5 rounded-full bg-d-secondary"></span> Đang hợp tác
                    </span>
                  </td>
                  <td className="py-4 px-6 text-center">
                    <button className="w-8 h-8 rounded-full hover:bg-d-surface-variant flex items-center justify-center text-d-on-surface-variant transition-colors mx-auto">
                      <MaterialIcon name="more_horiz" />
                    </button>
                  </td>
                </tr>

                {/* Row 3: Pending */}
                <tr className="hover:bg-d-surface transition-colors group">
                  <td className="py-4 px-6">
                    <div className="flex items-center gap-3">
                      <div className="w-10 h-10 rounded-lg bg-d-surface-variant flex items-center justify-center shrink-0">
                        <MaterialIcon name="factory" className="text-d-on-surface-variant" />
                      </div>
                      <div>
                        <p className="font-d-label-md text-d-label-md text-d-on-surface font-medium mb-0.5">EcoPlast Industries</p>
                        <p className="font-d-label-sm text-d-label-sm text-d-on-surface-variant opacity-70">Đồng Nai, Việt Nam</p>
                      </div>
                    </div>
                  </td>
                  <td className="py-4 px-6">
                    <div className="flex flex-wrap gap-1">
                      <span className="px-2 py-1 rounded bg-d-surface-container text-d-on-surface-variant font-d-label-sm text-[11px]">PET</span>
                    </div>
                  </td>
                  <td className="py-4 px-6 text-right font-medium">--</td>
                  <td className="py-4 px-6 text-right font-medium">--</td>
                  <td className="py-4 px-6 text-center text-d-on-surface-variant">
                    --
                  </td>
                  <td className="py-4 px-6">
                    <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-d-surface-container-high text-d-on-surface-variant font-d-label-sm text-d-label-sm border border-d-border-subtle">
                      <MaterialIcon name="hourglass_empty" className="text-[14px]" /> Chờ duyệt
                    </span>
                  </td>
                  <td className="py-4 px-6 text-center">
                    <button className="w-8 h-8 rounded-full hover:bg-d-surface-variant flex items-center justify-center text-d-on-surface-variant transition-colors mx-auto">
                      <MaterialIcon name="more_horiz" />
                    </button>
                  </td>
                </tr>

              </tbody>
            </table>
          </div>
          
          {/* Pagination */}
          <div className="p-4 border-t border-d-border-subtle flex items-center justify-between bg-d-surface-container-lowest shrink-0">
            <span className="font-d-body-sm text-d-body-sm text-d-on-surface-variant">Trang 1 / 2</span>
            <div className="flex items-center gap-2">
              <button className="w-8 h-8 rounded-full border border-d-border-subtle flex items-center justify-center text-d-on-surface-variant opacity-50 cursor-not-allowed">
                <MaterialIcon name="chevron_left" className="text-[18px]" />
              </button>
              <button className="w-8 h-8 rounded-full bg-d-surface-container hover:bg-d-surface-variant transition-colors flex items-center justify-center text-d-on-surface">
                <MaterialIcon name="chevron_right" className="text-[18px]" />
              </button>
            </div>
          </div>
          </div>
          </>
        ) : (
          <div className="flex flex-col gap-6">
            {/* Filter Bar */}
            <div className="flex flex-col md:flex-row gap-4 mb-2">
              <div className="flex-1 flex items-center bg-white rounded-full px-4 py-3 border border-d-border-subtle focus-within:border-d-secondary transition-colors shadow-sm">
                <MaterialIcon name="search" className="text-d-outline mr-3" />
                <input className="bg-transparent border-none outline-none w-full font-d-body-md text-d-body-md text-d-on-surface placeholder-d-on-surface-variant/50 focus:ring-0" placeholder="Tìm kiếm theo loại phế liệu..." type="text" />
              </div>
              <div className="w-full md:w-64">
                <select className="w-full bg-white border border-d-border-subtle rounded-full px-4 py-3 font-d-body-md text-d-body-md text-d-on-surface focus:border-d-secondary focus:ring-0 shadow-sm appearance-none cursor-pointer">
                  <option value="">Lọc theo giá: Mặc định</option>
                  <option value="high">Giá cao đến thấp</option>
                  <option value="low">Giá thấp đến cao</option>
                </select>
              </div>
            </div>

            {/* Demand Grid */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              {/* Card 1 */}
              <div className="bg-white border border-d-border-subtle rounded-[20px] p-6 hover:shadow-xl transition-shadow duration-300 flex flex-col group relative overflow-hidden">
                <div className="absolute top-0 left-0 w-full h-1 bg-gradient-to-r from-d-error to-d-tertiary"></div>
                <div className="flex justify-between items-start mb-4">
                  <h3 className="font-d-headline-md text-d-headline-md text-d-on-surface">Công ty TNHH Nhựa Xanh</h3>
                  <span className="bg-d-error-container/30 text-d-error font-d-label-sm text-d-label-sm px-3 py-1 rounded-full flex items-center">
                    🔥 Cần gấp
                  </span>
                </div>
                <p className="font-d-body-lg text-d-body-lg font-medium text-d-secondary mb-4 flex-grow">Thu mua 5 Tấn Nhựa PET Trong</p>
                <div className="bg-d-surface-accent rounded-lg p-4 mb-6">
                  <div className="flex justify-between items-center mb-2">
                    <span className="font-d-body-sm text-d-body-sm text-d-on-surface-variant">Đơn giá dự kiến:</span>
                    <span className="font-d-label-md text-d-label-md font-bold text-d-on-surface">18.500 đ - 19.200 đ / kg</span>
                  </div>
                  <div className="flex justify-between items-center">
                    <span className="font-d-body-sm text-d-body-sm text-d-on-surface-variant">Hạn chót:</span>
                    <span className="font-d-label-md text-d-label-md text-d-error font-medium">30/08/2026</span>
                  </div>
                </div>
                <button className="w-full bg-d-on-surface text-d-surface py-3 rounded-full font-d-body-md text-d-body-md font-bold hover:bg-d-secondary transition-colors flex justify-center items-center gap-2 group-hover:bg-d-primary">
                  Tạo lô bán ngay
                  <MaterialIcon name="arrow_forward" className="text-[20px] group-hover:translate-x-1 transition-transform" />
                </button>
              </div>

              {/* Card 2 */}
              <div className="bg-white border border-d-border-subtle rounded-[20px] p-6 hover:shadow-xl transition-shadow duration-300 flex flex-col group relative overflow-hidden">
                <div className="absolute top-0 left-0 w-full h-1 bg-d-surface-variant"></div>
                <div className="flex justify-between items-start mb-4">
                  <h3 className="font-d-headline-md text-d-headline-md text-d-on-surface">Nhà máy Giấy Đồng Nai</h3>
                  <span className="bg-d-surface-container-high text-d-on-surface font-d-label-sm text-d-label-sm px-3 py-1 rounded-full">
                    Nhu cầu thường xuyên
                  </span>
                </div>
                <p className="font-d-body-lg text-d-body-lg font-medium text-d-secondary mb-4 flex-grow">Thu mua Giấy Carton phế liệu không giới hạn</p>
                <div className="bg-d-surface-container-low rounded-lg p-4 mb-6">
                  <div className="flex justify-between items-center mb-2">
                    <span className="font-d-body-sm text-d-body-sm text-d-on-surface-variant">Đơn giá dự kiến:</span>
                    <span className="font-d-label-md text-d-label-md font-bold text-d-on-surface">3.500 đ - 4.100 đ / kg</span>
                  </div>
                  <div className="flex justify-between items-center">
                    <span className="font-d-body-sm text-d-body-sm text-d-on-surface-variant">Hạn chót:</span>
                    <span className="font-d-label-md text-d-label-md text-d-on-surface-variant font-medium">Liên tục</span>
                  </div>
                </div>
                <button className="w-full bg-transparent border border-d-on-surface text-d-on-surface py-3 rounded-full font-d-body-md text-d-body-md font-bold hover:bg-d-surface-container transition-colors flex justify-center items-center gap-2 group-hover:bg-d-surface-variant">
                  Tạo lô bán ngay
                  <MaterialIcon name="arrow_forward" className="text-[20px] group-hover:translate-x-1 transition-transform" />
                </button>
              </div>

              {/* Card 3 */}
              <div className="bg-white border border-d-border-subtle rounded-[20px] p-6 hover:shadow-xl transition-shadow duration-300 flex flex-col group relative overflow-hidden">
                <div className="absolute top-0 left-0 w-full h-1 bg-gradient-to-r from-d-error to-d-tertiary"></div>
                <div className="flex justify-between items-start mb-4">
                  <h3 className="font-d-headline-md text-d-headline-md text-d-on-surface">Nhà máy Thép Việt</h3>
                  <span className="bg-d-error-container/30 text-d-error font-d-label-sm text-d-label-sm px-3 py-1 rounded-full flex items-center">
                    🔥 Cần gấp
                  </span>
                </div>
                <p className="font-d-body-lg text-d-body-lg font-medium text-d-secondary mb-4 flex-grow">Cần mua 20 Tấn Sắt vụn công trình</p>
                <div className="bg-d-surface-accent rounded-lg p-4 mb-6">
                  <div className="flex justify-between items-center mb-2">
                    <span className="font-d-body-sm text-d-body-sm text-d-on-surface-variant">Đơn giá dự kiến:</span>
                    <span className="font-d-label-md text-d-label-md font-bold text-d-on-surface">8.500 đ - 9.000 đ / kg</span>
                  </div>
                  <div className="flex justify-between items-center">
                    <span className="font-d-body-sm text-d-body-sm text-d-on-surface-variant">Hạn chót:</span>
                    <span className="font-d-label-md text-d-label-md text-d-error font-medium">25/08/2026</span>
                  </div>
                </div>
                <button className="w-full bg-d-on-surface text-d-surface py-3 rounded-full font-d-body-md text-d-body-md font-bold hover:bg-d-secondary transition-colors flex justify-center items-center gap-2 group-hover:bg-d-primary">
                  Tạo lô bán ngay
                  <MaterialIcon name="arrow_forward" className="text-[20px] group-hover:translate-x-1 transition-transform" />
                </button>
              </div>

              {/* Card 4 */}
              <div className="bg-white border border-d-border-subtle rounded-[20px] p-6 hover:shadow-xl transition-shadow duration-300 flex flex-col group relative overflow-hidden">
                <div className="absolute top-0 left-0 w-full h-1 bg-d-secondary"></div>
                <div className="flex justify-between items-start mb-4">
                  <h3 className="font-d-headline-md text-d-headline-md text-d-on-surface">Công ty Tái chế Đồng Cáp</h3>
                  <span className="bg-d-surface-accent text-d-secondary font-d-label-sm text-d-label-sm px-3 py-1 rounded-full">
                    Nhu cầu cao
                  </span>
                </div>
                <p className="font-d-body-lg text-d-body-lg font-medium text-d-secondary mb-4 flex-grow">Thu mua 500kg Đồng cáp loại 1</p>
                <div className="bg-d-surface-container-low rounded-lg p-4 mb-6">
                  <div className="flex justify-between items-center mb-2">
                    <span className="font-d-body-sm text-d-body-sm text-d-on-surface-variant">Đơn giá dự kiến:</span>
                    <span className="font-d-label-md text-d-label-md font-bold text-d-on-surface">95.000 đ / kg</span>
                  </div>
                  <div className="flex justify-between items-center">
                    <span className="font-d-body-sm text-d-body-sm text-d-on-surface-variant">Hạn chót:</span>
                    <span className="font-d-label-md text-d-label-md text-d-on-surface-variant font-medium">Linh hoạt</span>
                  </div>
                </div>
                <button className="w-full bg-transparent border border-d-on-surface text-d-on-surface py-3 rounded-full font-d-body-md text-d-body-md font-bold hover:bg-d-surface-container transition-colors flex justify-center items-center gap-2 group-hover:bg-d-surface-variant">
                  Tạo lô bán ngay
                  <MaterialIcon name="arrow_forward" className="text-[20px] group-hover:translate-x-1 transition-transform" />
                </button>
              </div>
            </div>
          </div>
        )}

      </div>
    </div>
  );
};

export default Partners;
