import { useEffect, useRef, useState } from 'react';
import { useLocation, Link, useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import {
  LayoutDashboard, Users, Tags, Banknote, ScrollText, ShieldCheck, ChevronsLeft, ChevronsRight, Menu, X,
  LogOut, ChevronRight, ChevronDown, Home,
} from 'lucide-react';
import '../../features/depot/styles/depot-theme.css';
import BrandLogo, { BrandMark } from '@/components/brand/BrandLogo';
import { useAuth } from '@/hooks/useAuth';

/*
 * Khung giao diện Admin: sidebar nhóm menu (thu gọn được, có drawer trên mobile),
 * header dính trên cùng với breadcrumb và menu người dùng.
 * Màu: xanh lá chủ đạo của Retrack (#446900 và dải tối của nó), điểm nhấn lime #a3e635.
 */
const NAV = [
  { title: 'TỔNG QUAN', items: [{ icon: LayoutDashboard, label: 'Dashboard', to: '/admin' }] },
  {
    title: 'QUẢN LÝ',
    items: [
      { icon: Users, label: 'Người dùng', to: '/admin/users' },
      { icon: ShieldCheck, label: 'Hồ sơ kho vựa', to: '/admin/depot-verifications' },
    ],
  },
  {
    title: 'TÀI CHÍNH',
    items: [
      { icon: Tags, label: 'Bảng giá thị trường', to: '/admin/market-prices' },
      { icon: Banknote, label: 'Phí & Doanh thu', to: '/admin/billing' },
    ],
  },
  { title: 'HỆ THỐNG', items: [{ icon: ScrollText, label: 'Audit logs', to: '/admin/audit-logs' }] },
];

const C = {
  surface: 'linear-gradient(180deg,#2a4300 0%,#1b2d00 55%,#121e00 100%)',
  brandText: '#f4fbe6',
  eyebrow: '#a3e635',
  section: '#7f9a4d',
  icon: '#b5cf86',
  label: '#e3efcb',
  activeIcon: '#a3e635',
  activeLabel: '#ffffff',
  activeBg: 'rgba(163,230,53,.16)',
  hover: 'rgba(255,255,255,.07)',
  border: 'rgba(255,255,255,.08)',
  footerSub: '#93ad63',
};

function useCollapsed() {
  const [collapsed, setCollapsed] = useState(() => {
    try { return localStorage.getItem('retrack_admin_sidebar') === '1'; } catch { return false; }
  });
  const toggle = () => setCollapsed((c) => {
    const next = !c;
    try { localStorage.setItem('retrack_admin_sidebar', next ? '1' : '0'); } catch { /* bỏ qua khi không có storage */ }
    return next;
  });
  return [collapsed, toggle];
}

function initialsOf(name) {
  return (name ?? 'AD').split(' ').filter(Boolean).slice(-2).map((s) => s[0]).join('').toUpperCase() || 'AD';
}

function SidebarNav({ collapsed, isActive, onNavigate }) {
  return (
    <nav className="flex flex-col px-3 pb-4 pt-1" aria-label="Menu quản trị">
      {NAV.map((group, gi) => (
        <div key={group.title}>
          {collapsed ? (gi > 0 && <div className="mx-2 my-2 border-t" style={{ borderColor: C.border }} />) : (
            <div className={`px-2.5 pb-2 text-[10px] font-bold ${gi === 0 ? 'pt-1' : 'pt-5'}`} style={{ color: C.section, letterSpacing: '.12em' }}>{group.title}</div>
          )}
          <ul className={`flex flex-col ${collapsed ? 'gap-1' : 'gap-0.5'}`}>
            {group.items.map(({ icon: Icon, label, to }) => {
              const active = isActive(to);
              return (
                <li key={to} className={collapsed ? 'flex justify-center' : ''}>
                  <Link
                    to={to}
                    onClick={onNavigate}
                    title={collapsed ? label : undefined}
                    aria-label={collapsed ? label : undefined}
                    aria-current={active ? 'page' : undefined}
                    className={`group flex items-center transition-colors ${collapsed ? 'size-10 justify-center rounded-lg' : 'w-full gap-3 rounded-[10px] px-2.5 py-2.5'}`}
                    style={{ background: active ? C.activeBg : undefined, color: active ? C.activeLabel : C.label }}
                    onMouseEnter={(e) => { if (!active) e.currentTarget.style.background = C.hover; }}
                    onMouseLeave={(e) => { if (!active) e.currentTarget.style.background = ''; }}
                  >
                    <Icon size={collapsed ? 20 : 18} strokeWidth={2} color={active ? C.activeIcon : C.icon} className="shrink-0" />
                    {!collapsed && <span className={`flex-1 truncate text-[13.5px] ${active ? 'font-bold' : 'font-medium'}`}>{label}</span>}
                    {!collapsed && active && <span className="size-1.5 rounded-full bg-[#a3e635]" />}
                  </Link>
                </li>
              );
            })}
          </ul>
        </div>
      ))}
    </nav>
  );
}

function Brand({ collapsed }) {
  return collapsed ? <BrandMark size={34} /> : (
    <div className="flex items-center gap-2.5">
      <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-white shadow-md"><BrandMark size={32} /></span>
      <div className="leading-none">
        <div className="text-[19px] font-black tracking-tight" style={{ color: C.brandText }}>RETRACK</div>
        <div className="pt-1 text-[8.5px] font-bold" style={{ color: C.eyebrow, letterSpacing: '.16em' }}>QUẢN TRỊ HỆ THỐNG</div>
      </div>
    </div>
  );
}

function UserFooter({ user, collapsed }) {
  return (
    <div className={`flex items-center gap-2.5 p-3 ${collapsed ? 'justify-center' : ''}`} style={{ borderTop: `1px solid ${C.border}` }}>
      <span className="flex size-[34px] shrink-0 items-center justify-center rounded-[9px] bg-[#a3e635] text-[13px] font-black text-[#1b2d00]">{initialsOf(user?.fullName)}</span>
      {!collapsed && (
        <div className="min-w-0 flex-1 leading-snug">
          <div className="truncate text-[13px] font-semibold" style={{ color: C.brandText }}>{user?.fullName ?? 'Admin'}</div>
          <div className="truncate text-[11px]" style={{ color: C.footerSub }}>Quản trị viên</div>
        </div>
      )}
    </div>
  );
}

export default function AdminLayout({ children, crumb }) {
  const location = useLocation();
  const navigate = useNavigate();
  const { user, logout } = useAuth();
  const [collapsed, toggleCollapsed] = useCollapsed();
  const [drawer, setDrawer] = useState(false);
  const [menu, setMenu] = useState(false);
  const menuRef = useRef(null);

  const isActive = (to) => (to === '/admin' ? location.pathname === '/admin' : location.pathname.startsWith(to));

  // Đóng drawer khi đổi trang hoặc nhấn Esc; đóng menu người dùng khi bấm ra ngoài.
  useEffect(() => { setDrawer(false); setMenu(false); }, [location.pathname]);
  useEffect(() => {
    const onKey = (e) => { if (e.key === 'Escape') { setDrawer(false); setMenu(false); } };
    const onClick = (e) => { if (menuRef.current && !menuRef.current.contains(e.target)) setMenu(false); };
    document.addEventListener('keydown', onKey);
    document.addEventListener('mousedown', onClick);
    return () => { document.removeEventListener('keydown', onKey); document.removeEventListener('mousedown', onClick); };
  }, []);
  useEffect(() => {
    const prev = document.body.style.overflow;
    if (drawer) document.body.style.overflow = 'hidden';
    return () => { document.body.style.overflow = prev; };
  }, [drawer]);

  const handleLogout = () => {
    logout();
    toast.success('Đã đăng xuất');
    navigate('/login');
  };

  return (
    <div className="flex min-h-screen bg-d-background font-d-body-md text-d-on-background">
      {/* Sidebar desktop: ghim vào khung nhìn, menu cuộn trong chính nó */}
      <aside
        aria-label="Thanh điều hướng"
        className={`sticky top-0 hidden h-dvh shrink-0 flex-col self-start transition-[width] duration-200 ease-out lg:flex ${collapsed ? 'w-16' : 'w-[260px]'}`}
        style={{ background: C.surface }}
      >
        <div className={`flex shrink-0 items-center ${collapsed ? 'justify-center px-2 pb-3 pt-4' : 'px-[18px] pb-4 pt-5'}`}>
          <Link to="/admin" aria-label="RETRACK quản trị"><Brand collapsed={collapsed} /></Link>
        </div>
        <button
          type="button"
          onClick={toggleCollapsed}
          aria-label={collapsed ? 'Mở rộng menu' : 'Thu gọn menu'}
          className="absolute -right-3 top-1/2 z-20 flex size-6 -translate-y-1/2 items-center justify-center rounded-full border border-d-border-subtle bg-white text-d-on-surface-variant shadow-md transition hover:text-d-primary"
        >
          {collapsed ? <ChevronsRight size={14} /> : <ChevronsLeft size={14} />}
        </button>
        <div className="min-h-0 flex-1 overflow-y-auto overscroll-contain"><SidebarNav collapsed={collapsed} isActive={isActive} /></div>
        <UserFooter user={user} collapsed={collapsed} />
      </aside>

      {/* Drawer mobile */}
      {drawer && (
        <div className="fixed inset-0 z-50 lg:hidden">
          <button type="button" aria-label="Đóng menu" className="absolute inset-0 bg-slate-900/50 backdrop-blur-[2px]" onClick={() => setDrawer(false)} />
          <aside role="dialog" aria-modal="true" aria-label="Menu quản trị" className="absolute inset-y-0 left-0 flex w-72 max-w-[85vw] flex-col shadow-2xl" style={{ background: C.surface }}>
            <div className="flex items-center justify-between px-[18px] py-4" style={{ borderBottom: `1px solid ${C.border}` }}>
              <Link to="/admin" onClick={() => setDrawer(false)}><Brand /></Link>
              <button type="button" onClick={() => setDrawer(false)} aria-label="Đóng" className="flex size-9 items-center justify-center rounded-full text-white/80 hover:bg-white/10"><X size={20} /></button>
            </div>
            <div className="min-h-0 flex-1 overflow-y-auto pt-3"><SidebarNav collapsed={false} isActive={isActive} onNavigate={() => setDrawer(false)} /></div>
            <UserFooter user={user} collapsed={false} />
          </aside>
        </div>
      )}

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-40 flex h-16 items-center gap-3 border-b border-d-border-subtle bg-white/90 px-4 backdrop-blur md:px-6">
          <button type="button" onClick={() => setDrawer(true)} aria-label="Mở menu" className="flex size-10 items-center justify-center rounded-full text-d-on-surface-variant hover:bg-d-surface-container lg:hidden"><Menu size={22} /></button>
          <Link to="/admin" className="lg:hidden" aria-label="RETRACK quản trị"><BrandLogo size={30} showText={false} /></Link>

          <nav aria-label="Đường dẫn" className="flex min-w-0 flex-1 items-center gap-1.5 text-sm text-d-on-surface-variant">
            <Link to="/admin" className="flex items-center gap-1.5 hover:text-d-primary"><Home size={15} /><span className="hidden sm:inline">Quản trị</span></Link>
            {crumb && <><ChevronRight size={14} className="shrink-0" /><span className="truncate font-bold text-d-primary">{crumb}</span></>}
          </nav>

          <div className="relative" ref={menuRef}>
            <button type="button" onClick={() => setMenu((m) => !m)} aria-haspopup="menu" aria-expanded={menu}
              className="flex items-center gap-2.5 rounded-full py-1 pl-1 pr-3 transition hover:bg-d-surface-container">
              <span className="flex size-9 items-center justify-center rounded-full bg-d-primary text-[13px] font-black text-white">{initialsOf(user?.fullName)}</span>
              <span className="hidden text-left leading-tight sm:block">
                <span className="block max-w-[10rem] truncate text-sm font-semibold text-d-on-surface">{user?.fullName ?? 'Admin'}</span>
                <span className="block text-[11px] text-d-on-surface-variant">Quản trị viên</span>
              </span>
              <ChevronDown size={15} className={`text-d-on-surface-variant transition ${menu ? 'rotate-180' : ''}`} />
            </button>
            {menu && (
              <div role="menu" className="absolute right-0 top-full z-50 mt-2 w-64 overflow-hidden rounded-2xl border border-d-border-subtle bg-white shadow-xl">
                <div className="border-b border-d-border-subtle bg-d-surface-accent px-4 py-3">
                  <p className="truncate text-sm font-bold text-d-on-surface">{user?.fullName ?? 'Admin'}</p>
                  <p className="truncate text-xs text-d-on-surface-variant">{user?.email}</p>
                  <span className="mt-1.5 inline-block rounded-full bg-d-primary/10 px-2.5 py-0.5 text-[11px] font-bold text-d-primary">ADMIN</span>
                </div>
                <button type="button" role="menuitem" onClick={handleLogout} className="flex w-full items-center gap-2.5 px-4 py-3 text-sm font-semibold text-d-error transition hover:bg-d-error-container/40">
                  <LogOut size={17} />Đăng xuất
                </button>
              </div>
            )}
          </div>
        </header>

        <main className="min-w-0 flex-1">
          <div className="mx-auto w-full max-w-[1400px]">{children}</div>
        </main>
      </div>
    </div>
  );
}
