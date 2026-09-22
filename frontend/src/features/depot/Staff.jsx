import React from 'react';
import { MaterialIcon } from '../../components/ui/MaterialIcon';

const Staff = () => {
  return (
    <div className="flex-1 p-4 md:p-6 w-full flex flex-col gap-8 h-[calc(100vh-4rem)] overflow-y-auto bg-d-background">
      {/* Page Header */}
      <section className="flex flex-col md:flex-row md:items-end justify-between gap-4 shrink-0">
        <div>
          <h2 className="font-d-headline-xl text-d-headline-xl text-d-on-surface">Quản Lý Nhân Sự</h2>
          <p className="font-d-body-md text-d-body-md text-d-on-surface-variant mt-2">Quản lý danh sách nhân viên và trạng thái làm việc.</p>
        </div>
        <button className="bg-d-primary-fixed text-d-on-primary-fixed font-d-label-md text-d-label-md py-3 px-6 rounded-full hover:bg-d-secondary-fixed transition-all duration-200 hover:-translate-y-0.5 shadow-sm flex items-center gap-2 shrink-0">
          <MaterialIcon name="add" className="text-[20px]" />
          Thêm nhân viên
        </button>
      </section>

      {/* KPI Cards (Bento style) */}
      <section className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6 shrink-0">
        {/* KPI 1 */}
        <div className="bg-white border border-d-border-subtle rounded-[20px] p-6 shadow-sm flex flex-col gap-4 relative overflow-hidden group">
          <div className="absolute -right-4 -top-4 w-24 h-24 bg-d-primary/5 rounded-full blur-2xl group-hover:bg-d-primary/10 transition-colors duration-500"></div>
          <div className="flex justify-between items-start">
            <div className="w-10 h-10 rounded-full bg-d-primary/10 flex items-center justify-center text-d-primary">
              <MaterialIcon name="groups" />
            </div>
            <span className="bg-d-surface-variant text-d-on-surface-variant px-2 py-1 rounded-full font-d-label-sm text-d-label-sm flex items-center gap-1">
              <MaterialIcon name="trending_up" className="text-[14px]" /> 12%
            </span>
          </div>
          <div>
            <p className="font-d-label-md text-d-label-md text-d-on-surface-variant mb-1">Tổng nhân viên</p>
            <p className="font-d-headline-lg text-d-headline-lg text-d-on-surface">12</p>
          </div>
        </div>

        {/* KPI 2 */}
        <div className="bg-white border border-d-border-subtle rounded-[20px] p-6 shadow-sm flex flex-col gap-4 relative overflow-hidden group">
          <div className="absolute -right-4 -top-4 w-24 h-24 bg-d-secondary/5 rounded-full blur-2xl group-hover:bg-d-secondary/10 transition-colors duration-500"></div>
          <div className="flex justify-between items-start">
            <div className="w-10 h-10 rounded-full bg-d-secondary/10 flex items-center justify-center text-d-secondary">
              <MaterialIcon name="check_circle" />
            </div>
          </div>
          <div>
            <p className="font-d-label-md text-d-label-md text-d-on-surface-variant mb-1">Đang hoạt động</p>
            <p className="font-d-headline-lg text-d-headline-lg text-d-on-surface">8</p>
          </div>
        </div>

        {/* KPI 3 */}
        <div className="bg-white border border-d-border-subtle rounded-[20px] p-6 shadow-sm flex flex-col gap-4 relative overflow-hidden group">
          <div className="absolute -right-4 -top-4 w-24 h-24 bg-[orange]/5 rounded-full blur-2xl group-hover:bg-[orange]/10 transition-colors duration-500"></div>
          <div className="flex justify-between items-start">
            <div className="w-10 h-10 rounded-full bg-[orange]/10 flex items-center justify-center text-[orange]">
              <MaterialIcon name="local_shipping" />
            </div>
          </div>
          <div>
            <p className="font-d-label-md text-d-label-md text-d-on-surface-variant mb-1">Đang đi thu gom</p>
            <p className="font-d-headline-lg text-d-headline-lg text-d-on-surface">3</p>
          </div>
        </div>

        {/* KPI 4 */}
        <div className="bg-white border border-d-border-subtle rounded-[20px] p-6 shadow-sm flex flex-col gap-4 relative overflow-hidden group">
          <div className="absolute -right-4 -top-4 w-24 h-24 bg-d-surface-variant/20 rounded-full blur-2xl group-hover:bg-d-surface-variant/40 transition-colors duration-500"></div>
          <div className="flex justify-between items-start">
            <div className="w-10 h-10 rounded-full bg-d-surface-variant flex items-center justify-center text-d-on-surface-variant">
              <MaterialIcon name="pause_circle" />
            </div>
          </div>
          <div>
            <p className="font-d-label-md text-d-label-md text-d-on-surface-variant mb-1">Tạm nghỉ / Off</p>
            <p className="font-d-headline-lg text-d-headline-lg text-d-on-surface">1</p>
          </div>
        </div>
      </section>

      {/* Data Table Section */}
      <section className="bg-white border border-d-border-subtle rounded-[20px] shadow-sm overflow-hidden flex flex-col shrink-0">
        {/* Filter Bar */}
        <div className="p-6 border-b border-d-border-subtle flex flex-col md:flex-row gap-4 items-center justify-between bg-d-surface-accent/30">
          <div className="relative w-full md:w-[40%]">
            <MaterialIcon name="search" className="absolute left-3 top-1/2 -translate-y-1/2 text-d-on-surface-variant" />
            <input 
              className="w-full bg-white border border-d-border-subtle rounded-full py-2.5 pl-10 pr-4 font-d-body-sm text-d-body-sm focus:border-d-secondary focus:outline-none transition-colors" 
              placeholder="Tìm kiếm nhân viên..." 
              type="text" 
            />
          </div>
          <div className="flex gap-4 w-full md:w-auto">
            <select className="flex-1 md:w-40 bg-white border border-d-border-subtle rounded-full py-2.5 px-4 font-d-body-sm text-d-body-sm appearance-none focus:border-d-secondary focus:outline-none cursor-pointer">
              <option value="">Vai trò</option>
              <option value="thu-gom">NV Thu gom</option>
              <option value="phan-loai">NV Phân loại</option>
            </select>
            <select className="flex-1 md:w-40 bg-white border border-d-border-subtle rounded-full py-2.5 px-4 font-d-body-sm text-d-body-sm appearance-none focus:border-d-secondary focus:outline-none cursor-pointer">
              <option value="">Trạng thái</option>
              <option value="dang-lam">Đang làm việc</option>
              <option value="thu-gom">Đang thu gom</option>
              <option value="tam-nghi">Tạm nghỉ</option>
            </select>
          </div>
        </div>

        {/* Table */}
        <div className="overflow-x-auto w-full">
          <table className="w-full text-left border-collapse min-w-[900px]">
            <thead>
              <tr className="bg-d-surface-container-low text-d-on-surface-variant font-d-label-md text-d-label-md uppercase tracking-wider">
                <th className="px-6 py-4 font-medium">NHÂN VIÊN</th>
                <th className="px-6 py-4 font-medium">SỐ ĐIỆN THOẠI</th>
                <th className="px-6 py-4 font-medium">VAI TRÒ</th>
                <th className="px-6 py-4 font-medium">BIỂN SỐ XE</th>
                <th className="px-6 py-4 font-medium">TRẠNG THÁI HIỆN TẠI</th>
                <th className="px-6 py-4 font-medium">ĐÁNH GIÁ</th>
                <th className="px-6 py-4 font-medium text-center">HÀNH ĐỘNG</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-d-border-subtle font-d-body-sm text-d-body-sm text-d-on-surface">
              {/* Row 1 */}
              <tr className="hover:bg-d-surface-accent/20 transition-colors group">
                <td className="px-6 py-4">
                  <div className="flex items-center gap-3">
                    <div className="w-10 h-10 rounded-full overflow-hidden border border-d-border-subtle">
                      <img className="w-full h-full object-cover" src="https://ui-avatars.com/api/?name=Tran+Hoang+Long&background=random" alt="Trần Hoàng Long" />
                    </div>
                    <span className="font-medium text-d-on-surface">Trần Hoàng Long</span>
                  </div>
                </td>
                <td className="px-6 py-4 text-d-on-surface-variant">090 111 2222</td>
                <td className="px-6 py-4">
                  <span className="bg-d-surface-variant/50 text-d-on-surface-variant px-2.5 py-1 rounded-md">NV Thu gom</span>
                </td>
                <td className="px-6 py-4 font-d-label-sm text-d-label-sm">59-X1 123.45</td>
                <td className="px-6 py-4">
                  <div className="flex items-center gap-2">
                    <div className="w-2 h-2 rounded-full bg-[orange]"></div>
                    <span>🛵 Đang thu gom</span>
                  </div>
                </td>
                <td className="px-6 py-4">
                  <div className="flex items-center gap-1">
                    <MaterialIcon name="star" className="text-[16px] text-yellow-500" />
                    <span className="font-medium">4.8</span>
                  </div>
                </td>
                <td className="px-6 py-4 text-center">
                  <button className="p-1.5 text-d-on-surface-variant hover:text-d-primary rounded-full hover:bg-d-surface-variant transition-colors">
                    <MaterialIcon name="more_vert" className="text-[20px]" />
                  </button>
                </td>
              </tr>
              {/* Row 2 */}
              <tr className="hover:bg-d-surface-accent/20 transition-colors group">
                <td className="px-6 py-4">
                  <div className="flex items-center gap-3">
                    <div className="w-10 h-10 rounded-full overflow-hidden border border-d-border-subtle">
                      <img className="w-full h-full object-cover" src="https://ui-avatars.com/api/?name=Le+Van+Tuan&background=random" alt="Lê Văn Tuấn" />
                    </div>
                    <span className="font-medium text-d-on-surface">Lê Văn Tuấn</span>
                  </div>
                </td>
                <td className="px-6 py-4 text-d-on-surface-variant">091 222 3333</td>
                <td className="px-6 py-4">
                  <span className="bg-d-surface-variant/50 text-d-on-surface-variant px-2.5 py-1 rounded-md">NV Thu gom</span>
                </td>
                <td className="px-6 py-4 font-d-label-sm text-d-label-sm">59-Y2 678.90</td>
                <td className="px-6 py-4">
                  <div className="flex items-center gap-2">
                    <div className="w-2 h-2 rounded-full bg-d-secondary"></div>
                    <span>Đang rảnh</span>
                  </div>
                </td>
                <td className="px-6 py-4">
                  <div className="flex items-center gap-1">
                    <MaterialIcon name="star" className="text-[16px] text-yellow-500" />
                    <span className="font-medium">4.9</span>
                  </div>
                </td>
                <td className="px-6 py-4 text-center">
                  <button className="p-1.5 text-d-on-surface-variant hover:text-d-primary rounded-full hover:bg-d-surface-variant transition-colors">
                    <MaterialIcon name="more_vert" className="text-[20px]" />
                  </button>
                </td>
              </tr>
              {/* Row 3 */}
              <tr className="hover:bg-d-surface-accent/20 transition-colors group">
                <td className="px-6 py-4">
                  <div className="flex items-center gap-3">
                    <div className="w-10 h-10 rounded-full overflow-hidden border border-d-border-subtle bg-d-surface-container flex items-center justify-center text-d-primary font-bold">
                      HT
                    </div>
                    <span className="font-medium text-d-on-surface">Hoàng Thị Thu</span>
                  </div>
                </td>
                <td className="px-6 py-4 text-d-on-surface-variant">098 333 4444</td>
                <td className="px-6 py-4">
                  <span className="bg-d-surface-variant/50 text-d-on-surface-variant px-2.5 py-1 rounded-md">NV Phân loại</span>
                </td>
                <td className="px-6 py-4 font-d-label-sm text-d-label-sm text-d-on-surface-variant">—</td>
                <td className="px-6 py-4">
                  <div className="flex items-center gap-2">
                    <div className="w-2 h-2 rounded-full bg-d-secondary"></div>
                    <span>Đang làm việc</span>
                  </div>
                </td>
                <td className="px-6 py-4">
                  <div className="flex items-center gap-1">
                    <MaterialIcon name="star" className="text-[16px] text-yellow-500" />
                    <span className="font-medium">5.0</span>
                  </div>
                </td>
                <td className="px-6 py-4 text-center">
                  <button className="p-1.5 text-d-on-surface-variant hover:text-d-primary rounded-full hover:bg-d-surface-variant transition-colors">
                    <MaterialIcon name="more_vert" className="text-[20px]" />
                  </button>
                </td>
              </tr>
              {/* Row 4 */}
              <tr className="hover:bg-d-surface-accent/20 transition-colors group">
                <td className="px-6 py-4">
                  <div className="flex items-center gap-3">
                    <div className="w-10 h-10 rounded-full overflow-hidden border border-d-border-subtle bg-d-surface-container flex items-center justify-center text-d-primary font-bold">
                      PM
                    </div>
                    <span className="font-medium text-d-on-surface">Phạm Minh</span>
                  </div>
                </td>
                <td className="px-6 py-4 text-d-on-surface-variant">090 555 6666</td>
                <td className="px-6 py-4">
                  <span className="bg-d-surface-variant/50 text-d-on-surface-variant px-2.5 py-1 rounded-md">NV Thu gom</span>
                </td>
                <td className="px-6 py-4 font-d-label-sm text-d-label-sm">59-Z3 111.22</td>
                <td className="px-6 py-4">
                  <div className="flex items-center gap-2 text-d-on-surface-variant">
                    <div className="w-2 h-2 rounded-full border-2 border-d-on-surface-variant"></div>
                    <span>Tạm nghỉ</span>
                  </div>
                </td>
                <td className="px-6 py-4">
                  <div className="flex items-center gap-1">
                    <MaterialIcon name="star" className="text-[16px] text-yellow-500" />
                    <span className="font-medium">4.2</span>
                  </div>
                </td>
                <td className="px-6 py-4 text-center">
                  <button className="p-1.5 text-d-on-surface-variant hover:text-d-primary rounded-full hover:bg-d-surface-variant transition-colors">
                    <MaterialIcon name="more_vert" className="text-[20px]" />
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
};

export default Staff;
