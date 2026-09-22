import React from 'react';
import { MaterialIcon } from '../../components/ui/MaterialIcon';

const StaffPerformance = () => {
  return (
    <div className="flex-1 p-4 md:p-6 w-full flex flex-col gap-8 h-[calc(100vh-4rem)] overflow-y-auto">
      {/* Page Header */}
      <div className="flex flex-col md:flex-row justify-between items-start md:items-end gap-4 shrink-0">
        <div>
          <h2 className="font-d-headline-lg text-d-headline-lg text-d-on-surface">Hiệu Suất Nhân Sự</h2>
          <p className="font-d-body-md text-d-body-md text-d-on-surface-variant mt-2 max-w-2xl">Đánh giá năng suất và chất lượng công việc của nhân viên thu gom.</p>
        </div>
        <div className="relative">
          <button className="bg-d-surface-container-lowest border border-d-border-subtle px-4 py-2 rounded-lg flex items-center gap-2 font-d-label-md text-d-on-surface hover:border-d-primary transition-colors focus:border-d-primary focus:outline-none">
            <MaterialIcon name="calendar_today" className="text-d-on-surface-variant" />
            Tháng 8 / 2026
            <MaterialIcon name="arrow_drop_down" className="text-d-on-surface-variant" />
          </button>
        </div>
      </div>

      {/* Top Section */}
      <div className="grid grid-cols-1 md:grid-cols-12 gap-6 shrink-0">
        {/* Left: KPI Cards */}
        <div className="md:col-span-4 flex flex-col gap-4">
          <div className="bg-white rounded-[20px] border border-d-border-subtle p-6 flex flex-col gap-2">
            <div className="flex justify-between items-center">
              <span className="font-d-label-md text-d-on-surface-variant uppercase tracking-wider">Tổng đơn hoàn thành</span>
              <MaterialIcon name="check_circle" className="text-d-secondary" />
            </div>
            <div className="flex items-baseline gap-3">
              <span className="font-d-headline-xl text-d-headline-xl text-d-on-surface">342</span>
              <span className="font-d-body-sm text-d-body-sm text-d-secondary bg-d-surface-accent px-2 py-1 rounded-full">+15%</span>
            </div>
          </div>
          
          <div className="bg-white rounded-[20px] border border-d-border-subtle p-6 flex flex-col gap-2">
            <div className="flex justify-between items-center">
              <span className="font-d-label-md text-d-on-surface-variant uppercase tracking-wider">Tổng khối lượng</span>
              <MaterialIcon name="scale" className="text-d-primary" />
            </div>
            <div className="flex items-baseline gap-3">
              <span className="font-d-headline-xl text-d-headline-xl text-d-on-surface">18.5</span>
              <span className="font-d-body-md text-d-body-md text-d-on-surface-variant">Tấn</span>
            </div>
          </div>
          
          <div className="bg-white rounded-[20px] border border-d-border-subtle p-6 flex flex-col gap-2">
            <div className="flex justify-between items-center">
              <span className="font-d-label-md text-d-on-surface-variant uppercase tracking-wider">Đánh giá trung bình</span>
              <MaterialIcon name="star" className="text-d-tertiary" />
            </div>
            <div className="flex items-baseline gap-3">
              <span className="font-d-headline-xl text-d-headline-xl text-d-on-surface">4.8</span>
              <span className="font-d-body-md text-d-body-md text-d-on-surface-variant">/ 5.0</span>
            </div>
          </div>
        </div>

        {/* Right: Bar Chart */}
        <div className="md:col-span-8 bg-white rounded-[20px] border border-d-border-subtle p-6 md:p-8 flex flex-col h-full">
          <h3 className="font-d-headline-md text-d-headline-md text-d-on-surface mb-6">Top 5 Nhân viên năng suất nhất</h3>
          <div className="flex-1 flex flex-col justify-around gap-4">
            {/* Bar 1 */}
            <div className="flex items-center gap-4">
              <div className="flex items-center gap-3 w-48 shrink-0">
                <img className="w-10 h-10 rounded-full object-cover bg-d-surface-container" src="https://ui-avatars.com/api/?name=Nguyen+Van+A&background=random" alt="Nguyễn Văn A" />
                <span className="font-d-body-md text-d-body-md font-medium text-d-on-surface truncate">Nguyễn Văn A</span>
              </div>
              <div className="flex-1 bg-d-surface-container-high h-6 rounded-full overflow-hidden flex items-center relative">
                <div className="bg-d-primary-container h-full w-[95%] rounded-full absolute left-0 top-0"></div>
                <span className="absolute right-4 font-d-label-sm text-d-on-primary-container z-10 font-bold">5.2 Tấn</span>
              </div>
            </div>
            {/* Bar 2 */}
            <div className="flex items-center gap-4">
              <div className="flex items-center gap-3 w-48 shrink-0">
                <img className="w-10 h-10 rounded-full object-cover bg-d-surface-container" src="https://ui-avatars.com/api/?name=Tran+Thi+B&background=random" alt="Trần Thị B" />
                <span className="font-d-body-md text-d-body-md font-medium text-d-on-surface truncate">Trần Thị B</span>
              </div>
              <div className="flex-1 bg-d-surface-container-high h-6 rounded-full overflow-hidden flex items-center relative">
                <div className="bg-d-primary-container h-full w-[80%] rounded-full absolute left-0 top-0 opacity-90"></div>
                <span className="absolute right-[22%] font-d-label-sm text-d-on-primary-container z-10 font-bold">4.4 Tấn</span>
              </div>
            </div>
            {/* Bar 3 */}
            <div className="flex items-center gap-4">
              <div className="flex items-center gap-3 w-48 shrink-0">
                <img className="w-10 h-10 rounded-full object-cover bg-d-surface-container" src="https://ui-avatars.com/api/?name=Le+Hoang+C&background=random" alt="Lê Hoàng C" />
                <span className="font-d-body-md text-d-body-md font-medium text-d-on-surface truncate">Lê Hoàng C</span>
              </div>
              <div className="flex-1 bg-d-surface-container-high h-6 rounded-full overflow-hidden flex items-center relative">
                <div className="bg-d-primary-container h-full w-[70%] rounded-full absolute left-0 top-0 opacity-80"></div>
                <span className="absolute right-[32%] font-d-label-sm text-d-on-primary-container z-10 font-bold">3.8 Tấn</span>
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* Bottom Section: Data Table */}
      <div className="bg-white rounded-[20px] border border-d-border-subtle overflow-hidden shrink-0">
        <div className="p-6 border-b border-d-border-subtle bg-d-surface-accent/30">
          <h3 className="font-d-headline-md text-d-headline-md text-d-on-surface">Chi Tiết Năng Suất Từng Nhân Viên</h3>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse min-w-[800px]">
            <thead>
              <tr className="bg-d-surface-container-low border-b border-d-border-subtle">
                <th className="p-4 font-d-label-sm text-d-label-sm text-d-on-surface-variant font-medium">HẠNG</th>
                <th className="p-4 font-d-label-sm text-d-label-sm text-d-on-surface-variant font-medium">NHÂN VIÊN</th>
                <th className="p-4 font-d-label-sm text-d-label-sm text-d-on-surface-variant font-medium text-right">SỐ ĐƠN HOÀN THÀNH</th>
                <th className="p-4 font-d-label-sm text-d-label-sm text-d-on-surface-variant font-medium text-right">TỔNG KHỐI LƯỢNG</th>
                <th className="p-4 font-d-label-sm text-d-label-sm text-d-on-surface-variant font-medium text-right">TỶ LỆ HỦY ĐƠN</th>
                <th className="p-4 font-d-label-sm text-d-label-sm text-d-on-surface-variant font-medium text-center">ĐÁNH GIÁ (★)</th>
                <th className="p-4 font-d-label-sm text-d-label-sm text-d-on-surface-variant font-medium text-center">ĐÁNH GIÁ TIÊU CỰC</th>
              </tr>
            </thead>
            <tbody className="font-d-body-sm text-d-body-sm">
              <tr className="border-b border-d-border-subtle hover:bg-d-surface transition-colors">
                <td className="p-4 text-center">
                  <MaterialIcon name="workspace_premium" className="text-[#FFD700]" />
                </td>
                <td className="p-4 flex items-center gap-3">
                  <img className="w-8 h-8 rounded-full object-cover" src="https://ui-avatars.com/api/?name=Nguyen+Van+A&background=random" alt="Nguyễn Văn A" />
                  <span className="font-medium text-d-on-surface">Nguyễn Văn A</span>
                </td>
                <td className="p-4 text-right text-d-on-surface">120</td>
                <td className="p-4 text-right text-d-on-surface">5.2 T</td>
                <td className="p-4 text-right text-d-secondary">1.2%</td>
                <td className="p-4 text-center font-medium text-d-on-surface">4.9</td>
                <td className="p-4 text-center text-d-on-surface-variant">0</td>
              </tr>
              <tr className="border-b border-d-border-subtle hover:bg-d-surface transition-colors">
                <td className="p-4 text-center">
                  <MaterialIcon name="workspace_premium" className="text-[#C0C0C0]" />
                </td>
                <td className="p-4 flex items-center gap-3">
                  <img className="w-8 h-8 rounded-full object-cover" src="https://ui-avatars.com/api/?name=Tran+Thi+B&background=random" alt="Trần Thị B" />
                  <span className="font-medium text-d-on-surface">Trần Thị B</span>
                </td>
                <td className="p-4 text-right text-d-on-surface">98</td>
                <td className="p-4 text-right text-d-on-surface">4.4 T</td>
                <td className="p-4 text-right text-d-secondary">2.5%</td>
                <td className="p-4 text-center font-medium text-d-on-surface">4.8</td>
                <td className="p-4 text-center text-d-on-surface-variant">1</td>
              </tr>
              <tr className="border-b border-d-border-subtle hover:bg-d-surface transition-colors">
                <td className="p-4 text-center">
                  <MaterialIcon name="workspace_premium" className="text-[#CD7F32]" />
                </td>
                <td className="p-4 flex items-center gap-3">
                  <img className="w-8 h-8 rounded-full object-cover" src="https://ui-avatars.com/api/?name=Le+Hoang+C&background=random" alt="Lê Hoàng C" />
                  <span className="font-medium text-d-on-surface">Lê Hoàng C</span>
                </td>
                <td className="p-4 text-right text-d-on-surface">85</td>
                <td className="p-4 text-right text-d-on-surface">3.8 T</td>
                <td className="p-4 text-right text-d-secondary">3.0%</td>
                <td className="p-4 text-center font-medium text-d-on-surface">4.7</td>
                <td className="p-4 text-center text-d-on-surface-variant">0</td>
              </tr>
              <tr className="bg-d-error-container/20 border-b border-d-border-subtle hover:bg-d-error-container/30 transition-colors">
                <td className="p-4 text-center font-bold text-d-on-surface-variant">4</td>
                <td className="p-4 flex items-center gap-3">
                  <img className="w-8 h-8 rounded-full object-cover" src="https://ui-avatars.com/api/?name=Pham+Minh&background=random" alt="Phạm Minh" />
                  <span className="font-medium text-d-on-surface">Phạm Minh</span>
                </td>
                <td className="p-4 text-right text-d-on-surface">42</td>
                <td className="p-4 text-right text-d-on-surface">1.8 T</td>
                <td className="p-4 text-right text-d-error font-bold">12.5%</td>
                <td className="p-4 text-center font-medium text-d-error">3.8</td>
                <td className="p-4 text-center text-d-error font-bold">5</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};

export default StaffPerformance;
