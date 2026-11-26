import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { AlertTriangle, ClipboardList, CheckCheck, RotateCcw, RefreshCw, Unlock, HandCoins } from 'lucide-react';
import { Modal, ConfirmModal, Notice } from '@/components/liquidation/LiquidationUI';
import { Panel, Field, inputClass, DataTable, Th, Td, TableMessage, Pagination, PrimaryButton, GhostButton, DetailList } from '@/features/admin/components/AdminUI';
import { moneyAdminApi } from '../moneyAdminApi';
import {
  vnd, pct, fmtDateTime, fmtDate, flowLabel, kindLabel, signedForKind, deduct, apiError, payoutActions, PAYOUT_STATUS_OPTIONS, payoutStatusMeta, addDays, vnStartOfDayIso, PROVIDER_LABEL,
} from '../moneyLabels';
import { PayoutStatusChip, FieldError } from '../moneyUi';
import PayeeFilter from './PayeeFilter';

const PAGE_SIZE = 15;

function invalidateAll(qc) {
  for (const k of ['payouts', 'payees', 'ledger', 'balance']) qc.invalidateQueries({ queryKey: ['money-admin', k] });
}

function MarkPaidDialog({ run, onClose, onDone }) {
  const needsConfirm = payoutActions(run.status).markPaidNeedsConfirm;
  const [reference, setReference] = useState('');
  const [note, setNote] = useState('');
  const [confirm, setConfirm] = useState(false);
  const [err, setErr] = useState('');
  const m = useMutation({
    mutationFn: () => moneyAdminApi.markPaid(run.id, { reference: reference.trim(), note: note.trim() || null, confirm }),
    onSuccess: (data) => { toast.success('Đã đánh dấu đã chi tay'); onDone(data); },
    onError: (e) => setErr(apiError(e)),
  });
  const submit = (e) => {
    e.preventDefault();
    setErr('');
    if (!reference.trim()) return setErr('Nhập mã tham chiếu giao dịch (ví dụ mã chuyển khoản ngân hàng).');
    if (needsConfirm && !confirm) return setErr('Tick xác nhận bạn đã chuyển tiền ngoài hệ thống.');
    m.mutate();
  };
  return (
    <Modal size="md" icon={HandCoins} title={`Đánh dấu đã chi tay: ${run.code}`} onClose={onClose} busy={m.isPending}
      footer={(
        <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
          <GhostButton onClick={onClose} disabled={m.isPending}>Hủy</GhostButton>
          <button type="submit" form="markpaid-form" disabled={m.isPending} className="inline-flex items-center justify-center gap-2 rounded-full font-semibold transition active:scale-[.98] disabled:pointer-events-none disabled:opacity-50 outline-none focus-visible:ring-2 focus-visible:ring-d-primary/30 focus-visible:ring-offset-1 min-h-11 px-5 py-2 text-sm bg-d-primary text-white hover:bg-[#385500]">{m.isPending ? 'Đang lưu…' : 'Đánh dấu đã chi'}</button>
        </div>
      )}>
      <form id="markpaid-form" onSubmit={submit} className="flex flex-col gap-4">
        <p className="text-sm">Số tiền kỳ chi: <strong>{vnd(run.net)}</strong>. Chỉ dùng khi bạn đã chuyển tiền cho người nhận bằng cách khác.</p>
        {needsConfirm && <Notice tone="red" icon={AlertTriangle}>Kỳ đang ở trạng thái <strong>{payoutStatusMeta(run.status).label}</strong>: lệnh qua nhà cung cấp có thể vẫn đang chạy. Chỉ đánh dấu khi chắc chắn không bị chi trùng.</Notice>}
        <Field label="Mã tham chiếu"><input value={reference} onChange={(e) => setReference(e.target.value)} maxLength={100} className={inputClass} /></Field>
        <Field label="Ghi chú (không bắt buộc)"><input value={note} onChange={(e) => setNote(e.target.value)} maxLength={500} className={inputClass} /></Field>
        {needsConfirm && (
          <label className="flex items-start gap-2 text-sm">
            <input type="checkbox" checked={confirm} onChange={(e) => setConfirm(e.target.checked)} className="mt-0.5 size-4 accent-[#446900]" />
            <span>Tôi xác nhận đã chuyển tiền ngoài hệ thống và lệnh của nhà cung cấp sẽ không gây chi trùng.</span>
          </label>
        )}
        <FieldError>{err}</FieldError>
      </form>
    </Modal>
  );
}

