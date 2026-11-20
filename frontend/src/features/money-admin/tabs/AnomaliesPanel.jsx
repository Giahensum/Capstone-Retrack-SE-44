import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { TriangleAlert, Undo2, CheckCheck } from 'lucide-react';
import { Modal, Notice } from '@/components/liquidation/LiquidationUI';
import { Panel, Field, inputClass, DataTable, Th, Td, TableMessage, Pagination, PrimaryButton, GhostButton, DetailList } from '@/features/admin/components/AdminUI';
import { moneyAdminApi } from '../moneyAdminApi';
import { vnd, fmtDateTime, flowLabel, apiError, FLOW_LABEL } from '../moneyLabels';
import { ANOMALY_KIND_OPTIONS, ANOMALY_STATUS, anomalyKindMeta, anomalyActions } from '../refundLabels';
import { AnomalyKindChip, AnomalyStatusChip, BankStatusChip } from '../moneyUi';
import { AnomalyRefundDialog, ResolveAnomalyDialog } from './RefundDialogs';

const PAGE_SIZE = 15;
const FLOWS = Object.keys(FLOW_LABEL);

function AnomalyDetail({ item, onClose, onRefund, onResolve }) {
  const q = useQuery({
    queryKey: ['money-admin', 'anomalies', 'detail', item.id],
    queryFn: () => moneyAdminApi.getAnomaly(item.id),
    initialData: item,
    initialDataUpdatedAt: 0,
  });
  const a = q.data ?? item;
  const act = anomalyActions(a);
  const kind = anomalyKindMeta(a.kind);
  return (
    <Modal size="lg" icon={TriangleAlert} title={`Bất thường: ${kind.label}`} subtitle={a.reference} onClose={onClose}>
      <div className="flex flex-col gap-4">
        <div className="flex flex-wrap items-center gap-2">
          <AnomalyKindChip kind={a.kind} /><AnomalyStatusChip status={a.status} />
          <span className="text-sm text-d-on-surface-variant">{kind.hint}</span>
        </div>
        {q.isError && <p role="alert" className="text-sm font-semibold text-d-error">{apiError(q.error, 'Không tải được chi tiết mới nhất, đang hiện dữ liệu từ danh sách.')}</p>}
        {a.hasRefund && <Notice tone="blue" icon={Undo2}>Bất thường này đã có lệnh hoàn tiền. Xem ở mục Hoàn tiền.</Notice>}
        <div className="flex flex-wrap gap-2" aria-label="Thao tác">
          {act.refund && <PrimaryButton icon={Undo2} onClick={() => onRefund(a)}>Hoàn tiền</PrimaryButton>}
          {act.resolve && <GhostButton onClick={() => onResolve(a)}><CheckCheck size={14} className="mr-1 inline" aria-hidden="true" />Đánh dấu đã xử lý</GhostButton>}
        </div>
        <DetailList rows={[
          { label: 'Tham chiếu', value: a.reference },
          { label: 'Số tiền được hoàn', value: vnd(a.refundableAmount) },
          { label: 'Số tiền đã hoàn', value: vnd(a.refundAmount) },
          { label: 'Khoản thu', value: a.collection ? `${flowLabel(a.collection.flow)} · ${vnd(a.collection.amount)} · ${a.collection.status}` : '—' },
          { label: 'Người trả', value: a.payer?.name },
          { label: 'Người nhận', value: a.payee?.name },
          { label: 'Tài khoản người trả', value: a.payerBank ? <span className="inline-flex items-center gap-2"><BankStatusChip status={a.payerBank.status} /><span className="font-mono text-xs">{a.payerBank.masked ?? ''}</span></span> : '—' },
          { label: 'Ghi chú', value: a.note },
          { label: 'Tạo lúc', value: fmtDateTime(a.createdAt) },
          { label: 'Xử lý lúc', value: fmtDateTime(a.resolvedAt) },
        ]} />
      </div>
    </Modal>
  );
}

