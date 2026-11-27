import { createContext, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { ChevronsLeft, ChevronsRight, ChevronRight, ChevronDown, Home, LogOut, Menu, X } from 'lucide-react';
import '../../features/depot/styles/depot-theme.css';
import { BrandMark } from '@/components/brand/BrandLogo';

/*
 * Khung giao diện dùng chung cho mọi vai trò có sidebar (Admin, Chủ kho, Người bán, Nhà máy).
 * Bố cục theo mẫu quản trị FITLY: sidebar tối ghim vào khung nhìn (thu gọn được, drawer trên mobile),
 * header dính với breadcrumb, cụm hành động riêng của vai trò và menu tài khoản.
 * Màu giữ nhận diện RETRACK: xanh lá #446900 và dải tối của nó, điểm nhấn lime #a3e635.
 *
 * nav: [{ title, items: [{ key, icon, label, to?, onClick?, active?, badge? }] }]
 *  - mục có `to` được tô sáng theo quy tắc "đường dẫn khớp dài nhất thắng";
 *  - mục dùng `onClick` (Factory điều hướng theo tab) tự truyền `active`.
 */
// Trang bên trong đọc icon/nhãn của mục menu đang chọn để dựng đầu trang đồng nhất (PageHeader).
const ShellPageContext = createContext(null);
export function useShellPage() { return useContext(ShellPageContext); }

export const SHELL_COLORS = {
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
const C = SHELL_COLORS;

export function initialsOf(name, fallback = 'RT') {
  return (name ?? '').trim().split(/\s+/).filter(Boolean).slice(-2).map((s) => s[0]).join('').toLocaleUpperCase('vi-VN') || fallback;
}

function useCollapsed(storageKey) {
  const [collapsed, setCollapsed] = useState(() => {
    try { return localStorage.getItem(storageKey) === '1'; } catch { return false; }
  });
  const toggle = () => setCollapsed((c) => {
    const next = !c;
    try { localStorage.setItem(storageKey, next ? '1' : '0'); } catch { /* bỏ qua khi không có storage */ }
    return next;
  });
  return [collapsed, toggle];
}

function useActiveTo(nav, pathname) {
  return useMemo(() => {
    const all = nav.flatMap((g) => g.items).map((i) => i.to).filter(Boolean);
    return all.filter((to) => pathname === to || pathname.startsWith(to + '/')).sort((a, b) => b.length - a.length)[0] ?? null;
  }, [nav, pathname]);
}

function Badge({ value, collapsed }) {
  if (!value) return null;
  const text = value > 99 ? '99+' : Number(value).toLocaleString('vi-VN');
  return collapsed
    ? <span className="absolute right-1 top-1 size-2 rounded-full bg-[#a3e635] ring-2 ring-[#1b2d00]" aria-hidden="true" />
    : <span className="flex h-5 min-w-5 items-center justify-center rounded-full bg-[#a3e635] px-1.5 text-[11px] font-black text-[#1b2d00]">{text}</span>;
}

function SidebarNav({ nav, collapsed, activeTo, onNavigate, label }) {
  return (
    <nav className="flex flex-col px-3 pb-4 pt-1" aria-label={label}>
      {nav.map((group, gi) => (
        <div key={group.title}>
          {collapsed ? (gi > 0 && <div className="mx-2 my-2 border-t" style={{ borderColor: C.border }} />) : (
            <div className={`px-2.5 pb-2 text-[10px] font-bold uppercase ${gi === 0 ? 'pt-1' : 'pt-5'}`} style={{ color: C.section, letterSpacing: '.12em' }}>{group.title}</div>
          )}
          <ul className={`flex flex-col ${collapsed ? 'gap-1' : 'gap-0.5'}`}>
            {group.items.map((item) => {
              const { icon: Icon, label: text, to, onClick, badge } = item;
              const active = item.active ?? (to != null && to === activeTo);
              const className = `group relative flex items-center transition-colors ${collapsed ? 'size-10 justify-center rounded-lg' : 'w-full gap-3 rounded-[10px] px-2.5 py-2.5 text-left'}`;
              const style = { background: active ? C.activeBg : undefined, color: active ? C.activeLabel : C.label };
              const hover = {
                onMouseEnter: (e) => { if (!active) e.currentTarget.style.background = C.hover; },
                onMouseLeave: (e) => { if (!active) e.currentTarget.style.background = ''; },
              };
              const inner = (
                <>
                  <Icon size={collapsed ? 20 : 18} strokeWidth={2} color={active ? C.activeIcon : C.icon} className="shrink-0" />
                  {!collapsed && <span className={`flex-1 truncate text-[13.5px] ${active ? 'font-bold' : 'font-medium'}`}>{text}</span>}
                  <Badge value={badge} collapsed={collapsed} />
                  {!collapsed && active && !badge && <span className="size-1.5 rounded-full bg-[#a3e635]" />}
                </>
              );
              const common = { className, style, title: collapsed ? text : undefined, 'aria-label': collapsed ? text : undefined, 'aria-current': active ? 'page' : undefined, ...hover };
              return (
                <li key={item.key ?? to} className={collapsed ? 'flex justify-center' : ''}>
                  {to != null
                    ? <Link to={to} onClick={onNavigate} {...common}>{inner}</Link>
                    : <button type="button" onClick={() => { onClick?.(); onNavigate?.(); }} {...common}>{inner}</button>}
                </li>
              );
            })}
          </ul>
        </div>
      ))}
    </nav>
  );
}

function Brand({ collapsed, eyebrow }) {
  return collapsed ? <BrandMark size={34} /> : (
    <div className="flex items-center gap-2.5">
      <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-white shadow-md"><BrandMark size={32} /></span>
      <div className="leading-none">
        <div className="text-[19px] font-black tracking-tight" style={{ color: C.brandText }}>RETRACK</div>
        <div className="pt-1 text-[8.5px] font-bold uppercase" style={{ color: C.eyebrow, letterSpacing: '.16em' }}>{eyebrow}</div>
      </div>
    </div>
  );
}

function UserFooter({ user, roleLabel, collapsed }) {
  return (
    <div className={`flex items-center gap-2.5 p-3 ${collapsed ? 'justify-center' : ''}`} style={{ borderTop: `1px solid ${C.border}` }}>
      <span className="flex size-[34px] shrink-0 items-center justify-center rounded-[9px] bg-[#a3e635] text-[13px] font-black text-[#1b2d00]">{initialsOf(user?.fullName)}</span>
      {!collapsed && (
        <div className="min-w-0 flex-1 leading-snug">
          <div className="truncate text-[13px] font-semibold" style={{ color: C.brandText }}>{user?.fullName ?? user?.email ?? 'Người dùng'}</div>
          <div className="truncate text-[11px]" style={{ color: C.footerSub }}>{roleLabel}</div>
        </div>
      )}
    </div>
  );
}

function SidebarExtra({ children, collapsed }) {
  if (!children || collapsed) return null;
  return <div className="px-3 pb-3">{children}</div>;
}

// Logo/breadcrumb về trang chủ của vai trò: link nếu có homeTo, nút nếu điều hướng nội bộ (Factory).
function HomeLink({ home, children, className, onDone }) {
  const label = `RETRACK — ${home.homeLabel}`;
  return home.homeTo
    ? <Link to={home.homeTo} className={className} aria-label={label} onClick={onDone}>{children}</Link>
    : <button type="button" className={className} aria-label={label} onClick={() => { home.onHome?.(); onDone?.(); }}>{children}</button>;
}

export default function AppShell({
  nav,
  eyebrow,
  homeTo,
  onHome,
  homeLabel = 'Trang chủ',
  crumb,
  roleLabel,
  roleBadge,
  user,
  onLogout,
  menuItems = [],
  headerActions,
  sidebarExtra,
  storageKey = 'retrack_sidebar',
  navLabel = 'Menu điều hướng',
  contentScroll = false,
  scrollKey,
  children,
}) {
  const location = useLocation();
  const contentRef = useRef(null);
  useEffect(() => {
    if (contentScroll) contentRef.current?.scrollTo({ top: 0, left: 0 });
  }, [contentScroll, location.pathname, scrollKey]);
  const [collapsed, toggleCollapsed] = useCollapsed(storageKey);
  const [drawer, setDrawer] = useState(false);
  const [menu, setMenu] = useState(false);
  const menuRef = useRef(null);
  const activeTo = useActiveTo(nav, location.pathname);
  const activeItem = nav.flatMap((g) => g.items).find((i) => i.active ?? (i.to != null && i.to === activeTo));
  const crumbText = crumb ?? activeItem?.label;
  const shellPage = useMemo(() => (activeItem ? { icon: activeItem.icon, label: activeItem.label } : null), [activeItem?.icon, activeItem?.label]);

  // Đóng drawer khi đổi trang hoặc nhấn Esc; đóng menu tài khoản khi bấm ra ngoài.
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

  const home = { homeTo, onHome, homeLabel };

  return (
    <div className={`flex bg-d-background font-d-body-md text-d-on-background ${contentScroll ? 'h-dvh overflow-hidden' : 'min-h-screen'}`}>
      {/* Sidebar desktop: ghim vào khung nhìn, menu cuộn trong chính nó */}
      <aside
        aria-label="Thanh điều hướng"
        className={`sticky top-0 z-30 hidden h-dvh shrink-0 flex-col self-start transition-[width] duration-200 ease-out lg:flex ${collapsed ? 'w-16' : 'w-[260px]'}`}
        style={{ background: C.surface }}
      >
        <div className={`flex shrink-0 items-center ${collapsed ? 'justify-center px-2 pb-3 pt-4' : 'px-[18px] pb-4 pt-5'}`}>
          <HomeLink home={home}><Brand collapsed={collapsed} eyebrow={eyebrow} /></HomeLink>
        </div>
        <button
          type="button"
          onClick={toggleCollapsed}
          aria-label={collapsed ? 'Mở rộng menu' : 'Thu gọn menu'}
          className="absolute -right-3 top-1/2 z-20 flex size-6 -translate-y-1/2 items-center justify-center rounded-full border border-d-border-subtle bg-white text-d-on-surface-variant shadow-md transition hover:text-d-primary"
        >
          {collapsed ? <ChevronsRight size={14} /> : <ChevronsLeft size={14} />}
        </button>
        <div className="shell-scroll min-h-0 flex-1 overflow-y-auto overscroll-contain">
          <SidebarNav nav={nav} collapsed={collapsed} activeTo={activeTo} label={navLabel} />
          <SidebarExtra collapsed={collapsed}>{sidebarExtra}</SidebarExtra>
        </div>
        <UserFooter user={user} roleLabel={roleLabel} collapsed={collapsed} />
      </aside>

      {/* Drawer mobile */}
      {drawer && (
        <div className="fixed inset-0 z-50 lg:hidden">
          <button type="button" aria-label="Đóng menu" className="absolute inset-0 bg-slate-900/50 backdrop-blur-[2px]" onClick={() => setDrawer(false)} />
          <aside role="dialog" aria-modal="true" aria-label={navLabel} className="absolute inset-y-0 left-0 flex w-72 max-w-[85vw] flex-col shadow-2xl" style={{ background: C.surface }}>
            <div className="flex items-center justify-between px-[18px] py-4" style={{ borderBottom: `1px solid ${C.border}` }}>
              <HomeLink home={home} onDone={() => setDrawer(false)}><Brand eyebrow={eyebrow} /></HomeLink>
              <button type="button" onClick={() => setDrawer(false)} aria-label="Đóng" className="flex size-9 items-center justify-center rounded-full text-white/80 hover:bg-white/10"><X size={20} /></button>
            </div>
            <div className="shell-scroll min-h-0 flex-1 overflow-y-auto pt-3">
              <SidebarNav nav={nav} collapsed={false} activeTo={activeTo} label={navLabel} onNavigate={() => setDrawer(false)} />
              <SidebarExtra collapsed={false}>{sidebarExtra}</SidebarExtra>
            </div>
            <UserFooter user={user} roleLabel={roleLabel} collapsed={false} />
          </aside>
        </div>
      )}

      <div className={`flex min-w-0 flex-1 flex-col ${contentScroll ? 'min-h-0' : ''}`}>
        <header className="sticky top-0 z-40 flex h-16 shrink-0 items-center gap-3 border-b border-d-border-subtle bg-white/90 px-4 backdrop-blur md:px-6">
          <button type="button" onClick={() => setDrawer(true)} aria-label="Mở menu" aria-expanded={drawer} className="flex size-10 items-center justify-center rounded-full text-d-on-surface-variant hover:bg-d-surface-container lg:hidden"><Menu size={22} /></button>
          <HomeLink home={home} className="lg:hidden"><BrandMark size={30} /></HomeLink>

          <nav aria-label="Đường dẫn" className={`${contentScroll ? 'hidden sm:flex' : 'flex'} min-w-0 flex-1 items-center gap-1.5 text-sm text-d-on-surface-variant`}>
            <HomeLink home={home} className="flex items-center gap-1.5 hover:text-d-primary"><Home size={15} /><span className="hidden sm:inline">{homeLabel}</span></HomeLink>
            {crumbText && <><ChevronRight size={14} className="hidden shrink-0 sm:block" /><span className="hidden truncate font-bold text-d-primary sm:inline">{crumbText}</span></>}
          </nav>

          {headerActions && <div className="flex min-w-0 items-center gap-2">{headerActions}</div>}

          <div className="relative" ref={menuRef}>
            <button type="button" onClick={() => setMenu((m) => !m)} aria-haspopup="menu" aria-expanded={menu} aria-label="Tài khoản"
              className="flex items-center gap-2.5 rounded-full py-1 pl-1 pr-1 transition hover:bg-d-surface-container sm:pr-3">
              <span className="flex size-9 items-center justify-center rounded-full bg-d-primary text-[13px] font-black text-white">{initialsOf(user?.fullName)}</span>
              <span className="hidden text-left leading-tight md:block">
                <span className="block max-w-[10rem] truncate text-sm font-semibold text-d-on-surface">{user?.fullName ?? 'Người dùng'}</span>
                <span className="block text-[11px] text-d-on-surface-variant">{roleLabel}</span>
              </span>
              <ChevronDown size={15} className={`hidden text-d-on-surface-variant transition sm:block ${menu ? 'rotate-180' : ''}`} />
            </button>
            {menu && (
              <div role="menu" className="absolute right-0 top-full z-50 mt-2 w-64 overflow-hidden rounded-2xl border border-d-border-subtle bg-white shadow-xl">
                <div className="border-b border-d-border-subtle bg-d-surface-accent px-4 py-3">
                  <p className="truncate text-sm font-bold text-d-on-surface">{user?.fullName ?? 'Người dùng'}</p>
                  {user?.email && <p className="truncate text-xs text-d-on-surface-variant">{user.email}</p>}
                  {roleBadge && <span className="mt-1.5 inline-block rounded-full bg-d-primary/10 px-2.5 py-0.5 text-[11px] font-bold text-d-primary">{roleBadge}</span>}
                </div>
                {menuItems.map(({ key, icon: Icon, label, to, onClick }) => (to
                  ? <Link key={key ?? to} to={to} role="menuitem" onClick={() => setMenu(false)} className="flex w-full items-center gap-2.5 px-4 py-3 text-sm font-medium text-d-on-surface transition hover:bg-d-surface-container">{Icon && <Icon size={17} className="text-d-on-surface-variant" />}{label}</Link>
                  : <button key={key ?? label} type="button" role="menuitem" onClick={() => { setMenu(false); onClick?.(); }} className="flex w-full items-center gap-2.5 px-4 py-3 text-left text-sm font-medium text-d-on-surface transition hover:bg-d-surface-container">{Icon && <Icon size={17} className="text-d-on-surface-variant" />}{label}</button>))}
                <button type="button" role="menuitem" onClick={() => { setMenu(false); onLogout?.(); }} className="flex w-full items-center gap-2.5 border-t border-d-border-subtle px-4 py-3 text-sm font-semibold text-d-error transition hover:bg-d-error-container/40">
                  <LogOut size={17} />Đăng xuất
                </button>
              </div>
            )}
          </div>
        </header>

        <main ref={contentRef} tabIndex={contentScroll ? 0 : undefined} className={`retrack-content min-w-0 flex-1 ${contentScroll ? 'min-h-0 overflow-y-auto overscroll-contain' : ''}`}><ShellPageContext.Provider value={shellPage}>{children}</ShellPageContext.Provider></main>
      </div>
    </div>
  );
}
