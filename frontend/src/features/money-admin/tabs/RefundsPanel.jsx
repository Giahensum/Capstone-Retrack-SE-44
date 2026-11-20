import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { AlertTriangle, Undo2, CheckCheck, RotateCcw, RefreshCw, Ban, HandCoins } from 'lucide-react';
import { Modal, ConfirmModal, Notice } from '@/components/liquidation/LiquidationUI';
import { Panel, Field, inputClass, DataTable, Th, Td, TableMessage, Pagination, PrimaryButton, GhostButton, DetailList } from '@/features/admin/components/AdminUI';
import { moneyAdminApi } from '../moneyAdminApi';
import { vnd, fmtDateTime, apiError, PROVIDER_LABEL } from '../moneyLabels';
import { REFUND_STATUS_OPTIONS, refundStatusMeta, refundKindLabel, refundActions } from '../refundLabels';
import { RefundStatusChip } from '../moneyUi';
import { MarkRefundPaidDialog, RealMoneyNotice, ServerError, invalidateRefundQueries } from './RefundDialogs';

const PAGE_SIZE = 15;
const BTN = 'inline-flex items-center gap-2 rounded-full px-4 py-2 text-sm font-bold transition disabled:opacity-60';

function RefundDetail({ id, onClose, onGoPayees }) {
  const qc = useQueryClient();
  const key = ['money-admin', 'refunds', 'detail', id];
  const q = useQuery({ queryKey: key, queryFn: () => moneyAdminApi.getRefund(id) });
  const [confirmAction, setConfirmAction] = useState(null);
  const [markPaid, setMarkPaid] = useState(false);
  const [err, setErr] = useState('');

  const refresh = (msg) => {
    qc.invalidateQueries({ queryKey: key });
    invalidateRefundQueries(qc);
    if (msg) toast.success(msg);
    setErr('');
  };
  const act = useMutation({
    mutationFn: ({ kind }) => ({ approve: moneyAdminApi.approveRefund, retry: moneyAdminApi.retryRefund, reconcile: moneyAdminApi.reconcileRefund, cancel: moneyAdminApi.cancelRefund })[kind](id),
    onSuccess: (_data, { msg }) => { setConfirmAction(null); refresh(msg); },
    onError: (e) => { setConfirmAction(null); setErr(apiError(e)); qc.invalidateQueries({ queryKey: key }); },
  });

  const r = q.data?.refund;
  const bank = q.data?.bank;
  const a = r ? refundActions(r.status) : {};
  const real = r?.provider === 'PAYOS';
  const busy = act.isPending;
  const meta = r ? refundStatusMeta(r.status) : null;
  const confirmTitle = { approve: 'Duyệt lệnh hoàn?', retry: 'Thử lại lệnh hoàn?', cancel: 'Hủy lệnh hoàn?' };
  const confirmLabel = { approve: 'Duyệt', retry: 'Thử lại', cancel: 'Hủy lệnh hoàn' };

  return (
    <Modal size="xl" icon={Undo2} title={r ? `Hoàn tiền ${r.code}` : 'Chi tiết hoàn tiền'} subtitle={r?.payerName} onClose={onClose} busy={busy}>
      {q.isLoading && <p role="status" className="py-8 text-center text-sm text-d-on-surface-variant">Đang tải…</p>}
      {q.isError && <p role="alert" className="py-8 text-center text-sm font-semibold text-d-error">{apiError(q.error, 'Không tải được chi tiết hoàn tiền.')}</p>}
      {r && (
        <div className="flex flex-col gap-5">
          {real && <RealMoneyNotice />}
          <div className="flex flex-wrap items-center gap-2">
            <RefundStatusChip status={r.status} />
            <span className="text-sm text-d-on-surface-variant">{meta.hint}</span>
          </div>
          {r.failReason && <Notice tone="amber" icon={AlertTriangle}>{r.failReason}</Notice>}
          <ServerError message={err} onGoPayees={onGoPayees} />

          <div className="flex flex-wrap gap-2" aria-label="Thao tác">
            {a.approve && <button type="button" disabled={busy} onClick={() => setConfirmAction({ kind: 'approve', msg: 'Đã duyệt lệnh hoàn' })} className={`${BTN} bg-d-primary text-white`}><CheckCheck size={16} aria-hidden="true" />Duyệt</button>}
            {a.retry && <button type="button" disabled={busy} onClick={() => setConfirmAction({ kind: 'retry', msg: 'Đã gửi lại lệnh hoàn' })} className={`${BTN} bg-amber-500 text-white`}><RotateCcw size={16} aria-hidden="true" />Thử lại</button>}
            {a.reconcile && <button type="button" disabled={busy} onClick={() => act.mutate({ kind: 'reconcile', msg: 'Đã đối soát với nhà cung cấp' })} className={`${BTN} bg-blue-600 text-white`}><RefreshCw size={16} aria-hidden="true" />{busy ? 'Đang đối soát…' : 'Đối soát'}</button>}
            {a.cancel && <button type="button" disabled={busy} onClick={() => setConfirmAction({ kind: 'cancel', msg: 'Đã hủy lệnh hoàn' })} className={`${BTN} border border-red-300 bg-white text-red-700`}><Ban size={16} aria-hidden="true" />Hủy</button>}
            {a.markPaid && <button type="button" disabled={busy} onClick={() => setMarkPaid(true)} className={`${BTN} border border-d-border-subtle bg-white text-d-on-surface-variant`}><HandCoins size={16} aria-hidden="true" />Đánh dấu đã hoàn tay</button>}
          </div>
          {r.status === 'UNKNOWN' && <Notice tone="red" icon={AlertTriangle}>Chưa rõ lệnh đã đến ngân hàng chưa. Dùng <strong>Đối soát</strong>; không tạo lệnh mới để tránh hoàn trùng.</Notice>}

          <DetailList rows={[
            { label: 'Người trả (nhận lại tiền)', value: r.payerName },
            { label: 'Loại hoàn', value: refundKindLabel(r.kind) },
            { label: 'SỐ TIỀN HOÀN', value: vnd(r.amount) },
            { label: 'Lý do', value: r.reason },
            { label: 'Khoản thu', value: r.collectionId },
            { label: 'Bất thường liên quan', value: r.anomalyId ?? '—' },
            { label: 'Nhà cung cấp', value: PROVIDER_LABEL[r.provider] ?? r.provider },
            { label: 'Mã tham chiếu nhà cung cấp', value: r.providerRef },
            { label: 'Trạng thái phía nhà cung cấp', value: r.gatewayStatus },
            { label: 'Khóa idempotency', value: q.data.idempotencyKey },
            { label: 'Số lần gửi', value: r.attempt },
            { label: 'Tạo lúc', value: fmtDateTime(r.createdAt) },
            { label: 'Đã hoàn lúc', value: fmtDateTime(r.paidAt) },
          ]} />

          <div>
            <h3 className="mb-2 text-sm font-bold">Tài khoản nhận lại tiền (đã che)</h3>
            {bank ? (
              <DetailList rows={[
                { label: 'Ngân hàng', value: bank.bankName }, { label: 'Số tài khoản', value: bank.accountMasked }, { label: 'Chủ tài khoản', value: bank.holder },
              ]} />
            ) : <p className="text-sm text-d-on-surface-variant">Lệnh chưa lưu ảnh chụp tài khoản ngân hàng.</p>}
          </div>
        </div>
      )}
      {confirmAction && r && (
        <ConfirmModal tone={real || confirmAction.kind === 'cancel' ? 'red' : 'primary'} title={confirmTitle[confirmAction.kind]} confirmLabel={confirmLabel[confirmAction.kind]} busy={busy}
          onConfirm={() => act.mutate(confirmAction)} onCancel={() => setConfirmAction(null)}>
          <div className="flex flex-col gap-3 text-sm">
            <p>Người trả <strong>{r.payerName}</strong>, số tiền <strong>{vnd(r.amount)}</strong>.</p>
            {real && confirmAction.kind !== 'cancel' && <Notice tone="red" icon={AlertTriangle}><strong>Sẽ gửi tiền thật qua payOS.</strong></Notice>}
          </div>
        </ConfirmModal>
      )}
      {markPaid && r && <MarkRefundPaidDialog refund={r} onClose={() => setMarkPaid(false)} onDone={() => { setMarkPaid(false); refresh(); }} />}
    </Modal>
  );
}

