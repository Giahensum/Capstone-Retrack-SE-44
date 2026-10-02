import React from 'react';
import { materialLabel } from './materialLabels';
import { Link } from 'react-router-dom';
import { useDepotQuery, number, money } from './depotApi';
import { QueryState } from './components/DepotUI';
import { MaterialIcon } from '@/components/ui/MaterialIcon';

const Dashboard = () => {
  const query = useDepotQuery('dashboard');
  const stock = useDepotQuery('inventory');
  const history = useDepotQuery('inventory/receipts', { pageSize: 5 });
  const d = query.data;

  const total = stock.data?.reduce((n, i) => n + Math.max(0, i.onHandKg), 0) ?? 0;
  const topMaterials = [...(stock.data || [])].sort((a, b) => b.onHandKg - a.onHandKg).slice(0, 4);
  const colors = ['#a3e635', '#006c49', '#4edea3', '#e7b6f3']; // Matching original colors

  let currentOffset = 0;
  const segments = topMaterials.map((m, i) => {
    const percentage = total ? (m.onHandKg / total) : 0;
    const strokeDasharray = `${percentage * 251.2} 251.2`;
    const strokeDashoffset = -currentOffset * 251.2;
    currentOffset += percentage;
    return { ...m, strokeDasharray, strokeDashoffset, color: colors[i] };
  });

  return (
    <div className="flex flex-col p-4 md:p-6 gap-4 md:gap-5 w-full h-[calc(100vh-4rem)] overflow-hidden">
      {/* Page Title */}
      <div className="flex flex-col md:flex-row md:items-end justify-between shrink-0">
        <div>
          <h1 className="font-d-headline-xl-mobile md:font-d-headline-xl text-d-headline-xl-mobile md:text-d-headline-xl text-d-on-surface">Bảng điều khiển</h1>
          <p className="font-d-body-md text-d-body-md text-d-on-surface-variant mt-2">Theo dõi hoạt động kho vựa hôm nay.</p>
        </div>
      </div>

      <QueryState query={query}>
        {/* KPI Grid */}
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 shrink-0">
          {/* KPI 1 */}
          <div className="d-glass-panel rounded-xl p-4 md:p-5 relative overflow-hidden group">
            <div className="absolute top-0 right-0 w-24 h-24 bg-d-primary/5 rounded-bl-full -mr-4 -mt-4 transition-transform group-hover:scale-110"></div>
            <div className="flex justify-between items-start mb-3">
              <div className="w-10 h-10 rounded-lg bg-d-surface-container flex items-center justify-center text-d-primary">
                <MaterialIcon name="receipt_long" />
              </div>
            </div>
            <div>
              <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant mb-1">Đơn hàng mới (Hôm nay)</p>
              <h3 className="font-d-headline-lg text-d-headline-lg text-d-on-surface">{d?.newRequestsToday ?? 0}</h3>
            </div>
          </div>

          {/* KPI 2 */}
          <div className="d-glass-panel rounded-xl p-4 md:p-5 relative overflow-hidden group">
            <div className="absolute top-0 right-0 w-24 h-24 bg-d-secondary/5 rounded-bl-full -mr-4 -mt-4 transition-transform group-hover:scale-110"></div>
            <div className="flex justify-between items-start mb-3">
              <div className="w-10 h-10 rounded-lg bg-d-surface-container flex items-center justify-center text-d-secondary">
                <MaterialIcon name="inventory" />
              </div>
            </div>
            <div>
              <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant mb-1">Tồn kho khả dụng (kg)</p>
              <h3 className="font-d-headline-lg text-d-headline-lg text-d-on-surface">{number(d?.availableKg)}</h3>
            </div>
          </div>

          {/* KPI 3 */}
          <div className="d-glass-panel rounded-xl p-4 md:p-5 relative overflow-hidden group">
            <div className="absolute top-0 right-0 w-24 h-24 bg-[#784f85]/5 rounded-bl-full -mr-4 -mt-4 transition-transform group-hover:scale-110"></div>
            <div className="flex justify-between items-start mb-3">
              <div className="w-10 h-10 rounded-lg bg-d-surface-container flex items-center justify-center text-[#784f85]">
                <MaterialIcon name="local_shipping" />
              </div>
            </div>
            <div>
              <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant mb-1">Lô đang xuất</p>
              <h3 className="font-d-headline-lg text-d-headline-lg text-d-on-surface">{d?.activeBatches ?? 0}</h3>
            </div>
          </div>

          {/* KPI 4 */}
          <div className="d-glass-panel rounded-xl p-4 md:p-5 relative overflow-hidden group">
            <div className="absolute top-0 right-0 w-24 h-24 bg-d-primary/5 rounded-bl-full -mr-4 -mt-4 transition-transform group-hover:scale-110"></div>
            <div className="flex justify-between items-start mb-3">
              <div className="w-10 h-10 rounded-lg bg-d-surface-container flex items-center justify-center text-d-primary">
                <MaterialIcon name="payments" />
              </div>
            </div>
            <div>
              <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant mb-1">Doanh thu tháng (VNĐ)</p>
              <h3 className="font-d-headline-lg text-d-headline-lg text-d-on-surface">{money(d?.revenue)}</h3>
            </div>
          </div>
        </div>
      </QueryState>

      {d?.rejectedQualityBatches > 0 && (
        <div role="status" className="rounded-xl border border-d-error bg-d-error-container/20 p-4 text-d-error flex items-start gap-3 shrink-0">
          <MaterialIcon name="warning" className="text-d-error mt-0.5" />
          <div>
            <p className="font-bold">Có {d.rejectedQualityBatches} lô bị từ chối sau kiểm tra chất lượng</p>
            <p className="text-sm">Cần xử lý khẩn cấp. Chưa được coi là hàng đã quay về kho. <Link className="underline font-semibold" to="/depot/batches">Xem chi tiết lô</Link></p>
          </div>
        </div>
      )}

      {/* Quick Actions Row */}
      <div className="flex flex-wrap gap-4 py-2 border-y border-d-border-subtle/50 shrink-0">
        <Link to="/depot/staff" className="px-6 py-2.5 rounded-full bg-d-surface-variant text-d-on-surface font-d-label-md text-d-label-md hover:bg-d-surface-dim transition-colors flex items-center gap-2">
          <MaterialIcon name="person_add" className="text-[18px]" />
          Thêm nhân sự
        </Link>
        <Link to="/depot/payments" className="px-6 py-2.5 rounded-full border border-d-outline text-d-on-surface font-d-label-md text-d-label-md hover:bg-d-surface-variant transition-colors flex items-center gap-2">
          <MaterialIcon name="fact_check" className="text-[18px]" />
          Duyệt thanh toán
        </Link>
        <Link to="/depot/batches" className="px-6 py-2.5 rounded-full bg-d-primary-container text-d-on-primary-container font-d-label-md text-d-label-md hover:bg-d-primary-fixed-dim hover:-translate-y-0.5 transition-transform flex items-center gap-2 shadow-sm">
          <MaterialIcon name="add_box" className="text-[18px]" />
          Tạo lô xuất
        </Link>
      </div>

      {/* Two Column Layout: Table & Chart */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 flex-1 min-h-0 overflow-y-auto lg:overflow-hidden pb-4 md:pb-0">
        {/* Left: Recent Inbound Table (60%) */}
        <div className="lg:col-span-7 bg-d-surface-container-lowest rounded-[20px] p-5 border border-d-border-subtle shadow-[0_20px_40px_rgba(23,33,27,0.02)] flex flex-col h-full overflow-hidden">
          <div className="flex justify-between items-center mb-4 shrink-0">
            <h2 className="font-d-headline-md text-d-headline-md text-d-on-surface">Nhập kho gần đây</h2>
            <Link to="/depot/inventory" className="text-d-primary font-d-label-sm text-d-label-sm hover:underline">Xem tất cả</Link>
          </div>
          <div className="overflow-y-auto flex-1 min-h-0">
            <QueryState query={history}>
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
                  {history.data?.items.map((p) => (
                    <tr key={p.id} className="hover:bg-d-surface-container-low transition-colors group">
                      <td className="py-2.5 text-d-on-surface font-medium">#{p.id.slice(0,8)}</td>
                      <td className="py-2.5 text-d-on-surface-variant flex items-center gap-2">
                        <div className="w-6 h-6 rounded-full bg-d-surface-variant flex items-center justify-center text-[10px] font-bold text-d-on-surface-variant">
                          {p.sellerName.charAt(0).toUpperCase()}
                        </div>
                        {p.sellerName}
                      </td>
                      <td className="py-2.5 text-d-on-surface-variant max-w-[150px] truncate">
                        {p.items?.map(i => materialLabel(i.materialType)).join(', ') || 'Chưa cập nhật'}
                      </td>
                      <td className="py-2.5 text-d-on-surface text-right">{number(p.items.reduce((n,i)=>n+i.weightKg,0))} kg</td>
                      <td className="py-2.5 text-right">
                        <span className="inline-flex items-center px-2 py-1 rounded-full bg-d-surface-accent text-d-secondary font-d-label-sm text-d-label-sm">Đã nhập</span>
                      </td>
                    </tr>
                  ))}
                  {!history.data?.items.length && (
                    <tr>
                      <td colSpan="5" className="py-8 text-center text-d-on-surface-variant font-d-body-sm">Chưa có nhập kho gần đây.</td>
                    </tr>
                  )}
                </tbody>
              </table>
            </QueryState>
          </div>
        </div>

        {/* Right: Inventory Donut Chart (40%) */}
        <div className="lg:col-span-5 bg-d-surface-container-lowest rounded-[20px] p-5 border border-d-border-subtle shadow-[0_20px_40px_rgba(23,33,27,0.02)] flex flex-col h-full overflow-hidden">
          <h2 className="font-d-headline-md text-d-headline-md text-d-on-surface mb-2 shrink-0">Cơ cấu tồn kho</h2>
          <QueryState query={stock}>
            <div className="flex-1 flex flex-col items-center justify-center relative min-h-0">
              {total > 0 ? (
                <>
                  <svg className="w-40 h-40 md:w-36 md:h-36 drop-shadow-md shrink-0" viewBox="0 0 100 100">
                    <circle cx="50" cy="50" fill="transparent" r="40" stroke="#dae5dc" strokeWidth="20"></circle>
                    {segments.map((s, i) => (
                      <circle
                        key={i}
                        className="d-donut-segment transition-all duration-1000 ease-out"
                        cx="50" cy="50" fill="transparent" r="40"
                        stroke={s.color}
                        strokeDasharray={s.strokeDasharray}
                        strokeDashoffset={s.strokeDashoffset}
                        strokeWidth="20"
                        transform="rotate(-90 50 50)">
                      </circle>
                    ))}
                  </svg>
                  <div className="absolute inset-0 flex flex-col items-center justify-center pointer-events-none">
                    <span className="font-d-headline-lg text-d-headline-lg text-d-on-surface font-bold">
                      {total >= 1000 ? number((total / 1000).toFixed(1)) : number(total)}
                    </span>
                    <span className="font-d-label-sm text-d-label-sm text-d-on-surface-variant">
                      {total >= 1000 ? 'Tấn' : 'kg'}
                    </span>
                  </div>
                </>
              ) : (
                <div className="text-center text-d-on-surface-variant">Kho trống</div>
              )}
            </div>

            {total > 0 && (
              <div className="grid grid-cols-2 gap-3 mt-4 shrink-0">
                {segments.map((s, i) => (
                  <div key={i} className="flex items-center gap-2">
                    <div className="w-3 h-3 rounded-full" style={{ backgroundColor: s.color }}></div>
                    <span className="font-d-body-sm text-d-body-sm text-d-on-surface-variant truncate">
                      {materialLabel(s.materialType)} ({Math.round((s.onHandKg / total) * 100)}%)
                    </span>
                  </div>
                ))}
              </div>
            )}
          </QueryState>
        </div>
      </div>
    </div>
  );
};

export default Dashboard;
