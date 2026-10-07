import React, { useState } from 'react';
import { Outlet, Link, useLocation } from 'react-router-dom';
import '../../features/depot/styles/depot-theme.css';
import clsx from 'clsx';
import { DepotSelector, useDepot } from '@/features/depot/DepotContext';
import { useAuthStore } from '@/app/store/useAuthStore';
import { MaterialIcon } from '@/features/depot/components/DepotIcon';
import DepotBrandLogo from '@/features/depot/components/DepotBrandLogo';

const SidebarItem = ({ iconName, label, to, active }) => (
  <Link
    to={to}
    className={clsx(
      "flex items-center gap-3 px-4 py-3 rounded-xl transition-colors",
      active 
        ? "bg-d-primary/10 text-d-primary font-medium" 
        : "text-d-on-surface-variant hover:bg-d-surface-container"
    )}
  >
    <MaterialIcon name={iconName} />
    <span className="font-d-label-md text-d-label-md">{label}</span>
  </Link>
);

const DepotLayout = () => {
  const location = useLocation();
  const [menuOpen, setMenuOpen] = useState(false);
  const { depotId } = useDepot();
  const user = useAuthStore((s) => s.user);
  const logout = useAuthStore((s) => s.logout);

  const navItems = [
    { iconName: 'dashboard', label: 'Tổng quan', to: '/depot/dashboard' },
    { iconName: 'inventory_2', label: 'Hồ sơ kho vựa', to: '/depot/profile' },
    { iconName: 'warehouse', label: 'Quản lý kho', to: '/depot/inventory' },
    { iconName: 'local_shipping', label: 'Lô xuất hàng', to: '/depot/batches' },
    { iconName: 'factory', label: 'Nhà máy đối tác', to: '/depot/partners' },
    { iconName: 'badge', label: 'Quản lý nhân sự', to: '/depot/staff' },
    { iconName: 'monitoring', label: 'Hiệu suất nhân sự', to: '/depot/staff/performance' },
    { iconName: 'payments', label: 'Thanh toán chờ duyệt', to: '/depot/payments' },
    { iconName: 'receipt', label: 'Phí nền tảng', to: '/depot/payments/fees' },
    { iconName: 'bar_chart', label: 'Báo cáo doanh thu', to: '/depot/reports' },
  ];

  return (
    <div className="bg-d-background flex h-screen overflow-hidden font-d-body-md text-d-on-background">
      {/* SideNavBar */}
      {menuOpen && <button aria-label="Đóng menu" className="fixed inset-0 bg-black/30 z-40 lg:hidden" onClick={() => setMenuOpen(false)} />}
      <aside aria-label="Menu kho vựa" className={clsx("w-64 bg-d-surface-container-lowest border-r border-d-border-subtle h-screen fixed top-0 left-0 flex flex-col z-50 transition-transform lg:translate-x-0", menuOpen ? "translate-x-0" : "-translate-x-full")}>
        <div className="h-16 flex items-center px-6 border-b border-d-border-subtle">
          <Link to="/depot/dashboard" className="inline-flex items-center gap-2 font-d-headline-md text-d-headline-md font-bold tracking-tighter text-d-primary" aria-label="ReTrack — về tổng quan kho">
            <DepotBrandLogo /> <span>RETRACK</span>
          </Link>
        </div>
        
        <div className="flex-1 overflow-y-auto py-6">
          <nav className="px-4 space-y-2" onClick={() => setMenuOpen(false)}>
            {navItems.map((item) => (
              <SidebarItem 
                key={item.to}
                {...item}
                active={location.pathname === item.to || (location.pathname === '/depot' && item.to === '/depot/dashboard')}
              />
            ))}
          </nav>
        </div>
        
        <div className="p-4 border-t border-d-border-subtle">
          <div className="flex items-center gap-3 px-4 py-2">
            <div className="w-8 h-8 rounded-full bg-d-primary-container text-d-on-primary-container flex items-center justify-center font-d-label-md font-bold">
              AD
            </div>
            <div>
              <p className="font-d-label-md text-d-label-md text-d-on-surface">{user?.fullName}</p>
              <button onClick={logout} className="font-d-label-sm text-d-label-sm text-d-on-surface-variant">Đăng xuất</button>
            </div>
          </div>
        </div>
      </aside>

      {/* Main Content Area */}
      <main className="flex-1 min-w-0 lg:ml-64 flex flex-col h-screen overflow-hidden">
        {/* TopAppBar */}
        <header className="d-glass-panel flex justify-between gap-3 items-center min-h-16 px-4 lg:px-8 shrink-0 z-30">
          <button aria-label="Mở menu" aria-expanded={menuOpen} className="lg:hidden" onClick={() => setMenuOpen(true)}><MaterialIcon name="menu" /></button>
          <Link to="/depot/dashboard" className="lg:hidden" aria-label="ReTrack — về tổng quan kho"><DepotBrandLogo small /></Link>
          <div className="flex items-center text-d-on-surface-variant font-d-body-sm text-d-body-sm gap-2">
            <Link to="/depot/dashboard" className="hidden sm:inline hover:text-d-primary transition-colors">Trang chủ</Link>
            <MaterialIcon name="chevron_right" className="text-[16px]" />
            <span className="text-d-primary font-bold">{navItems.find((item) => item.to === location.pathname)?.label ?? 'Tổng quan'}</span>
          </div>
          
          <div className="flex items-center min-w-0 gap-3">
            <DepotSelector />
          </div>
        </header>

        {/* Page Content */}
        <div className="flex-1 overflow-y-auto bg-d-background">
          {depotId ? <Outlet key={depotId} /> : <p role="status" className="p-6">Chọn kho để quản lý dữ liệu.</p>}
        </div>
      </main>
    </div>
  );
};

export default DepotLayout;
