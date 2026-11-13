import { Routes, Route, Navigate, Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { LayoutDashboard, Users, Truck, Package, Banknote, TrendingUp, PieChart, ShieldCheck, Tags, ScrollText, ArrowRight } from 'lucide-react';
import AdminLayout from '@/components/layout/AdminLayout';
import { adminApi } from '../api';
import { formatCurrency, ROLE_LABEL } from '@/lib/utils';
import RevenueAreaChart from '../components/RevenueAreaChart';
import RoleDonutChart, { ROLE_COLORS } from '../components/RoleDonutChart';
import { PageHeader, StatCard, Panel } from '../components/AdminUI';
import UserManagement from './UserManagement';
import MarketPrices from './MarketPrices';
import Billing from './Billing';
import AuditLogs from './AuditLogs';
import DepotVerifications from './DepotVerifications';

function sevenDaysAgoISO() {
  const d = new Date();
  d.setDate(d.getDate() - 6);
  d.setHours(0, 0, 0, 0);
  return d.toISOString();
}

const KPI = [
  { key: 'totalUsers', label: 'Tổng người dùng', icon: Users, tone: 'primary' },
  { key: 'totalPickupRequests', label: 'Yêu cầu thu gom', icon: Truck, tone: 'teal', hint: (d) => `${d.completedPickupRequests} đã hoàn tất` },
  { key: 'totalInventoryBatches', label: 'Lô hàng trong kho', icon: Package, tone: 'amber' },
  { key: 'totalPlatformRevenue', label: 'Doanh thu nền tảng', icon: Banknote, tone: 'lime', format: formatCurrency, hint: (d) => `${formatCurrency(d.revenueThisMonth)} tháng này` },
];

const SHORTCUTS = [
  { to: '/admin/users', icon: Users, label: 'Quản lý người dùng', desc: 'Tìm kiếm, tạo mới, khóa tài khoản', tone: 'bg-d-primary/10 text-d-primary' },
  { to: '/admin/depot-verifications', icon: ShieldCheck, label: 'Duyệt hồ sơ kho vựa', desc: 'Đối soát giấy tờ trước khi mở giao dịch', tone: 'bg-teal-50 text-teal-700' },
  { to: '/admin/market-prices', icon: Tags, label: 'Bảng giá thị trường', desc: 'Giá tham khảo theo vật liệu', tone: 'bg-amber-50 text-amber-700' },
  { to: '/admin/audit-logs', icon: ScrollText, label: 'Nhật ký hệ thống', desc: 'Lịch sử hành động quản trị', tone: 'bg-blue-50 text-blue-700' },
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
      <div className="flex w-full flex-col gap-6 p-4 md:p-6">
        <PageHeader icon={LayoutDashboard} title="Dashboard" description="Tổng quan số liệu toàn hệ thống RETRACK." />

        <section className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4" aria-label="Chỉ số chính">
          {KPI.map((k) => (
            <StatCard key={k.key} label={k.label} icon={k.icon} tone={k.tone} loading={isLoading}
              value={!data ? '—' : k.format ? k.format(data[k.key]) : data[k.key]} hint={data && k.hint ? k.hint(data) : undefined} />
          ))}
        </section>
        {isError && <p role="alert" className="text-sm text-d-error">Không tải được số liệu tổng quan.</p>}

        <section className="grid grid-cols-1 gap-6 xl:grid-cols-3">
          <Panel icon={TrendingUp} title="Doanh thu 7 ngày gần nhất" className="xl:col-span-2">
            <div className="p-5">
              {revenueLoading ? (
                <div className="h-[240px] animate-pulse rounded-2xl bg-d-surface-container" />
              ) : revenueError ? (
                <p className="text-sm text-d-error">Không tải được dữ liệu doanh thu.</p>
              ) : !revenue?.points?.length ? (
                <div className="flex h-[240px] flex-col items-center justify-center text-d-on-surface-variant">
                  <TrendingUp size={34} className="mb-2 opacity-30" />
                  <p className="text-sm">Chưa có doanh thu trong 7 ngày qua.</p>
                </div>
              ) : (
                <RevenueAreaChart points={revenue.points} />
              )}
            </div>
          </Panel>

          <Panel icon={PieChart} title="Người dùng theo vai trò">
            <div className="p-5">
              {isLoading ? (
                <div className="h-[200px] animate-pulse rounded-2xl bg-d-surface-container" />
              ) : isError || !data ? (
                <p className="text-sm text-d-error">Không tải được số liệu người dùng.</p>
              ) : (
                <>
                  <RoleDonutChart usersByRole={data.usersByRole} />
                  <div className="mt-4 grid grid-cols-2 gap-x-3 gap-y-2">
                    {Object.entries(data.usersByRole).map(([role, count]) => (
                      <div key={role} className="flex items-center gap-1.5 text-sm">
                        <span className="size-2.5 shrink-0 rounded-full" style={{ backgroundColor: ROLE_COLORS[role] ?? '#727a64' }} />
                        <span className="truncate text-d-on-surface-variant">{ROLE_LABEL[role] ?? role}</span>
                        <span className="ml-auto font-bold text-d-on-surface">{count}</span>
                      </div>
                    ))}
                  </div>
                </>
              )}
            </div>
          </Panel>
        </section>

        <section aria-label="Lối tắt" className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
          {SHORTCUTS.map(({ to, icon: Icon, label, desc, tone }) => (
            <Link key={to} to={to} className="group flex items-center gap-3.5 rounded-2xl border border-d-border-subtle bg-white p-4 shadow-sm transition hover:-translate-y-0.5 hover:border-d-primary/40 hover:shadow-md">
              <span className={`flex size-11 shrink-0 items-center justify-center rounded-xl ${tone}`}><Icon size={21} /></span>
              <span className="min-w-0 flex-1"><span className="block truncate text-sm font-bold text-d-on-surface">{label}</span><span className="block truncate text-xs text-d-on-surface-variant">{desc}</span></span>
              <ArrowRight size={17} className="shrink-0 text-d-outline transition group-hover:translate-x-1 group-hover:text-d-primary" />
            </Link>
          ))}
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
      <Route path="depot-verifications" element={<DepotVerifications />} />
      <Route path="*" element={<Navigate to="/admin" replace />} />
    </Routes>
  );
}
