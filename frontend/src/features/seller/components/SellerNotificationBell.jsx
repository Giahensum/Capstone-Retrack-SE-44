import { useState, useRef, useEffect } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { sellerApi } from '../api';
import { MaterialIcon } from '@/features/depot/components/DepotIcon';

export default function SellerNotificationBell() {
  const [open, setOpen] = useState(false);
  const boxRef = useRef(null);
  const triggerRef = useRef(null);
  const panelRef = useRef(null);
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const { data: unreadCount = 0 } = useQuery({
    queryKey: ['seller-notifications-unread'],
    queryFn: () => sellerApi.getUnreadCount(),
    refetchInterval: 30000,
  });

  const { data: noticesData, isLoading } = useQuery({
    queryKey: ['seller-notifications-dropdown', open],
    queryFn: () => sellerApi.getNotifications({ page: 1, pageSize: 5 }),
    enabled: open,
  });

  const markReadMutation = useMutation({
    mutationFn: (id) => sellerApi.markRead(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['seller-notifications-unread'] });
      queryClient.invalidateQueries({ queryKey: ['seller-notifications-dropdown'] });
      queryClient.invalidateQueries({ queryKey: ['seller-notifications-list'] });
    },
  });

  const markAllMutation = useMutation({
    mutationFn: () => sellerApi.markAllRead(),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['seller-notifications-unread'] });
      queryClient.invalidateQueries({ queryKey: ['seller-notifications-dropdown'] });
      queryClient.invalidateQueries({ queryKey: ['seller-notifications-list'] });
    },
  });

  useEffect(() => {
    if (!open) return;
    panelRef.current?.focus();
    const handleOutside = (e) => {
      if (!boxRef.current?.contains(e.target)) setOpen(false);
    };
    const handleEscape = (e) => {
      if (e.key === 'Escape') {
        setOpen(false);
        triggerRef.current?.focus();
      }
    };
    document.addEventListener('pointerdown', handleOutside);
    document.addEventListener('keydown', handleEscape);
    return () => {
      document.removeEventListener('pointerdown', handleOutside);
      document.removeEventListener('keydown', handleEscape);
    };
  }, [open]);

  const handleOpenNotice = async (notice) => {
    if (!notice.isRead) {
      await markReadMutation.mutateAsync(notice.id);
    }
    setOpen(false);
    if (notice.pickupRequestId) {
      navigate(`/seller/requests/${notice.pickupRequestId}`);
    } else {
      navigate('/seller/notifications');
    }
  };

  const items = noticesData?.items || [];

  return (
    <div ref={boxRef} className="relative shrink-0">
      <button
        ref={triggerRef}
        type="button"
        aria-label={`Mở thông báo${unreadCount > 0 ? `, ${unreadCount} chưa đọc` : ''}`}
        aria-expanded={open}
        onClick={() => setOpen(!open)}
        className="relative flex h-10 w-10 items-center justify-center rounded-full border border-d-border-subtle bg-white text-d-on-surface-variant hover:text-d-primary hover:bg-d-surface-container transition-colors focus-visible:outline-2 focus-visible:outline-d-primary"
      >
        <MaterialIcon name="notifications" className="text-xl" />
        {unreadCount > 0 && (
          <span className="absolute -top-1 -right-1 min-w-[20px] h-5 rounded-full bg-d-error px-1 text-[11px] leading-5 font-bold text-white text-center shadow-xs">
            {unreadCount > 99 ? '99+' : unreadCount}
          </span>
        )}
      </button>

      {open && (
        <section
          ref={panelRef}
          role="dialog"
          aria-label="Thông báo người bán"
          tabIndex={-1}
          className="fixed top-20 left-4 right-4 sm:left-auto sm:absolute sm:top-12 sm:right-0 sm:w-[24rem] rounded-2xl border border-d-border-subtle bg-white shadow-xl overflow-hidden text-d-on-surface z-50 animate-in fade-in zoom-in-95 duration-150"
        >
          {/* Header */}
          <div className="flex items-center justify-between p-4 border-b border-d-border-subtle bg-d-surface-container-lowest">
            <div>
              <h2 className="font-bold text-base text-d-on-surface">Thông báo</h2>
              <p className="text-xs text-d-on-surface-variant mt-0.5">
                {unreadCount > 0 ? `${unreadCount} thông báo chưa đọc` : 'Bạn đã đọc tất cả thông báo'}
              </p>
            </div>
            {unreadCount > 0 && (
              <button
                type="button"
                onClick={() => markAllMutation.mutate()}
                disabled={markAllMutation.isPending}
                className="text-xs text-d-primary font-semibold hover:underline"
              >
                Đọc tất cả
              </button>
            )}
          </div>

          {/* List */}
          <div className="max-h-[360px] overflow-y-auto divide-y divide-d-border-subtle">
            {isLoading ? (
              <div className="py-10 text-center text-d-on-surface-variant text-xs flex flex-col items-center gap-2">
                <span className="w-5 h-5 border-2 border-d-primary border-t-transparent rounded-full animate-spin" />
                <span>Đang tải thông báo...</span>
              </div>
            ) : items.length === 0 ? (
              <div className="py-10 text-center text-d-on-surface-variant px-4">
                <MaterialIcon name="notifications" className="text-3xl text-d-primary mx-auto mb-2 opacity-50" />
                <p className="font-medium text-sm text-d-on-surface">Không có thông báo mới</p>
                <p className="text-xs text-d-on-surface-variant mt-1">
                  Cập nhật về đơn thu gom và thanh toán sẽ xuất hiện ở đây.
                </p>
              </div>
            ) : (
              items.map((notice) => (
                <div
                  key={notice.id}
                  onClick={() => handleOpenNotice(notice)}
                  className={`p-3.5 hover:bg-d-surface-container cursor-pointer transition-colors flex items-start gap-3 ${
                    !notice.isRead ? 'bg-d-primary/5 font-medium' : ''
                  }`}
                >
                  <div
                    className={`w-9 h-9 rounded-xl flex items-center justify-center shrink-0 ${
                      notice.category === 'PAYMENT'
                        ? 'bg-[#ecfdf5] text-[#059669]'
                        : notice.category === 'PICKUP'
                        ? 'bg-[#f0fdf4] text-d-primary'
                        : 'bg-d-surface-container text-d-on-surface-variant'
                    }`}
                  >
                    <MaterialIcon
                      name={
                        notice.category === 'PAYMENT'
                          ? 'payments'
                          : notice.category === 'PICKUP'
                          ? 'local_shipping'
                          : 'notifications'
                      }
                      className="text-lg"
                    />
                  </div>
                  <div className="min-w-0 flex-1">
                    <div className="flex items-center justify-between gap-1">
                      <h3 className={`text-xs truncate ${!notice.isRead ? 'font-bold text-d-on-surface' : 'text-d-on-surface-variant'}`}>
                        {notice.title}
                      </h3>
                      {!notice.isRead && (
                        <span className="w-2 h-2 rounded-full bg-d-primary shrink-0" />
                      )}
                    </div>
                    <p className="text-xs text-d-on-surface-variant line-clamp-2 mt-0.5 leading-relaxed">
                      {notice.message}
                    </p>
                    <span className="text-[10px] text-d-on-surface-variant/80 mt-1 block">
                      {new Date(notice.createdAt).toLocaleString('vi-VN', {
                        hour: '2-digit',
                        minute: '2-digit',
                        day: '2-digit',
                        month: '2-digit',
                      })}
                    </span>
                  </div>
                </div>
              ))
            )}
          </div>

          {/* Footer */}
          <Link
            to="/seller/notifications"
            onClick={() => setOpen(false)}
            className="block border-t border-d-border-subtle p-3 text-center text-xs font-bold text-d-primary hover:bg-d-surface-container transition-colors"
          >
            Xem tất cả thông báo →
          </Link>
        </section>
      )}
    </div>
  );
}
