import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { AlertTriangle, BadgeCheck, Landmark, Users, CheckCircle2, XCircle } from 'lucide-react';
import { Modal, Notice } from '@/components/liquidation/LiquidationUI';
import { Panel, SearchBox, inputClass, labelClass, DataTable, Th, Td, TableMessage, Pagination, PrimaryButton, GhostButton, DetailList } from '@/features/admin/components/AdminUI';
import { moneyAdminApi } from '../moneyAdminApi';
import { vnd, roleLabel, apiError, fmtDateTime, BANK_STATUS_OPTIONS, bankStatusMeta } from '../moneyLabels';
import { validateReason } from '../refundLabels';
import { BankStatusChip, FieldError, useDebounced } from '../moneyUi';

const PAGE_SIZE = 15;

function BankReview({ payee, onClose }) {
  const qc = useQueryClient();
  const key = ['money-admin', 'payees', 'bank', payee.userId];
  const q = useQuery({ queryKey: key, queryFn: () => moneyAdminApi.getPayeeBank(payee.userId), staleTime: 0, gcTime: 0 });
  const [err, setErr] = useState('');
  const [rejecting, setRejecting] = useState(false);
  const [reason, setReason] = useState('');
  const [reasonErr, setReasonErr] = useState('');
  const b = q.data;

  const onDone = (msg) => {
    toast.success(msg);
    qc.invalidateQueries({ queryKey: ['money-admin', 'payees'] });
    qc.invalidateQueries({ queryKey: ['money-admin', 'payouts'] });
    onClose();
  };
  const onFail = (e) => {
    if (e?.response?.status === 409) {
      // Người nhận vừa đổi tài khoản: tải lại thông tin mới để đối chiếu, không tự thử lại.
      setErr(`${apiError(e)} Đã tải lại thông tin mới nhất bên dưới.`);
      setRejecting(false);
      qc.invalidateQueries({ queryKey: ['money-admin', 'payees'] });
      q.refetch();
    } else setErr(apiError(e));
  };
  const verify = useMutation({
    mutationFn: () => moneyAdminApi.verifyPayeeBank(payee.userId, b.accountNumber),
    onSuccess: () => onDone('Đã duyệt tài khoản ngân hàng; các kỳ chi đang giữ sẽ tự được giải phóng'),
    onError: onFail,
  });
  const reject = useMutation({
    mutationFn: () => moneyAdminApi.rejectPayeeBank(payee.userId, b.accountNumber, reason.trim()),
    onSuccess: () => onDone('Đã từ chối tài khoản ngân hàng'),
    onError: onFail,
  });
  const busy = verify.isPending || reject.isPending;
  const complete = b && b.bankBin && b.accountNumber && b.accountName;
  const decidable = complete && b.status === 'PENDING_VERIFICATION' && !q.isFetching;

  const submitReject = (e) => {
    e.preventDefault();
    const r = validateReason(reason, 'Lý do từ chối');
    setReasonErr(r ?? '');
    if (r) return;
    setErr('');
    reject.mutate();
  };

  return (
    <Modal size="md" icon={Landmark} title="Duyệt tài khoản ngân hàng" subtitle={payee.fullName} onClose={onClose} busy={busy}
      footer={rejecting ? (
        <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
          <GhostButton onClick={() => { setRejecting(false); setReasonErr(''); }} disabled={busy}>Quay lại</GhostButton>
          <button type="submit" form="reject-bank-form" disabled={busy} className="inline-flex items-center justify-center gap-2 rounded-full font-semibold transition active:scale-[.98] disabled:pointer-events-none disabled:opacity-50 outline-none focus-visible:ring-2 focus-visible:ring-d-primary/30 focus-visible:ring-offset-1 min-h-11 px-5 py-2 text-sm bg-d-error text-white hover:bg-[#93000a]">
            <XCircle size={16} aria-hidden="true" />{reject.isPending ? 'Đang từ chối…' : 'Xác nhận từ chối'}
          </button>
        </div>
      ) : (
        <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
          <GhostButton onClick={onClose} disabled={busy}>Đóng</GhostButton>
          <GhostButton onClick={() => { setErr(''); setRejecting(true); }} disabled={!decidable || busy} className="!border-red-300 !text-red-700">Từ chối</GhostButton>
          <PrimaryButton icon={BadgeCheck} onClick={() => { setErr(''); verify.mutate(); }} disabled={!decidable || busy}>
            {verify.isPending ? 'Đang duyệt…' : 'Duyệt'}
          </PrimaryButton>
        </div>
      )}>
      {q.isLoading && <p role="status" className="py-6 text-center text-sm text-d-on-surface-variant">Đang tải…</p>}
      {q.isError && <p role="alert" className="py-6 text-center text-sm font-semibold text-d-error">{apiError(q.error, 'Không tải được tài khoản ngân hàng.')}</p>}
      {b && (
        <div className="flex flex-col gap-4">
          <div className="flex flex-wrap items-center gap-2">
            <BankStatusChip status={b.status} />
            {b.verifiedAt && <span className="text-xs text-d-on-surface-variant">Duyệt lúc {fmtDateTime(b.verifiedAt)}</span>}
          </div>
          {b.status === 'REJECTED' && b.rejectReason && <Notice tone="red" icon={XCircle}>Đã từ chối: {b.rejectReason}</Notice>}
          <DetailList rows={[
            { label: 'Họ tên trên hồ sơ', value: b.fullName },
            { label: 'Ngân hàng', value: b.bankName }, { label: 'Mã BIN', value: b.bankBin },
            { label: 'Số tài khoản (đầy đủ)', value: <span className="font-mono">{b.accountNumber ?? '—'}</span> },
            { label: 'Tên chủ tài khoản', value: b.accountName },
            { label: 'Đổi lần cuối', value: fmtDateTime(b.changedAt) },
          ]} />
          {complete ? (
            <Notice tone={b.nameMatchesFullName ? 'teal' : 'amber'} icon={b.nameMatchesFullName ? CheckCircle2 : XCircle}>
              {b.nameMatchesFullName ? 'Tên chủ tài khoản khớp họ tên trên hồ sơ.' : 'Tên chủ tài khoản KHÔNG khớp họ tên trên hồ sơ. Hãy đối chiếu kỹ (giấy tờ, ảnh chụp) trước khi duyệt.'}
            </Notice>
          ) : <Notice tone="amber" icon={AlertTriangle}>Người nhận chưa nhập đủ thông tin ngân hàng, chưa thể duyệt.</Notice>}
          {complete && b.status !== 'PENDING_VERIFICATION' && <Notice tone="blue" icon={AlertTriangle}>Tài khoản này không còn ở trạng thái chờ duyệt. Chỉ tài khoản mới nhập hoặc vừa đổi mới cần Admin duyệt.</Notice>}
          {rejecting && (
            <form id="reject-bank-form" onSubmit={submitReject} className="flex flex-col gap-2">
              <label htmlFor="reject-bank-reason" className={labelClass}>Lý do từ chối (5 đến 255 ký tự, người nhận sẽ thấy)</label>
              <textarea id="reject-bank-reason" value={reason} onChange={(e) => setReason(e.target.value)} maxLength={255} rows={3} disabled={busy}
                aria-invalid={!!reasonErr} className={`${inputClass} !h-auto py-2`} />
              <FieldError>{reasonErr}</FieldError>
            </form>
          )}
          {err && <Notice tone="red" icon={AlertTriangle}>{err}</Notice>}
          <p className="text-xs text-d-on-surface-variant">Nút Duyệt và Từ chối gửi kèm số tài khoản bạn đang xem; nếu người nhận vừa đổi số, máy chủ sẽ từ chối với lỗi 409 và bạn phải đối chiếu lại. Không còn tự xác minh theo tên và không còn thời gian giữ.</p>
        </div>
      )}
    </Modal>
  );
}

