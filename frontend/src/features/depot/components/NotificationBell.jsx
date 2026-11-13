import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useDepotQuery, useDepotMutation } from '../depotApi';
import { MaterialIcon } from './DepotIcon';
import { QueryState, MutationError } from './DepotUI';
import NotificationCard from './NotificationCard';

export default function NotificationBell({ unread }) {
  const [open, setOpen] = useState(false);
  const box = useRef(null), trigger = useRef(null), panel = useRef(null);
  const query = useDepotQuery('notifications', { pageSize: 5 }, open, 30000);
  const read = useDepotMutation('patch', (id, body) => `notifications/${id}/${body.isRead ? 'read' : 'unread'}`);
  const navigate = useNavigate();
  useEffect(() => {
    if (!open) return;
    panel.current?.focus();
    const outside = e => { if (!box.current?.contains(e.target)) setOpen(false); };
    const escape = e => { if (e.key === 'Escape') { setOpen(false); trigger.current?.focus(); } };
    document.addEventListener('pointerdown', outside); document.addEventListener('keydown', escape);
    return () => { document.removeEventListener('pointerdown', outside); document.removeEventListener('keydown', escape); };
  }, [open]);
  const show = async (notice, target) => {
    try { if (!notice.isRead) await read.mutateAsync({ id: notice.id, body: { isRead: true } }); setOpen(false); navigate(target.to); }
    catch { /* Lỗi mutation được hiển thị trong bảng xem nhanh. */ }
  };
  return <div ref={box} className="relative shrink-0">
    <button ref={trigger} type="button" aria-label={unread.isError ? 'Thông báo: không tải được số chưa đọc' : `Mở thông báo${unread.isSuccess ? `, ${unread.data} chưa đọc` : ''}`} aria-expanded={open} aria-controls="depot-notification-panel" onClick={() => setOpen(!open)}
      className="relative flex h-11 w-11 items-center justify-center rounded-full border border-d-border-subtle bg-white text-d-primary hover:bg-d-surface-accent focus-visible:outline-2 focus-visible:outline-d-primary">
      <MaterialIcon name="notifications" className="text-xl" />
      {unread.isSuccess && unread.data > 0 && <span aria-hidden="true" className="absolute -top-1 -right-1 min-w-5 rounded-full bg-d-error px-1 text-[11px] leading-5 font-bold text-white">{unread.data > 99 ? '99+' : unread.data}</span>}
      {unread.isError && <span aria-hidden="true" className="absolute top-0 right-0 rounded-full bg-d-error text-white px-1 text-xs">!</span>}
    </button>
    {open && <section ref={panel} id="depot-notification-panel" role="dialog" aria-label="Thông báo nhanh" tabIndex={-1}
      className="fixed top-20 left-4 right-4 sm:left-auto sm:absolute sm:top-14 sm:right-0 sm:w-[25rem] rounded-[20px] border border-d-border-subtle bg-d-surface shadow-xl overflow-hidden text-d-on-surface">
      <div className="flex items-center justify-between gap-3 p-4 border-b border-d-border-subtle"><div><h2 className="font-bold text-lg">Thông báo</h2><p className="text-xs text-d-on-surface-variant">5 thông báo mới nhất của kho đang chọn</p></div><button type="button" aria-label="Đóng thông báo" className="h-11 w-11 shrink-0 rounded-full hover:bg-d-surface-accent" onClick={() => { setOpen(false); trigger.current?.focus(); }}>✕</button></div>
      <div className="max-h-[60dvh] overflow-y-auto p-3 space-y-3"><MutationError mutation={read} /><QueryState query={query}>
        {query.data?.items.map(notice => <NotificationCard key={notice.id} notice={notice} busy={read.isPending} onOpen={show} onRead={item => read.mutate({ id: item.id, body: { isRead: !item.isRead } })} compact />)}
        {query.data?.totalCount === 0 && <div role="status" className="py-8 text-center"><MaterialIcon name="notifications" className="text-3xl text-d-primary mx-auto mb-3" /><p className="font-medium">Chưa có thông báo</p><p className="text-sm text-d-on-surface-variant mt-1">Các cập nhật liên quan kho sẽ xuất hiện tại đây.</p></div>}
      </QueryState></div>
      <Link to="/depot/notifications" onClick={() => setOpen(false)} className="block border-t border-d-border-subtle p-4 text-center font-semibold text-d-primary hover:bg-d-surface-accent">Xem tất cả thông báo</Link>
    </section>}
  </div>;
}
