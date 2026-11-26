import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import { LayoutDashboard, Users, Tags, Banknote, ScrollText, ShieldCheck, ArrowLeftRight, Flag } from 'lucide-react';
import AppShell from '@/components/layout/AppShell';
import { useAuth } from '@/hooks/useAuth';

/* Khung giao diện Admin: dùng AppShell chung, chỉ khai báo menu và vai trò. */
const NAV = [
  { title: 'Tổng quan', items: [{ icon: LayoutDashboard, label: 'Dashboard', to: '/admin' }] },
  {
    title: 'Quản lý',
    items: [
      { icon: Users, label: 'Người dùng', to: '/admin/users' },
      { icon: ShieldCheck, label: 'Hồ sơ kho vựa', to: '/admin/depot-verifications' },
      { icon: Flag, label: 'Báo bỏ mua thanh lý', to: '/admin/liquidation-disputes' },
    ],
  },
  {
    title: 'Tài chính',
    items: [
      { icon: Tags, label: 'Bảng giá thị trường', to: '/admin/market-prices' },
      { icon: Banknote, label: 'Gói dịch vụ & Doanh thu', to: '/admin/billing' },
      { icon: ArrowLeftRight, label: 'Đối soát thu chi cũ', to: '/admin/money' },
    ],
  },
  { title: 'Hệ thống', items: [{ icon: ScrollText, label: 'Audit logs', to: '/admin/audit-logs' }] },
];

export default function AdminLayout({ children, crumb }) {
  const navigate = useNavigate();
  const { user, logout } = useAuth();

  const handleLogout = () => {
    logout();
    toast.success('Đã đăng xuất');
    navigate('/login');
  };

  return (
    <AppShell
      nav={NAV}
      eyebrow="Quản trị hệ thống"
      homeTo="/admin"
      homeLabel="Quản trị"
      crumb={crumb}
      roleLabel="Quản trị viên"
      roleBadge="ADMIN"
      user={user}
      onLogout={handleLogout}
      storageKey="retrack_admin_sidebar"
      navLabel="Menu quản trị"
    >
      <div className="mx-auto w-full max-w-[1400px]">{children}</div>
    </AppShell>
  );
}
