import { useLocation, Link, useNavigate } from 'react-router-dom';
import clsx from 'clsx';
import toast from 'react-hot-toast';
import '../../features/depot/styles/depot-theme.css';
import { MaterialIcon } from '../ui/MaterialIcon';
import { useAuth } from '@/hooks/useAuth';

const NAV_ITEMS = [
  { iconName: 'dashboard', label: 'Tổng quan', to: '/admin' },
  { iconName: 'group', label: 'Người dùng', to: '/admin/users' },
  { iconName: 'sell', label: 'Bảng giá thị trường', to: '/admin/market-prices' },
  { iconName: 'payments', label: 'Phí & Doanh thu', to: '/admin/billing' },
  { iconName: 'history_edu', label: 'Audit logs', to: '/admin/audit-logs' },
];

function SidebarItem({ iconName, label, to, active }) {
  return (
    <Link
      to={to}
      className={clsx(
        'flex items-center gap-3 px-4 py-3 rounded-xl transition-colors',
        active ? 'bg-d-primary/10 text-d-primary font-medium' : 'text-d-on-surface-variant hover:bg-d-surface-container'
      )}
    >
      <MaterialIcon name={iconName} />
      <span className="font-d-label-md text-d-label-md">{label}</span>
    </Link>
  );
}

export default function AdminLayout({ children, crumb }) {
  const location = useLocation();
  const navigate = useNavigate();
  const { user, logout } = useAuth();

  const isActive = (to) => (to === '/admin' ? location.pathname === '/admin' : location.pathname.startsWith(to));

  const handleLogout = () => {
    logout();
    toast.success('Đã đăng xuất');
    navigate('/login');
  };

  const initials = (user?.fullName ?? 'AD').split(' ').filter(Boolean).slice(-2).map((s) => s[0]).join('').toUpperCase();

  return (
    <div className="bg-d-background flex h-screen overflow-hidden font-d-body-md text-d-on-background">
      {/* SideNavBar */}
      <aside className="w-64 bg-d-surface-container-lowest border-r border-d-border-subtle h-screen fixed top-0 left-0 flex flex-col z-40">
        <div className="h-16 flex items-center px-6 border-b border-d-border-subtle">
          <Link to="/admin" className="font-d-headline-md text-d-headline-md font-bold tracking-tighter text-d-primary">
            RETRACK
          </Link>
          <span className="ml-2 text-[10px] font-d-label-sm text-d-label-sm text-d-on-surface-variant uppercase tracking-wider bg-d-surface-variant px-2 py-0.5 rounded-full">
            Admin
          </span>
        </div>

        <div className="flex-1 overflow-y-auto py-6">
          <nav className="px-4 space-y-2">
            {NAV_ITEMS.map((item) => (
              <SidebarItem key={item.to} {...item} active={isActive(item.to)} />
            ))}
          </nav>
        </div>

        <div className="p-4 border-t border-d-border-subtle">
          <div className="flex items-center gap-3 px-4 py-2">
            <div className="w-8 h-8 rounded-full bg-d-primary-container text-d-on-primary-container flex items-center justify-center font-d-label-md font-bold">
              {initials || 'AD'}
            </div>
            <div className="min-w-0">
              <p className="font-d-label-md text-d-label-md text-d-on-surface truncate">{user?.fullName ?? 'Admin'}</p>
              <p className="font-d-label-sm text-d-label-sm text-d-on-surface-variant">Quản trị viên</p>
            </div>
          </div>
          <button
            onClick={handleLogout}
            className="w-full mt-1 flex items-center gap-3 px-4 py-2.5 rounded-xl text-d-error hover:bg-d-error-container/40 transition-colors font-d-label-md text-d-label-md"
          >
            <MaterialIcon name="logout" />
            Đăng xuất
          </button>
        </div>
      </aside>

      {/* Main Content Area */}
      <main className="flex-1 ml-64 flex flex-col h-screen overflow-hidden">
        {/* TopAppBar */}
        <header className="d-glass-panel flex justify-between items-center h-16 px-8 shrink-0 z-40">
          <div className="flex items-center text-d-on-surface-variant font-d-body-sm text-d-body-sm gap-2">
            <span className="hover:text-d-primary cursor-pointer transition-colors" onClick={() => navigate('/admin')}>Quản trị</span>
            {crumb && (
              <>
                <MaterialIcon name="chevron_right" className="text-[16px]" />
                <span className="text-d-primary font-bold">{crumb}</span>
              </>
            )}
          </div>

          <div className="flex items-center gap-4 text-d-on-surface-variant">
            <span className="font-d-label-sm text-d-label-sm">{user?.email}</span>
          </div>
        </header>

        {/* Page Content */}
        <div className="flex-1 overflow-y-auto bg-d-background">{children}</div>
      </main>
    </div>
  );
}
