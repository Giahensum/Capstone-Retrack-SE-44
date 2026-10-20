import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import toast from 'react-hot-toast';
import { sellerApi } from '../api';
import { MaterialIcon } from '@/features/depot/components/DepotIcon';

export default function SellerNotifications() {
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [activeTab, setActiveTab] = useState('ALL'); // 'ALL' | 'UNREAD' | 'PICKUP' | 'PAYMENT'

  const queryParams = {
    page,
    pageSize: 15,
    unreadOnly: activeTab === 'UNREAD' ? true : undefined,
    category: activeTab === 'PICKUP' || activeTab === 'PAYMENT' ? activeTab : undefined,
  };

  const { data, isLoading } = useQuery({
    queryKey: ['seller-notifications-list', queryParams],
    queryFn: () => sellerApi.getNotifications(queryParams),
    staleTime: 10000,
  });

  const { data: unreadCount = 0 } = useQuery({
    queryKey: ['seller-notifications-unread'],
    queryFn: () => sellerApi.getUnreadCount(),
  });

  const markReadMutation = useMutation({
    mutationFn: (id) => sellerApi.markRead(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['seller-notifications-unread'] });
      queryClient.invalidateQueries({ queryKey: ['seller-notifications-list'] });
    },
  });

  const markUnreadMutation = useMutation({
    mutationFn: (id) => sellerApi.markUnread(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['seller-notifications-unread'] });
      queryClient.invalidateQueries({ queryKey: ['seller-notifications-list'] });
    },
  });

  const markAllMutation = useMutation({
    mutationFn: () => sellerApi.markAllRead(),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['seller-notifications-unread'] });
      queryClient.invalidateQueries({ queryKey: ['seller-notifications-list'] });
      toast.success('Đã đánh dấu tất cả thông báo là đã đọc');
    },
  });

  const items = data?.items || [];
  const totalCount = data?.totalCount || 0;
  const totalPages = Math.ceil(totalCount / 15) || 1;

  const tabs = [
    { id: 'ALL', label: 'Tất cả' },
    { id: 'UNREAD', label: `Chưa đọc ${unreadCount > 0 ? `(${unreadCount})` : ''}` },
    { id: 'PICKUP', label: 'Đơn thu gom' },
    { id: 'PAYMENT', label: 'Thanh toán' },
  ];

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-5xl mx-auto space-y-6">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-d-on-surface tracking-tight flex items-center gap-2">
            <MaterialIcon name="notifications" className="text-d-primary text-2xl" />
            <span>Thông báo của bạn</span>
          </h1>
          <p className="text-sm text-d-on-surface-variant mt-1">
            Theo dõi tiến độ đơn thu gom, lịch hẹn, kết quả cân phế liệu và biên lai thanh toán
          </p>
        </div>

        {unreadCount > 0 && (
          <button
            type="button"
            onClick={() => markAllMutation.mutate()}
            disabled={markAllMutation.isPending}
            className="inline-flex items-center gap-1.5 px-4 py-2 rounded-xl border border-d-border-subtle bg-white text-d-primary hover:bg-d-surface-container font-semibold text-xs transition-colors shrink-0 shadow-2xs"
          >
            <MaterialIcon name="done_all" className="text-base" />
            <span>Đánh dấu tất cả đã đọc</span>
          </button>
        )}
      </div>

      {/* Filter Tabs */}
      <div className="flex items-center gap-2 border-b border-d-border-subtle pb-3 overflow-x-auto">
        {tabs.map((tab) => (
          <button
            key={tab.id}
            type="button"
            onClick={() => {
              setActiveTab(tab.id);
              setPage(1);
            }}
            className={`px-4 py-2 rounded-xl text-xs font-bold transition-all shrink-0 ${
              activeTab === tab.id
                ? 'bg-d-primary text-d-on-primary shadow-xs'
                : 'bg-d-surface-container-lowest border border-d-border-subtle text-d-on-surface-variant hover:text-d-on-surface hover:bg-d-surface-container'
            }`}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {/* Notification List */}
      <div className="space-y-3">
        {isLoading ? (
          <div className="py-16 text-center text-d-on-surface-variant flex flex-col items-center justify-center gap-3">
            <span className="w-6 h-6 border-2 border-d-primary border-t-transparent rounded-full animate-spin" />
            <span className="text-sm">Đang tải thông báo...</span>
          </div>
        ) : items.length === 0 ? (
          <div className="py-16 text-center bg-d-surface-container-lowest border border-d-border-subtle rounded-2xl p-8">
            <div className="w-16 h-16 rounded-2xl bg-d-surface-container text-d-primary flex items-center justify-center mx-auto mb-4">
              <MaterialIcon name="notifications" className="text-3xl opacity-60" />
            </div>
            <h2 className="text-base font-bold text-d-on-surface">Không có thông báo nào</h2>
            <p className="text-xs text-d-on-surface-variant mt-1 max-w-sm mx-auto">
              {activeTab === 'UNREAD'
                ? 'Tuyệt vời! Bạn không còn thông báo nào chưa đọc.'
                : 'Mọi hoạt động điều phối, cân hàng và thanh toán sẽ hiển thị tại đây.'}
            </p>
          </div>
        ) : (
          items.map((notice) => {
            const isPickup = notice.category === 'PICKUP';
            const isPayment = notice.category === 'PAYMENT';

            return (
              <div
                key={notice.id}
                className={`p-4 sm:p-5 rounded-2xl border transition-all flex flex-col sm:flex-row sm:items-center justify-between gap-4 shadow-2xs ${
                  !notice.isRead
                    ? 'bg-white border-d-primary/30 ring-1 ring-d-primary/10'
                    : 'bg-d-surface-container-lowest border-d-border-subtle hover:border-d-border'
                }`}
              >
                <div className="flex items-start gap-3.5 min-w-0">
                  <div
                    className={`w-11 h-11 rounded-2xl flex items-center justify-center shrink-0 ${
                      isPayment
                        ? 'bg-[#ecfdf5] text-[#059669]'
                        : isPickup
                        ? 'bg-[#f0fdf4] text-d-primary'
                        : 'bg-d-surface-container text-d-on-surface-variant'
                    }`}
                  >
                    <MaterialIcon
                      name={isPayment ? 'payments' : isPickup ? 'local_shipping' : 'notifications'}
                      className="text-2xl"
                    />
                  </div>

                  <div className="min-w-0">
                    <div className="flex items-center gap-2 flex-wrap">
                      <h2 className={`text-sm ${!notice.isRead ? 'font-bold text-d-on-surface' : 'font-semibold text-d-on-surface-variant'}`}>
                        {notice.title}
                      </h2>
                      {!notice.isRead && (
                        <span className="text-[10px] font-bold bg-d-primary/10 text-d-primary px-2 py-0.5 rounded-full">
                          Mới
                        </span>
                      )}
                      <span className="text-[10px] text-d-on-surface-variant bg-d-surface-container px-2 py-0.5 rounded-full uppercase tracking-wider font-semibold">
                        {isPayment ? 'Thanh toán' : isPickup ? 'Thu gom' : 'Hệ thống'}
                      </span>
                    </div>

                    <p className="text-xs text-d-on-surface-variant mt-1 leading-relaxed">
                      {notice.message}
                    </p>

                    <span className="text-[11px] text-d-on-surface-variant/70 mt-2 block font-mono">
                      🕒 {new Date(notice.createdAt).toLocaleString('vi-VN', {
                        hour: '2-digit',
                        minute: '2-digit',
                        day: '2-digit',
                        month: '2-digit',
                        year: 'numeric',
                      })}
                    </span>
                  </div>
                </div>

                {/* Actions */}
                <div className="flex items-center gap-2 self-end sm:self-center shrink-0">
                  {notice.pickupRequestId && (
                    <Link
                      to={`/seller/requests/${notice.pickupRequestId}`}
                      onClick={() => {
                        if (!notice.isRead) markReadMutation.mutate(notice.id);
                      }}
                      className="inline-flex items-center gap-1 px-3 py-1.5 rounded-xl bg-d-primary-container text-d-on-primary-container text-xs font-bold hover:brightness-95 transition-all shadow-2xs"
                    >
                      <span>Xem đơn</span>
                      <MaterialIcon name="arrow_forward" className="text-sm" />
                    </Link>
                  )}

                  <button
                    type="button"
                    onClick={() => {
                      if (notice.isRead) {
                        markUnreadMutation.mutate(notice.id);
                      } else {
                        markReadMutation.mutate(notice.id);
                      }
                    }}
                    title={notice.isRead ? 'Đánh dấu chưa đọc' : 'Đánh dấu đã đọc'}
                    className="p-2 rounded-xl text-d-on-surface-variant hover:text-d-primary hover:bg-d-surface-container transition-colors"
                  >
                    <MaterialIcon
                      name={notice.isRead ? 'mark_chat_unread' : 'mark_chat_read'}
                      className="text-lg"
                    />
                  </button>
                </div>
              </div>
            );
          })
        )}
      </div>

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center justify-between pt-4 border-t border-d-border-subtle text-xs text-d-on-surface-variant">
          <span>
            Hiển thị trang {page} / {totalPages} ({totalCount} thông báo)
          </span>
          <div className="flex items-center gap-2">
            <button
              type="button"
              disabled={page <= 1}
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              className="px-3 py-1.5 rounded-xl border border-d-border-subtle bg-white font-semibold disabled:opacity-40 disabled:cursor-not-allowed hover:bg-d-surface-container transition-colors"
            >
              ← Trước
            </button>
            <button
              type="button"
              disabled={page >= totalPages}
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              className="px-3 py-1.5 rounded-xl border border-d-border-subtle bg-white font-semibold disabled:opacity-40 disabled:cursor-not-allowed hover:bg-d-surface-container transition-colors"
            >
              Sau →
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
