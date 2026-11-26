import { Link, useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import { useQuery } from '@tanstack/react-query';
import {
  LayoutDashboard, ScanLine, SquarePlus, ReceiptText, Gavel, Bell, BarChart3, Wallet, UserRound, Plus,
} from 'lucide-react';
import AppShell from '@/components/layout/AppShell';
import { useAuth } from '@/hooks/useAuth';
import SellerNotificationBell from '@/features/seller/components/SellerNotificationBell';
import { sellerApi } from '@/features/seller/api';

/* Khung giao diện Người bán: dùng AppShell chung, giữ menu, chuông thông báo và lối tắt tạo đơn. */
export default function SellerLayout({ children }) {
  const navigate = useNavigate();
  const { user, logout } = useAuth();

  const { data: unreadCount = 0 } = useQuery({
    queryKey: ['seller-notifications-unread'],
    queryFn: () => sellerApi.getUnreadCount(),
    refetchInterval: 30000,
  });

  const nav = [
    {
      title: 'Chung',
      items: [
        { icon: LayoutDashboard, label: 'Tổng quan', to: '/seller' },
        { icon: Bell, label: 'Thông báo', to: '/seller/notifications', badge: Number(unreadCount) || 0 },
      ],
    },
    {
      title: 'Thu gom',
      items: [
        { icon: ScanLine, label: 'AI Nhận diện phế liệu', to: '/seller/ai-scanner' },
        { icon: SquarePlus, label: 'Tạo đơn thu gom', to: '/seller/create' },
        { icon: ReceiptText, label: 'Đơn của tôi', to: '/seller/requests' },
        { icon: Gavel, label: 'Lô thanh lý', to: '/seller/liquidations' },
      ],
    },
    {
      title: 'Thu nhập',
      items: [
        { icon: BarChart3, label: 'Thống kê thu nhập', to: '/seller/income' },
        { icon: Wallet, label: 'Thu nhập & chi trả', to: '/seller/earnings' },
      ],
    },
    { title: 'Tài khoản', items: [{ icon: UserRound, label: 'Hồ sơ cá nhân', to: '/seller/profile' }] },
  ];

  const handleLogout = () => {
    logout();
    toast.success('Đã đăng xuất');
    navigate('/login');
  };

  return (
    <AppShell
      nav={nav}
      eyebrow="Người bán"
      homeTo="/seller"
      homeLabel="Người bán"
      roleLabel="Người bán"
      roleBadge="SELLER"
      user={user}
      onLogout={handleLogout}
      menuItems={[{ icon: UserRound, label: 'Hồ sơ cá nhân', to: '/seller/profile' }]}
      storageKey="retrack_seller_sidebar"
      navLabel="Menu người bán"
      headerActions={<>
        <SellerNotificationBell />
        <Link to="/seller/create" className="hidden items-center gap-1.5 rounded-full bg-d-primary px-4 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-[#385500] sm:inline-flex">
          <Plus size={17} />Tạo đơn
        </Link>
      </>}
    >
      {children}
    </AppShell>
  );
}
