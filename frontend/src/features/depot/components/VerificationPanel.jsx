import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import api from '@/lib/axios';
import { useDepot, depotError } from '../DepotContext';
import { Dialog, QueryState, Pager, inputClass, buttonClass, MutationError } from './DepotUI';

export const verificationLabels = { UNVERIFIED: 'Chưa nộp hồ sơ', SUBMITTED: 'Chờ Admin duyệt', APPROVED: 'Đã xác minh', REJECTED: 'Cần bổ sung hồ sơ' };
function DocumentButton({ file }) {
  const [busy, setBusy] = useState(false), [error, setError] = useState('');
  return <div><button type="button" className="rounded-xl border border-d-border-subtle px-3 py-2 text-d-primary disabled:opacity-40 break-all" disabled={busy} onClick={async () => {
    setBusy(true); setError('');
    try {
      const response = await api.get(`/depot/verifications/documents/${file.id}`, { responseType: 'blob' });
      const url = URL.createObjectURL(response.data), a = document.createElement('a'); a.href = url; a.download = file.name; a.click(); setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch { setError('Không tải được hồ sơ. Hãy kiểm tra quyền truy cập hoặc thử lại.'); } finally { setBusy(false); }
  }}>{busy ? 'Đang tải…' : `Tải ${file.name}`}</button>{error && <p role="alert" className="text-d-error">{error}</p>}</div>;
}
export default function VerificationPanel({ admin = false, status }) {
  const depot = useDepot(), client = useQueryClient();
  const [page, setPage] = useState(1), [filter, setFilter] = useState(admin ? 'SUBMITTED' : '');
  const [files, setFiles] = useState([]), [note, setNote] = useState(''), [operationId, setOperationId] = useState(() => crypto.randomUUID());
  const [review, setReview] = useState(null), [reason, setReason] = useState('');
  const refresh = async () => { await Promise.all([client.invalidateQueries({ queryKey: ['verification'] }), client.invalidateQueries({ queryKey: ['depot'] })]); };
  const query = useQuery({ queryKey: ['verification', admin ? 'admin' : depot?.depotId, page, filter], enabled: admin || !!depot?.depotId,
    queryFn: async () => (await api.get(admin ? '/admin/depot-verifications' : '/depot/verifications', { params: { depotId: admin ? undefined : depot.depotId, page, status: filter } })).data.data });
  const upload = useMutation({ mutationFn: async () => {
    const form = new FormData(); form.append('operationId', operationId); form.append('note', note); files.forEach(file => form.append('files', file));
    await api.post('/depot/verifications', form, { params: { depotId: depot.depotId }, headers: { 'Content-Type': 'multipart/form-data' } });
  }, onSuccess: async () => { setFiles([]); setNote(''); setOperationId(crypto.randomUUID()); await refresh(); } });
  const decide = useMutation({ mutationFn: () => api.patch(`/admin/depot-verifications/${review.id}`, { approve: review.approve, reason }),
    onSuccess: async () => { setReview(null); setReason(''); await refresh(); } });
  const invalidFiles = files.length > 3 || files.some(file => file.size === 0 || file.size > 2 * 1024 * 1024 || !['application/pdf', 'image/png', 'image/jpeg'].includes(file.type));
  return <section className="rounded-[20px] border border-d-border-subtle bg-white p-4 md:p-6 space-y-5 mt-6">
    <h2 className="text-xl font-bold">{admin ? 'Duyệt hồ sơ kho vựa' : 'Hồ sơ xác minh kho'}</h2>
    {!admin && <><p role="status" className="font-semibold text-d-primary">{verificationLabels[status] ?? status}</p><p className="text-sm text-d-on-surface-variant">Hồ sơ chỉ chủ kho và Admin được xem. Kho chưa được duyệt chưa thể tạo giao dịch mới; các đơn và nghĩa vụ thanh toán đang có vẫn được xử lý.</p>
      {['UNVERIFIED', 'REJECTED'].includes(status) && <form className="space-y-4" onSubmit={e => { e.preventDefault(); upload.mutate(); }}>
        <label className="block">Giấy đăng ký kinh doanh / hồ sơ hộ kinh doanh và tài liệu bổ sung<input key={operationId} className={inputClass} required type="file" multiple accept="application/pdf,image/png,image/jpeg" onChange={e => setFiles(Array.from(e.target.files ?? []))} /></label>
        <p className="text-sm">1–3 tệp PDF, PNG hoặc JPEG, tối đa 2 MB mỗi tệp. Kèm giấy phép chuyên ngành nếu hoạt động của kho yêu cầu.</p>
        {invalidFiles && <p role="alert" className="text-d-error">Tệp vượt giới hạn số lượng, dung lượng hoặc sai định dạng.</p>}
        <label className="block">Ghi chú hồ sơ<textarea className={inputClass} maxLength={2000} value={note} onChange={e => setNote(e.target.value)} /></label>
        <MutationError mutation={upload} /><button className={buttonClass} disabled={upload.isPending || !files.length || invalidFiles}>{upload.isPending ? 'Đang nộp…' : 'Nộp hồ sơ xác minh'}</button>
      </form>}</>}
    <label className="block">Trạng thái hồ sơ<select className={inputClass} value={filter} onChange={e => { setFilter(e.target.value); setPage(1); }}><option value="">Tất cả</option>{['SUBMITTED', 'APPROVED', 'REJECTED'].map(value => <option key={value} value={value}>{verificationLabels[value]}</option>)}</select></label>
    <QueryState query={query}><div className="space-y-4">{query.data?.items.map(item => <article key={item.id} className="rounded-xl border border-d-border-subtle p-4 space-y-3">
      <h3 className="font-semibold">{item.depotName} — {verificationLabels[item.status]}</h3><p className="text-sm">{item.depotAddress} · MST: {item.taxCode || 'Chưa cung cấp'}</p><p className="text-sm">Nộp: {new Date(item.submittedAt).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' })}</p>
      <p className="whitespace-pre-wrap break-words">{item.note}</p><div className="flex flex-wrap gap-3">{item.files.map(file => <DocumentButton key={file.id} file={file} />)}</div>
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