export default function PayeesTab() {
  const qc = useQueryClient();
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [bankStatus, setBankStatus] = useState('');
  const [review, setReview] = useState(null);
  const debounced = useDebounced(search);
  const q = useQuery({
    queryKey: ['money-admin', 'payees', 'list', debounced, bankStatus, page],
    queryFn: () => moneyAdminApi.listPayees({ search: debounced || undefined, bankStatus: bankStatus || undefined, page, pageSize: PAGE_SIZE }),
    placeholderData: (p) => p,
  });
  const auto = useMutation({
    mutationFn: ({ userId, autoEnabled }) => moneyAdminApi.setAutoPayout(userId, autoEnabled),
    onSuccess: () => { toast.success('Đã lưu công tắc tự chi'); qc.invalidateQueries({ queryKey: ['money-admin', 'payees'] }); },
    onError: (e) => toast.error(apiError(e)),
  });
  const items = q.data?.items ?? [];

  return (
    <Panel icon={Users} title="Người nhận tiền" toolbar={(
      <div className="flex flex-wrap items-center gap-2">
        <select aria-label="Lọc theo trạng thái tài khoản ngân hàng" value={bankStatus} onChange={(e) => { setBankStatus(e.target.value); setPage(1); }} className={`${inputClass} !h-10 w-52 cursor-pointer`}>
          <option value="">Mọi trạng thái ngân hàng</option>
          {BANK_STATUS_OPTIONS.map((k) => <option key={k} value={k}>{bankStatusMeta(k).label}</option>)}
        </select>
        <SearchBox value={search} onChange={(e) => { setSearch(e.target.value); setPage(1); }} placeholder="Tìm theo tên" className="w-64" />
      </div>
    )}>
      <DataTable minWidth={1000} head={<><Th>Người nhận</Th><Th>Vai trò</Th><Th className="text-right">Đang giữ</Th><Th className="text-right">Sẵn sàng chi</Th><Th className="text-right">Đang chi</Th><Th>Ngân hàng</Th><Th>Tự chi</Th><Th className="text-right">Thao tác</Th></>}>
        <TableMessage cols={8} loading={q.isLoading} error={q.isError} empty={!q.isLoading && !q.isError && items.length === 0}>
          {q.isError ? apiError(q.error, 'Không tải được danh sách người nhận.') : 'Không có người nhận phù hợp.'}
        </TableMessage>
        {items.map((p) => (
          <tr key={p.userId} className={p.isActive ? '' : 'opacity-60'}>
            <Td className="font-semibold">{p.fullName}{!p.isActive && <span className="ml-2 text-xs font-normal text-d-error">(đã khóa)</span>}</Td>
            <Td>{roleLabel(p.role)}</Td>
            <Td className="text-right">{vnd(p.heldAmount)}</Td>
            <Td className="text-right font-semibold">{vnd(p.availableAmount)}</Td>
            <Td className="text-right">{vnd(p.inPayoutAmount)}</Td>
            <Td>
              <div className="flex flex-col items-start gap-1">
                <BankStatusChip status={p.bank?.status} />
                {p.bank?.accountNumberMasked && <span className="text-xs text-d-on-surface-variant">{p.bank.bankName} · {p.bank.accountNumberMasked}</span>}
                {p.bank?.status === 'REJECTED' && p.bank?.rejectReason && <span className="max-w-xs text-xs text-d-error">Lý do: {p.bank.rejectReason}</span>}
              </div>
            </Td>
            <Td>
              <label className="inline-flex cursor-pointer items-center gap-2 text-xs">
                <input type="checkbox" role="switch" checked={p.autoPayoutEnabled} disabled={auto.isPending}
                  aria-label={`Tự chi cho ${p.fullName}`} onChange={(e) => auto.mutate({ userId: p.userId, autoEnabled: e.target.checked })} className="size-5 accent-[#446900]" />
                {p.autoPayoutEnabled ? 'Bật' : 'Tắt'}
              </label>
            </Td>
            <Td className="text-right"><GhostButton onClick={() => setReview(p)} className="!px-4 !py-1.5">{p.bank?.status === 'PENDING_VERIFICATION' ? 'Duyệt tài khoản' : 'Xem tài khoản'}</GhostButton></Td>
          </tr>
        ))}
      </DataTable>
      {q.data && <Pagination page={page} pageSize={q.data.pageSize} totalCount={q.data.totalCount} onPageChange={setPage} />}
      {review && <BankReview payee={review} onClose={() => setReview(null)} />}
    </Panel>
  );
}
