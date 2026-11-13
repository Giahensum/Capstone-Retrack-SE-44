import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { sellerApi } from '../api';
import { formatCurrency, formatDate, PICKUP_STATUS_LABEL, PICKUP_STATUS_STYLE } from '@/lib/utils';
import { MaterialIcon } from '@/features/depot/components/DepotIcon';

const STATUS_TABS = [
  { key: '', label: 'Tất cả' },
  { key: 'PENDING', label: 'Chờ xử lý' },
  { key: 'SCHEDULED', label: 'Đã nhận' },
  { key: 'IN_PROGRESS', label: 'Đang thu gom' },
  { key: 'WEIGHED', label: 'Chờ xác nhận cân' },
  { key: 'SELLER_CONFIRMED', label: 'Đã xác nhận cân' },
  { key: 'AWAITING_PAYMENT', label: 'Chờ kho chuyển tiền' },
  { key: 'PAYMENT_SENT', label: 'Chờ xác nhận tiền' },
  { key: 'DONE', label: 'Hoàn thành' },
  { key: 'CANCELLED', label: 'Đã hủy' },
];

export default function RequestList() {
  const [statusFilter, setStatusFilter] = useState('');
  const [search, setSearch] = useState('');

  const { data: requests = [], isLoading } = useQuery({
    queryKey: ['seller-requests', statusFilter],
    queryFn: () => sellerApi.getRequests(statusFilter || undefined),
  });

  const filtered = requests.filter(
    (r) =>
      !search ||
      r.address?.toLowerCase().includes(search.toLowerCase()) ||
      r.description?.toLowerCase().includes(search.toLowerCase()) ||
      r.depotName?.toLowerCase().includes(search.toLowerCase())
  );

  return (
    <div className="flex flex-col p-4 md:p-6 gap-6 w-full max-w-7xl mx-auto">
      {/* Page Title & Action */}
      <div className="flex flex-col md:flex-row md:items-end justify-between gap-4">
        <div>
          <h1 className="font-d-headline-xl-mobile md:font-d-headline-xl text-d-headline-xl-mobile md:text-d-headline-xl text-d-on-surface">
            Đơn thu gom của tôi
          </h1>
          <p className="font-d-body-md text-d-body-md text-d-on-surface-variant mt-2">
            Theo dõi tiến độ, khối lượng cân phế liệu và trạng thái thanh toán theo thời gian thực.
          </p>
        </div>

        <Link
          to="/seller/create"
          className="inline-flex items-center justify-center gap-2 rounded-full bg-d-primary hover:bg-[#385500] text-d-on-primary font-semibold px-6 py-3 transition-all shadow-sm active:scale-95 text-sm shrink-0"
        >
          <MaterialIcon name="add" className="text-lg" />
          <span>Tạo đơn mới</span>
        </Link>
      </div>

      {/* Filter Tabs */}
      <div className="flex gap-2 overflow-x-auto pb-1 scrollbar-hide">
        {STATUS_TABS.map((tab) => {
          const active = statusFilter === tab.key;
          return (
            <button
              key={tab.key}
              onClick={() => setStatusFilter(tab.key)}
              className={`px-4 py-2 rounded-full text-xs font-semibold whitespace-nowrap transition-all ${
                active
                  ? 'bg-d-primary text-d-on-primary shadow-xs'
                  : 'bg-d-surface-container-lowest border border-d-border-subtle text-d-on-surface-variant hover:bg-d-surface-container-low'
              }`}
            >
              {tab.label}
            </button>
          );
        })}
      </div>

      {/* Search Bar */}
      <div className="relative">
        <MaterialIcon
          name="search"
          className="absolute left-4 top-1/2 -translate-y-1/2 text-d-on-surface-variant text-lg"
        />
        <input
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="Tìm kiếm theo địa chỉ, mô tả phế liệu, tên kho vựa..."
          className="w-full bg-d-surface-container-lowest border border-d-border-subtle rounded-xl pl-11 pr-4 py-3 text-sm text-d-on-surface placeholder:text-d-on-surface-variant/60 focus:border-d-primary focus:ring-1 focus:ring-d-primary outline-none transition-all"
        />
      </div>

      {/* Requests List */}
      {isLoading ? (
        <div className="flex justify-center py-20">
          <div className="w-8 h-8 border-3 border-d-primary border-t-transparent rounded-full animate-spin" />
        </div>
      ) : filtered.length === 0 ? (
        <div className="bg-d-surface-container-lowest border border-d-border-subtle rounded-2xl p-12 text-center shadow-sm">
          <div className="w-14 h-14 rounded-full bg-d-surface-container flex items-center justify-center mx-auto mb-3 text-d-on-surface-variant">
            <MaterialIcon name="inventory_2" className="text-2xl" />
          </div>
          <p className="text-sm font-semibold text-d-on-surface">Không tìm thấy đơn nào</p>
          <p className="text-xs text-d-on-surface-variant mt-1">
            Thử thay đổi bộ lọc hoặc tạo một yêu cầu thu gom mới
          </p>
        </div>
      ) : (
        <div className="space-y-3">
          {filtered.map((req) => (
            <Link
              key={req.id}
              to={`/seller/requests/${req.id}`}
              className="block bg-d-surface-container-lowest border border-d-border-subtle rounded-2xl p-5 hover:border-d-primary hover:shadow-sm transition-all group"
            >
              <div className="flex items-start justify-between gap-4">
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 mb-2 flex-wrap">
                    <span
                      className={`text-[11px] font-bold px-3 py-1 rounded-full border ${
                        PICKUP_STATUS_STYLE[req.status] ??
                        'bg-d-surface-container text-d-on-surface-variant border-d-border-subtle'
                      }`}
                    >
                      {PICKUP_STATUS_LABEL[req.status] ?? req.status}
                    </span>
                    {req.depotName && (
                      <span className="text-xs text-d-on-surface-variant font-medium">
                        • 🏢 {req.depotName}
                      </span>
                    )}
                  </div>

                  <p className="text-sm font-bold text-d-on-surface truncate group-hover:text-d-primary transition-colors">
                    {req.description || 'Đơn thu gom không có mô tả chi tiết'}
                  </p>
                  <p className="text-xs text-d-on-surface-variant mt-1 truncate">
                    📍 {req.address}
                  </p>

                  <div className="flex items-center gap-4 mt-3 text-xs text-d-on-surface-variant">
                    <span>🕒 {formatDate(req.createdAt)}</span>
                    {req.grossAmount > 0 && (
                      <span className="font-bold text-d-primary">
                        💰 {formatCurrency(req.grossAmount)}
                      </span>
                    )}
                  </div>
                </div>

                <div className="flex items-center gap-2 shrink-0 self-center">
                  <MaterialIcon
                    name="chevron_right"
                    className="text-d-on-surface-variant group-hover:text-d-primary text-xl transition-colors"
                  />
                </div>
              </div>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}
