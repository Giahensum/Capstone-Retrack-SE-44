import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useDepotQuery, useDepotMutation } from './depotApi';
import { Page, QueryState, Pager, inputClass, buttonClass, MutationError } from './components/DepotUI';
export default function Notifications() {
  const [page, setPage] = useState(1), [status, setStatus] = useState('');
  const query = useDepotQuery('notifications', { page, status });
  const unread = useDepotQuery('notifications/unread-count');
  const read = useDepotMutation('patch', id => `notifications/${id}/read`);
  return <Page title="Thông báo kho vựa" description="Theo dõi đơn chờ thanh toán, vận chuyển và sự cố liên quan kho đang chọn.">
    <p role={unread.isError ? 'alert' : 'status'} className="text-d-primary font-semibold">{unread.isSuccess ? `${unread.data} thông báo chưa đọc` : unread.isError ? 'Không tải được số thông báo chưa đọc.' : 'Đang kiểm tra thông báo chưa đọc…'}</p>
    <label className="max-w-md">Lọc trạng thái đọc<select className={inputClass} value={status} onChange={e => { setStatus(e.target.value); setPage(1); }}><option value="">Tất cả</option><option value="UNREAD">Chưa đọc</option><option value="READ">Đã đọc</option></select></label>
    <MutationError mutation={read} /><QueryState query={query}><div className="space-y-4">{query.data?.items.map(x => <article key={x.id} className="rounded-2xl border border-d-border-subtle bg-white p-4 space-y-3">
      <h2 className="font-bold text-lg [overflow-wrap:anywhere]">{!x.isRead && <span className="text-d-primary mr-2">●</span>}{x.title}</h2><p className="whitespace-pre-wrap [overflow-wrap:anywhere]">{x.message}</p><p className="text-sm text-d-on-surface-variant">{new Date(x.createdAt).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' })}</p>
      <div className="flex flex-wrap gap-3">{x.batchId && <Link className={buttonClass} to={`/depot/batches?batchId=${x.batchId}`}>Xem lô / nhật ký xử lý</Link>}{x.pickupId && <Link className={buttonClass} to={`/depot/payments?requestId=${x.pickupId}`}>Xem đơn thanh toán</Link>}
        {!x.isRead && <button disabled={read.isPending} className="rounded-full border border-d-border-subtle px-4 py-2" onClick={() => read.mutate({ id: x.id })}>Đánh dấu đã đọc</button>}</div>
    </article>)}{query.data?.totalCount === 0 && <p role="status">Chưa có thông báo phù hợp.</p>}</div><Pager page={page} setPage={setPage} total={query.data?.totalCount} /></QueryState>
  </Page>;
}
