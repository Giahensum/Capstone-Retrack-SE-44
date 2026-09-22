import React, { useState } from 'react';
import { MaterialIcon } from '../../components/ui/MaterialIcon';
import CreateBatchModal from './components/CreateBatchModal';

const Batches = () => {
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);

  return (
    <div className="flex flex-col p-4 md:p-6 w-full h-[calc(100vh-4rem)] gap-6 overflow-hidden bg-d-surface">
      <CreateBatchModal isOpen={isCreateModalOpen} onClose={() => setIsCreateModalOpen(false)} />
      
      {/* Page Header */}
      <div className="flex justify-between items-end shrink-0">
        <div>
          <h2 className="font-d-headline-lg text-d-headline-lg text-d-on-surface mb-1">Danh sách Lô Xuất Hàng</h2>
          <p className="font-d-body-md text-d-body-md text-d-outline">Quản lý và theo dõi trạng thái các lô vật liệu tái chế đang xuất kho.</p>
        </div>
        <button 
          onClick={() => setIsCreateModalOpen(true)}
          className="bg-d-primary-container text-d-on-primary-container font-d-label-md text-d-label-md px-6 py-2.5 rounded-full hover:bg-d-secondary-container transition-all shadow-sm font-bold flex items-center gap-2"
        >
          <MaterialIcon name="add" className="text-[18px]" /> Tạo lô xuất hàng mới
        </button>
      </div>

      {/* KPI Bento Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 shrink-0">
        <div className="bg-white/70 backdrop-blur-md border border-d-border-subtle p-6 rounded-xl flex flex-col justify-between shadow-sm">
          <div className="flex justify-between items-start mb-4">
            <span className="material-symbols-outlined text-d-primary bg-d-primary-container/20 p-2 rounded-lg">publish</span>
            <span className="font-d-label-sm text-d-label-sm text-d-secondary px-2 py-1 bg-d-surface-accent rounded-full">+12%</span>
          </div>
          <div>
            <p className="font-d-body-sm text-d-body-sm text-d-outline mb-1">Lô đang đăng</p>
            <p className="font-d-headline-md text-d-headline-md text-d-on-surface">14</p>
          </div>
        </div>

        <div className="bg-white/70 backdrop-blur-md border border-d-border-subtle p-6 rounded-xl flex flex-col justify-between shadow-sm">
          <div className="flex justify-between items-start mb-4">
            <span className="material-symbols-outlined text-d-secondary bg-d-secondary-container/20 p-2 rounded-lg">local_shipping</span>
          </div>
          <div>
            <p className="font-d-body-sm text-d-body-sm text-d-outline mb-1">Lô đang vận chuyển</p>
            <p className="font-d-headline-md text-d-headline-md text-d-on-surface">08</p>
          </div>
        </div>

        <div className="bg-white/70 backdrop-blur-md border border-d-border-subtle p-6 rounded-xl flex flex-col justify-between shadow-sm">
          <div className="flex justify-between items-start mb-4">
            <span className="material-symbols-outlined text-d-primary bg-d-primary-container/20 p-2 rounded-lg">weight</span>
          </div>
          <div>
            <p className="font-d-body-sm text-d-body-sm text-d-outline mb-1">Tổng KL đang xuất</p>
            <p className="font-d-headline-md text-d-headline-md text-d-primary">45,500 <span className="text-d-body-md text-d-outline">kg</span></p>
          </div>
        </div>

        <div className="bg-white/70 backdrop-blur-md border border-d-border-subtle p-6 rounded-xl flex flex-col justify-between shadow-sm">
          <div className="flex justify-between items-start mb-4">
            <span className="material-symbols-outlined text-d-on-surface-variant bg-d-surface-variant/30 p-2 rounded-lg">check_circle</span>
          </div>
          <div>
            <p className="font-d-body-sm text-d-body-sm text-d-outline mb-1">Lô đã hoàn tất (Tháng này)</p>
            <p className="font-d-headline-md text-d-headline-md text-d-on-surface">42</p>
          </div>
        </div>
      </div>

      {/* Filters */}
      <div className="flex items-center gap-4 overflow-x-auto pb-2 shrink-0 scrollbar-hide">
        <button className="px-4 py-1.5 rounded-full bg-d-primary-container text-d-on-primary-container font-d-label-md text-d-label-md border border-d-primary-container whitespace-nowrap font-bold">Tất cả</button>
        <button className="px-4 py-1.5 rounded-full bg-d-surface-container-lowest text-d-on-surface-variant font-d-label-md text-d-label-md border border-d-border-subtle hover:bg-d-surface-container whitespace-nowrap">Nháp</button>
        <button className="px-4 py-1.5 rounded-full bg-d-surface-container-lowest text-d-on-surface-variant font-d-label-md text-d-label-md border border-d-border-subtle hover:bg-d-surface-container whitespace-nowrap">Đang đăng</button>
        <button className="px-4 py-1.5 rounded-full bg-d-surface-container-lowest text-d-on-surface-variant font-d-label-md text-d-label-md border border-d-border-subtle hover:bg-d-surface-container whitespace-nowrap">Đã chỉ định</button>
        <button className="px-4 py-1.5 rounded-full bg-d-surface-container-lowest text-d-on-surface-variant font-d-label-md text-d-label-md border border-d-border-subtle hover:bg-d-surface-container whitespace-nowrap">Đang vận chuyển</button>
        <button className="px-4 py-1.5 rounded-full bg-d-surface-container-lowest text-d-on-surface-variant font-d-label-md text-d-label-md border border-d-border-subtle hover:bg-d-surface-container whitespace-nowrap">Hoàn tất</button>
        <button className="px-4 py-1.5 rounded-full bg-d-surface-container-lowest text-d-on-surface-variant font-d-label-md text-d-label-md border border-d-border-subtle hover:bg-d-surface-container whitespace-nowrap">Đã hủy</button>
      </div>

      {/* Data Table Card (Scrollable) */}
      <div className="bg-white/70 backdrop-blur-md rounded-[20px] overflow-hidden border border-d-border-subtle flex flex-col flex-1 min-h-0 shadow-sm">
        <div className="flex-1 overflow-y-auto">
          <table className="w-full text-left border-collapse">
            <thead className="sticky top-0 bg-d-surface-container-lowest z-10">
              <tr className="border-b border-d-border-subtle shadow-sm">
                <th className="py-4 px-6 font-d-label-md text-d-label-md text-d-on-surface-variant whitespace-nowrap">Mã Lô</th>
                <th className="py-4 px-6 font-d-label-md text-d-label-md text-d-on-surface-variant whitespace-nowrap">Loại Vật Liệu</th>
                <th className="py-4 px-6 font-d-label-md text-d-label-md text-d-on-surface-variant text-right whitespace-nowrap">Khối Lượng</th>
                <th className="py-4 px-6 font-d-label-md text-d-label-md text-d-on-surface-variant whitespace-nowrap">Người Mua/Nhà Máy</th>
                <th className="py-4 px-6 font-d-label-md text-d-label-md text-d-on-surface-variant whitespace-nowrap">Trạng Thái</th>
                <th className="py-4 px-6 font-d-label-md text-d-label-md text-d-on-surface-variant text-center whitespace-nowrap">Hành Động</th>
              </tr>
            </thead>
            <tbody className="font-d-body-sm text-d-body-sm">
              
              {/* Row 1: Đang đăng */}
              <tr className="border-b border-d-border-subtle hover:bg-d-surface-container-low transition-colors">
                <td className="py-4 px-6 font-d-label-sm text-d-label-sm font-bold whitespace-nowrap">SHP-2023-104</td>
                <td className="py-4 px-6 whitespace-nowrap">
                  <div className="flex items-center gap-2">
                    <span className="w-2 h-2 rounded-full bg-blue-500"></span>
                    PET Nhựa Trong
                  </div>
                </td>
                <td className="py-4 px-6 text-right font-d-label-md text-d-label-md whitespace-nowrap">12,500 kg</td>
                <td className="py-4 px-6 text-d-outline italic whitespace-nowrap">Chưa xác định (Marketplace)</td>
                <td className="py-4 px-6 whitespace-nowrap">
                  <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-blue-50 text-blue-700 font-d-label-sm text-d-label-sm border border-blue-200">
                    <MaterialIcon name="storefront" className="text-[14px]" />
                    Đang đăng
                  </span>
                </td>
                <td className="py-4 px-6 text-center whitespace-nowrap">
                  <button className="text-d-outline hover:text-d-primary"><MaterialIcon name="more_horiz" /></button>
                </td>
              </tr>

              {/* Row 2: Đã chỉ định */}
              <tr className="border-b border-d-border-subtle hover:bg-d-surface-container-low transition-colors">
                <td className="py-4 px-6 font-d-label-sm text-d-label-sm font-bold whitespace-nowrap">SHP-2023-103</td>
                <td className="py-4 px-6 whitespace-nowrap">
                  <div className="flex items-center gap-2">
                    <span className="w-2 h-2 rounded-full bg-gray-500"></span>
                    Nhôm Phế Liệu
                  </div>
                </td>
                <td className="py-4 px-6 text-right font-d-label-md text-d-label-md whitespace-nowrap">5,200 kg</td>
                <td className="py-4 px-6 whitespace-nowrap">Nhà máy Tái chế Bình Dương</td>
                <td className="py-4 px-6 whitespace-nowrap">
                  <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-purple-50 text-purple-700 font-d-label-sm text-d-label-sm border border-purple-200">
                    <MaterialIcon name="assignment_ind" className="text-[14px]" />
                    Đã chỉ định
                  </span>
                </td>
                <td className="py-4 px-6 text-center whitespace-nowrap">
                  <button className="text-d-outline hover:text-d-primary"><MaterialIcon name="more_horiz" /></button>
                </td>
              </tr>

              {/* Row 3: Đang vận chuyển */}
              <tr className="border-b border-d-border-subtle hover:bg-d-surface-container-low transition-colors">
                <td className="py-4 px-6 font-d-label-sm text-d-label-sm font-bold whitespace-nowrap">SHP-2023-102</td>
                <td className="py-4 px-6 whitespace-nowrap">
                  <div className="flex items-center gap-2">
                    <span className="w-2 h-2 rounded-full bg-yellow-500"></span>
                    Giấy Carton
                  </div>
                </td>
                <td className="py-4 px-6 text-right font-d-label-md text-d-label-md whitespace-nowrap">8,000 kg</td>
                <td className="py-4 px-6 whitespace-nowrap">Công ty Bao bì Đồng Nai</td>
                <td className="py-4 px-6 whitespace-nowrap">
                  <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-orange-50 text-orange-700 font-d-label-sm text-d-label-sm border border-orange-200">
                    <MaterialIcon name="local_shipping" className="text-[14px]" />
                    Đang vận chuyển
                  </span>
                </td>
                <td className="py-4 px-6 text-center whitespace-nowrap">
                  <button className="text-d-outline hover:text-d-primary"><MaterialIcon name="more_horiz" /></button>
                </td>
              </tr>

              {/* Row 4: Hoàn tất */}
              <tr className="border-b border-d-border-subtle hover:bg-d-surface-container-low transition-colors">
                <td className="py-4 px-6 font-d-label-sm text-d-label-sm font-bold whitespace-nowrap">SHP-2023-100</td>
                <td className="py-4 px-6 whitespace-nowrap">
                  <div className="flex items-center gap-2">
                    <span className="w-2 h-2 rounded-full bg-blue-500"></span>
                    HDPE Hạt Nhựa
                  </div>
                </td>
                <td className="py-4 px-6 text-right font-d-label-md text-d-label-md whitespace-nowrap">20,000 kg</td>
                <td className="py-4 px-6 whitespace-nowrap">Tập đoàn Nhựa ABC</td>
                <td className="py-4 px-6 whitespace-nowrap">
                  <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-d-surface-accent text-d-secondary font-d-label-sm text-d-label-sm border border-d-secondary-container">
                    <MaterialIcon name="check_circle" className="text-[14px]" />
                    Hoàn tất
                  </span>
                </td>
                <td className="py-4 px-6 text-center whitespace-nowrap">
                  <button className="text-d-outline hover:text-d-primary"><MaterialIcon name="more_horiz" /></button>
                </td>
              </tr>

              {/* Row 5: Nháp */}
              <tr className="border-b border-d-border-subtle hover:bg-d-surface-container-low transition-colors text-d-on-surface-variant opacity-70">
                <td className="py-4 px-6 font-d-label-sm text-d-label-sm font-bold whitespace-nowrap">SHP-2023-105</td>
                <td className="py-4 px-6 whitespace-nowrap">Chưa phân loại</td>
                <td className="py-4 px-6 text-right font-d-label-md text-d-label-md whitespace-nowrap">--</td>
                <td className="py-4 px-6 text-d-outline italic whitespace-nowrap">--</td>
                <td className="py-4 px-6 whitespace-nowrap">
                  <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-d-surface-variant text-d-on-surface-variant font-d-label-sm text-d-label-sm">
                    <MaterialIcon name="edit_document" className="text-[14px]" />
                    Nháp
                  </span>
                </td>
                <td className="py-4 px-6 text-center whitespace-nowrap">
                  <button className="text-d-outline hover:text-d-primary"><MaterialIcon name="more_horiz" /></button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        <div className="px-6 py-4 flex items-center justify-between border-t border-d-border-subtle bg-d-surface-container-lowest shrink-0">
          <span className="font-d-body-sm text-d-body-sm text-d-outline">Hiển thị 1-5 trong số 24 lô hàng</span>
          <div className="flex items-center gap-1">
            <button className="w-8 h-8 rounded-full flex items-center justify-center text-d-outline hover:bg-d-surface-container transition-colors"><MaterialIcon name="chevron_left" className="text-[20px]" /></button>
            <button className="w-8 h-8 rounded-full bg-d-primary text-d-on-primary flex items-center justify-center font-d-label-md text-d-label-md shadow-sm">1</button>
            <button className="w-8 h-8 rounded-full flex items-center justify-center text-d-on-surface hover:bg-d-surface-container transition-colors font-d-label-md text-d-label-md">2</button>
            <button className="w-8 h-8 rounded-full flex items-center justify-center text-d-on-surface hover:bg-d-surface-container transition-colors font-d-label-md text-d-label-md">3</button>
            <button className="w-8 h-8 rounded-full flex items-center justify-center text-d-outline hover:bg-d-surface-container transition-colors"><MaterialIcon name="chevron_right" className="text-[20px]" /></button>
          </div>
        </div>
      </div>

    </div>
  );
};

export default Batches;
