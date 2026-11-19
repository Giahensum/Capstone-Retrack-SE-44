import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { AlertTriangle, BadgeCheck, Landmark, Users, CheckCircle2, XCircle } from 'lucide-react';
import { Modal, Notice } from '@/components/liquidation/LiquidationUI';
import { Panel, SearchBox, DataTable, Th, Td, TableMessage, Pagination, PrimaryButton, GhostButton, DetailList } from '@/features/admin/components/AdminUI';
import { moneyAdminApi } from '../moneyAdminApi';
import { vnd, roleLabel, apiError, fmtDateTime } from '../moneyLabels';
import { BankStatusChip, useDebounced } from '../moneyUi';

const PAGE_SIZE = 15;

function BankReview({ payee, onClose }) {
  const qc = useQueryClient();
  const key = ['money-admin', 'payees', 'bank', payee.userId];
  const q = useQuery({ queryKey: key, queryFn: () => moneyAdminApi.getPayeeBank(payee.userId), staleTime: 0, gcTime: 0 });
  const [err, setErr] = useState('');
  const b = q.data;
  const verify = useMutation({
    mutationFn: () => moneyAdminApi.verifyPayeeBank(payee.userId, b.accountNumber),
    onSuccess: () => { toast.success('Đã xác minh tài khoản ngân hàng'); qc.invalidateQueries({ queryKey: ['money-admin', 'payees'] }); onClose(); },
    onError: (e) => {
      if (e?.response?.status === 409) {
        setErr(`${apiError(e)} Đã tải lại thông tin mới nhất bên dưới.`);
        q.refetch();
      } else setErr(apiError(e));
    },
  });
  const complete = b && b.bankBin && b.accountNumber && b.accountName;
  return (
    <Modal size="md" icon={Landmark} title="Duyệt tài khoản ngân hàng" subtitle={payee.fullName} onClose={onClose} busy={verify.isPending}
      footer={(
        <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
          <GhostButton onClick={onClose} disabled={verify.isPending}>Đóng</GhostButton>
          <PrimaryButton icon={BadgeCheck} onClick={() => { setErr(''); verify.mutate(); }} disabled={!complete || verify.isPending || b?.status === 'VERIFIED' || q.isFetching}>
            {verify.isPending ? 'Đang xác minh…' : 'Xác minh'}
          </PrimaryButton>
        </div>
      )}>
      {q.isLoading && <p role="status" className="py-6 text-center text-sm text-d-on-surface-variant">Đang tải…</p>}
      {q.isError && <p role="alert" className="py-6 text-center text-sm font-semibold text-d-error">{apiError(q.error, 'Không tải được tài khoản ngân hàng.')}</p>}
      {b && (
        <div className="flex flex-col gap-4">
          <div className="flex items-center gap-2"><BankStatusChip status={b.status} />{b.verifiedAt && <span className="text-xs text-d-on-surface-variant">Xác minh {fmtDateTime(b.verifiedAt)}</span>}</div>
          <DetailList rows={[
            { label: 'Họ tên trên hồ sơ', value: b.fullName },
            { label: 'Ngân hàng', value: b.bankName }, { label: 'Mã BIN', value: b.bankBin },
            { label: 'Số tài khoản (đầy đủ)', value: <span className="font-mono">{b.accountNumber ?? '—'}</span> },
            { label: 'Tên chủ tài khoản', value: b.accountName },
            { label: 'Đổi lần cuối', value: fmtDateTime(b.changedAt) },
          ]} />
          {complete ? (
            <Notice tone={b.nameMatchesFullName ? 'teal' : 'amber'} icon={b.nameMatchesFullName ? CheckCircle2 : XCircle}>
              {b.nameMatchesFullName ? 'Tên chủ tài khoản khớp họ tên trên hồ sơ.' : 'Tên chủ tài khoản KHÔNG khớp họ tên trên hồ sơ. Hãy đối chiếu kỹ (giấy tờ, ảnh chụp) trước khi xác minh.'}
            </Notice>
          ) : <Notice tone="amber" icon={AlertTriangle}>Người nhận chưa nhập đủ thông tin ngân hàng, chưa thể xác minh.</Notice>}
          {err && <Notice tone="red" icon={AlertTriangle}>{err}</Notice>}
          <p className="text-xs text-d-on-surface-variant">Nút Xác minh gửi kèm số tài khoản bạn đang xem; nếu người nhận vừa đổi số, máy chủ sẽ từ chối và bạn phải đối chiếu lại.</p>
        </div>
      )}
    </Modal>
  );
}

export default function PayeesTab() {
  const qc = useQueryClient();
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [review, setReview] = useState(null);
  const debounced = useDebounced(search);
  const q = useQuery({
    queryKey: ['money-admin', 'payees', 'list', debounced, page],
    queryFn: () => moneyAdminApi.listPayees({ search: debounced || undefined, page, pageSize: PAGE_SIZE }),
    placeholderData: (p) => p,
  });
  const auto = useMutation({
    mutationFn: ({ userId, autoEnabled }) => moneyAdminApi.setAutoPayout(userId, autoEnabled),
    onSuccess: () => { toast.success('Đã lưu công tắc tự chi'); qc.invalidateQueries({ queryKey: ['money-admin', 'payees'] }); },
    onError: (e) => toast.error(apiError(e)),
  });
  const items = q.data?.items ?? [];

  return (
    <Panel icon={Users} title="Người nhận tiền" toolbar={<SearchBox value={search} onChange={(e) => { setSearch(e.target.value); setPage(1); }} placeholder="Tìm theo tên" className="w-64" />}>
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
                {p.bank?.holdUntil && <span className="text-xs text-amber-700">Giữ đến {fmtDateTime(p.bank.holdUntil)}</span>}
              </div>
            </Td>
            <Td>
              <label className="inline-flex cursor-pointer items-center gap-2 text-xs">
                <input type="checkbox" role="switch" checked={p.autoPayoutEnabled} disabled={auto.isPending}
                  aria-label={`Tự chi cho ${p.fullName}`} onChange={(e) => auto.mutate({ userId: p.userId, autoEnabled: e.target.checked })} className="size-5 accent-[#446900]" />
                {p.autoPayoutEnabled ? 'Bật' : 'Tắt'}
              </label>
            </Td>
            <Td className="text-right"><GhostButton onClick={() => setReview(p)} className="!px-4 !py-1.5">Duyệt tài khoản</GhostButton></Td>
          </tr>
        ))}
      </DataTable>
      {q.data && <Pagination page={page} pageSize={q.data.pageSize} totalCount={q.data.totalCount} onPageChange={setPage} />}
      {review && <BankReview payee={review} onClose={() => setReview(null)} />}
    </Panel>
  );
}
