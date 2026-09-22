import React, { useState } from 'react';
import { MaterialIcon } from '../../components/ui/MaterialIcon';

const PlatformFees = () => {
  const [activeTab, setActiveTab] = useState('chi-tiet'); // 'chi-tiet' or 'lich-su'

  return (
    <div className="flex-1 p-4 md:p-6 w-full flex flex-col gap-8 h-[calc(100vh-4rem)] overflow-y-auto bg-d-background">
      {/* Page Header */}
      <section className="flex flex-col gap-4 shrink-0">
        <div>
          <h2 className="font-d-headline-xl text-d-headline-xl text-d-on-surface">Phí nền tảng</h2>
          <p className="font-d-body-md text-d-body-md text-d-on-surface-variant mt-2">Quản lý và thanh toán phí nền tảng cho hệ thống Retrack.</p>
        </div>
      </section>

      {/* Warning/Info Banner */}
      <div className="bg-d-surface-container-high rounded-xl p-4 flex items-start space-x-4 border border-d-border-subtle shrink-0">
        <MaterialIcon name="info" className="text-d-secondary flex-shrink-0 mt-0.5" />
        <div>
          <h4 className="font-d-body-md text-d-body-md font-semibold text-d-on-background mb-1">Chính sách thu phí nền tảng</h4>
          <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant">Hệ thống sẽ tự động khấu trừ 5% phí nền tảng từ số tiền thanh toán cho Người bán. Kho vựa sẽ tạm giữ số tiền này và thanh toán lại cho RETRACK vào ngày 5 của tháng kế tiếp. Hóa đơn tháng hiện tại sẽ được chốt vào ngày cuối cùng của tháng.</p>
        </div>
      </div>

      {/* KPI Cards Bento Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-6 shrink-0">
        {/* KPI 1 */}
        <div className="bg-white rounded-[20px] p-6 border border-d-border-subtle shadow-sm flex flex-col justify-between relative overflow-hidden group hover:shadow-md transition-shadow">
          <div className="absolute top-0 right-0 w-32 h-32 bg-d-error-container rounded-full opacity-20 -mr-10 -mt-10 blur-2xl pointer-events-none group-hover:bg-d-error transition-colors"></div>
          <div className="flex items-center justify-between mb-4 relative z-10">
            <h3 className="font-d-label-md text-d-label-md text-d-on-surface-variant uppercase tracking-wider">TỔNG PHÍ ĐANG GIỮ LẠI (TẠM TÍNH)</h3>
            <div className="w-10 h-10 rounded-full bg-d-error-container flex items-center justify-center">
              <MaterialIcon name="payments" className="text-d-on-error-container text-[20px]" />
            </div>
          </div>
          <div className="relative z-10">
            <div className="font-d-headline-xl text-d-headline-xl text-d-error mb-2 tracking-tight">3.250.000 <span className="text-3xl">đ</span></div>
            <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant flex items-center">
              <MaterialIcon name="schedule" className="text-[16px] mr-1" />
              Sẽ chốt hóa đơn vào cuối ngày 31/08/2026
            </p>
          </div>
        </div>

        {/* KPI 2 */}
        <div className="bg-white rounded-[20px] p-6 border border-d-border-subtle shadow-sm flex flex-col justify-between relative overflow-hidden group hover:shadow-md transition-shadow">
          <div className="absolute top-0 right-0 w-32 h-32 bg-d-primary-container rounded-full opacity-20 -mr-10 -mt-10 blur-2xl pointer-events-none group-hover:bg-d-primary transition-colors"></div>
          <div className="flex items-center justify-between mb-4 relative z-10">
            <h3 className="font-d-label-md text-d-label-md text-d-on-surface-variant uppercase tracking-wider">SỐ GIAO DỊCH PHÁT SINH PHÍ</h3>
            <div className="w-10 h-10 rounded-full bg-d-surface-container flex items-center justify-center">
              <MaterialIcon name="receipt_long" className="text-d-secondary text-[20px]" />
            </div>
          </div>
          <div className="relative z-10">
            <div className="font-d-headline-xl text-d-headline-xl text-d-on-background mb-2 tracking-tight">45 <span className="text-3xl text-d-on-surface-variant font-medium">đơn</span></div>
            <div className="flex items-center font-d-body-sm text-d-body-sm text-d-secondary bg-d-surface-accent inline-flex px-2 py-1 rounded-full w-max">
              <MaterialIcon name="trending_up" className="text-[16px] mr-1" />
              Tăng 12 đơn so với cùng kỳ
            </div>
          </div>
        </div>
      </div>

      {/* Tabs */}
      <div className="flex gap-4 border-b border-d-border-subtle shrink-0">
        <button 
          onClick={() => setActiveTab('chi-tiet')}
          className={`pb-2 px-1 font-d-label-md text-d-label-md border-b-2 transition-colors ${activeTab === 'chi-tiet' ? 'border-d-primary text-d-primary' : 'border-transparent text-d-on-surface-variant hover:text-d-on-surface'}`}
        >
          Chi tiết giao dịch tháng hiện tại
        </button>
        <button 
          onClick={() => setActiveTab('lich-su')}
          className={`pb-2 px-1 font-d-label-md text-d-label-md border-b-2 transition-colors ${activeTab === 'lich-su' ? 'border-d-primary text-d-primary' : 'border-transparent text-d-on-surface-variant hover:text-d-on-surface'}`}
        >
          Lịch sử hóa đơn
        </button>
      </div>

      {/* Tab Content: Chi Tiết Giao Dịch */}
      {activeTab === 'chi-tiet' && (
        <div className="bg-white rounded-[20px] border border-d-border-subtle shadow-sm overflow-hidden flex flex-col shrink-0">
          <div className="p-6 border-b border-d-border-subtle flex justify-between items-center bg-d-surface-container-lowest">
            <h3 className="font-d-headline-md text-d-headline-md text-d-on-background">Chi tiết giao dịch tháng 8/2026</h3>
            <button className="px-4 py-2 border border-d-border-subtle rounded-full font-d-body-sm text-d-body-sm hover:bg-d-surface-container transition-colors flex items-center gap-2 text-d-on-surface">
              <MaterialIcon name="download" className="text-[18px]" />
              Xuất báo cáo
            </button>
          </div>
          <div className="overflow-x-auto w-full">
            <table className="w-full text-left border-collapse min-w-[800px]">
              <thead>
                <tr className="bg-d-surface-container-low border-b border-d-border-subtle font-d-label-md text-d-label-md text-d-on-surface-variant">
                  <th className="py-4 px-6 font-semibold">MÃ ĐƠN</th>
                  <th className="py-4 px-6 font-semibold">NGÀY GIAO DỊCH</th>
                  <th className="py-4 px-6 font-semibold">NGƯỜI BÁN</th>
                  <th className="py-4 px-6 font-semibold text-right">TỔNG GIÁ TRỊ PHẾ LIỆU</th>
                  <th className="py-4 px-6 font-semibold text-right">PHÍ NỀN TẢNG (5%)</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-d-border-subtle font-d-body-sm text-d-body-sm text-d-on-background">
                <tr className="hover:bg-d-surface-container-lowest transition-colors">
                  <td className="py-4 px-6 font-medium text-d-secondary">#RT-8921</td>
                  <td className="py-4 px-6 text-d-on-surface-variant">12/08/2026 14:30</td>
                  <td className="py-4 px-6 flex items-center space-x-3">
                    <div className="w-8 h-8 rounded-full bg-d-tertiary-container text-d-on-tertiary-container flex items-center justify-center font-bold text-xs">N</div>
                    <span>Nguyễn Văn A</span>
                  </td>
                  <td className="py-4 px-6 text-right font-medium">1.500.000 đ</td>
                  <td className="py-4 px-6 text-right font-bold text-d-error">75.000 đ</td>
                </tr>
                <tr className="hover:bg-d-surface-container-lowest transition-colors bg-white/50">
                  <td className="py-4 px-6 font-medium text-d-secondary">#RT-8922</td>
                  <td className="py-4 px-6 text-d-on-surface-variant">12/08/2026 15:45</td>
                  <td className="py-4 px-6 flex items-center space-x-3">
                    <div className="w-8 h-8 rounded-full bg-d-primary-container text-d-on-primary-container flex items-center justify-center font-bold text-xs">T</div>
                    <span>Trần Thị B</span>
                  </td>
                  <td className="py-4 px-6 text-right font-medium">3.200.000 đ</td>
                  <td className="py-4 px-6 text-right font-bold text-d-error">160.000 đ</td>
                </tr>
                <tr className="hover:bg-d-surface-container-lowest transition-colors">
                  <td className="py-4 px-6 font-medium text-d-secondary">#RT-8923</td>
                  <td className="py-4 px-6 text-d-on-surface-variant">13/08/2026 09:15</td>
                  <td className="py-4 px-6 flex items-center space-x-3">
                    <div className="w-8 h-8 rounded-full bg-d-secondary-container text-d-on-secondary-container flex items-center justify-center font-bold text-xs">L</div>
                    <span>Lê Văn C</span>
                  </td>
                  <td className="py-4 px-6 text-right font-medium">850.000 đ</td>
                  <td className="py-4 px-6 text-right font-bold text-d-error">42.500 đ</td>
                </tr>
                <tr className="hover:bg-d-surface-container-lowest transition-colors bg-white/50">
                  <td className="py-4 px-6 font-medium text-d-secondary">#RT-8924</td>
                  <td className="py-4 px-6 text-d-on-surface-variant">13/08/2026 11:20</td>
                  <td className="py-4 px-6 flex items-center space-x-3">
                    <div className="w-8 h-8 rounded-full bg-d-error-container text-d-on-error-container flex items-center justify-center font-bold text-xs">P</div>
                    <span>Phạm Thị D</span>
                  </td>
                  <td className="py-4 px-6 text-right font-medium">4.100.000 đ</td>
                  <td className="py-4 px-6 text-right font-bold text-d-error">205.000 đ</td>
                </tr>
                <tr className="hover:bg-d-surface-container-lowest transition-colors">
                  <td className="py-4 px-6 font-medium text-d-secondary">#RT-8925</td>
                  <td className="py-4 px-6 text-d-on-surface-variant">14/08/2026 08:30</td>
                  <td className="py-4 px-6 flex items-center space-x-3">
                    <div className="w-8 h-8 rounded-full bg-d-surface-variant text-d-on-surface-variant flex items-center justify-center font-bold text-xs">H</div>
                    <span>Hoàng Văn E</span>
                  </td>
                  <td className="py-4 px-6 text-right font-medium">1.200.000 đ</td>
                  <td className="py-4 px-6 text-right font-bold text-d-error">60.000 đ</td>
                </tr>
              </tbody>
            </table>
          </div>
          {/* Pagination */}
          <div className="p-4 border-t border-d-border-subtle bg-white flex items-center justify-between text-d-on-surface-variant font-d-body-sm">
            <div>Hiển thị 1-5 của 45 giao dịch</div>
            <div className="flex space-x-1">
              <button className="w-8 h-8 flex items-center justify-center rounded-md hover:bg-d-surface-container transition-colors disabled:opacity-50" disabled>
                <MaterialIcon name="chevron_left" className="text-[18px]" />
              </button>
              <button className="w-8 h-8 flex items-center justify-center rounded-md bg-d-secondary text-white font-medium">1</button>
              <button className="w-8 h-8 flex items-center justify-center rounded-md hover:bg-d-surface-container transition-colors">2</button>
              <button className="w-8 h-8 flex items-center justify-center rounded-md hover:bg-d-surface-container transition-colors">3</button>
              <span className="w-8 h-8 flex items-center justify-center">...</span>
              <button className="w-8 h-8 flex items-center justify-center rounded-md hover:bg-d-surface-container transition-colors">9</button>
              <button className="w-8 h-8 flex items-center justify-center rounded-md hover:bg-d-surface-container transition-colors">
                <MaterialIcon name="chevron_right" className="text-[18px]" />
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Tab Content: Lịch sử hóa đơn */}
      {activeTab === 'lich-su' && (
        <div className="bg-white rounded-[20px] border border-d-border-subtle shadow-sm overflow-hidden flex flex-col shrink-0 animate-in fade-in slide-in-from-bottom-2 duration-300">
          <div className="p-6 border-b border-d-border-subtle bg-d-surface-container-lowest">
            <h3 className="font-d-headline-md text-d-headline-md text-d-on-background">Danh sách hóa đơn phí nền tảng các tháng trước</h3>
          </div>
          <div className="overflow-x-auto w-full">
            <table className="w-full text-left border-collapse min-w-[800px]">
              <thead>
                <tr className="bg-d-surface-container-low border-b border-d-border-subtle font-d-label-md text-d-label-md text-d-on-surface-variant">
                  <th className="py-4 px-6 font-semibold">KỲ HÓA ĐƠN</th>
                  <th className="py-4 px-6 font-semibold">NGÀY CHỐT</th>
                  <th className="py-4 px-6 font-semibold text-right">SỐ TIỀN</th>
                  <th className="py-4 px-6 font-semibold">TRẠNG THÁI</th>
                  <th className="py-4 px-6 font-semibold">NGÀY THANH TOÁN</th>
                  <th className="py-4 px-6 font-semibold text-right">HÀNH ĐỘNG</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-d-border-subtle font-d-body-sm text-d-body-sm text-d-on-background">
                {/* Row 1: Unpaid */}
                <tr className="hover:bg-d-surface-container-lowest transition-colors bg-d-error-container/10">
                  <td className="py-5 px-6 font-medium text-d-on-background">Tháng 07/2026</td>
                  <td className="py-5 px-6 text-d-on-surface-variant">31/07/2026</td>
                  <td className="py-5 px-6 text-right font-bold text-d-error">8.450.000 đ</td>
                  <td className="py-5 px-6">
                    <span className="bg-[#FFF8E1] text-[#F57F17] border border-[#FFECB3] px-3 py-1 rounded-full font-d-label-sm flex items-center w-max">
                      <MaterialIcon name="warning" className="text-[14px] mr-1" />
                      Chờ thanh toán
                    </span>
                  </td>
                  <td className="py-5 px-6 text-d-on-surface-variant">-</td>
                  <td className="py-5 px-6 text-right">
                    <button className="bg-d-secondary text-white px-4 py-2 rounded-full font-d-body-sm font-medium hover:bg-d-secondary-container hover:text-d-on-secondary-container transition-colors shadow-sm inline-flex items-center gap-1">
                      <MaterialIcon name="payment" className="text-[16px]" />
                      <span>Thanh toán ngay</span>
                    </button>
                  </td>
                </tr>
                {/* Row 2: Paid */}
                <tr className="hover:bg-d-surface-container-lowest transition-colors">
                  <td className="py-5 px-6 font-medium text-d-on-background">Tháng 06/2026</td>
                  <td className="py-5 px-6 text-d-on-surface-variant">30/06/2026</td>
                  <td className="py-5 px-6 text-right font-medium">7.200.000 đ</td>
                  <td className="py-5 px-6">
                    <span className="bg-d-surface-accent text-d-secondary border border-d-secondary-container px-3 py-1 rounded-full font-d-label-sm flex items-center w-max">
                      <MaterialIcon name="check_circle" className="text-[14px] mr-1" />
                      Đã thanh toán
                    </span>
                  </td>
                  <td className="py-5 px-6 text-d-on-surface-variant">04/07/2026</td>
                  <td className="py-5 px-6 text-right">
                    <button className="border border-d-border-subtle bg-transparent text-d-on-background px-4 py-2 rounded-full font-d-body-sm font-medium hover:bg-d-surface-container transition-colors inline-flex items-center gap-1">
                      <MaterialIcon name="visibility" className="text-[16px]" />
                      <span>Xem biên lai</span>
                    </button>
                  </td>
                </tr>
                {/* Row 3: Paid */}
                <tr className="hover:bg-d-surface-container-lowest transition-colors bg-white/50">
                  <td className="py-5 px-6 font-medium text-d-on-background">Tháng 05/2026</td>
                  <td className="py-5 px-6 text-d-on-surface-variant">31/05/2026</td>
                  <td className="py-5 px-6 text-right font-medium">6.850.000 đ</td>
                  <td className="py-5 px-6">
                    <span className="bg-d-surface-accent text-d-secondary border border-d-secondary-container px-3 py-1 rounded-full font-d-label-sm flex items-center w-max">
                      <MaterialIcon name="check_circle" className="text-[14px] mr-1" />
                      Đã thanh toán
                    </span>
                  </td>
                  <td className="py-5 px-6 text-d-on-surface-variant">05/06/2026</td>
                  <td className="py-5 px-6 text-right">
                    <button className="border border-d-border-subtle bg-transparent text-d-on-background px-4 py-2 rounded-full font-d-body-sm font-medium hover:bg-d-surface-container transition-colors inline-flex items-center gap-1">
                      <MaterialIcon name="visibility" className="text-[16px]" />
                      <span>Xem biên lai</span>
                    </button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
};

export default PlatformFees;
