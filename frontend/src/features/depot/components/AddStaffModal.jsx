import React from 'react';
import { MaterialIcon } from '../../../components/ui/MaterialIcon';

const AddStaffModal = ({ isOpen, onClose }) => {
  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-d-inverse-surface/60 backdrop-blur-sm p-4">
      {/* Modal Content */}
      <div className="bg-d-surface-container-lowest w-full max-w-[500px] rounded-[24px] shadow-[0_20px_40px_rgba(23,33,27,0.08)] flex flex-col transform transition-all duration-300 scale-100 opacity-100 max-h-[90vh]">
        {/* Header */}
        <div className="flex justify-between items-center px-6 py-5 border-b border-d-border-subtle shrink-0">
          <h2 className="font-d-body-lg text-d-body-lg font-bold text-d-on-surface">Thêm Nhân Viên Mới</h2>
          <button 
            onClick={onClose}
            className="text-d-on-surface-variant hover:text-d-on-surface transition-colors p-2 rounded-full hover:bg-d-surface-variant/50"
          >
            <MaterialIcon name="close" />
          </button>
        </div>

        {/* Body (Scrollable if needed) */}
        <div className="px-6 py-6 overflow-y-auto flex flex-col gap-6">
          {/* Avatar Upload */}
          <div className="flex flex-col items-center justify-center gap-3 w-full">
            <div className="relative w-24 h-24 rounded-full bg-d-surface-container flex items-center justify-center border-2 border-dashed border-d-outline-variant cursor-pointer hover:border-d-primary transition-colors group">
              <MaterialIcon name="photo_camera" className="text-d-outline text-[32px] group-hover:text-d-primary transition-colors" />
              <div className="absolute bottom-0 right-0 w-8 h-8 bg-d-primary-fixed rounded-full flex items-center justify-center border-2 border-d-surface-container-lowest shadow-sm">
                <MaterialIcon name="add" className="text-d-on-primary-fixed text-[16px]" />
              </div>
            </div>
            <span className="font-d-body-sm text-d-body-sm text-d-on-surface-variant">Tải ảnh đại diện</span>
          </div>

          {/* Form Grid */}
          <div className="grid grid-cols-1 gap-5">
            {/* Họ và tên */}
            <div className="flex flex-col gap-1.5">
              <label className="font-d-body-sm text-d-body-sm font-medium text-d-on-surface">Họ và tên <span class="text-d-error">*</span></label>
              <input 
                className="w-full h-11 px-4 bg-d-surface-container-lowest border border-d-outline-variant rounded-lg font-d-body-md text-d-body-md text-d-on-surface placeholder:text-d-outline focus:border-d-primary focus:ring-1 focus:ring-d-primary transition-all shadow-sm outline-none" 
                placeholder="Nhập họ và tên" 
                type="text" 
              />
            </div>

            {/* Số điện thoại */}
            <div className="flex flex-col gap-1.5">
              <label className="font-d-body-sm text-d-body-sm font-medium text-d-on-surface">Số điện thoại <span className="text-d-error">*</span></label>
              <input 
                className="w-full h-11 px-4 bg-d-surface-container-lowest border border-d-outline-variant rounded-lg font-d-body-md text-d-body-md text-d-on-surface placeholder:text-d-outline focus:border-d-primary focus:ring-1 focus:ring-d-primary transition-all shadow-sm outline-none" 
                placeholder="Nhập số điện thoại" 
                type="tel" 
              />
            </div>

            {/* Vai trò & Biển số xe Row */}
            <div className="grid grid-cols-2 gap-4">
              {/* Vai trò */}
              <div className="flex flex-col gap-1.5">
                <label className="font-d-body-sm text-d-body-sm font-medium text-d-on-surface">Vai trò</label>
                <div className="relative">
                  <select className="w-full h-11 px-4 appearance-none bg-d-surface-container-lowest border border-d-outline-variant rounded-lg font-d-body-md text-d-body-md text-d-on-surface focus:border-d-primary focus:ring-1 focus:ring-d-primary transition-all shadow-sm outline-none cursor-pointer pr-10">
                    <option value="thu_gom">Nhân viên thu gom</option>
                    <option value="quan_ly">Quản lý kho</option>
                  </select>
                  <MaterialIcon name="keyboard_arrow_down" className="absolute right-3 top-1/2 -translate-y-1/2 text-d-outline pointer-events-none" />
                </div>
              </div>
              
              {/* Biển số xe */}
              <div className="flex flex-col gap-1.5">
                <label className="font-d-body-sm text-d-body-sm font-medium text-d-on-surface">Biển số xe</label>
                <input 
                  className="w-full h-11 px-4 bg-d-surface-container-lowest border border-d-outline-variant rounded-lg font-d-body-md text-d-body-md text-d-on-surface placeholder:text-d-outline focus:border-d-primary focus:ring-1 focus:ring-d-primary transition-all shadow-sm outline-none uppercase" 
                  placeholder="VD: 29A-123.45" 
                  type="text" 
                />
              </div>
            </div>

            {/* Mật khẩu khởi tạo */}
            <div className="flex flex-col gap-1.5">
              <label className="font-d-body-sm text-d-body-sm font-medium text-d-on-surface">Mật khẩu khởi tạo <span className="text-d-error">*</span></label>
              <div className="relative">
                <input 
                  className="w-full h-11 px-4 bg-d-surface-container-lowest border border-d-outline-variant rounded-lg font-d-body-md text-d-body-md text-d-on-surface focus:border-d-primary focus:ring-1 focus:ring-d-primary transition-all shadow-sm outline-none pr-12" 
                  type="password" 
                  defaultValue="retrack@123" 
                />
                <button className="absolute right-3 top-1/2 -translate-y-1/2 text-d-outline hover:text-d-on-surface transition-colors p-1 flex items-center justify-center">
                  <MaterialIcon name="visibility" className="text-[20px]" />
                </button>
              </div>
            </div>
            
            {/* Divider */}
            <div className="w-full h-px bg-d-border-subtle my-1"></div>
            
            {/* Trạng thái tài khoản (Toggle) */}
            <div className="flex items-center justify-between">
              <div className="flex flex-col">
                <span className="font-d-body-sm text-d-body-sm font-medium text-d-on-surface">Trạng thái tài khoản</span>
                <span className="font-d-label-sm text-d-label-sm text-d-on-surface-variant">Cho phép đăng nhập ứng dụng</span>
              </div>
              {/* Toggle Switch (ON state) */}
              <div className="flex items-center gap-3">
                <span className="font-d-label-sm text-d-label-sm text-d-primary font-medium">Đang hoạt động</span>
                <button className="relative inline-flex h-6 w-11 items-center rounded-full bg-d-primary transition-colors focus:outline-none focus:ring-2 focus:ring-d-primary focus:ring-offset-2">
                  <span className="inline-block h-4 w-4 transform rounded-full bg-d-on-primary transition-transform translate-x-6 shadow-sm"></span>
                </button>
              </div>
            </div>
          </div>
        </div>

        {/* Footer */}
        <div className="px-6 py-5 bg-d-surface-container-lowest border-t border-d-border-subtle rounded-b-[24px] flex justify-between items-center shrink-0">
          <span className="font-d-label-sm text-d-label-sm text-d-on-surface-variant italic">* Thông tin bắt buộc</span>
          <div className="flex gap-3">
            <button 
              onClick={onClose}
              className="h-10 px-6 rounded-full border border-d-on-surface text-d-on-surface font-d-label-md text-d-label-md hover:bg-d-surface-variant/30 transition-colors"
            >
              Hủy
            </button>
            <button className="h-10 px-6 rounded-full bg-d-primary-fixed text-d-on-primary-fixed font-d-label-md text-d-label-md font-bold hover:bg-d-primary-fixed-dim hover:-translate-y-0.5 transition-all flex items-center gap-2 shadow-sm">
              <MaterialIcon name="check" className="text-[18px]" style={{ fontVariationSettings: "'FILL' 1" }} />
              Tạo tài khoản
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};

export default AddStaffModal;
