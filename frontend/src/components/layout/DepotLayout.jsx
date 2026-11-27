import { useState } from 'react';
import { Outlet, Link, useLocation, useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import {
  LayoutDashboard, Bell, Gavel, Tags, Truck, Factory, Warehouse, Store, Users, ClipboardList,
  Activity, CreditCard, Package, BarChart3, Plus, ShieldAlert,
} from 'lucide-react';
import AppShell from '@/components/layout/AppShell';
import { DepotSelector, useDepot } from '@/features/depot/DepotContext';
import { useAuthStore } from '@/app/store/useAuthStore';
import CreateDepotModal from '@/features/depot/components/CreateDepotModal';
import { useDepotQuery } from '@/features/depot/depotApi';
import { verificationLabels } from '@/features/depot/components/VerificationPanel';
import NotificationBell from '@/features/depot/components/NotificationBell';
import SubscriptionBanner from '@/features/depot/components/SubscriptionBanner';

/* Khung giao diện Chủ kho vựa: dùng AppShell chung, giữ nguyên nhóm menu, chọn kho, thông báo và các banner trạng thái. */
const DepotLayout = () => {
  const location = useLocation();
  const navigate = useNavigate();
  const [createDepot, setCreateDepot] = useState(false);
  const { depotId, query: ownedDepots, capacity } = useDepot();
  const profile = useDepotQuery('profile');
  const unread = useDepotQuery('notifications/unread-count', {}, true, 30000);
  const user = useAuthStore((s) => s.user);
  const logout = useAuthStore((s) => s.logout);

  const nav = [
    {
      title: 'Chung',
      items: [
        { icon: LayoutDashboard, label: 'Tổng quan', to: '/depot/dashboard' },
        { icon: Bell, label: 'Thông báo', to: '/depot/notifications', badge: Number(unread.data) || 0 },
      ],
    },
    {
      title: 'Giao dịch',
      items: [
        { icon: Gavel, label: 'Lô lớn mua vào', to: '/depot/liquidations' },
        { icon: Tags, label: 'Giá thu mua', to: '/depot/buying-prices' },
        { icon: Truck, label: 'Lô xuất hàng', to: '/depot/batches' },
        { icon: Factory, label: 'Nhà máy đối tác', to: '/depot/partners' },
      ],
    },
    {
      title: 'Kho và hồ sơ',
      items: [
        { icon: Warehouse, label: 'Quản lý kho', to: '/depot/inventory' },
        { icon: Store, label: 'Hồ sơ kho vựa', to: '/depot/profile' },
      ],
    },
    {
      title: 'Đội ngũ',
      items: [
        { icon: Users, label: 'Quản lý nhân sự', to: '/depot/staff' },
        { icon: ClipboardList, label: 'Điều phối thu gom', to: '/depot/dispatch' },
        { icon: Activity, label: 'Hiệu suất nhân sự', to: '/depot/staff/performance' },
      ],
    },
    {
      title: 'Tài chính',
      items: [
        { icon: CreditCard, label: 'Thanh toán chờ duyệt', to: '/depot/payments' },
        { icon: Package, label: 'Gói dịch vụ', to: '/depot/subscription' },
        { icon: BarChart3, label: 'Báo cáo doanh thu', to: '/depot/reports' },
      ],
    },
  ];

  // Trang không nằm trong menu (lịch sử chi trả, phí cũ) vẫn có breadcrumb rõ ràng.
  const extraCrumbs = { '/depot/earnings': 'Lịch sử chi trả qua sàn', '/depot/payments/fees': 'Phí nền tảng cũ' };

  const handleLogout = () => {
    logout();
    toast.success('Đã đăng xuất');
    navigate('/login');
  };

  const verification = profile.data?.verificationStatus;

  return (
    <AppShell
      contentScroll
      scrollKey={`${location.pathname}:${depotId ?? ''}`}
      nav={nav}
      eyebrow="Chủ kho vựa"
      homeTo="/depot/dashboard"
      homeLabel="Kho vựa"
      crumb={extraCrumbs[location.pathname]}
      roleLabel="Chủ kho vựa"
      roleBadge="DEPOT OWNER"
      user={user}
      onLogout={handleLogout}
      storageKey="retrack_depot_sidebar"
      navLabel="Menu kho vựa"
      headerActions={<>
        {depotId && <NotificationBell key={`${depotId}-${location.pathname}`} unread={unread} />}
        <DepotSelector />
      </>}
      sidebarExtra={<div className="space-y-2">
        <button type="button" disabled={!capacity.data?.canAdd} onClick={() => setCreateDepot(true)}
          className="flex w-full items-center justify-center gap-2 rounded-[10px] border border-dashed border-white/20 px-3 py-2.5 text-[13px] font-semibold text-[#e3efcb] transition hover:border-[#a3e635] hover:bg-white/5 hover:text-white disabled:cursor-not-allowed disabled:opacity-50">
          <Plus size={16} />Thêm kho vựa
        </button>
        <p className="text-center text-xs text-[#b5cf86]">{capacity.data ? 'Kho đã tạo: ' + capacity.data.used + '/' + capacity.data.limit + (capacity.data.canAdd ? '' : ' · Đã đạt hạn mức') : capacity.isError ? 'Chưa tải được hạn mức kho.' : 'Đang tải hạn mức kho…'}</p>
        {capacity.isError && <button type="button" className="w-full text-xs text-white underline" onClick={() => capacity.refetch()}>Thử lại hạn mức</button>}
      </div>}
    >
      <SubscriptionBanner />
      {profile.data && verification !== 'APPROVED' && (
        <div role="status" className="mx-4 mt-4 flex items-start gap-3 rounded-2xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900 md:mx-6">
          <ShieldAlert size={18} className="mt-0.5 shrink-0 text-amber-600" />
          <p><strong>{verificationLabels[verification] ?? 'Chưa xác minh kho'}.</strong> Giao dịch mới đang khóa. <Link className="font-semibold text-d-primary underline" to="/depot/profile">Xem và nộp hồ sơ</Link>.</p>
        </div>
      )}
      <div className="min-w-0">{depotId ? <Outlet key={depotId} />
        : ownedDepots.isPending ? <p role="status" className="p-6 text-d-on-surface-variant">Đang tải danh sách kho…</p>
        : ownedDepots.isError ? <p role="alert" className="p-6 text-d-error">Chưa tải được danh sách kho. Dùng nút Thử lại ở thanh chọn kho.</p>
        : (
          <div className="p-4 md:p-6">
            <div className="mx-auto max-w-xl rounded-2xl border border-d-border-subtle bg-white p-8 text-center shadow-sm">
              <span className="mx-auto flex size-14 items-center justify-center rounded-2xl bg-d-primary-container/40 text-d-primary"><Warehouse size={26} /></span>
              <h1 className="mt-4 text-2xl font-bold text-d-on-surface">Bắt đầu với kho vựa của bạn</h1>
              <p className="mt-2 text-d-on-surface-variant">Tạo kho đầu tiên, sau đó nộp hồ sơ để Admin xác minh.</p>
              <button type="button" disabled={!capacity.data?.canAdd} className="mt-6 inline-flex items-center gap-2 disabled:opacity-50 rounded-full bg-d-primary px-5 py-3 font-semibold text-white transition hover:bg-[#385500]" onClick={() => setCreateDepot(true)}><Plus size={18} />Tạo kho đầu tiên</button>
            </div>
          </div>
        )}
      </div>
      {createDepot && <CreateDepotModal onClose={() => setCreateDepot(false)} />}
    </AppShell>
  );
};

export default DepotLayout;
