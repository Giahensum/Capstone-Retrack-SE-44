import React, { useState } from 'react';
import { useDepotQuery, number, money } from './depotApi';
import { QueryState, Pager } from './components/DepotUI';
import { MaterialIcon } from '@/components/ui/MaterialIcon';

export default function StaffPerformance() {
  const [page, setPage] = useState(1);
  const [period, setPeriod] = useState(new Date().toISOString().slice(0, 7)); // YYYY-MM
  const query = useDepotQuery('reports/staff', { period, page });

  const totalCompleted = query.data?.items.reduce((sum, s) => sum + s.completedCount, 0) ?? 0;
  const totalWeight = query.data?.items.reduce((sum, s) => sum + s.weightKg, 0) ?? 0;
  const purchaseAmount = query.data?.items.reduce((sum, s) => sum + s.purchaseAmount, 0) ?? 0;
  const topStaff = [...(query.data?.items ?? [])].sort((a, b) => b.weightKg - a.weightKg).slice(0, 5);

  const kpiCards = [
    { label: 'Đơn hoàn thành (trang này)', value: number(totalCompleted), icon: 'check_circle', iconColor: 'text-d-secondary', badge: null },
    { label: 'Khối lượng (trang này)', value: number(totalWeight), unit: 'kg', icon: 'scale', iconColor: 'text-d-primary', badge: null },
    { label: 'Giá trị thu mua (trang này)', value: money(purchaseAmount), icon: 'payments', iconColor: 'text-[#CD7F32]', badge: null },
    { label: 'Nhân viên trong trang', value: number(query.data?.items.length ?? 0), icon: 'badge', iconColor: 'text-[#784f85]', badge: null },
  ];

  return (
    <div className="flex-1 p-4 md:p-6 w-full flex flex-col gap-6 h-[calc(100vh-4rem)] overflow-y-auto bg-d-surface">
      {/* Page Header */}
      <div className="flex flex-col md:flex-row justify-between items-start md:items-end gap-4 shrink-0">
        <div>
          <h1 className="font-d-headline-lg text-d-headline-lg text-d-on-surface">Hiệu Suất Nhân Sự</h1>
          <p className="font-d-body-md text-d-body-md text-d-on-surface-variant mt-1">Đánh giá năng suất và chất lượng công việc của nhân viên thu gom.</p>
        </div>
        <label className="bg-d-surface-container-lowest border border-d-border-subtle px-4 py-2 rounded-lg flex items-center gap-2 font-d-label-md text-d-on-surface hover:border-d-primary transition-colors focus-within:border-d-primary cursor-pointer shrink-0">
          <MaterialIcon name="calendar_today" className="text-d-on-surface-variant text-[18px]" />
          <input
            type="month"
            value={period}
            onChange={e => { setPeriod(e.target.value); setPage(1); }}
            className="bg-transparent border-none outline-none cursor-pointer"
          />
        </label>
      </div>

      <QueryState query={query}>
        {/* KPI Row — 4 cards ngang hàng, cân đối như các trang khác */}
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 shrink-0">
          {kpiCards.map((k, i) => (
            <div key={i} className="d-glass-panel rounded-xl p-4 md:p-5 relative overflow-hidden group">
              <div className="absolute top-0 right-0 w-20 h-20 bg-d-primary/5 rounded-bl-full -mr-3 -mt-3 transition-transform group-hover:scale-110" />
              <div className="flex justify-between items-start mb-3">
                <div className="w-9 h-9 rounded-lg bg-d-surface-container flex items-center justify-center">
                  <MaterialIcon name={k.icon} className={`text-[20px] ${k.iconColor}`} />
                </div>
              </div>
              <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant mb-1">{k.label}</p>
              <div className="flex items-baseline gap-2">
                <span className="font-d-headline-lg text-d-headline-lg text-d-on-surface">{k.value}</span>
                {k.unit && <span className="font-d-body-md text-d-on-surface-variant">{k.unit}</span>}
              </div>
            </div>
          ))}
        </div>

        {/* Top 5 Bar Chart — full width, compact height */}
        <div className="bg-d-surface-container-lowest rounded-[20px] border border-d-border-subtle p-5 md:p-6 shadow-sm shrink-0">
          <h3 className="font-d-headline-md text-d-headline-md text-d-on-surface mb-5">5 nhân viên có khối lượng cao nhất trong trang</h3>
          <div className="flex flex-col gap-3">
            {topStaff.map((staff, idx) => {
              const maxWeight = Math.max(...topStaff.map(s => s.weightKg), 1);
              const percent = (staff.weightKg / maxWeight) * 100;
              return (
                <div key={staff.id} className="flex items-center gap-4">
                  <div className="flex items-center gap-3 w-52 shrink-0">
                    <span aria-hidden="true" className="w-9 h-9 rounded-full bg-d-surface-container flex items-center justify-center font-bold">{staff.fullName?.charAt(0) || 'N'}</span>
                    <span className="font-d-body-md font-medium text-d-on-surface truncate" title={staff.fullName}>{staff.fullName}</span>
                  </div>
                  <div className="flex-1 bg-d-surface-container-high h-7 rounded-full overflow-hidden flex items-center relative">
                    <div
                      className="h-full rounded-full absolute left-0 top-0 transition-all duration-700"
                      style={{
                        width: `${Math.max(6, percent)}%`,
                        background: idx === 0 ? 'var(--color-d-primary-container)' : `rgba(163,230,53,${0.9 - idx * 0.15})`
                      }}
                    />
                    <span className="absolute right-3 font-d-label-sm text-d-on-surface-variant z-10 font-bold text-xs">{number(staff.weightKg)} kg</span>
                  </div>
                </div>
              );
            })}
            {(!query.data?.items || query.data.items.length === 0) && (
              <div className="py-8 flex items-center justify-center text-d-on-surface-variant italic text-sm">
                <MaterialIcon name="bar_chart" className="mr-2 text-d-on-surface-variant/50" />
                Chưa có dữ liệu tháng này.
              </div>
            )}
          </div>
        </div>

        {/* Detail Table */}
        <div className="bg-d-surface-container-lowest rounded-[20px] border border-d-border-subtle overflow-hidden shadow-sm">
          <div className="px-5 py-4 border-b border-d-border-subtle bg-d-surface-accent/30">
            <h3 className="font-d-headline-md text-d-headline-md text-d-on-surface">Chi Tiết Năng Suất Từng Nhân Viên</h3>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse min-w-[700px]">
              <thead>
                <tr className="bg-d-surface-container-low border-b border-d-border-subtle">
                  <th className="p-4 font-d-label-sm text-d-label-sm text-d-on-surface-variant font-medium text-center w-14">STT</th>
                  <th className="p-4 font-d-label-sm text-d-label-sm text-d-on-surface-variant font-medium">NHÂN VIÊN</th>
                  <th className="p-4 font-d-label-sm text-d-label-sm text-d-on-surface-variant font-medium">VAI TRÒ</th>
                  <th className="p-4 font-d-label-sm text-d-label-sm text-d-on-surface-variant font-medium text-right">ĐƠN H.THÀNH</th>
                  <th className="p-4 font-d-label-sm text-d-label-sm text-d-on-surface-variant font-medium text-right">KHỐI LƯỢNG</th>
                  <th className="p-4 font-d-label-sm text-d-label-sm text-d-on-surface-variant font-medium text-right">GIÁ TRỊ THU MUA</th>
                </tr>
              </thead>
              <tbody className="font-d-body-sm text-d-body-sm divide-y divide-d-border-subtle">
                {query.data?.items.map((staff, idx) => {
                  return (
                    <tr key={staff.id} className="hover:bg-d-surface-container-low/50 transition-colors">
                      <td className="p-4 text-center">
                        <span className="font-bold text-d-on-surface-variant text-sm">{idx + 1}</span>
                      </td>
                      <td className="p-4">
                        <div className="flex items-center gap-3">
                          <span aria-hidden="true" className="w-8 h-8 rounded-full bg-d-surface-container flex items-center justify-center shrink-0 font-bold">{staff.fullName?.charAt(0) || 'N'}</span>
                          <span className="font-medium text-d-on-surface truncate">{staff.fullName}</span>
                        </div>
                      </td>
                      <td className="p-4">
                        <span className={`px-2.5 py-1 rounded-full text-xs font-bold ${
                          staff.role === 'DRIVER' ? 'bg-blue-100 text-blue-700' : 'bg-d-surface-accent text-d-secondary'
                        }`}>
                          {staff.role === 'DRIVER' ? 'Tài xế' : 'Thu gom'}
                        </span>
                      </td>
                      <td className="p-4 text-right text-d-on-surface font-medium">{number(staff.completedCount)}</td>
                      <td className="p-4 text-right text-d-on-surface">{number(staff.weightKg)} kg</td>
                      <td className="p-4 text-right text-d-on-surface">{money(staff.purchaseAmount)}</td>
                    </tr>
                  );
                })}
                {(!query.data?.items || query.data.items.length === 0) && (
                  <tr>
                    <td colSpan="6" className="p-10 text-center text-d-on-surface-variant italic">Không có dữ liệu tháng này.</td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
          <div className="px-5 py-4 border-t border-d-border-subtle bg-d-surface-container-lowest">
            <Pager page={page} setPage={setPage} total={query.data?.totalCount} />
          </div>
        </div>
      </QueryState>
    </div>
  );
}
