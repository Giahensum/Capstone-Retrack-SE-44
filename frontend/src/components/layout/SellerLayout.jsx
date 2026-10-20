import { useState, useEffect } from 'react';
import { useLocation, Link, useNavigate } from 'react-router-dom';
import clsx from 'clsx';
import toast from 'react-hot-toast';
import { useAuth } from '@/hooks/useAuth';
import DepotBrandLogo from '@/features/depot/components/DepotBrandLogo';
import { MaterialIcon } from '@/features/depot/components/DepotIcon';

const NAV_ITEMS = [
  { iconName: 'dashboard', label: 'Tổng quan', to: '/seller' },
  { iconName: 'add_box', label: 'Tạo đơn thu gom', to: '/seller/create' },
  { iconName: 'receipt_long', label: 'Đơn của tôi', to: '/seller/requests' },
  { iconName: 'bar_chart', label: 'Thống kê thu nhập', to: '/seller/income' },
  { iconName: 'inventory_2', label: 'Hồ sơ cá nhân', to: '/seller/profile' },
];

export default function SellerLayout({ children }) {
  const location = useLocation();
  const navigate = useNavigate();
  const { user, logout } = useAuth();
  const [menuOpen, setMenuOpen] = useState(false);

  useEffect(() => {
    const media = window.matchMedia('(min-width: 1024px)');
    const change = () => {
      if (media.matches) setMenuOpen(false);
    };
    media.addEventListener('change', change);
    return () => media.removeEventListener('change', change);
  }, []);

  const isActive = (to) => {
    if (to === '/seller') return location.pathname === '/seller';
    return location.pathname.startsWith(to);
  };

  const handleLogout = () => {
    logout();
    toast.success('Đã đăng xuất');
    navigate('/login');
  };

  const initials = (user?.fullName ?? 'S')
    .trim()
    .split(/\s+/)
    .slice(-2)
    .map((s) => s[0])
    .join('')
    .toLocaleUpperCase('vi-VN') || 'NB';

  const currentNav = NAV_ITEMS.find((item) => isActive(item.to)) ?? NAV_ITEMS[0];

  return (
    <div className="bg-d-background flex h-screen overflow-hidden font-d-body-md text-d-on-background">
      {/* Mobile backdrop */}
      {menuOpen && (
        <button
          type="button"
          aria-label="Đóng menu"
          className="fixed inset-0 bg-black/30 z-40 lg:hidden"
          onClick={() => setMenuOpen(false)}
        />
      )}

      {/* Sidebar */}
      <aside
        aria-label="Menu Người bán"
        className={clsx(
          "w-64 bg-d-surface-container-lowest border-r border-d-border-subtle h-screen fixed top-0 left-0 flex flex-col z-50 transition-transform lg:translate-x-0",
          menuOpen ? "translate-x-0" : "-translate-x-full"
        )}
      >
        {/* Brand header */}
        <div className="h-16 flex items-center justify-between px-6 border-b border-d-border-subtle">
          <Link
            to="/seller"
            className="inline-flex items-center gap-2 font-d-headline-md text-d-headline-md font-bold tracking-tighter text-d-primary"
            aria-label="ReTrack — về trang chủ người bán"
            onClick={() => setMenuOpen(false)}
          >
            <DepotBrandLogo />
            <span>RETRACK</span>
          </Link>
          <span className="text-[10px] font-bold text-d-primary bg-d-primary-container/50 text-d-on-primary-container px-2 py-0.5 rounded-full uppercase tracking-wider">
            Seller
          </span>
        </div>

        {/* Navigation list */}
        <div className="flex-1 overflow-y-auto py-6">
          <nav className="px-4 space-y-1.5" onClick={() => setMenuOpen(false)}>
            {NAV_ITEMS.map((item) => {
              const active = isActive(item.to);
              return (
                <Link
                  key={item.to}
                  to={item.to}
                  className={clsx(
                    "flex items-center gap-3 px-4 py-3 rounded-xl transition-colors font-d-label-md text-d-label-md",
                    active
                      ? "bg-d-primary/10 text-d-primary font-semibold"
                      : "text-d-on-surface-variant hover:bg-d-surface-container"
                  )}
                >
                  <MaterialIcon name={item.iconName} className="text-xl shrink-0" />
                  <span>{item.label}</span>
                </Link>
              );
            })}

            <div className="pt-2">
              <Link
                to="/seller/create"
                onClick={() => setMenuOpen(false)}
                className="w-full flex items-center gap-2 rounded-xl px-4 py-3 text-d-primary font-semibold hover:bg-d-surface-container transition-colors text-d-label-md"
              >
                <span>＋ Tạo đơn thu gom mới</span>
              </Link>
            </div>
          </nav>
        </div>

        {/* User Card / Footer */}
        <div className="p-4 border-t border-d-border-subtle bg-d-surface-container-lowest">
          <div className="flex items-center gap-3 px-2 py-1">
            <div className="w-9 h-9 rounded-full bg-d-primary-container text-d-on-primary-container flex items-center justify-center font-d-label-md font-bold shrink-0">
              {initials}
            </div>
            <div className="min-w-0 flex-1">
              <p className="font-d-label-md text-d-label-md text-d-on-surface font-semibold truncate">
                {user?.fullName ?? 'Người bán'}
              </p>
              <p className="text-xs text-d-on-surface-variant truncate">
                {user?.email ?? 'seller@retrack.vn'}
              </p>
            </div>
          </div>
          <button
            onClick={handleLogout}
            className="w-full mt-2 flex items-center justify-center gap-2 px-3 py-2 rounded-xl text-d-on-surface-variant hover:text-d-error hover:bg-d-error-container/20 font-d-label-sm text-d-label-sm transition-colors"
          >
            <MaterialIcon name="cancel" className="text-sm" />
            <span>Đăng xuất</span>
          </button>
        </div>
      </aside>

      {/* Main Content Area */}
      <main className="flex-1 min-w-0 lg:ml-64 flex flex-col h-screen overflow-hidden">
        {/* TopAppBar */}
        <header className="d-glass-panel flex justify-between gap-3 items-center min-h-16 px-4 lg:px-8 shrink-0 z-30">
          <div className="flex items-center gap-3">
            <button
              type="button"
              aria-label="Mở menu"
              aria-expanded={menuOpen}
              className="lg:hidden text-d-on-surface p-1.5 rounded-lg hover:bg-d-surface-container"
              onClick={() => setMenuOpen(true)}
            >
              <MaterialIcon name="menu" className="text-2xl" />
            </button>
            <Link to="/seller" className="lg:hidden" aria-label="ReTrack — về trang chủ">
              <DepotBrandLogo small />
            </Link>
            <div className="hidden sm:flex min-w-0 items-center text-d-on-surface-variant font-d-body-sm text-d-body-sm gap-2">
              <Link to="/seller" className="hover:text-d-primary transition-colors">
                Trang chủ
              </Link>
              <MaterialIcon name="chevron_right" className="text-[16px]" />
              <span className="text-d-primary font-bold">{currentNav.label}</span>
            </div>
          </div>

          <div className="flex items-center gap-3">
            <Link
              to="/seller/create"
              className="hidden sm:inline-flex items-center gap-1.5 rounded-full bg-d-primary text-d-on-primary px-4 py-2 text-sm font-semibold hover:bg-[#385500] transition-colors shadow-sm"
            >
              <MaterialIcon name="add" className="text-lg" />
              <span>Tạo đơn</span>
            </Link>
            <div className="flex items-center gap-2 pl-2 border-l border-d-border-subtle">
              <div className="w-8 h-8 rounded-full bg-d-primary-container text-d-on-primary-container flex items-center justify-center text-xs font-bold">
                {initials}
              </div>
              <span className="hidden md:inline font-d-label-sm text-d-label-sm text-d-on-surface font-medium truncate max-w-[120px]">
                {user?.fullName}
              </span>
            </div>
          </div>
        </header>

        {/* Page Content */}
        <div className="flex-1 overflow-y-auto bg-d-background">
          {children}
        </div>
      </main>
    </div>
  );
}
