import { useLocation, Link, useNavigate } from 'react-router-dom';
import clsx from 'clsx';
import toast from 'react-hot-toast';
import { useAuth } from '@/hooks/useAuth';
import { Recycle, LayoutDashboard, PlusCircle, ClipboardList, User, Star, BarChart3, LogOut, Menu, X } from 'lucide-react';
import { useState } from 'react';

const NAV_ITEMS = [
  { icon: LayoutDashboard, label: 'Tổng quan', to: '/seller' },
  { icon: PlusCircle, label: 'Tạo đơn thu gom', to: '/seller/create' },
  { icon: ClipboardList, label: 'Đơn của tôi', to: '/seller/requests' },
  { icon: BarChart3, label: 'Thống kê thu nhập', to: '/seller/income' },
  { icon: User, label: 'Hồ sơ cá nhân', to: '/seller/profile' },
];

export default function SellerLayout({ children }) {
  const location = useLocation();
  const navigate = useNavigate();
  const { user, logout } = useAuth();
  const [mobileOpen, setMobileOpen] = useState(false);

  const isActive = (to) => to === '/seller' ? location.pathname === '/seller' : location.pathname.startsWith(to);

  const handleLogout = () => {
    logout();
    toast.success('Đã đăng xuất');
    navigate('/login');
  };

  const initials = (user?.fullName ?? 'S').split(' ').filter(Boolean).slice(-2).map(s => s[0]).join('').toUpperCase();

  const sidebarContent = (
    <>
      {/* Logo */}
      <div className="h-16 flex items-center px-5 border-b border-emerald-500/10 gap-3">
        <div className="w-9 h-9 bg-green-200 rounded-xl flex items-center justify-center shadow-lg shadow-green-200/50">
          <Recycle size={20} className="text-gray-900" />
        </div>
        <div>
          <span className="text-lg font-black text-gray-900 tracking-tight">RETRACK</span>
          <span className="ml-2 text-[9px] font-bold text-green-700 bg-green-200/15 px-2 py-0.5 rounded-full uppercase tracking-wider">Seller</span>
        </div>
      </div>

      {/* Nav */}
      <nav className="flex-1 overflow-y-auto py-4 px-3 space-y-1">
        {NAV_ITEMS.map(({ icon: Icon, label, to }) => (
          <Link
            key={to}
            to={to}
            onClick={() => setMobileOpen(false)}
            className={clsx(
              'flex items-center gap-3 px-4 py-3 rounded-xl text-sm font-medium transition-all duration-200',
              isActive(to)
                ? 'bg-green-200/15 text-green-700 shadow-sm'
                : 'text-gray-600 hover:text-gray-800 hover:bg-gray-100/60'
            )}
          >
            <Icon size={18} />
            {label}
          </Link>
        ))}
      </nav>

      {/* User card */}
      <div className="p-3 border-t border-gray-200">
        <div className="flex items-center gap-3 px-3 py-2">
          <div className="w-9 h-9 rounded-full bg-gradient-to-br from-emerald-500 to-teal-600 flex items-center justify-center text-sm font-bold text-gray-900 shadow-lg">
            {initials}
          </div>
          <div className="min-w-0 flex-1">
            <p className="text-sm font-semibold text-gray-800 truncate">{user?.fullName ?? 'Seller'}</p>
            <p className="text-xs text-gray-500 truncate">{user?.email}</p>
          </div>
        </div>
        <button
          onClick={handleLogout}
          className="w-full mt-1 flex items-center gap-3 px-4 py-2.5 rounded-xl text-red-400 hover:bg-red-500/10 transition-colors text-sm font-medium"
        >
          <LogOut size={16} />
          Đăng xuất
        </button>
      </div>
    </>
  );

  return (
    <div className="flex h-screen overflow-hidden bg-gray-50">
      {/* Desktop Sidebar */}
      <aside className="hidden lg:flex w-64 flex-col bg-white/80 backdrop-blur-xl border-r border-gray-200/60 h-screen fixed top-0 left-0 z-40">
        {sidebarContent}
      </aside>

      {/* Mobile overlay */}
      {mobileOpen && (
        <div className="fixed inset-0 z-50 lg:hidden">
          <div className="absolute inset-0 bg-black/60 backdrop-blur-sm" onClick={() => setMobileOpen(false)} />
          <aside className="absolute left-0 top-0 w-72 h-full bg-white flex flex-col shadow-2xl">
            {sidebarContent}
          </aside>
        </div>
      )}

      {/* Main */}
      <main className="flex-1 lg:ml-64 flex flex-col h-screen overflow-hidden">
        {/* Mobile top bar */}
        <header className="lg:hidden flex items-center justify-between px-4 py-3 border-b border-gray-200 bg-white/90 backdrop-blur-lg">
          <button onClick={() => setMobileOpen(true)} className="text-gray-600 p-1">
            <Menu size={24} />
          </button>
          <div className="flex items-center gap-2">
            <Recycle size={18} className="text-green-700" />
            <span className="font-bold text-gray-900">RETRACK</span>
          </div>
          <div className="w-8 h-8 rounded-full bg-green-200/20 flex items-center justify-center text-xs font-bold text-green-700">
            {initials}
          </div>
        </header>

        {/* Page content */}
        <div className="flex-1 overflow-y-auto">{children}</div>
      </main>
    </div>
  );
}

