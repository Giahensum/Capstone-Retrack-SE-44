import React from 'react';

// Using Material Icons inline to match other components
const MaterialIcon = ({ name, className, style }) => (
  <span className={`material-symbols-outlined ${className || ''}`} style={style}>{name}</span>
);

const Dashboard = () => {
  return (
    <div className="flex flex-col p-4 md:p-6 gap-4 md:gap-5 w-full h-[calc(100vh-4rem)] overflow-hidden">
      {/* Page Title */}
      <div className="flex flex-col md:flex-row md:items-end justify-between shrink-0">
        <div>
          <h1 className="font-d-headline-xl-mobile md:font-d-headline-xl text-d-headline-xl-mobile md:text-d-headline-xl text-d-on-surface">Bảng điều khiển</h1>
          <p className="font-d-body-md text-d-body-md text-d-on-surface-variant mt-2">Theo dõi hoạt động kho vựa hôm nay.</p>
        </div>
      </div>

      {/* KPI Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 shrink-0">
        {/* KPI 1 */}
        <div className="d-glass-panel rounded-xl p-4 md:p-5 relative overflow-hidden group">
          <div className="absolute top-0 right-0 w-24 h-24 bg-d-primary/5 rounded-bl-full -mr-4 -mt-4 transition-transform group-hover:scale-110"></div>
          <div className="flex justify-between items-start mb-3">
            <div className="w-10 h-10 rounded-lg bg-d-surface-container flex items-center justify-center text-d-primary">
              <MaterialIcon name="receipt_long" />
            </div>
            <span className="px-2.5 py-1 rounded-full bg-d-surface-accent text-d-secondary font-d-label-sm text-d-label-sm flex items-center gap-1">
              <MaterialIcon name="trending_up" className="text-[14px]" />
              +12%
            </span>
          </div>
          <div>
            <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant mb-1">Đơn hàng mới (Hôm nay)</p>
            <h3 className="font-d-headline-lg text-d-headline-lg text-d-on-surface">148</h3>
          </div>
        </div>

        {/* KPI 2 */}
        <div className="d-glass-panel rounded-xl p-4 md:p-5 relative overflow-hidden group">
          <div className="absolute top-0 right-0 w-24 h-24 bg-d-secondary/5 rounded-bl-full -mr-4 -mt-4 transition-transform group-hover:scale-110"></div>
          <div className="flex justify-between items-start mb-3">
            <div className="w-10 h-10 rounded-lg bg-d-surface-container flex items-center justify-center text-d-secondary">
              <MaterialIcon name="inventory" />
            </div>
            <span className="px-2.5 py-1 rounded-full bg-d-surface-accent text-d-secondary font-d-label-sm text-d-label-sm flex items-center gap-1">
              <MaterialIcon name="trending_up" className="text-[14px]" />
              +5.2%
            </span>
          </div>
          <div>
            <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant mb-1">Tồn kho hiện tại (Tấn)</p>
            <h3 className="font-d-headline-lg text-d-headline-lg text-d-on-surface">1,245.5</h3>
          </div>
        </div>

        {/* KPI 3 */}
        <div className="d-glass-panel rounded-xl p-4 md:p-5 relative overflow-hidden group">
          <div className="absolute top-0 right-0 w-24 h-24 bg-[#784f85]/5 rounded-bl-full -mr-4 -mt-4 transition-transform group-hover:scale-110"></div>
          <div className="flex justify-between items-start mb-3">
            <div className="w-10 h-10 rounded-lg bg-d-surface-container flex items-center justify-center text-[#784f85]">
              <MaterialIcon name="local_shipping" />
            </div>
            <span className="px-2.5 py-1 rounded-full bg-d-surface-container-highest text-d-on-surface-variant font-d-label-sm text-d-label-sm flex items-center gap-1">
              <MaterialIcon name="trending_flat" className="text-[14px]" />
              0%
            </span>
          </div>
          <div>
            <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant mb-1">Lô đang xuất</p>
            <h3 className="font-d-headline-lg text-d-headline-lg text-d-on-surface">3</h3>
          </div>
        </div>

        {/* KPI 4 */}
        <div className="d-glass-panel rounded-xl p-4 md:p-5 relative overflow-hidden group">
          <div className="absolute top-0 right-0 w-24 h-24 bg-d-primary/5 rounded-bl-full -mr-4 -mt-4 transition-transform group-hover:scale-110"></div>
          <div className="flex justify-between items-start mb-3">
            <div className="w-10 h-10 rounded-lg bg-d-surface-container flex items-center justify-center text-d-primary">
              <MaterialIcon name="payments" />
            </div>
            <span className="px-2.5 py-1 rounded-full bg-d-surface-accent text-d-secondary font-d-label-sm text-d-label-sm flex items-center gap-1">
              <MaterialIcon name="trending_up" className="text-[14px]" />
              +8.4%
            </span>
          </div>
          <div>
            <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant mb-1">Doanh thu dự kiến (VNĐ)</p>
            <h3 className="font-d-headline-lg text-d-headline-lg text-d-on-surface">452M</h3>
          </div>
        </div>
      </div>

      {/* Quick Actions Row */}
      <div className="flex flex-wrap gap-4 py-2 border-y border-d-border-subtle/50 shrink-0">
        <button className="px-6 py-2.5 rounded-full bg-d-surface-variant text-d-on-surface font-d-label-md text-d-label-md hover:bg-d-surface-dim transition-colors flex items-center gap-2">
          <MaterialIcon name="person_add" className="text-[18px]" />
          Thêm nhân sự
        </button>
        <button className="px-6 py-2.5 rounded-full border border-d-outline text-d-on-surface font-d-label-md text-d-label-md hover:bg-d-surface-variant transition-colors flex items-center gap-2">
          <MaterialIcon name="fact_check" className="text-[18px]" />
          Duyệt thanh toán
        </button>
        <button className="px-6 py-2.5 rounded-full bg-d-primary-container text-d-on-primary-container font-d-label-md text-d-label-md hover:bg-d-primary-fixed-dim hover:-translate-y-0.5 transition-transform flex items-center gap-2 shadow-sm">
          <MaterialIcon name="add_box" className="text-[18px]" />
          Tạo lô xuất
        </button>
      </div>

      {/* Two Column Layout: Table & Chart */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 flex-1 min-h-0 overflow-y-auto lg:overflow-hidden pb-4 md:pb-0">
        {/* Left: Recent Inbound Table (60%) */}
        <div className="lg:col-span-7 bg-d-surface-container-lowest rounded-[20px] p-5 border border-d-border-subtle shadow-[0_20px_40px_rgba(23,33,27,0.02)] flex flex-col h-full overflow-hidden">
          <div className="flex justify-between items-center mb-4 shrink-0">
            <h2 className="font-d-headline-md text-d-headline-md text-d-on-surface">Nhập kho gần đây</h2>
            <button className="text-d-primary font-d-label-sm text-d-label-sm hover:underline">Xem tất cả</button>
          </div>
          <div className="overflow-y-auto flex-1 min-h-0">
            <table className="w-full text-left relative">
              <thead className="sticky top-0 bg-d-surface-container-lowest z-10">
                <tr className="border-b border-d-border-subtle text-d-on-surface-variant font-d-label-sm text-d-label-sm uppercase tracking-wider">
                  <th className="py-2 font-medium">Mã đơn</th>
                  <th className="py-2 font-medium">Người bán</th>
                  <th className="py-2 font-medium">Vật liệu</th>
                  <th className="py-2 font-medium text-right">Khối lượng</th>
                  <th className="py-2 font-medium text-right">Trạng thái</th>
                </tr>
              </thead>
              <tbody className="font-d-body-sm text-d-body-sm divide-y divide-d-border-subtle">
                <tr className="hover:bg-d-surface-container-low transition-colors group">
                  <td className="py-2.5 text-d-on-surface font-medium">#INB-0421</td>
                  <td className="py-2.5 text-d-on-surface-variant flex items-center gap-2">
                    <div className="w-6 h-6 rounded-full bg-d-surface-variant flex items-center justify-center text-[10px] font-bold text-d-on-surface-variant">H</div>
                    Trần Hữu H.
                  </td>
                  <td className="py-2.5 text-d-on-surface-variant">Giấy carton</td>
                  <td className="py-2.5 text-d-on-surface text-right">450 kg</td>
                  <td className="py-2.5 text-right">
                    <span className="inline-flex items-center px-2 py-1 rounded-full bg-d-surface-accent text-d-secondary font-d-label-sm text-d-label-sm">Đã nhập</span>
                  </td>
                </tr>
                <tr className="hover:bg-d-surface-container-low transition-colors group">
                  <td className="py-2.5 text-d-on-surface font-medium">#INB-0420</td>
                  <td className="py-2.5 text-d-on-surface-variant flex items-center gap-2">
                    <div className="w-6 h-6 rounded-full bg-d-surface-variant flex items-center justify-center text-[10px] font-bold text-d-on-surface-variant">P</div>
                    Lê Thị P.
                  </td>
                  <td className="py-2.5 text-d-on-surface-variant">Nhựa PET</td>
                  <td className="py-2.5 text-d-on-surface text-right">125 kg</td>
                  <td className="py-2.5 text-right">
                    <span className="inline-flex items-center px-2 py-1 rounded-full bg-d-surface-container-highest text-d-on-surface-variant font-d-label-sm text-d-label-sm">Chờ phân loại</span>
                  </td>
                </tr>
                <tr className="hover:bg-d-surface-container-low transition-colors group">
                  <td className="py-2.5 text-d-on-surface font-medium">#INB-0419</td>
                  <td className="py-2.5 text-d-on-surface-variant flex items-center gap-2">
                    <div className="w-6 h-6 rounded-full bg-d-surface-variant flex items-center justify-center text-[10px] font-bold text-d-on-surface-variant">T</div>
                    Công ty TNHH T.
                  </td>
                  <td className="py-2.5 text-d-on-surface-variant">Sắt thép</td>
                  <td className="py-2.5 text-d-on-surface text-right">1,200 kg</td>
                  <td className="py-2.5 text-right">
                    <span className="inline-flex items-center px-2 py-1 rounded-full bg-d-surface-accent text-d-secondary font-d-label-sm text-d-label-sm">Đã nhập</span>
                  </td>
                </tr>
                <tr className="hover:bg-d-surface-container-low transition-colors group">
                  <td className="py-2.5 text-d-on-surface font-medium">#INB-0418</td>
                  <td className="py-2.5 text-d-on-surface-variant flex items-center gap-2">
                    <div className="w-6 h-6 rounded-full bg-d-surface-variant flex items-center justify-center text-[10px] font-bold text-d-on-surface-variant">V</div>
                    Nguyễn Văn V.
                  </td>
                  <td className="py-2.5 text-d-on-surface-variant">Đồng cáp</td>
                  <td className="py-2.5 text-d-on-surface text-right">45 kg</td>
                  <td className="py-2.5 text-right">
                    <span className="inline-flex items-center px-2 py-1 rounded-full bg-d-surface-accent text-d-secondary font-d-label-sm text-d-label-sm">Đã nhập</span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        {/* Right: Inventory Donut Chart (40%) */}
        <div className="lg:col-span-5 bg-d-surface-container-lowest rounded-[20px] p-5 border border-d-border-subtle shadow-[0_20px_40px_rgba(23,33,27,0.02)] flex flex-col h-full overflow-hidden">
          <h2 className="font-d-headline-md text-d-headline-md text-d-on-surface mb-2 shrink-0">Cơ cấu tồn kho</h2>
          <div className="flex-1 flex flex-col items-center justify-center relative min-h-0">
            {/* Simple CSS SVG Donut Chart representation */}
            <svg className="w-40 h-40 md:w-36 md:h-36 drop-shadow-md shrink-0" viewBox="0 0 100 100">
              <circle cx="50" cy="50" fill="transparent" r="40" stroke="#dae5dc" strokeWidth="20"></circle>
              {/* Paper 40% */}
              <circle className="d-donut-segment" cx="50" cy="50" fill="transparent" r="40" stroke="#a3e635" strokeDasharray="251.2" strokeDashoffset="150.72" strokeWidth="20" transform="rotate(-90 50 50)"></circle>
              {/* Plastic 30% */}
              <circle className="d-donut-segment" cx="50" cy="50" fill="transparent" r="40" stroke="#006c49" strokeDasharray="251.2" strokeDashoffset="175.84" strokeWidth="20" transform="rotate(54 50 50)"></circle>
              {/* Metal 20% */}
              <circle className="d-donut-segment" cx="50" cy="50" fill="transparent" r="40" stroke="#4edea3" strokeDasharray="251.2" strokeDashoffset="200.96" strokeWidth="20" transform="rotate(162 50 50)"></circle>
              {/* Glass 10% */}
              <circle className="d-donut-segment" cx="50" cy="50" fill="transparent" r="40" stroke="#e7b6f3" strokeDasharray="251.2" strokeDashoffset="226.08" strokeWidth="20" transform="rotate(234 50 50)"></circle>
            </svg>
            <div className="absolute inset-0 flex flex-col items-center justify-center pointer-events-none">
              <span className="font-d-headline-lg text-d-headline-lg text-d-on-surface font-bold">1.2k</span>
              <span className="font-d-label-sm text-d-label-sm text-d-on-surface-variant">Tấn</span>
            </div>
          </div>
          <div className="grid grid-cols-2 gap-3 mt-4 shrink-0">
            <div className="flex items-center gap-2">
              <div className="w-3 h-3 rounded-full bg-d-primary-container"></div>
              <span className="font-d-body-sm text-d-body-sm text-d-on-surface-variant">Giấy (40%)</span>
            </div>
            <div className="flex items-center gap-2">
              <div className="w-3 h-3 rounded-full bg-d-secondary"></div>
              <span className="font-d-body-sm text-d-body-sm text-d-on-surface-variant">Nhựa (30%)</span>
            </div>
            <div className="flex items-center gap-2">
              <div className="w-3 h-3 rounded-full bg-[#4edea3]"></div>
              <span className="font-d-body-sm text-d-body-sm text-d-on-surface-variant">Kim loại (20%)</span>
            </div>
            <div className="flex items-center gap-2">
              <div className="w-3 h-3 rounded-full bg-[#e7b6f3]"></div>
              <span className="font-d-body-sm text-d-body-sm text-d-on-surface-variant">Thủy tinh (10%)</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default Dashboard;
