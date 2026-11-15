import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import api from '@/lib/axios';
import { useDepot, depotError } from '../DepotContext';
import { Dialog, QueryState, Pager, inputClass, buttonClass, MutationError } from './DepotUI';
import VerificationUploadForm from './VerificationUploadForm';
import VerificationDocument from './VerificationDocument';

export const verificationLabels = { UNVERIFIED: 'Chưa nộp hồ sơ', SUBMITTED: 'Chờ Admin duyệt', APPROVED: 'Đã xác minh', REJECTED: 'Cần bổ sung hồ sơ' };
export default function VerificationPanel({ admin = false, status }) {
  const depot = useDepot(), client = useQueryClient();
  const [page, setPage] = useState(1), [filter, setFilter] = useState(admin ? 'SUBMITTED' : '');
  const [review, setReview] = useState(null), [reason, setReason] = useState('');
  const refresh = async () => { await Promise.all([client.invalidateQueries({ queryKey: ['verification'] }), client.invalidateQueries({ queryKey: ['depot'] })]); };
  const query = useQuery({ queryKey: ['verification', admin ? 'admin' : depot?.depotId, page, filter], enabled: admin || !!depot?.depotId,
    queryFn: async () => (await api.get(admin ? '/admin/depot-verifications' : '/depot/verifications', { params: { depotId: admin ? undefined : depot.depotId, page, status: filter } })).data.data });
  const decide = useMutation({ mutationFn: () => api.patch(`/admin/depot-verifications/${review.id}`, { approve: review.approve, reason }),
    onSuccess: async () => { setReview(null); setReason(''); await refresh(); } });
  return <section className="rounded-[20px] border border-d-border-subtle bg-white p-4 md:p-6 space-y-5 mt-6">
    <h2 className="text-xl font-bold">{admin ? 'Duyệt hồ sơ kho vựa' : 'Hồ sơ xác minh kho'}</h2>
    {!admin && <><p role="status" className="font-semibold text-d-primary">{verificationLabels[status] ?? status}</p><p className="text-sm text-d-on-surface-variant">Hồ sơ chỉ chủ kho và Admin được xem. Kho chưa được duyệt chưa thể tạo giao dịch mới; các đơn và nghĩa vụ thanh toán đang có vẫn được xử lý.</p>
      {['UNVERIFIED', 'REJECTED'].includes(status) && <VerificationUploadForm key={depot.depotId} depotId={depot.depotId} onSuccess={refresh} />}</>}
    <label className="block">Trạng thái hồ sơ<select className={inputClass} value={filter} onChange={e => { setFilter(e.target.value); setPage(1); }}><option value="">Tất cả</option>{['SUBMITTED', 'APPROVED', 'REJECTED'].map(value => <option key={value} value={value}>{verificationLabels[value]}</option>)}</select></label>
    <QueryState query={query}><div className="space-y-4">{query.data?.items.map(item => <article key={item.id} className="rounded-xl border border-d-border-subtle p-4 space-y-3">
      <h3 className="font-semibold">{item.depotName} — {verificationLabels[item.status]}</h3><p className="text-sm">{item.depotAddress} · MST: {item.taxCode || 'Chưa cung cấp'}</p><p className="text-sm">Nộp: {new Date(item.submittedAt).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' })}</p>
      <p className="whitespace-pre-wrap break-words">{item.note}</p><div className="flex flex-wrap gap-3">{item.files.map(file => <VerificationDocument key={file.id} file={file} />)}</div>
      {item.reviewReason && <p className="whitespace-pre-wrap break-words">Kết quả đối soát: {item.reviewReason}</p>}
      {admin && item.status === 'SUBMITTED' && <div className="flex flex-wrap gap-3"><button className={buttonClass} onClick={() => { setReview({ ...item, approve: true }); setReason(''); }}>Duyệt hồ sơ</button><button className={buttonClass} onClick={() => { setReview({ ...item, approve: false }); setReason(''); }}>Yêu cầu bổ sung</button></div>}
    </article>)}{query.data?.items.length === 0 && <p role="status">Chưa có hồ sơ phù hợp.</p>}</div><Pager page={page} setPage={setPage} total={query.data?.totalCount} /></QueryState>
    {review && <Dialog title={review.approve ? 'Xác nhận duyệt hồ sơ' : 'Yêu cầu bổ sung hồ sơ'} busy={decide.isPending} onClose={() => setReview(null)}><form className="space-y-4" onSubmit={e => { e.preventDefault(); decide.mutate(); }}>
      <p>{review.depotName}</p><label className="block">Lý do / kết quả đối soát<textarea className={inputClass} required maxLength={2000} value={reason} onChange={e => setReason(e.target.value)} /></label><MutationError mutation={decide} />
      <button className={buttonClass} disabled={decide.isPending || !reason.trim()}>{decide.isPending ? 'Đang lưu…' : 'Lưu kết quả'}</button>
    </form></Dialog>}
    {query.isError && <p role="alert" className="sr-only">{depotError(query.error)}</p>}
  </section>;
}