export default function RefundsPanel({ onGoPayees }) {
  const [status, setStatus] = useState('');
  const [payerId, setPayerId] = useState('');
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState(null);
  const params = { status: status || undefined, payerId: payerId.trim() || undefined, page, pageSize: PAGE_SIZE };
  const q = useQuery({ queryKey: ['money-admin', 'refunds', 'list', params], queryFn: () => moneyAdminApi.listRefunds(params), placeholderData: (p) => p });
  const items = q.data?.items ?? [];
  const reset = (fn) => (v) => { fn(v); setPage(1); };

  return (
    <div className="flex flex-col gap-4">
      <Panel icon={Undo2} title="Lọc hoàn tiền">
        <div className="flex flex-wrap items-end gap-4 p-5">
          <Field label="Trạng thái">
            <select value={status} onChange={(e) => reset(setStatus)(e.target.value)} className={`${inputClass} w-48 cursor-pointer`}>
              <option value="">Tất cả</option>
              {REFUND_STATUS_OPTIONS.map((s) => <option key={s} value={s}>{refundStatusMeta(s).label}</option>)}
            </select>
          </Field>
          <Field label="Mã người trả" hint="Dán mã người dùng nếu cần lọc một người trả.">
            <input value={payerId} onChange={(e) => reset(setPayerId)(e.target.value)} placeholder="Mã người trả" className={`${inputClass} w-72`} />
          </Field>
          <GhostButton onClick={() => { setStatus(''); setPayerId(''); setPage(1); }}>Xóa lọc</GhostButton>
        </div>
      </Panel>

      <Panel icon={Undo2} title="Danh sách hoàn tiền">
        <DataTable minWidth={1000} head={<><Th>Mã</Th><Th>Người trả</Th><Th>Loại</Th><Th className="text-right">Số tiền</Th><Th>Trạng thái</Th><Th>Tạo lúc</Th><Th className="text-right">Chi tiết</Th></>}>
          <TableMessage cols={7} loading={q.isLoading} error={q.isError} empty={!q.isLoading && !q.isError && items.length === 0}>
            {q.isError ? apiError(q.error, 'Không tải được danh sách hoàn tiền.') : 'Chưa có lệnh hoàn tiền nào phù hợp.'}
          </TableMessage>
          {items.map((r) => (
            <tr key={r.id} className="hover:bg-d-surface-accent/40">
              <Td className="font-mono text-xs">{r.code}</Td>
              <Td className="font-semibold">{r.payerName}</Td>
              <Td>{refundKindLabel(r.kind)}</Td>
              <Td className="text-right font-bold">{vnd(r.amount)}</Td>
              <Td>
                <RefundStatusChip status={r.status} />
                {r.failReason && <p className="mt-1 max-w-xs text-xs text-d-error">{r.failReason}</p>}
              </Td>
              <Td>{fmtDateTime(r.createdAt)}</Td>
              <Td className="text-right"><PrimaryButton onClick={() => setSelected(r.id)} className="!px-4 !py-1.5">Mở</PrimaryButton></Td>
            </tr>
          ))}
        </DataTable>
        {q.data && <Pagination page={page} pageSize={q.data.pageSize} totalCount={q.data.totalCount} onPageChange={setPage} />}
      </Panel>
      {selected && <RefundDetail id={selected} onClose={() => setSelected(null)} onGoPayees={onGoPayees} />}
    </div>
  );
}
