import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { sellerApi } from '../api';
import { formatCurrency, PICKUP_STATUS_LABEL, PICKUP_STATUS_STYLE } from '@/lib/utils';
import { MaterialIcon } from '@/features/depot/components/DepotIcon';

export default function SellerHome() {
  const { data: stats } = useQuery({
    queryKey: ['seller-stats'],
    queryFn: sellerApi.getStats,
  });

  const { data: requests } = useQuery({
    queryKey: ['seller-requests'],
    queryFn: () => sellerApi.getRequests(),
  });

  const recentRequests = (requests ?? []).slice(0, 5);

  return (
    <div className="flex flex-col p-4 md:p-6 gap-6 w-full max-w-7xl mx-auto">
      {/* Page Title */}
      <div className="flex flex-col md:flex-row md:items-end justify-between gap-4">
        <div>
          <h1 className="font-d-headline-xl-mobile md:font-d-headline-xl text-d-headline-xl-mobile md:text-d-headline-xl text-d-on-surface">
            Tổng quan người bán
          </h1>
          <p className="font-d-body-md text-d-body-md text-d-on-surface-variant mt-2">
            Theo dõi trạng thái thu gom phế liệu và doanh thu của bạn.
          </p>
        </div>

        <Link
          to="/seller/create"
          className="inline-flex items-center justify-center gap-2 rounded-full bg-d-primary hover:bg-[#385500] text-d-on-primary font-semibold px-6 py-3 transition-all shadow-sm active:scale-95 text-sm"
        >
          <MaterialIcon name="add" className="text-lg" />
          <span>Tạo đơn thu gom mới</span>
        </Link>
      </div>

      {/* Welcome Banner */}
      <div className="relative overflow-hidden rounded-[20px] bg-gradient-to-r from-d-primary to-[#2a4400] text-d-on-primary p-6 md:p-8 shadow-sm">
        <div className="absolute top-0 right-0 w-72 h-72 bg-white/5 rounded-full -translate-y-1/3 translate-x-1/3 blur-2xl" />
        <div className="relative z-10 flex flex-col sm:flex-row sm:items-center justify-between gap-6">
          <div className="space-y-2 max-w-xl">
            <div className="inline-flex items-center gap-2 bg-white/10 backdrop-blur-md px-3 py-1 rounded-full text-xs font-semibold text-d-primary-container">
              <MaterialIcon name="recycling" className="text-base" />
              <span>Nền tảng thu gom phế liệu thông minh ReTrack</span>
            </div>
            <h2 className="text-xl md:text-2xl font-bold tracking-tight">
              Biến phế liệu thành giá trị, chung tay bảo vệ môi trường ♻️
            </h2>
            <p className="text-sm text-white/80 leading-relaxed">
              Tạo đơn thu gom nhanh chóng, kết nối trực tiếp với các kho vựa uy tín trong khu vực với mức giá công khai minh bạch.
            </p>
          </div>
          <div className="shrink-0">
            <Link
              to="/seller/create"
              className="inline-flex items-center gap-2 bg-d-primary-container text-d-on-primary-container font-bold px-6 py-3 rounded-full hover:bg-[#b5f54a] transition-all shadow-md active:scale-95 text-sm"
            >
              <span>Bắt đầu tạo đơn</span>
              <MaterialIcon name="arrow_forward" className="text-base" />
            </Link>
          </div>
        </div>
      </div>

      {/* KPI Grid */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {[
          {
            label: 'Tổng số đơn',
            value: stats?.total ?? 0,
            icon: 'receipt_long',
            color: 'text-d-primary',
          },
          {
            label: 'Đang xử lý',
            value: stats?.inProgress ?? 0,
            icon: 'schedule',
            color: 'text-[#d97706]',
          },
          {
            label: 'Đã hoàn thành',
            value: stats?.done ?? 0,
            icon: 'check_circle',
            color: 'text-d-secondary',
          },
          {
            label: 'Thu nhập tháng này',
            value: formatCurrency(stats?.monthlyIncome ?? 0),
            icon: 'payments',
            color: 'text-d-primary',
          },
        ].map(({ label, value, icon, color }) => (
          <div
            key={label}
            className="d-glass-panel bg-d-surface-container-lowest border border-d-border-subtle rounded-2xl p-5 relative overflow-hidden group shadow-sm hover:shadow-md transition-shadow"
          >
            <div className="flex justify-between items-start mb-3">
              <div className={`w-10 h-10 rounded-xl bg-d-surface-container flex items-center justify-center ${color}`}>
                <MaterialIcon name={icon} className="text-xl" />
              </div>
            </div>
            <div>
              <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant mb-1">
                {label}
              </p>
              <h3 className="font-d-headline-lg text-d-headline-lg text-d-on-surface">
                {value}
              </h3>
            </div>
          </div>
        ))}
      </div>

      {/* Quick Action Bento Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
        <Link
          to="/seller/ai-scanner"
          className="d-bento-card flex items-start gap-4 group relative overflow-hidden border-d-primary/30 hover:border-d-primary"
        >
          <div className="w-12 h-12 rounded-2xl bg-d-primary-container text-d-on-primary-container flex items-center justify-center shrink-0 group-hover:scale-105 transition-transform shadow-xs">
            <MaterialIcon name="document_scanner" className="text-2xl text-d-primary" />
          </div>
          <div className="flex-1 min-w-0">
            <div className="flex items-center gap-2">
              <h3 className="font-bold text-d-on-surface text-base group-hover:text-d-primary transition-colors truncate">
                AI Nhận diện phế liệu
              </h3>
              <span className="text-[10px] font-bold text-d-primary bg-d-primary/10 px-2 py-0.5 rounded-full shrink-0">
                ✨ Mới
              </span>
            </div>
            <p className="text-xs text-d-on-surface-variant mt-1 leading-relaxed">
              Quét camera hoặc tải ảnh để AI phân loại 12 nhóm rác, tra cứu giá thị trường tức thì.
            </p>
          </div>
          <MaterialIcon name="chevron_right" className="text-d-on-surface-variant group-hover:text-d-primary mt-3 text-lg transition-colors shrink-0" />
        </Link>

        <Link
          to="/seller/create"
          className="d-bento-card flex items-start gap-4 group"
        >
          <div className="w-12 h-12 rounded-2xl bg-d-surface-accent text-d-primary flex items-center justify-center shrink-0 group-hover:scale-105 transition-transform">
            <MaterialIcon name="add_box" className="text-2xl" />
          </div>
          <div className="flex-1 min-w-0">
            <h3 className="font-bold text-d-on-surface text-base group-hover:text-d-primary transition-colors">
              Tạo đơn thu gom mới
            </h3>
            <p className="text-xs text-d-on-surface-variant mt-1 leading-relaxed">
              Chọn mẫu phế liệu gợi ý hoặc nhập tay thông tin, chọn kho vựa hoặc nổ đơn tự động.
            </p>
          </div>
          <MaterialIcon name="chevron_right" className="text-d-on-surface-variant group-hover:text-d-primary mt-3 text-lg transition-colors shrink-0" />
        </Link>

        <Link
          to="/seller/requests"
          className="d-bento-card flex items-start gap-4 group sm:col-span-2 lg:col-span-1"
        >
          <div className="w-12 h-12 rounded-2xl bg-d-surface-container text-d-secondary flex items-center justify-center shrink-0 group-hover:scale-105 transition-transform">
            <MaterialIcon name="fact_check" className="text-2xl" />
          </div>
          <div className="flex-1 min-w-0">
            <h3 className="font-bold text-d-on-surface text-base group-hover:text-d-primary transition-colors">
              Quản lý đơn hàng
            </h3>
            <p className="text-xs text-d-on-surface-variant mt-1 leading-relaxed">
              Theo dõi chi tiết quy trình: đã điều phối xe, xác nhận khối lượng cân và nhận thanh toán.
            </p>
          </div>
          <MaterialIcon name="chevron_right" className="text-d-on-surface-variant group-hover:text-d-primary mt-3 text-lg transition-colors shrink-0" />
        </Link>
      </div>

      {/* Recent Requests Section */}
      <div className="bg-d-surface-container-lowest border border-d-border-subtle rounded-2xl shadow-sm overflow-hidden">
        <div className="flex items-center justify-between px-6 py-4 border-b border-d-border-subtle bg-d-surface-container-low">
          <div>
            <h2 className="font-bold text-d-on-surface text-base">
              Đơn thu gom gần đây
            </h2>
            <p className="text-xs text-d-on-surface-variant mt-0.5">
              5 đơn thu gom phát sinh mới nhất
            </p>
          </div>
          <Link
            to="/seller/requests"
            className="text-xs text-d-primary font-bold hover:underline flex items-center gap-1"
          >
            <span>Xem tất cả</span>
            <MaterialIcon name="arrow_forward" className="text-sm" />
          </Link>
        </div>

        {recentRequests.length === 0 ? (
          <div className="p-12 text-center">
            <div className="w-14 h-14 rounded-full bg-d-surface-container flex items-center justify-center mx-auto mb-3 text-d-on-surface-variant">
              <MaterialIcon name="inventory_2" className="text-2xl" />
            </div>
            <p className="text-sm font-semibold text-d-on-surface">Chưa có đơn thu gom nào</p>
            <p className="text-xs text-d-on-surface-variant mt-1">Bấm nút Tạo đơn để gửi yêu cầu đầu tiên của bạn</p>
            <Link
              to="/seller/create"
              className="inline-flex items-center gap-1.5 mt-4 rounded-full bg-d-primary text-d-on-primary px-5 py-2 text-xs font-semibold hover:bg-[#385500] transition-colors"
            >
              <MaterialIcon name="add" className="text-sm" />
              <span>Tạo đơn ngay</span>
            </Link>
          </div>
        ) : (
          <div className="divide-y divide-d-border-subtle">
            {recentRequests.map((req) => (
              <Link
                key={req.id}
                to={`/seller/requests/${req.id}`}
                className="flex items-center justify-between px-6 py-4 hover:bg-d-surface-container-low transition-colors group"
              >
                <div className="min-w-0 flex-1 pr-4">
                  <div className="flex items-center gap-2 mb-1">
                    <span className="font-semibold text-sm text-d-on-surface truncate group-hover:text-d-primary transition-colors">
                      {req.description || req.address || 'Đơn thu gom'}
                    </span>
                    {req.depotName && (
                      <span className="text-xs text-d-on-surface-variant shrink-0">
                        • {req.depotName}
                      </span>
                    )}
                  </div>
                  <div className="flex items-center gap-3 text-xs text-d-on-surface-variant">
                    <span className="truncate max-w-[280px]">📍 {req.address}</span>
                    <span>•</span>
                    <span>{new Date(req.createdAt).toLocaleDateString('vi-VN')}</span>
                    {req.grossAmount > 0 && (
                      <>
                        <span>•</span>
                        <span className="font-bold text-d-primary">{formatCurrency(req.grossAmount)}</span>
                      </>
                    )}
                  </div>
                </div>
                <div className="flex items-center gap-3 shrink-0">
                  <StatusBadge status={req.status} />
                  <MaterialIcon name="chevron_right" className="text-d-on-surface-variant group-hover:text-d-primary text-lg transition-colors" />
                </div>
              </Link>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

function StatusBadge({ status }) {
  return (
    <span
      className={`text-[11px] font-bold px-3 py-1 rounded-full border ${
        PICKUP_STATUS_STYLE[status] ?? 'bg-d-surface-container text-d-on-surface-variant border-d-border-subtle'
      }`}
    >
      {PICKUP_STATUS_LABEL[status] ?? status}
    </span>
  );
}
