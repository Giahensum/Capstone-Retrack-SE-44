import React, { useState } from 'react';
import { MaterialIcon } from '../../components/ui/MaterialIcon';

const RevenueReport = () => {
  const [dateRange, setDateRange] = useState('Tháng này');

  return (
    <div className="flex-1 p-4 md:p-6 w-full flex flex-col gap-8 h-[calc(100vh-4rem)] overflow-y-auto bg-d-background">
      {/* Page Header */}
      <section className="flex flex-col md:flex-row md:items-center justify-between gap-4 shrink-0">
        <div>
          <h2 className="font-d-headline-lg text-d-headline-lg text-d-on-surface">Báo cáo doanh thu</h2>
          <p className="font-d-body-md text-d-body-md text-d-on-surface-variant mt-1">Theo dõi dòng tiền và hiệu quả kinh doanh của kho vựa.</p>
        </div>
        <div className="flex items-center gap-3">
          <div className="relative">
            <select 
              value={dateRange}
              onChange={(e) => setDateRange(e.target.value)}
              className="appearance-none bg-white border border-d-border-subtle rounded-full pl-4 pr-10 py-2 font-d-label-md text-d-label-md text-d-on-surface focus:outline-none focus:ring-2 focus:ring-d-primary focus:border-transparent cursor-pointer shadow-sm"
            >
              <option value="Tuần này">Tuần này</option>
              <option value="Tháng này">Tháng này</option>
              <option value="Quý này">Quý này</option>
              <option value="Năm nay">Năm nay</option>
            </select>
            <MaterialIcon name="expand_more" className="absolute right-3 top-1/2 -translate-y-1/2 text-d-on-surface-variant pointer-events-none" />
          </div>
          <button className="bg-white border border-d-border-subtle hover:bg-d-surface-container rounded-full px-4 py-2 font-d-label-md text-d-label-md text-d-on-surface flex items-center gap-2 transition-colors shadow-sm">
            <MaterialIcon name="download" className="text-[18px]" />
            Xuất dữ liệu
          </button>
        </div>
      </section>

      {/* KPIs Bento Grid */}
      <section className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 shrink-0">
        {/* KPI 1 */}
        <div className="bg-white rounded-2xl p-6 border border-d-border-subtle shadow-sm relative overflow-hidden group hover:shadow-md transition-shadow">
          <div className="absolute -right-4 -top-4 w-24 h-24 bg-d-primary/10 rounded-full blur-xl group-hover:bg-d-primary/20 transition-colors"></div>
          <div className="flex justify-between items-start mb-4 relative z-10">
            <div className="w-10 h-10 rounded-xl bg-d-primary-container text-d-on-primary-container flex items-center justify-center">
              <MaterialIcon name="account_balance_wallet" className="text-[20px]" />
            </div>
            <span className="inline-flex items-center gap-1 px-2 py-1 rounded-full bg-d-surface-accent text-d-secondary font-d-label-sm text-d-label-sm">
              <MaterialIcon name="trending_up" className="text-[14px]" />
              +15.2%
            </span>
          </div>
          <div className="relative z-10">
            <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant mb-1">Tổng doanh thu ({dateRange})</p>
            <h3 className="font-d-headline-lg text-d-headline-lg text-d-on-surface font-bold">142.5<span className="text-xl ml-1 text-d-on-surface-variant font-normal">Tr</span></h3>
          </div>
        </div>

        {/* KPI 2 */}
        <div className="bg-white rounded-2xl p-6 border border-d-border-subtle shadow-sm relative overflow-hidden group hover:shadow-md transition-shadow">
          <div className="absolute -right-4 -top-4 w-24 h-24 bg-d-secondary/10 rounded-full blur-xl group-hover:bg-d-secondary/20 transition-colors"></div>
          <div className="flex justify-between items-start mb-4 relative z-10">
            <div className="w-10 h-10 rounded-xl bg-d-secondary-container text-d-on-secondary-container flex items-center justify-center">
              <MaterialIcon name="inventory" className="text-[20px]" />
            </div>
            <span className="inline-flex items-center gap-1 px-2 py-1 rounded-full bg-d-surface-accent text-d-secondary font-d-label-sm text-d-label-sm">
              <MaterialIcon name="trending_up" className="text-[14px]" />
              +8.4%
            </span>
          </div>
          <div className="relative z-10">
            <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant mb-1">Khối lượng bán ra</p>
            <h3 className="font-d-headline-lg text-d-headline-lg text-d-on-surface font-bold">24.5<span className="text-xl ml-1 text-d-on-surface-variant font-normal">Tấn</span></h3>
          </div>
        </div>

        {/* KPI 3 */}
        <div className="bg-white rounded-2xl p-6 border border-d-border-subtle shadow-sm relative overflow-hidden group hover:shadow-md transition-shadow">
          <div className="absolute -right-4 -top-4 w-24 h-24 bg-d-tertiary/10 rounded-full blur-xl group-hover:bg-d-tertiary/20 transition-colors"></div>
          <div className="flex justify-between items-start mb-4 relative z-10">
            <div className="w-10 h-10 rounded-xl bg-d-tertiary-container text-d-on-tertiary-container flex items-center justify-center">
              <MaterialIcon name="receipt_long" className="text-[20px]" />
            </div>
            <span className="inline-flex items-center gap-1 px-2 py-1 rounded-full bg-d-error-container text-d-on-error-container font-d-label-sm text-d-label-sm">
              <MaterialIcon name="trending_down" className="text-[14px]" />
              -2.1%
            </span>
          </div>
          <div className="relative z-10">
            <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant mb-1">Số giao dịch</p>
            <h3 className="font-d-headline-lg text-d-headline-lg text-d-on-surface font-bold">128<span className="text-xl ml-1 text-d-on-surface-variant font-normal">Đơn</span></h3>
          </div>
        </div>

        {/* KPI 4 */}
        <div className="bg-d-inverse-surface rounded-2xl p-6 shadow-sm relative overflow-hidden group hover:shadow-md transition-shadow">
          <div className="absolute -right-4 -top-4 w-24 h-24 bg-d-primary/20 rounded-full blur-xl transition-colors"></div>
          <div className="flex justify-between items-start mb-4 relative z-10">
            <div className="w-10 h-10 rounded-xl bg-white/10 text-white flex items-center justify-center backdrop-blur-sm">
              <MaterialIcon name="savings" className="text-[20px]" />
            </div>
          </div>
          <div className="relative z-10">
            <p className="font-d-body-sm text-d-body-sm text-d-inverse-on-surface opacity-80 mb-1">Lợi nhuận gộp ước tính</p>
            <h3 className="font-d-headline-lg text-d-headline-lg text-white font-bold">38.2<span className="text-xl ml-1 text-white/80 font-normal">Tr</span></h3>
          </div>
        </div>
      </section>

      {/* Chart & Top Materials */}
      <section className="grid grid-cols-1 lg:grid-cols-3 gap-6 shrink-0">
        {/* Main Chart Area */}
        <div className="lg:col-span-2 bg-white rounded-[20px] p-6 border border-d-border-subtle shadow-sm flex flex-col">
          <div className="flex justify-between items-center mb-6">
            <h3 className="font-d-headline-md text-d-headline-md text-d-on-surface">Biểu đồ doanh thu</h3>
            <div className="flex items-center gap-4">
              <div className="flex items-center gap-2">
                <span className="w-3 h-3 rounded-full bg-d-primary"></span>
                <span className="font-d-body-sm text-d-body-sm text-d-on-surface-variant">Tháng này</span>
              </div>
              <div className="flex items-center gap-2">
                <span className="w-3 h-3 rounded-full bg-d-surface-variant"></span>
                <span className="font-d-body-sm text-d-body-sm text-d-on-surface-variant">Tháng trước</span>
              </div>
            </div>
          </div>
          <div className="flex-1 min-h-[300px] flex items-end justify-between gap-2 px-2 pb-6 pt-10 border-b border-l border-d-border-subtle relative">
            {/* Y-axis labels */}
            <div className="absolute left-[-30px] top-0 bottom-0 flex flex-col justify-between font-d-label-sm text-d-label-sm text-d-on-surface-variant">
              <span>150M</span>
              <span>100M</span>
              <span>50M</span>
              <span>0M</span>
            </div>
            
            {/* Chart Bars - Mock Data */}
            {[
              { day: '01', val1: 40, val2: 30 },
              { day: '05', val1: 65, val2: 50 },
              { day: '10', val1: 45, val2: 60 },
              { day: '15', val1: 90, val2: 70 },
              { day: '20', val1: 120, val2: 85 },
              { day: '25', val1: 85, val2: 95 },
              { day: '30', val1: 140, val2: 110 },
            ].map((d, i) => (
              <div key={i} className="flex flex-col items-center gap-2 relative w-full h-full justify-end group">
                <div className="flex items-end justify-center gap-1 w-full h-full relative">
                  <div 
                    className="w-[30%] bg-d-surface-variant rounded-t-sm hover:brightness-95 transition-all" 
                    style={{ height: `${(d.val2/150)*100}%` }}
                  ></div>
                  <div 
                    className="w-[30%] bg-d-primary rounded-t-sm group-hover:bg-d-primary-fixed transition-all" 
                    style={{ height: `${(d.val1/150)*100}%` }}
                  ></div>
                </div>
                <span className="absolute -bottom-6 font-d-label-sm text-d-label-sm text-d-on-surface-variant">{d.day}</span>
              </div>
            ))}
          </div>
        </div>

        {/* Top Materials */}
        <div className="bg-white rounded-[20px] p-6 border border-d-border-subtle shadow-sm flex flex-col">
          <div className="flex justify-between items-center mb-6">
            <h3 className="font-d-headline-md text-d-headline-md text-d-on-surface">Vật liệu sinh lời cao</h3>
            <button className="text-d-primary hover:bg-d-primary/10 p-1 rounded-full transition-colors">
              <MaterialIcon name="more_horiz" />
            </button>
          </div>
          
          <div className="flex flex-col gap-5 flex-1">
            {[
              { name: 'Nhựa PET Trong', percent: 45, val: '64.1 Tr', color: 'bg-d-primary' },
              { name: 'Giấy Carton', percent: 25, val: '35.6 Tr', color: 'bg-d-secondary' },
              { name: 'Đồng Cáp Loại 1', percent: 18, val: '25.6 Tr', color: 'bg-d-tertiary' },
              { name: 'Sắt Thép Đặc', percent: 12, val: '17.1 Tr', color: 'bg-d-error' },
            ].map((item, i) => (
              <div key={i} className="flex flex-col gap-2">
                <div className="flex justify-between font-d-body-sm text-d-body-sm">
                  <span className="font-medium text-d-on-surface">{item.name}</span>
                  <span className="font-bold text-d-on-surface-variant">{item.val}</span>
                </div>
                <div className="w-full h-2 bg-d-surface-container rounded-full overflow-hidden">
                  <div className={`h-full ${item.color} rounded-full`} style={{ width: `${item.percent}%` }}></div>
                </div>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Recent Transactions Table */}
      <section className="bg-white rounded-[20px] border border-d-border-subtle shadow-sm overflow-hidden flex flex-col shrink-0">
        <div className="p-6 border-b border-d-border-subtle flex justify-between items-center bg-d-surface-container-lowest">
          <h3 className="font-d-headline-md text-d-headline-md text-d-on-surface">Giao dịch doanh thu gần đây</h3>
          <div className="relative">
            <span className="absolute left-3 top-1/2 -translate-y-1/2 text-d-on-surface-variant">
              <MaterialIcon name="search" className="text-[20px]" />
            </span>
            <input 
              type="text" 
              placeholder="Tìm mã lô hàng..." 
              className="pl-9 pr-4 py-2 border border-d-border-subtle rounded-full bg-d-surface-container-lowest focus:outline-none focus:ring-2 focus:ring-d-primary text-d-body-sm w-64"
            />
          </div>
        </div>
        <div className="overflow-x-auto w-full">
          <table className="w-full text-left border-collapse min-w-[800px]">
            <thead>
              <tr className="bg-d-surface-container-low border-b border-d-border-subtle font-d-label-md text-d-label-md text-d-on-surface-variant">
                <th className="py-4 px-6 font-semibold">MÃ LÔ XUẤT</th>
                <th className="py-4 px-6 font-semibold">NHÀ MÁY / NGƯỜI MUA</th>
                <th className="py-4 px-6 font-semibold">VẬT LIỆU</th>
                <th className="py-4 px-6 font-semibold text-right">TỔNG THU</th>
                <th className="py-4 px-6 font-semibold text-right">LỢI NHUẬN TẠM TÍNH</th>
                <th className="py-4 px-6 font-semibold text-center">TRẠNG THÁI</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-d-border-subtle font-d-body-sm text-d-body-sm text-d-on-background">
              <tr className="hover:bg-d-surface-container-lowest transition-colors">
                <td className="py-4 px-6 font-bold text-d-primary">#OUT-8812</td>
                <td className="py-4 px-6">Nhà máy Tái chế Nhựa Vina</td>
                <td className="py-4 px-6">Nhựa PET Trong</td>
                <td className="py-4 px-6 text-right font-medium">45.000.000 đ</td>
                <td className="py-4 px-6 text-right font-bold text-d-secondary">8.200.000 đ</td>
                <td className="py-4 px-6 text-center">
                  <span className="inline-flex items-center gap-1 px-3 py-1 bg-d-surface-accent text-d-secondary rounded-full font-d-label-sm border border-d-secondary-container">
                    Hoàn tất
                  </span>
                </td>
              </tr>
              <tr className="hover:bg-d-surface-container-lowest transition-colors bg-white/50">
                <td className="py-4 px-6 font-bold text-d-primary">#OUT-8811</td>
                <td className="py-4 px-6">Tập đoàn Giấy Miền Nam</td>
                <td className="py-4 px-6">Giấy Carton phế liệu</td>
                <td className="py-4 px-6 text-right font-medium">22.500.000 đ</td>
                <td className="py-4 px-6 text-right font-bold text-d-secondary">3.800.000 đ</td>
                <td className="py-4 px-6 text-center">
                  <span className="inline-flex items-center gap-1 px-3 py-1 bg-[#FFF8E1] text-[#F57F17] rounded-full font-d-label-sm border border-[#FFECB3]">
                    Đang xử lý
                  </span>
                </td>
              </tr>
              <tr className="hover:bg-d-surface-container-lowest transition-colors">
                <td className="py-4 px-6 font-bold text-d-primary">#OUT-8810</td>
                <td className="py-4 px-6">Công ty Luyện Kim Bình Dương</td>
                <td className="py-4 px-6">Đồng Cáp</td>
                <td className="py-4 px-6 text-right font-medium">85.000.000 đ</td>
                <td className="py-4 px-6 text-right font-bold text-d-secondary">15.500.000 đ</td>
                <td className="py-4 px-6 text-center">
                  <span className="inline-flex items-center gap-1 px-3 py-1 bg-d-surface-accent text-d-secondary rounded-full font-d-label-sm border border-d-secondary-container">
                    Hoàn tất
                  </span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
};

export default RevenueReport;