function RunDetail({ id, onClose }) {
  const qc = useQueryClient();
  const q = useQuery({ queryKey: ['money-admin', 'payouts', 'detail', id], queryFn: () => moneyAdminApi.getPayout(id) });
  const [confirmAction, setConfirmAction] = useState(null);
  const [markPaid, setMarkPaid] = useState(false);
  const [err, setErr] = useState('');

  const done = (data, msg) => {
    qc.setQueryData(['money-admin', 'payouts', 'detail', id], data);
    invalidateAll(qc);
    if (msg) toast.success(msg);
    setErr('');
  };
  const act = useMutation({
    mutationFn: ({ kind }) => ({ approve: moneyAdminApi.approvePayout, retry: moneyAdminApi.retryPayout, reconcile: moneyAdminApi.reconcilePayout, release: moneyAdminApi.releasePayout })[kind](id),
    onSuccess: (data, { msg }) => { setConfirmAction(null); done(data, msg); },
    onError: (e) => { setConfirmAction(null); setErr(apiError(e)); },
  });

  const d = q.data;
  const run = d?.run;
  const a = run ? payoutActions(run.status) : {};
  const real = run?.provider === 'PAYOS';
  const busy = act.isPending;

  const BTN = 'inline-flex items-center gap-2 rounded-full px-4 py-2 text-sm font-bold transition disabled:opacity-60';
  const doConfirm = (kind, msg) => setConfirmAction({ kind, msg });

  return (
    <Modal size="xl" icon={ClipboardList} title={run ? `Kỳ chi ${run.code}` : 'Chi tiết kỳ chi'} subtitle={run?.payeeName} onClose={onClose} busy={busy}>
      {q.isLoading && <p role="status" className="py-8 text-center text-sm text-d-on-surface-variant">Đang tải…</p>}
      {q.isError && <p role="alert" className="py-8 text-center text-sm font-semibold text-d-error">{apiError(q.error, 'Không tải được chi tiết kỳ chi.')}</p>}
      {run && (
        <div className="flex flex-col gap-5">
          <div className="flex flex-wrap items-center gap-2">
            <PayoutStatusChip status={run.status} />
            <span className="text-sm text-d-on-surface-variant">{payoutStatusMeta(run.status).hint}</span>
          </div>
          {run.reason && <Notice tone="amber" icon={AlertTriangle}>{run.reason}</Notice>}
          {err && <Notice tone="red" icon={AlertTriangle}>{err}</Notice>}

          <div className="flex flex-wrap gap-2" aria-label="Thao tác">
            {a.approve && <button type="button" disabled={busy} onClick={() => doConfirm('approve', 'Đã duyệt kỳ chi')} className={`${BTN} bg-d-primary text-white`}><CheckCheck size={16} aria-hidden="true" />Duyệt</button>}
            {a.retry && <button type="button" disabled={busy} onClick={() => doConfirm('retry', 'Đã gửi lại kỳ chi')} className={`${BTN} bg-amber-500 text-white`}><RotateCcw size={16} aria-hidden="true" />Thử lại</button>}
            {a.reconcile && <button type="button" disabled={busy} onClick={() => act.mutate({ kind: 'reconcile', msg: 'Đã đối soát với nhà cung cấp' })} className={`${BTN} bg-blue-600 text-white`}><RefreshCw size={16} aria-hidden="true" />{busy ? 'Đang đối soát…' : 'Đối soát với nhà cung cấp'}</button>}
            {a.release && <button type="button" disabled={busy} onClick={() => act.mutate({ kind: 'release', msg: 'Đã giải phóng kỳ chi' })} className={`${BTN} bg-teal-600 text-white`}><Unlock size={16} aria-hidden="true" />Giải phóng</button>}
            {a.markPaid && <button type="button" disabled={busy} onClick={() => setMarkPaid(true)} className={`${BTN} border border-d-border-subtle bg-white text-d-on-surface-variant`}><HandCoins size={16} aria-hidden="true" />Đánh dấu đã chi tay</button>}
          </div>
          {run.status === 'UNKNOWN' && <Notice tone="red" icon={AlertTriangle}>Chưa rõ lệnh đã đến ngân hàng chưa. Dùng <strong>Đối soát với nhà cung cấp</strong>; không tạo lệnh mới để tránh chi trùng.</Notice>}

          <DetailList rows={[
            { label: 'Người nhận', value: run.payeeName },
            { label: 'Kỳ', value: `${fmtDate(run.periodStart)} đến ${fmtDate(run.periodEnd)}` },
            { label: 'Tổng gộp', value: vnd(run.gross) },
            { label: 'Hoa hồng', value: vnd(run.commission) },
            { label: 'Hoàn tiền', value: vnd(deduct(run.refundAmount)) },
            { label: 'Nợ mang sang', value: vnd(deduct(run.carriedDebt)) },
            { label: 'THỰC CHI', value: vnd(run.net) },
            { label: 'Nhà cung cấp', value: PROVIDER_LABEL[run.provider] ?? run.provider },
            { label: 'Mã tham chiếu nhà cung cấp', value: run.providerRef },
            { label: 'Trạng thái phía nhà cung cấp', value: run.gatewayStatus },
            { label: 'Khóa idempotency', value: d.idempotencyKey },
            { label: 'Số lần gửi', value: run.attempt },
            { label: 'Tạo lúc', value: fmtDateTime(run.createdAt) },
            { label: 'Duyệt lúc', value: fmtDateTime(run.approvedAt) },
            { label: 'Gửi lần cuối', value: fmtDateTime(run.lastAttemptAt) },
            { label: 'Đã chi lúc', value: fmtDateTime(run.paidAt) },
          ]} />

          <div>
            <h3 className="mb-2 text-sm font-bold">Tài khoản nhận (đã che)</h3>
            {d.bank ? (
              <DetailList rows={[
                { label: 'Ngân hàng', value: d.bank.bankName }, { label: 'Mã BIN', value: d.bank.bin },
                { label: 'Số tài khoản', value: d.bank.accountMasked }, { label: 'Chủ tài khoản', value: d.bank.holder },
              ]} />
            ) : <p className="text-sm text-d-on-surface-variant">Kỳ chưa lưu ảnh chụp tài khoản ngân hàng.</p>}
          </div>

          <div>
            <h3 className="mb-2 text-sm font-bold">Các dòng trong kỳ ({d.lines.length})</h3>
            <div className="overflow-hidden rounded-xl border border-d-border-subtle">
              <DataTable minWidth={720} head={<><Th>Dòng tiền</Th><Th>Loại</Th><Th className="text-right">Gộp</Th><Th className="text-right">Tỷ lệ</Th><Th className="text-right">Hoa hồng</Th><Th className="text-right">Thực nhận</Th><Th>Sẵn sàng từ</Th></>}>
                <TableMessage cols={7} empty={d.lines.length === 0}>Kỳ không có dòng nào.</TableMessage>
                {d.lines.map((l) => (
                  <tr key={l.lineId}>
                    <Td>{flowLabel(l.flow)}</Td><Td>{kindLabel(l.kind)}</Td>
                    <Td className="text-right">{vnd(signedForKind(l.kind, l.gross))}</Td><Td className="text-right">{pct(l.rate)}</Td>
                    <Td className="text-right">{vnd(signedForKind(l.kind, l.commission))}</Td><Td className="text-right font-semibold">{vnd(l.net)}</Td>
                    <Td>{fmtDateTime(l.availableAt)}</Td>
                  </tr>
                ))}
              </DataTable>
            </div>
          </div>
        </div>
      )}
      {confirmAction && run && (
        <ConfirmModal tone={real ? 'red' : 'primary'} title={confirmAction.kind === 'approve' ? 'Duyệt kỳ chi?' : 'Thử lại kỳ chi?'}
          confirmLabel={confirmAction.kind === 'approve' ? 'Duyệt' : 'Thử lại'} busy={busy}
          onConfirm={() => act.mutate(confirmAction)} onCancel={() => setConfirmAction(null)}>
          <div className="flex flex-col gap-3 text-sm">
            <p>Người nhận <strong>{run.payeeName}</strong>, số tiền <strong>{vnd(run.net)}</strong>.</p>
            {real && <Notice tone="red" icon={AlertTriangle}><strong>Sẽ gửi tiền thật qua payOS.</strong></Notice>}
          </div>
        </ConfirmModal>
      )}
      {markPaid && run && <MarkPaidDialog run={run} onClose={() => setMarkPaid(false)} onDone={(data) => { setMarkPaid(false); done(data); }} />}
    </Modal>
  );
}