export default function AnomaliesPanel({ onGoPayees }) {
  const [f, setF] = useState({ status: 'OPEN', kind: '', flow: '' });
  const [page, setPage] = useState(1);
  const [detail, setDetail] = useState(null);
  const [refunding, setRefunding] = useState(null);
  const [resolving, setResolving] = useState(null);
  const set = (patch) => { setF((x) => ({ ...x, ...patch })); setPage(1); };
  const params = { status: f.status || undefined, kind: f.kind || undefined, flow: f.flow || undefined, page, pageSize: PAGE_SIZE };
  const q = useQuery({ queryKey: ['money-admin', 'anomalies', 'list', params], queryFn: () => moneyAdminApi.listAnomalies(params), placeholderData: (p) => p });
  const items = q.data?.items ?? [];

  return (
    <div className="flex flex-col gap-4">
      <Panel icon={TriangleAlert} title="Lọc bất thường">
        <div className="flex flex-wrap items-end gap-4 p-5">
          <Field label="Trạng thái">
            <select value={f.status} onChange={(e) => set({ status: e.target.value })} className={`${inputClass} w-44 cursor-pointer`}>
              <option value="">Tất cả</option>
              {Object.entries(ANOMALY_STATUS).map(([k, v]) => <option key={k} value={k}>{v.label}</option>)}
            </select>
          </Field>
          <Field label="Loại">
            <select value={f.kind} onChange={(e) => set({ kind: e.target.value })} className={`${inputClass} w-48 cursor-pointer`}>
              <option value="">Tất cả</option>
              {ANOMALY_KIND_OPTIONS.map((k) => <option key={k} value={k}>{anomalyKindMeta(k).label}</option>)}
            </select>
          </Field>
          <Field label="Dòng tiền">
            <select value={f.flow} onChange={(e) => set({ flow: e.target.value })} className={`${inputClass} w-48 cursor-pointer`}>
              <option value="">Tất cả</option>
              {FLOWS.map((k) => <option key={k} value={k}>{FLOW_LABEL[k]}</option>)}
            </select>
          </Field>
          <GhostButton onClick={() => { setF({ status: 'OPEN', kind: '', flow: '' }); setPage(1); }}>Xóa lọc</GhostButton>
        </div>
      </Panel>

      <Panel icon={TriangleAlert} title="Bất thường thu tiền">
        <DataTable minWidth={1200} head={<><Th>Loại</Th><Th>Tham chiếu</Th><Th>Dòng tiền</Th><Th className="text-right">Khoản thu</Th><Th className="text-right">Được hoàn</Th><Th>Người trả</Th><Th>Người nhận</Th><Th>Ngân hàng người trả</Th><Th>Trạng thái</Th><Th>Tạo lúc</Th><Th className="text-right">Thao tác</Th></>}>
          <TableMessage cols={11} loading={q.isLoading} error={q.isError} empty={!q.isLoading && !q.isError && items.length === 0}>
            {q.isError ? apiError(q.error, 'Không tải được danh sách bất thường.') : 'Không có bất thường nào phù hợp.'}
          </TableMessage>
          {items.map((a) => {
            const act = anomalyActions(a);
            return (
              <tr key={a.id} className="align-top">
                <Td><AnomalyKindChip kind={a.kind} /></Td>
                <Td className="font-mono text-xs">{a.reference ?? '—'}</Td>
                <Td>{flowLabel(a.collection?.flow)}</Td>
                <Td className="text-right">{vnd(a.collection?.amount)}</Td>
                <Td className="text-right font-bold">{vnd(a.refundableAmount)}{a.hasRefund && <p className="text-xs font-normal text-d-on-surface-variant">đã có lệnh hoàn</p>}</Td>
                <Td className="font-semibold">{a.payer?.name ?? '—'}</Td>
                <Td>{a.payee?.name ?? '—'}</Td>
                <Td><BankStatusChip status={a.payerBank?.status} /></Td>
                <Td><AnomalyStatusChip status={a.status} /></Td>
                <Td>{fmtDateTime(a.createdAt)}</Td>
                <Td className="text-right">
                  <div className="inline-flex flex-wrap justify-end gap-1.5">
                    <GhostButton onClick={() => setDetail(a)} className="!px-3 !py-1.5">Chi tiết</GhostButton>
                    {act.refund && <PrimaryButton icon={Undo2} onClick={() => setRefunding(a)} className="!px-3 !py-1.5">Hoàn tiền</PrimaryButton>}
                    {act.resolve && <GhostButton onClick={() => setResolving(a)} className="!px-3 !py-1.5">Đã xử lý</GhostButton>}
                  </div>
                </Td>
              </tr>
            );
          })}
        </DataTable>
        {q.data && <Pagination page={page} pageSize={q.data.pageSize} totalCount={q.data.totalCount} onPageChange={setPage} />}
      </Panel>

      {detail && <AnomalyDetail item={detail} onClose={() => setDetail(null)} onRefund={(a) => { setDetail(null); setRefunding(a); }} onResolve={(a) => { setDetail(null); setResolving(a); }} />}
      {refunding && <AnomalyRefundDialog anomaly={refunding} onClose={() => setRefunding(null)} onGoPayees={onGoPayees} />}
      {resolving && <ResolveAnomalyDialog anomaly={resolving} onClose={() => setResolving(null)} />}
    </div>
  );
}
