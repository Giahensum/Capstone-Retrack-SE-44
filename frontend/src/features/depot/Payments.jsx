import React from 'react';
import { MaterialIcon } from '../../components/ui/MaterialIcon';

const Payments = () => {
  return (
    <div className="flex-1 p-4 md:p-6 w-full flex flex-col gap-8 h-[calc(100vh-4rem)] overflow-y-auto bg-d-background">
      {/* Header */}
      <div className="mb-2 flex flex-col md:flex-row justify-between items-start md:items-end gap-6 shrink-0">
        <div>
          <h1 className="font-d-headline-xl-mobile md:font-d-headline-xl text-d-headline-xl-mobile md:text-d-headline-xl text-d-on-surface mb-2">Thanh toán chờ duyệt</h1>
          <p className="font-d-body-md text-d-body-md text-d-on-surface-variant">Quản lý và phê duyệt các khoản thanh toán cho người bán.</p>
        </div>
        <button className="font-d-label-md text-d-label-md px-6 py-3 bg-d-secondary-container text-d-on-secondary-container rounded-full hover:bg-d-secondary-fixed hover:-translate-y-0.5 transition-all flex items-center gap-2 shadow-sm">
          <MaterialIcon name="download" />
          Xuất báo cáo
        </button>
      </div>

      {/* Summary Cards (Bento style) */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6 shrink-0">
        {/* Highlighted Card: Total Debt */}
        <div className="bg-d-surface-accent rounded-[20px] p-8 border border-d-border-subtle relative overflow-hidden group hover:shadow-sm transition-all duration-300">
          <div className="absolute top-0 right-0 p-6 opacity-10 group-hover:opacity-20 transition-opacity">
            <MaterialIcon name="account_balance_wallet" className="text-6xl text-d-primary" />
          </div>
          <div className="relative z-10">
            <p className="font-d-label-md text-d-label-md text-d-on-surface-variant mb-2 uppercase tracking-wider">Tổng nợ cần trả</p>
            <h2 className="font-d-headline-lg text-d-headline-lg text-d-primary mb-1">45.250.000 đ</h2>
            <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant flex items-center gap-1">
              <MaterialIcon name="trending_up" className="text-[16px] text-d-error" />
              +12% so với tuần trước
            </p>
          </div>
        </div>
        {/* Card: Pending Approvals */}
        <div className="bg-white rounded-[20px] p-8 border border-d-border-subtle hover:shadow-sm transition-all duration-300">
          <p className="font-d-label-md text-d-label-md text-d-on-surface-variant mb-2 uppercase tracking-wider">Tổng đơn chờ</p>
          <h2 className="font-d-headline-lg text-d-headline-lg text-d-on-surface mb-1">24</h2>
          <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant flex items-center gap-1">
            <MaterialIcon name="schedule" className="text-[16px] text-d-primary" />
            Cần xử lý ngay
          </p>
        </div>
        {/* Card: Processed Today */}
        <div className="bg-white rounded-[20px] p-8 border border-d-border-subtle hover:shadow-sm transition-all duration-300 hidden md:block">
          <p className="font-d-label-md text-d-label-md text-d-on-surface-variant mb-2 uppercase tracking-wider">Đã xử lý hôm nay</p>
          <h2 className="font-d-headline-lg text-d-headline-lg text-d-on-surface mb-1">12.400.000 đ</h2>
          <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant flex items-center gap-1">
            <MaterialIcon name="check_circle" className="text-[16px] text-d-primary" />
            15 giao dịch hoàn thành
          </p>
        </div>
      </div>

      {/* Filter Bar */}
      <div className="bg-white rounded-[20px] border border-d-border-subtle p-4 flex flex-col md:flex-row gap-4 items-center justify-between shadow-sm shrink-0">
        <div className="relative w-full md:w-96">
          <MaterialIcon name="search" className="absolute left-4 top-1/2 -translate-y-1/2 text-d-on-surface-variant" />
          <input 
            className="w-full bg-d-surface-container border border-d-border-subtle rounded-full py-3 pl-12 pr-4 font-d-body-sm text-d-body-sm focus:outline-none focus:border-d-primary focus:ring-1 focus:ring-d-primary transition-all" 
            placeholder="Tìm kiếm theo Người bán hoặc Mã đơn..." 
            type="text" 
          />
        </div>
        <div className="flex gap-2 w-full md:w-auto overflow-x-auto pb-2 md:pb-0">
          <button className="px-4 py-2 rounded-full bg-d-primary-fixed text-d-on-primary-fixed font-d-label-sm text-d-label-sm whitespace-nowrap">Tất cả</button>
          <button className="px-4 py-2 rounded-full bg-white border border-d-border-subtle text-d-on-surface-variant font-d-label-sm text-d-label-sm hover:bg-d-surface-variant transition-colors whitespace-nowrap">Giá trị cao</button>
          <button className="px-4 py-2 rounded-full bg-white border border-d-border-subtle text-d-on-surface-variant font-d-label-sm text-d-label-sm hover:bg-d-surface-variant transition-colors whitespace-nowrap">Cũ nhất trước</button>
        </div>
      </div>

      {/* Data Table */}
      <div className="bg-white rounded-[20px] border border-d-border-subtle overflow-hidden shadow-sm flex flex-col shrink-0">
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse">
            <thead>
              <tr className="bg-d-surface-container/50 border-b border-d-border-subtle">
                <th className="py-4 px-6 font-d-label-sm text-d-label-sm text-d-on-surface-variant uppercase tracking-wider">Mã đơn</th>
                <th className="py-4 px-6 font-d-label-sm text-d-label-sm text-d-on-surface-variant uppercase tracking-wider">Người bán &amp; SĐT</th>
                <th className="py-4 px-6 font-d-label-sm text-d-label-sm text-d-on-surface-variant uppercase tracking-wider text-right">Tiền gốc</th>
                <th className="py-4 px-6 font-d-label-sm text-d-label-sm text-d-on-surface-variant uppercase tracking-wider text-right">Phí (5%)</th>
                <th className="py-4 px-6 font-d-label-sm text-d-label-sm text-d-primary uppercase tracking-wider text-right bg-d-surface-accent/50">Thực trả</th>
                <th className="py-4 px-6 font-d-label-sm text-d-label-sm text-d-on-surface-variant uppercase tracking-wider text-center">Hành động</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-d-border-subtle">
              {/* Row 1 */}
              <tr className="hover:bg-d-surface-container-low transition-colors group">
                <td className="py-4 px-6 font-d-label-md text-d-label-md text-d-on-surface cursor-pointer hover:text-d-primary">#REQ-8924</td>
                <td className="py-4 px-6">
                  <div className="font-d-body-md text-d-body-md font-medium text-d-on-surface">Nguyễn Văn A</div>
                  <div className="font-d-body-sm text-d-body-sm text-d-on-surface-variant">090 123 4567</div>
                </td>
                <td className="py-4 px-6 font-d-body-md text-d-body-md text-d-on-surface text-right">2.500.000 đ</td>
                <td className="py-4 px-6 font-d-body-md text-d-body-md text-d-error text-right">- 125.000 đ</td>
                <td className="py-4 px-6 font-d-headline-md text-d-headline-md text-d-primary font-bold text-right bg-d-surface-accent/10">2.375.000 đ</td>
                <td className="py-4 px-6 text-center">
                  <button className="font-d-label-sm text-d-label-sm px-4 py-2 bg-d-on-surface text-d-on-primary rounded-full hover:bg-d-on-surface-variant transition-all transform active:scale-95 inline-flex items-center gap-1">
                    <MaterialIcon name="check" className="text-[16px]" />
                    Duyệt
                  </button>
                </td>
              </tr>
              {/* Row 2 */}
              <tr className="hover:bg-d-surface-container-low transition-colors group">
                <td className="py-4 px-6 font-d-label-md text-d-label-md text-d-on-surface cursor-pointer hover:text-d-primary">#REQ-8923</td>
                <td className="py-4 px-6">
                  <div className="font-d-body-md text-d-body-md font-medium text-d-on-surface">Trần Thị B</div>
                  <div className="font-d-body-sm text-d-body-sm text-d-on-surface-variant">098 765 4321</div>
                </td>
                <td className="py-4 px-6 font-d-body-md text-d-body-md text-d-on-surface text-right">8.400.000 đ</td>
                <td className="py-4 px-6 font-d-body-md text-d-body-md text-d-error text-right">- 420.000 đ</td>
                <td className="py-4 px-6 font-d-headline-md text-d-headline-md text-d-primary font-bold text-right bg-d-surface-accent/10">7.980.000 đ</td>
                <td className="py-4 px-6 text-center">
                  <button className="font-d-label-sm text-d-label-sm px-4 py-2 bg-d-on-surface text-d-on-primary rounded-full hover:bg-d-on-surface-variant transition-all transform active:scale-95 inline-flex items-center gap-1">
                    <MaterialIcon name="check" className="text-[16px]" />
                    Duyệt
                  </button>
                </td>
              </tr>
              {/* Row 3 */}
              <tr className="hover:bg-d-surface-container-low transition-colors group">
                <td className="py-4 px-6 font-d-label-md text-d-label-md text-d-on-surface cursor-pointer hover:text-d-primary">#REQ-8921</td>
                <td className="py-4 px-6">
                  <div className="font-d-body-md text-d-body-md font-medium text-d-on-surface">Lê Minh C</div>
                  <div className="font-d-body-sm text-d-body-sm text-d-on-surface-variant">091 234 5678</div>
                </td>
                <td className="py-4 px-6 font-d-body-md text-d-body-md text-d-on-surface text-right">1.250.000 đ</td>
                <td className="py-4 px-6 font-d-body-md text-d-body-md text-d-error text-right">- 62.500 đ</td>
                <td className="py-4 px-6 font-d-headline-md text-d-headline-md text-d-primary font-bold text-right bg-d-surface-accent/10">1.187.500 đ</td>
                <td className="py-4 px-6 text-center">
                  <button className="font-d-label-sm text-d-label-sm px-4 py-2 bg-d-on-surface text-d-on-primary rounded-full hover:bg-d-on-surface-variant transition-all transform active:scale-95 inline-flex items-center gap-1">
                    <MaterialIcon name="check" className="text-[16px]" />
                    Duyệt
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        {/* Pagination */}
        <div className="p-4 border-t border-d-border-subtle flex justify-between items-center bg-d-surface-container/30">
          <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant">Hiển thị 1 đến 3 của 24 mục</p>
          <div className="flex gap-2">
            <button className="p-2 rounded-lg border border-d-border-subtle text-d-on-surface-variant hover:bg-d-surface-variant disabled:opacity-50" disabled>
              <MaterialIcon name="chevron_left" className="text-[20px]" />
            </button>
            <button className="p-2 rounded-lg border border-d-border-subtle text-d-on-surface-variant hover:bg-d-surface-variant">
              <MaterialIcon name="chevron_right" className="text-[20px]" />
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};

export default Payments;
