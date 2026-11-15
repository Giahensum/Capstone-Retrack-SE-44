import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useDepotQuery, useDepotMutation } from './depotApi';
import { Page, QueryState, Pager, inputClass, buttonClass, MutationError } from './components/DepotUI';
import NotificationCard from './components/NotificationCard';
import { MaterialIcon } from './components/DepotIcon';
import { notificationGroups } from './notificationLinks';
export default function Notifications() {
  const [page, setPage] = useState(1), [status, setStatus] = useState(''), [category, setCategory] = useState('');
  const [search, setSearch] = useState(''), [draft, setDraft] = useState(''), [feedback, setFeedback] = useState('');
  const query = useDepotQuery('notifications', { page, status, category, search }, true, 30000);
  const unread = useDepotQuery('notifications/unread-count', {}, true, 30000);
  const read = useDepotMutation('patch', (id, body) => `notifications/${id}/${body.isRead ? 'read' : 'unread'}`, () => { if (status) setPage(1); });
  const all = useDepotMutation('patch', 'notifications/read-all', () => { setPage(1); setFeedback('Đã đánh dấu các thông báo hiện có của kho là đã đọc.'); });
  const navigate = useNavigate(), busy = read.isPending || all.isPending;
  const mark = notice => { setFeedback(''); read.mutate({ id: notice.id, body: { isRead: !notice.isRead } }); };
  const show = async (notice, target) => {
    try { if (!notice.isRead) await read.mutateAsync({ id: notice.id, body: { isRead: true } }); navigate(target.to); }
    catch { /* Giữ màn hình để hiển thị lỗi và thử lại. */ }
  };
  return <Page title="Thông báo kho vựa" description="Cập nhật thanh toán, lô hàng, hồ sơ và hoạt động của kho đang chọn."
    action={<button type="button" className={buttonClass + ' min-h-11'} disabled={busy || !unread.isSuccess || unread.data === 0} onClick={() => { setFeedback(''); all.mutate(); }}>{all.isPending ? 'Đang cập nhật…' : 'Đánh dấu tất cả đã đọc'}</button>}>
    <div className="flex items-center gap-3 rounded-2xl border border-d-border-subtle bg-white p-4"><span className="rounded-xl bg-d-surface-accent p-3 text-d-primary"><MaterialIcon name="notifications" /></span><div>
      <p role={unread.isError ? 'alert' : 'status'} className="font-semibold">{unread.isSuccess ? `${unread.data} thông báo chưa đọc` : unread.isError ? 'Không tải được số thông báo chưa đọc.' : 'Đang kiểm tra thông báo chưa đọc…'}</p>
      <p className="text-xs sm:text-sm text-d-on-surface-variant mt-1">Tự cập nhật mỗi 30 giây khi mở ứng dụng · Giờ Việt Nam</p>
    </div></div>
    <div className="grid sm:grid-cols-2 lg:grid-cols-3 gap-4 rounded-2xl border border-d-border-subtle bg-white p-4">
      <form className="sm:col-span-2 lg:col-span-1" onSubmit={e => { e.preventDefault(); setSearch(draft.trim()); setPage(1); }}><label className="block">Tìm thông báo<div className="flex gap-2 items-end"><input type="search" maxLength={255} className={inputClass} placeholder="Tiêu đề hoặc nội dung…" value={draft} onChange={e => { setDraft(e.target.value); if (!e.target.value) { setSearch(''); setPage(1); } }} /><button type="submit" aria-label="Tìm thông báo" className="rounded-xl p-3 min-h-12 border border-d-border-subtle text-d-primary hover:bg-d-surface-accent"><MaterialIcon name="search" /></button></div></label></form>
      <label>Nhóm thông báo<select className={inputClass} value={category} onChange={e => { setCategory(e.target.value); setPage(1); }}><option value="">Tất cả nhóm</option>{Object.entries(notificationGroups).map(([key, group]) => <option value={key} key={key}>{group.label}</option>)}</select></label>
      <label>Lọc trạng thái đọc<select className={inputClass} value={status} onChange={e => { setStatus(e.target.value); setPage(1); }}><option value="">Tất cả</option><option value="UNREAD">Chưa đọc</option><option value="READ">Đã đọc</option></select></label>
    </div>
    {feedback && <p role="status" className="text-d-primary">{feedback}</p>}
    <MutationError mutation={read} /><MutationError mutation={all} /><QueryState query={query}><div className="space-y-3">{query.data?.items.map(notice => <NotificationCard key={notice.id} notice={notice} busy={busy} onRead={mark} onOpen={show} />)}
      {query.data?.totalCount === 0 && <div role="status" className="rounded-2xl border border-d-border-subtle bg-white p-10 text-center"><MaterialIcon name="notifications" className="mx-auto text-4xl text-d-primary mb-4" /><h2 className="font-semibold">Chưa có thông báo phù hợp</h2><p className="text-sm text-d-on-surface-variant mt-2">Thử bộ lọc khác hoặc quay lại khi có cập nhật mới.</p></div>}
    </div><Pager page={page} setPage={setPage} total={query.data?.totalCount} /></QueryState>
  </Page>;
}
