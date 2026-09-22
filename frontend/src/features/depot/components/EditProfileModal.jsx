import React from 'react';
import { MaterialIcon } from '../../../components/ui/MaterialIcon';

const EditProfileModal = ({ isOpen, onClose }) => {
  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-d-inverse-surface/40 backdrop-blur-sm">
      {/* Modal Container */}
      <div className="w-full max-w-[560px] rounded-2xl flex flex-col max-h-[90vh] bg-d-surface-container-lowest relative overflow-hidden transform transition-all shadow-[0_20px_40px_rgba(23,33,27,0.04)] border border-d-border-subtle">
        
        {/* Modal Header */}
        <div className="flex items-center justify-between px-6 py-5 border-b border-d-border-subtle bg-d-surface-container-lowest/90 backdrop-blur-md z-10 sticky top-0 shrink-0">
          <h2 className="font-d-headline-md text-d-headline-md text-d-on-surface font-bold">Chỉnh sửa hồ sơ kho vựa</h2>
          <button 
            onClick={onClose}
            className="w-10 h-10 flex items-center justify-center rounded-full hover:bg-d-surface-variant text-d-on-surface-variant transition-colors group"
          >
            <MaterialIcon name="close" className="group-hover:text-d-error transition-colors" />
          </button>
        </div>

        {/* Modal Body (Scrollable) */}
        <div className="px-6 py-6 overflow-y-auto flex-1 bg-d-surface flex flex-col gap-8 min-h-0">
          
          {/* Cover Photo Section */}
          <section>
            <label className="block mb-2 font-d-body-sm text-d-body-sm text-d-on-surface-variant font-semibold uppercase tracking-wider text-[11px] opacity-70">
              Ảnh bìa
            </label>
            <div className="relative w-full h-32 rounded-xl overflow-hidden group bg-gradient-to-br from-d-primary-container to-d-secondary-container">
              {/* Placeholder Gradient for Cover */}
              <div className="absolute inset-0 bg-black/20 group-hover:bg-black/40 transition-colors flex items-center justify-center">
                <button className="bg-d-surface-container-lowest text-d-on-surface px-4 py-2 rounded-full font-d-label-md text-d-label-md flex items-center gap-2 shadow-sm hover:-translate-y-[2px] active:scale-95 opacity-0 group-hover:opacity-100 transition-all duration-200">
                  <MaterialIcon name="add_photo_alternate" className="text-[18px]" />
                  Thay đổi ảnh bìa
                </button>
              </div>
            </div>
          </section>

          {/* Form Fields */}
          <section className="grid grid-cols-1 md:grid-cols-2 gap-x-6 gap-y-4">
            <div className="md:col-span-2">
              <label className="block mb-2 font-d-body-sm text-d-body-sm text-d-on-surface-variant font-semibold uppercase tracking-wider text-[11px] opacity-70">Tên kho vựa</label>
              <input 
                className="w-full bg-d-surface-container-low/50 border border-transparent rounded-xl px-4 py-3 text-d-body-md font-d-body-md text-d-on-surface focus:outline-none focus:bg-d-surface-container-lowest focus:ring-2 focus:ring-d-primary focus:border-transparent transition-all" 
                type="text" 
                defaultValue="Kho Vựa Minh Sang" 
              />
            </div>
            <div>
              <label className="block mb-2 font-d-body-sm text-d-body-sm text-d-on-surface-variant font-semibold uppercase tracking-wider text-[11px] opacity-70">Số điện thoại</label>
              <input 
                className="w-full bg-d-surface-container-low/50 border border-transparent rounded-xl px-4 py-3 text-d-body-md font-d-body-md text-d-on-surface focus:outline-none focus:bg-d-surface-container-lowest focus:ring-2 focus:ring-d-primary focus:border-transparent transition-all" 
                type="tel" 
                defaultValue="0901 234 567" 
              />
            </div>
            <div>
              <label className="block mb-2 font-d-body-sm text-d-body-sm text-d-on-surface-variant font-semibold uppercase tracking-wider text-[11px] opacity-70">Email</label>
              <input 
                className="w-full bg-d-surface-container-low/50 border border-transparent rounded-xl px-4 py-3 text-d-body-md font-d-body-md text-d-on-surface focus:outline-none focus:bg-d-surface-container-lowest focus:ring-2 focus:ring-d-primary focus:border-transparent transition-all" 
                type="email" 
                defaultValue="minhsang@retrack.vn" 
              />
            </div>
            <div className="md:col-span-2">
              <label className="block mb-2 font-d-body-sm text-d-body-sm text-d-on-surface-variant font-semibold uppercase tracking-wider text-[11px] opacity-70">Mã số thuế</label>
              <input 
                className="w-full bg-d-surface-container-low/50 border border-transparent rounded-xl px-4 py-3 text-d-body-md font-d-body-md text-d-on-surface focus:outline-none focus:bg-d-surface-container-lowest focus:ring-2 focus:ring-d-primary focus:border-transparent transition-all" 
                type="text" 
                defaultValue="0312456789" 
              />
            </div>
            <div className="md:col-span-2">
              <label className="block mb-2 font-d-body-sm text-d-body-sm text-d-on-surface-variant font-semibold uppercase tracking-wider text-[11px] opacity-70">Địa chỉ đầy đủ</label>
              <input 
                className="w-full bg-d-surface-container-low/50 border border-transparent rounded-xl px-4 py-3 text-d-body-md font-d-body-md text-d-on-surface focus:outline-none focus:bg-d-surface-container-lowest focus:ring-2 focus:ring-d-primary focus:border-transparent transition-all" 
                type="text" 
                defaultValue="123 Đường Tái Chế, Quận Bình Tân, TP.HCM" 
              />
            </div>
          </section>

          {/* Operating Hours */}
          <section className="bg-d-surface-container-low p-4 rounded-xl border border-d-border-subtle">
            <label className="block mb-4 font-d-body-sm text-d-body-sm text-d-on-surface-variant font-semibold uppercase tracking-wider text-[11px] opacity-70">Giờ hoạt động</label>
            <div className="flex items-center gap-4 mb-5">
              <div className="flex-1">
                <label className="font-d-body-sm text-d-body-sm text-d-on-surface-variant block mb-1">Mở cửa</label>
                <input 
                  className="w-full bg-d-surface-container-lowest border border-transparent rounded-xl px-4 py-2 text-d-body-md font-d-body-md text-d-on-surface focus:outline-none focus:ring-2 focus:ring-d-primary transition-all" 
                  type="time" 
                  defaultValue="07:00" 
                />
              </div>
              <MaterialIcon name="arrow_forward" className="text-d-outline-variant mt-6" />
              <div className="flex-1">
                <label className="font-d-body-sm text-d-body-sm text-d-on-surface-variant block mb-1">Đóng cửa</label>
                <input 
                  className="w-full bg-d-surface-container-lowest border border-transparent rounded-xl px-4 py-2 text-d-body-md font-d-body-md text-d-on-surface focus:outline-none focus:ring-2 focus:ring-d-primary transition-all" 
                  type="time" 
                  defaultValue="18:00" 
                />
              </div>
            </div>
            
            <div>
              <label className="font-d-body-sm text-d-body-sm text-d-on-surface-variant block mb-2">Ngày làm việc</label>
              <div className="flex flex-wrap gap-2">
                {['T2', 'T3', 'T4', 'T5', 'T6', 'T7'].map(day => (
                  <label key={day} className="cursor-pointer">
                    <input defaultChecked className="peer sr-only" type="checkbox" />
                    <div className="w-10 h-10 rounded-full flex items-center justify-center font-d-label-sm text-d-label-sm bg-d-surface-container border border-d-border-subtle text-d-on-surface-variant peer-checked:bg-d-primary-container peer-checked:text-d-on-primary-container peer-checked:border-d-primary transition-colors">
                      {day}
                    </div>
                  </label>
                ))}
                {/* Sun Unchecked */}
                <label className="cursor-pointer">
                  <input className="peer sr-only" type="checkbox" />
                  <div className="w-10 h-10 rounded-full flex items-center justify-center font-d-label-sm text-d-label-sm bg-d-surface-container border border-d-border-subtle text-d-on-surface-variant peer-checked:bg-d-primary-container peer-checked:text-d-on-primary-container peer-checked:border-d-primary transition-colors">
                    CN
                  </div>
                </label>
              </div>
            </div>
          </section>

          {/* Material Tags */}
          <section>
            <label className="block mb-3 font-d-body-sm text-d-body-sm text-d-on-surface-variant font-semibold uppercase tracking-wider text-[11px] opacity-70">Vật liệu thu mua</label>
            <div className="flex flex-wrap gap-2">
              {['Nhựa', 'Giấy', 'Sắt thép', 'Đồng', 'Nhôm'].map(mat => (
                <div key={mat} className="px-4 py-2 rounded-full bg-d-primary-container text-d-on-primary-container font-d-label-sm text-d-label-sm border border-d-primary/20 flex items-center gap-2 cursor-pointer hover:brightness-95 transition-all">
                  {mat} <MaterialIcon name="check" className="text-[16px]" />
                </div>
              ))}
              <div className="px-4 py-2 rounded-full bg-transparent text-d-on-surface-variant font-d-label-sm text-d-label-sm border border-d-outline-variant flex items-center gap-2 cursor-pointer hover:bg-d-surface-container transition-all">
                <MaterialIcon name="add" className="text-[16px]" /> Thêm
              </div>
            </div>
          </section>

          {/* Description */}
          <section>
            <div className="flex justify-between items-end mb-2">
              <label className="block font-d-body-sm text-d-body-sm text-d-on-surface-variant font-semibold uppercase tracking-wider text-[11px] opacity-70">Mô tả kho vựa</label>
              <span className="font-d-label-sm text-d-label-sm text-d-outline-variant">167/500</span>
            </div>
            <textarea 
              className="w-full bg-d-surface-container-low/50 border border-transparent rounded-xl px-4 py-3 text-d-body-md font-d-body-md text-d-on-surface focus:outline-none focus:bg-d-surface-container-lowest focus:ring-2 focus:ring-d-primary focus:border-transparent transition-all h-24 resize-none" 
              placeholder="Giới thiệu về kho vựa của bạn..."
              defaultValue="Chuyên thu mua các loại phế liệu công nghiệp, số lượng lớn. Đội ngũ bốc xếp chuyên nghiệp, thanh toán nhanh gọn. Hỗ trợ xe tải vận chuyển tận nơi trong khu vực nội thành."
            ></textarea>
          </section>

        </div>
        
        {/* Modal Footer */}
        <div className="px-6 py-5 border-t border-d-border-subtle bg-d-surface-container-lowest sticky bottom-0 z-10 flex justify-end gap-4 items-center shrink-0">
          <button 
            onClick={onClose}
            className="px-8 py-3 rounded-full border border-d-outline text-d-on-surface font-d-label-md text-d-label-md hover:-translate-y-[2px] active:scale-95 bg-transparent hover:bg-d-surface-container transition-all duration-200"
          >
            Hủy bỏ
          </button>
          <button className="px-8 py-3 rounded-full bg-d-primary-container text-d-on-primary-container font-bold text-d-label-md hover:-translate-y-[2px] active:scale-95 hover:shadow-lg flex items-center gap-2 transition-all duration-200">
            <MaterialIcon name="save" className="text-[20px]" /> 
            Lưu thay đổi
          </button>
        </div>

      </div>
    </div>
  );
};

export default EditProfileModal;
