import { Link, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '@/hooks/useAuth'
import { ROLES } from '@/lib/utils'
import {
  LayoutDashboard, Package, Truck, BarChart3,
  Factory, Users, Settings, LogOut, Menu, X, Recycle
} from 'lucide-react'
import { useState } from 'react'
import { cn } from '@/lib/cn'
import toast from 'react-hot-toast'

interface NavItem {
  label: string
  href: string
  icon: React.ReactNode
}

const NAV_BY_ROLE: Record<string, NavItem[]> = {
  [ROLES.SELLER]: [
    { label: 'Dashboard', href: '/seller', icon: <LayoutDashboard size={18} /> },
    { label: 'Yêu cầu thu gom', href: '/seller/requests', icon: <Package size={18} /> },
  ],
  [ROLES.DEPOT_OWNER]: [
    { label: 'Dashboard', href: '/depot', icon: <LayoutDashboard size={18} /> },
    { label: 'Yêu cầu đến', href: '/depot/requests', icon: <Package size={18} /> },
    { label: 'Lô hàng', href: '/depot/batches', icon: <Truck size={18} /> },
    { label: 'Đối tác', href: '/depot/partners', icon: <Factory size={18} /> },
    { label: 'Thống kê', href: '/depot/stats', icon: <BarChart3 size={18} /> },
  ],
  [ROLES.DEPOT_EMPLOYEE]: [
    { label: 'Dashboard', href: '/employee', icon: <LayoutDashboard size={18} /> },
    { label: 'Nhận đơn', href: '/employee/requests', icon: <Package size={18} /> },
  ],
  [ROLES.DRIVER]: [
    { label: 'Dashboard', href: '/driver', icon: <LayoutDashboard size={18} /> },
    { label: 'Công việc', href: '/driver/jobs', icon: <Truck size={18} /> },
  ],
  [ROLES.FACTORY]: [
    { label: 'Dashboard', href: '/factory', icon: <LayoutDashboard size={18} /> },
    { label: 'Nhu cầu', href: '/factory/demands', icon: <Package size={18} /> },
    { label: 'Lô hàng đến', href: '/factory/batches', icon: <Truck size={18} /> },
    { label: 'Đối tác', href: '/factory/partners', icon: <Factory size={18} /> },
  ],
  [ROLES.ADMIN]: [
    { label: 'Dashboard', href: '/admin', icon: <LayoutDashboard size={18} /> },
    { label: 'Người dùng', href: '/admin/users', icon: <Users size={18} /> },
    { label: 'Doanh thu', href: '/admin/revenue', icon: <BarChart3 size={18} /> },
    { label: 'Cấu hình', href: '/admin/config', icon: <Settings size={18} /> },
  ],
}

export function Sidebar() {
  const { user, role, logout } = useAuth()
  const location = useLocation()
  const navigate = useNavigate()
  const [collapsed, setCollapsed] = useState(false)

  const navItems = role ? (NAV_BY_ROLE[role] ?? []) : []

  const handleLogout = () => {
    logout()
    toast.success('Đã đăng xuất')
    navigate('/login')
  }

  return (
    <aside
      className={cn(
        'flex flex-col h-screen bg-slate-900 border-r border-slate-800 transition-all duration-300',
        collapsed ? 'w-16' : 'w-60'
      )}
    >
      {/* Logo */}
      <div className="flex items-center gap-3 px-4 py-5 border-b border-slate-800">
        <div className="w-8 h-8 bg-emerald-500 rounded-lg flex items-center justify-center flex-shrink-0">
          <Recycle size={18} className="text-white" />
        </div>
        {!collapsed && (
          <span className="font-bold text-slate-100 text-lg tracking-tight">ReTrack</span>
        )}
        <button
          onClick={() => setCollapsed(!collapsed)}
          className="ml-auto text-slate-400 hover:text-slate-100 transition-colors"
        >
          {collapsed ? <Menu size={18} /> : <X size={18} />}
        </button>
      </div>

      {/* Nav Items */}
      <nav className="flex-1 py-4 px-2 space-y-1">
        {navItems.map((item) => {
          const active = location.pathname === item.href
          return (
            <Link
              key={item.href}
              to={item.href}
              className={cn(
                'flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-all duration-150',
                active
                  ? 'bg-emerald-500/15 text-emerald-400 border border-emerald-500/20'
                  : 'text-slate-400 hover:bg-slate-800 hover:text-slate-100'
              )}
              title={collapsed ? item.label : undefined}
            >
              <span className="flex-shrink-0">{item.icon}</span>
              {!collapsed && item.label}
            </Link>
          )
        })}
      </nav>

      {/* User + Logout */}
      <div className="border-t border-slate-800 p-3">
        {!collapsed && (
          <div className="px-2 mb-2">
            <p className="text-xs font-semibold text-slate-100 truncate">{user?.fullName}</p>
            <p className="text-xs text-slate-400 truncate">{user?.role}</p>
          </div>
        )}
        <button
          onClick={handleLogout}
          className={cn(
            'flex items-center gap-3 px-3 py-2 rounded-lg text-sm text-red-400 hover:bg-red-500/10 transition-colors w-full',
            collapsed && 'justify-center'
          )}
          title={collapsed ? 'Đăng xuất' : undefined}
        >
          <LogOut size={16} />
          {!collapsed && 'Đăng xuất'}
        </button>
      </div>
    </aside>
  )
}
