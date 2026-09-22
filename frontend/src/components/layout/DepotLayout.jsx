import React, { useState } from 'react';
import { Outlet, Link, useLocation } from 'react-router-dom';
import '../../features/depot/styles/depot-theme.css';
import clsx from 'clsx';

// Because we're using Material Symbols, we just render a span with the icon name.
const MaterialIcon = ({ name, className }) => (
  <span className={clsx("material-symbols-outlined", className)}>{name}</span>
);

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
      <aside className="w-64 bg-d-surface-container-lowest border-r border-d-border-subtle h-screen fixed top-0 left-0 flex flex-col z-40">
        <div className="h-16 flex items-center px-6 border-b border-d-border-subtle">
          <Link to="/depot/dashboard" className="font-d-headline-md text-d-headline-md font-bold tracking-tighter text-d-primary">
            RETRACK
          </Link>
        </div>
        
        <div className="flex-1 overflow-y-auto py-6">
          <nav className="px-4 space-y-2">
            {navItems.map((item) => (
              <SidebarItem 
                key={item.to}
                {...item}
                active={location.pathname.startsWith(item.to) || (location.pathname === '/depot' && item.to === '/depot/dashboard')}
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
              <p className="font-d-label-md text-d-label-md text-d-on-surface">Depot Admin</p>
              <p className="font-d-label-sm text-d-label-sm text-d-on-surface-variant">Kho ABC</p>
            </div>
          </div>
        </div>
      </aside>

      {/* Main Content Area */}
      <main className="flex-1 ml-64 flex flex-col h-screen overflow-hidden">
        {/* TopAppBar */}
        <header className="d-glass-panel flex justify-between items-center h-16 px-8 shrink-0 z-40">
          <div className="flex items-center text-d-on-surface-variant font-d-body-sm text-d-body-sm gap-2">
            <span className="hover:text-d-primary cursor-pointer transition-colors">Trang chủ</span>
            <MaterialIcon name="chevron_right" className="text-[16px]" />
            <span className="text-d-primary font-bold">Quản lý kho</span>
          </div>
          
          <div className="flex items-center gap-6">
            <div className="relative w-64">
              <MaterialIcon name="search" className="absolute left-3 top-1/2 -translate-y-1/2 text-d-outline" />
              <input 
                type="text" 
                className="w-full bg-d-surface-container-lowest border border-d-border-subtle rounded-full py-2 pl-10 pr-4 font-d-body-sm text-d-body-sm text-d-on-surface focus:outline-none focus:border-d-secondary transition-colors" 
                placeholder="Tìm kiếm..." 
              />
            </div>
            
            <div className="flex items-center gap-4 text-d-on-surface-variant">
              <button className="hover:text-d-primary transition-colors active:opacity-70 relative">
                <MaterialIcon name="notifications" />
                <span className="absolute top-0 right-0 w-2 h-2 bg-d-error rounded-full"></span>
              </button>
              <button className="hover:text-d-primary transition-colors active:opacity-70">
                <MaterialIcon name="help_outline" />
              </button>
            </div>
          </div>
        </header>

        {/* Page Content */}
        <div className="flex-1 overflow-y-auto bg-d-background">
          <Outlet />
        </div>
      </main>
    </div>
  );
};

export default DepotLayout;
