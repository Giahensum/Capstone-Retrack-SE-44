import { Routes, Route, Navigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import AdminLayout from '@/components/layout/AdminLayout';
import { MaterialIcon } from '@/components/ui/MaterialIcon';
import { adminApi } from '../api';
import { formatCurrency, ROLE_LABEL } from '@/lib/utils';
import RevenueAreaChart from '../components/RevenueAreaChart';
import RoleDonutChart, { ROLE_COLORS } from '../components/RoleDonutChart';
import UserManagement from './UserManagement';
import MarketPrices from './MarketPrices';
import Billing from './Billing';
import AuditLogs from './AuditLogs';

function sevenDaysAgoISO() {
  const d = new Date();
  d.setDate(d.getDate() - 6);
  d.setHours(0, 0, 0, 0);
  return d.toISOString();
}

const KPI = [
  { key: 'totalUsers', label: 'Tổng người dùng', icon: 'group', tone: 'bg-d-primary/10 text-d-primary' },
  { key: 'totalPickupRequests', label: 'Yêu cầu thu gom', icon: 'local_shipping', tone: 'bg-d-secondary/10 text-d-secondary', hint: (d) => `${d.completedPickupRequests} đã hoàn tất` },
  { key: 'totalInventoryBatches', label: 'Lô hàng trong kho', icon: 'inventory_2', tone: 'bg-[#f59e0b]/10 text-[#b45309]' },
  { key: 'totalPlatformRevenue', label: 'Doanh thu nền tảng', icon: 'payments', tone: 'bg-d-primary-container text-d-on-primary-container', format: formatCurrency, hint: (d) => `${formatCurrency(d.revenueThisMonth)} tháng này` },
];

function StatsOverview() {
  const { data, isLoading, isError } = useQuery({
    queryKey: ['admin', 'dashboard', 'stats'],
    queryFn: adminApi.getDashboardStats,
  });

  const { data: revenue, isLoading: revenueLoading, isError: revenueError } = useQuery({
    queryKey: ['admin', 'dashboard', 'revenue-trend'],
    queryFn: () => adminApi.getRevenue({ from: sevenDaysAgoISO(), to: new Date().toISOString(), groupBy: 'day' }),
  });

  return (
    <AdminLayout crumb="Tổng quan">
      <div className="p-4 md:p-6 w-full flex flex-col gap-8">
        <section>
          <h2 className="font-d-headline-xl text-d-headline-xl text-d-on-surface">Admin Dashboard</h2>
          <p className="font-d-body-md text-d-body-md text-d-on-surface-variant mt-2">Tổng quan số liệu toàn hệ thống RETRACK.</p>
        </section>

        {/* KPI Cards */}
        <section className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6">
          {KPI.map((k) => (
            <div key={k.key} className="bg-white border border-d-border-subtle rounded-[20px] p-6 shadow-sm flex flex-col gap-4 relative overflow-hidden group hover:shadow-md transition-shadow">
              <div className="absolute -right-4 -top-4 w-24 h-24 bg-d-primary/5 rounded-full blur-2xl group-hover:bg-d-primary/10 transition-colors duration-500" />
              <div className={`w-10 h-10 rounded-full flex items-center justify-center relative z-10 ${k.tone}`}>
                <MaterialIcon name={k.icon} />
              </div>
              <div className="relative z-10">
                <p className="font-d-label-md text-d-label-md text-d-on-surface-variant mb-1">{k.label}</p>
                <p className="font-d-headline-lg text-d-headline-lg text-d-on-surface">
                  {!data ? '—' : k.format ? k.format(data[k.key]) : data[k.key]}
                </p>
                {data && k.hint && <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant mt-1">{k.hint(data)}</p>}
              </div>
            </div>
          ))}
        </section>

        <section className="grid grid-cols-1 xl:grid-cols-3 gap-6">
          <div className="xl:col-span-2 bg-white border border-d-border-subtle rounded-[20px] shadow-sm p-6">
            <h3 className="font-d-headline-md text-d-headline-md text-d-on-background mb-4">Doanh thu 7 ngày gần nhất</h3>
            {revenueLoading ? (
              <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant">Đang tải...</p>
            ) : revenueError ? (
              <p className="font-d-body-sm text-d-body-sm text-d-error">Không tải được dữ liệu doanh thu.</p>
            ) : !revenue?.points?.length ? (
              <div className="h-[240px] flex flex-col items-center justify-center text-d-on-surface-variant">
                <MaterialIcon name="trending_up" className="text-[32px] mb-2 opacity-30" />
                <p className="font-d-body-sm text-d-body-sm">Chưa có doanh thu trong 7 ngày qua.</p>
              </div>
            ) : (
              <RevenueAreaChart points={revenue.points} />
            )}
          </div>

          <div className="bg-white border border-d-border-subtle rounded-[20px] shadow-sm p-6">
            <h3 className="font-d-headline-md text-d-headline-md text-d-on-background mb-4">Phân bổ người dùng theo vai trò</h3>
            {isLoading ? (
              <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant">Đang tải...</p>
            ) : isError || !data ? (
              <p className="font-d-body-sm text-d-body-sm text-d-error">Không tải được số liệu người dùng.</p>
            ) : (
              <>
                <RoleDonutChart usersByRole={data.usersByRole} />
                <div className="grid grid-cols-2 gap-x-3 gap-y-2 mt-4">
                  {Object.entries(data.usersByRole).map(([role, count]) => (
                    <div key={role} className="flex items-center gap-1.5 font-d-body-sm text-d-body-sm">
                      <span className="w-2 h-2 rounded-full shrink-0" style={{ backgroundColor: ROLE_COLORS[role] ?? '#727a64' }} />
                      <span className="text-d-on-surface-variant truncate">{ROLE_LABEL[role] ?? role}</span>
                      <span className="text-d-on-surface font-medium ml-auto">{count}</span>
                    </div>
                  ))}
                </div>
              </>
            )}
          </div>
        </section>
      </div>
    </AdminLayout>
  );
}

export default function AdminDashboard() {
  return (
    <Routes>
      <Route index element={<StatsOverview />} />
      <Route path="users" element={<UserManagement />} />
      <Route path="market-prices" element={<MarketPrices />} />
      <Route path="billing" element={<Billing />} />
      <Route path="audit-logs" element={<AuditLogs />} />
      <Route path="*" element={<Navigate to="/admin" replace />} />
    </Routes>
  );
}
