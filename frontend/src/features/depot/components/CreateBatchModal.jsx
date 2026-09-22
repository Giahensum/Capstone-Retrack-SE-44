import React, { useState } from 'react';
import { MaterialIcon } from '../../../components/ui/MaterialIcon';

const CreateBatchModal = ({ isOpen, onClose }) => {
  const [salesStrategy, setSalesStrategy] = useState('public');

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-d-inverse-surface/40 backdrop-blur-sm">
      <div className="w-full max-w-[640px] rounded-2xl flex flex-col max-h-[90vh] bg-d-surface-container-lowest shadow-[0_20px_40px_rgba(23,33,27,0.04)] border border-d-border-subtle overflow-hidden">
        
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-5 border-b border-d-border-subtle bg-d-surface-container-lowest/90 backdrop-blur-md shrink-0">
          <div>
            <h2 className="font-d-headline-md text-d-headline-md text-d-on-surface font-bold">Tạo Lô Xuất Hàng</h2>
            <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant mt-1">Vui lòng điền thông tin chi tiết để tạo lô hàng xuất khẩu mới.</p>
          </div>
          <button 
            onClick={onClose}
            className="w-10 h-10 flex items-center justify-center rounded-full hover:bg-d-surface-variant text-d-on-surface-variant transition-colors"
          >
            <MaterialIcon name="close" className="hover:text-d-error transition-colors" />
          </button>
        </div>

        {/* Body */}
        <div className="px-6 py-6 overflow-y-auto flex-1 bg-d-surface min-h-0">
          <form className="space-y-6" onSubmit={(e) => e.preventDefault()}>
            
            {/* Material Type Select */}
            <div className="space-y-2">
              <label className="block font-d-label-md text-d-label-md text-d-on-surface">Loại phế liệu</label>
              <div className="relative">
                <select className="w-full bg-white border border-d-outline-variant text-d-on-surface font-d-body-md rounded-lg focus:ring-2 focus:ring-d-primary focus:border-d-primary block p-3 appearance-none transition-colors outline-none shadow-sm" defaultValue="">
                  <option disabled value="">Chọn loại phế liệu</option>
                  <option value="pet">Nhựa PET</option>
                  <option value="hdpe">Nhựa HDPE</option>
                  <option value="aluminum">Nhôm</option>
                  <option value="paper">Giấy Carton</option>
                </select>
                <div className="pointer-events-none absolute inset-y-0 right-0 flex items-center px-4 text-d-on-surface-variant">
                  <MaterialIcon name="expand_more" />
                </div>
              </div>
              <p className="font-d-label-sm text-d-label-sm text-d-primary flex items-center gap-1 mt-1">
                <MaterialIcon name="info" className="text-[16px]" /> Tồn kho khả dụng: 2,450 kg
              </p>
            </div>

            {/* Quantity Input */}
            <div className="space-y-2">
              <label className="block font-d-label-md text-d-label-md text-d-on-surface">Số lượng (kg)</label>
              <div className="relative">
                <input 
                  type="number" 
                  className="w-full bg-white border border-d-outline-variant text-d-on-surface font-d-body-md rounded-lg focus:ring-2 focus:ring-d-primary focus:border-d-primary block p-3 transition-colors outline-none shadow-sm" 
                  placeholder="Nhập số lượng" 
                  min="1" max="2450" required 
                />
                <div className="absolute inset-y-0 right-0 pr-3 flex items-center pointer-events-none">
                  <span className="text-d-on-surface-variant font-d-body-sm opacity-70">kg</span>
                </div>
              </div>
            </div>

            {/* Description Textarea */}
            <div className="space-y-2">
              <label className="block font-d-label-md text-d-label-md text-d-on-surface">Mô tả / Ghi chú</label>
              <textarea 
                className="w-full bg-white border border-d-outline-variant text-d-on-surface font-d-body-md rounded-lg focus:ring-2 focus:ring-d-primary focus:border-d-primary block p-3 transition-colors resize-y outline-none shadow-sm" 
                rows="3" 
                placeholder="Nhập ghi chú thêm cho lô hàng này..."
                maxLength="500"
              ></textarea>
            </div>

            {/* Image Upload */}
            <div className="space-y-2">
              <label className="block font-d-label-md text-d-label-md text-d-on-surface">Hình ảnh lô hàng (tùy chọn, tối đa 5 ảnh)</label>
              <div className="border-2 border-dashed border-d-outline-variant rounded-xl p-8 flex flex-col items-center justify-center gap-3 bg-white hover:bg-d-surface-container-low transition-colors cursor-pointer group">
                <div className="w-12 h-12 rounded-full bg-d-primary/10 flex items-center justify-center text-d-primary group-hover:scale-110 transition-transform">
                  <MaterialIcon name="add_a_photo" className="text-[28px]" />
                </div>
                <div className="text-center">
                  <p className="font-d-body-md text-d-on-surface">Kéo thả ảnh vào đây hoặc <span className="text-d-primary font-bold">Chọn từ thiết bị</span></p>
                  <p className="font-d-label-sm text-d-on-surface-variant mt-1">JPG, PNG, WEBP — Tối đa 5MB mỗi ảnh</p>
                </div>
              </div>
            </div>

            {/* Sales Strategy Radio */}
            <div className="space-y-3 pt-2">
              <label className="block font-d-label-md text-d-label-md text-d-on-surface">Hình thức bán</label>
              <div className="space-y-3">
                <label className="flex items-center gap-3 cursor-pointer group">
                  <input 
                    type="radio" 
                    name="salesStrategy" 
                    value="public" 
                    checked={salesStrategy === 'public'} 
                    onChange={() => setSalesStrategy('public')}
                    className="w-5 h-5 text-d-primary border-d-outline-variant focus:ring-d-primary bg-white cursor-pointer"
                  />
                  <span className="font-d-body-md text-d-on-surface group-hover:text-d-primary transition-colors">Đăng công khai lên Marketplace</span>
                </label>
                <label className="flex items-center gap-3 cursor-pointer group">
                  <input 
                    type="radio" 
                    name="salesStrategy" 
                    value="direct" 
                    checked={salesStrategy === 'direct'}
                    onChange={() => setSalesStrategy('direct')}
                    className="w-5 h-5 text-d-primary border-d-outline-variant focus:ring-d-primary bg-white cursor-pointer"
                  />
                  <span className="font-d-body-md text-d-on-surface group-hover:text-d-primary transition-colors">Chỉ định nhà máy cụ thể</span>
                </label>
              </div>
            </div>

            {/* Conditional Factory Select */}
            {salesStrategy === 'direct' && (
              <div className="space-y-2 animate-in slide-in-from-top-2 duration-200 fade-in">
                <label className="block font-d-label-md text-d-label-md text-d-on-surface">Chọn nhà máy</label>
                <div className="relative">
                  <select className="w-full bg-white border border-d-outline-variant text-d-on-surface font-d-body-md rounded-lg focus:ring-2 focus:ring-d-primary focus:border-d-primary block p-3 appearance-none transition-colors outline-none shadow-sm" defaultValue="">
                    <option disabled value="">Chọn nhà máy đối tác</option>
                    <option value="f1">Nhà máy Tái chế Xanh VN</option>
                    <option value="f2">EcoPlast Industries</option>
                    <option value="f3">Công ty TNHH Vòng Tuần Hoàn</option>
                  </select>
                  <div className="pointer-events-none absolute inset-y-0 right-0 flex items-center px-4 text-d-on-surface-variant">
                    <MaterialIcon name="expand_more" />
                  </div>
                </div>
              </div>
            )}
            
          </form>
        </div>

        {/* Footer */}
        <div className="px-6 py-5 border-t border-d-border-subtle bg-d-surface-container-lowest shrink-0 flex items-center justify-end gap-4">
          <button 
            onClick={onClose}
            className="px-6 py-2.5 rounded-lg border border-d-outline-variant text-d-on-surface font-d-label-md hover:bg-d-surface-container-low transition-colors"
          >
            Hủy
          </button>
          <button className="px-6 py-2.5 rounded-lg bg-d-primary text-d-on-primary font-d-label-md hover:bg-[#3b5b00] transition-colors shadow-sm">
            Tạo lô xuất hàng
          </button>
        </div>

      </div>
    </div>
  );
};

export default CreateBatchModal;