export default function RunsTab({ openRunId, clearOpenRun }) {
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState('');
  const [payeeId, setPayeeId] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [selected, setSelected] = useState(null);
  useEffect(() => { if (openRunId) { setSelected(openRunId); clearOpenRun?.(); } }, [openRunId, clearOpenRun]);

  const rangeError = from && to && from > to ? 'Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.' : '';
  const params = {
    status: status || undefined, payeeId: payeeId || undefined,
    from: from ? vnStartOfDayIso(from) : undefined, to: to ? vnStartOfDayIso(addDays(to, 1)) : undefined,
    page, pageSize: PAGE_SIZE,
  };
  const q = useQuery({
    queryKey: ['money-admin', 'payouts', 'list', params],
    queryFn: () => moneyAdminApi.listPayouts(params),
    enabled: !rangeError,
    placeholderData: (p) => p,
  });
  const items = q.data?.items ?? [];
  const reset = (fn) => (v) => { fn(v); setPage(1); };

  return (
    <div className="flex flex-col gap-4">
      <Panel icon={ClipboardList} title="Lọc kỳ chi">
        <div className="flex flex-wrap items-end gap-4 p-5">
          <Field label="Trạng thái">
            <select value={status} onChange={(e) => reset(setStatus)(e.target.value)} className={`${inputClass} w-48 cursor-pointer`}>
              <option value="">Tất cả</option>
              {PAYOUT_STATUS_OPTIONS.map((s) => <option key={s} value={s}>{payoutStatusMeta(s).label}</option>)}
            </select>
          </Field>
          <PayeeFilter value={payeeId} onChange={reset(setPayeeId)} />
          <Field label="Tạo từ ngày"><input type="date" value={from} onChange={(e) => reset(setFrom)(e.target.value)} className={`${inputClass} w-44`} /></Field>
          <Field label="Đến ngày"><input type="date" value={to} onChange={(e) => reset(setTo)(e.target.value)} className={`${inputClass} w-44`} /></Field>
          <GhostButton onClick={() => { setStatus(''); setPayeeId(''); setFrom(''); setTo(''); setPage(1); }}>Xóa lọc</GhostButton>
        </div>
        {rangeError && <div className="px-5 pb-4"><FieldError>{rangeError}</FieldError></div>}
      </Panel>

      <Panel icon={ClipboardList} title="Danh sách kỳ chi">
        <DataTable minWidth={900} head={<><Th>Mã kỳ</Th><Th>Người nhận</Th><Th>Kỳ</Th><Th className="text-right">Thực chi</Th><Th>Trạng thái</Th><Th>Tạo lúc</Th><Th className="text-right">Chi tiết</Th></>}>
          <TableMessage cols={7} loading={q.isLoading} error={q.isError} empty={!q.isLoading && !q.isError && items.length === 0}>
            {q.isError ? apiError(q.error, 'Không tải được danh sách kỳ chi.') : 'Chưa có kỳ chi nào phù hợp.'}
          </TableMessage>
          {items.map((r) => (
            <tr key={r.id} className="hover:bg-d-surface-accent/40">
              <Td className="font-mono text-xs">{r.code}</Td>
              <Td className="font-semibold">{r.payeeName}</Td>
              <Td>{fmtDate(r.periodStart)} đến {fmtDate(r.periodEnd)}</Td>
              <Td className="text-right font-bold">{vnd(r.net)}</Td>
              <Td><PayoutStatusChip status={r.status} /></Td>
              <Td>{fmtDateTime(r.createdAt)}</Td>
              <Td className="text-right"><PrimaryButton onClick={() => setSelected(r.id)} className="!px-4 !py-1.5">Mở</PrimaryButton></Td>
            </tr>
          ))}
        </DataTable>
        {q.data && <Pagination page={page} pageSize={q.data.pageSize} totalCount={q.data.totalCount} onPageChange={setPage} />}
      </Panel>
      {selected && <RunDetail id={selected} onClose={() => setSelected(null)} />}
    </div>
  );
}
