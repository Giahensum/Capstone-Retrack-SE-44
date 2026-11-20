import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Receipt, BookOpen, Undo2 } from 'lucide-react';
import { Panel, Field, inputClass, DataTable, Th, Td, TableMessage, Pagination, GhostButton } from '@/features/admin/components/AdminUI';
import { Badge } from '@/components/liquidation/LiquidationUI';
import { moneyAdminApi } from '../moneyAdminApi';
import {
  vnd, pct, fmtDateTime, flowLabel, sourceLabel, kindLabel, signedForKind, apiError, COLLECTION_STATUS, LINE_STATE, collectionStatusMeta, lineStateMeta, FLOW_LABEL,
} from '../moneyLabels';
import PayeeFilter from './PayeeFilter';
import { CollectionRefundDialog } from './RefundDialogs';

const PAGE_SIZE = 20;
const FLOWS = ['PICKUP', 'BATCH', 'LIQUIDATION'];

function FlowSelect({ value, onChange, options = FLOWS }) {
  return (
    <Field label="Dòng tiền">
      <select value={value} onChange={(e) => onChange(e.target.value)} className={`${inputClass} w-48 cursor-pointer`}>
        <option value="">Tất cả</option>
        {options.map((f) => <option key={f} value={f}>{FLOW_LABEL[f] ?? f}</option>)}
      </select>
    </Field>
  );
}

function CollectionsPanel({ onGoPayees }) {
  const [refunding, setRefunding] = useState(null);
  const [f, setF] = useState({ status: '', flow: '', payeeId: '' });
  const [page, setPage] = useState(1);
  const set = (patch) => { setF((x) => ({ ...x, ...patch })); setPage(1); };
  const params = { status: f.status || undefined, flow: f.flow || undefined, payeeId: f.payeeId || undefined, page, pageSize: PAGE_SIZE };
  const q = useQuery({ queryKey: ['money-admin', 'collections', params], queryFn: () => moneyAdminApi.listCollections(params), placeholderData: (p) => p });
  const items = q.data?.items ?? [];
  return (
    <Panel icon={Receipt} title="Khoản thu vào sàn">
      <div className="flex flex-wrap items-end gap-4 border-b border-d-border-subtle p-5">
        <Field label="Trạng thái">
          <select value={f.status} onChange={(e) => set({ status: e.target.value })} className={`${inputClass} w-48 cursor-pointer`}>
            <option value="">Tất cả</option>
            {Object.entries(COLLECTION_STATUS).map(([k, v]) => <option key={k} value={k}>{v.label}</option>)}
          </select>
        </Field>
        <FlowSelect value={f.flow} onChange={(flow) => set({ flow })} options={[...FLOWS, 'LIQUIDATION_DEPOSIT', 'LIQUIDATION_BALANCE']} />
        <PayeeFilter value={f.payeeId} onChange={(payeeId) => set({ payeeId })} />
        <GhostButton onClick={() => { setF({ status: '', flow: '', payeeId: '' }); setPage(1); }}>Xóa lọc</GhostButton>
      </div>
      <DataTable minWidth={1100} head={<><Th>Dòng tiền</Th><Th>Nguồn</Th><Th>Người trả</Th><Th>Người nhận</Th><Th className="text-right">Số tiền</Th><Th className="text-right">Đã nhận</Th><Th>Trạng thái</Th><Th>Thu lúc</Th><Th className="text-right">Thao tác</Th></>}>
        <TableMessage cols={9} loading={q.isLoading} error={q.isError} empty={!q.isLoading && !q.isError && items.length === 0}>
          {q.isError ? apiError(q.error, 'Không tải được khoản thu.') : 'Chưa có khoản thu nào phù hợp.'}
        </TableMessage>
        {items.map((c) => {
          const m = collectionStatusMeta(c.status);
          return (
            <tr key={c.id}>
              <Td>{flowLabel(c.flow)}</Td><Td>{sourceLabel(c.sourceType)}</Td>
              <Td>{c.payerName}</Td><Td>{c.payeeName}</Td>
              <Td className="text-right font-semibold">{vnd(c.amount)}</Td>
              <Td className="text-right">{c.receivedAmount == null ? '—' : vnd(c.receivedAmount)}</Td>
              <Td><Badge tone={m.tone}>{m.label}</Badge></Td>
              <Td>{fmtDateTime(c.paidAt ?? c.createdAt)}</Td>
              <Td className="text-right">{c.status === 'PAID' && <GhostButton onClick={() => setRefunding(c)} className="!px-3 !py-1.5"><Undo2 size={14} className="mr-1 inline" aria-hidden="true" />Hoàn tiền</GhostButton>}</Td>
            </tr>
          );
        })}
      </DataTable>
      {q.data && <Pagination page={page} pageSize={q.data.pageSize} totalCount={q.data.totalCount} onPageChange={setPage} />}
      {refunding && <CollectionRefundDialog collection={refunding} onClose={() => setRefunding(null)} onGoPayees={onGoPayees} />}
    </Panel>
  );
}

function LedgerPanel() {
  const [f, setF] = useState({ state: '', flow: '', payeeId: '' });
  const [page, setPage] = useState(1);
  const set = (patch) => { setF((x) => ({ ...x, ...patch })); setPage(1); };
  const params = { state: f.state || undefined, flow: f.flow || undefined, payeeId: f.payeeId || undefined, page, pageSize: PAGE_SIZE };
  const q = useQuery({ queryKey: ['money-admin', 'ledger', params], queryFn: () => moneyAdminApi.listLedger(params), placeholderData: (p) => p });
  const items = q.data?.items ?? [];
  return (
    <Panel icon={BookOpen} title="Sổ cái phải chi cho người nhận (chỉ đọc)">
      <div className="flex flex-wrap items-end gap-4 border-b border-d-border-subtle p-5">
        <Field label="Trạng thái dòng">
          <select value={f.state} onChange={(e) => set({ state: e.target.value })} className={`${inputClass} w-48 cursor-pointer`}>
            <option value="">Tất cả</option>
            {Object.entries(LINE_STATE).map(([k, v]) => <option key={k} value={k}>{v.label}</option>)}
          </select>
        </Field>
        <FlowSelect value={f.flow} onChange={(flow) => set({ flow })} />
        <PayeeFilter value={f.payeeId} onChange={(payeeId) => set({ payeeId })} />
        <GhostButton onClick={() => { setF({ state: '', flow: '', payeeId: '' }); setPage(1); }}>Xóa lọc</GhostButton>
      </div>
      <DataTable minWidth={1100} head={<><Th>Người nhận</Th><Th>Dòng tiền</Th><Th>Loại</Th><Th className="text-right">Gộp</Th><Th className="text-right">Tỷ lệ</Th><Th className="text-right">Hoa hồng</Th><Th className="text-right">Thực nhận</Th><Th>Trạng thái</Th><Th>Sẵn sàng từ</Th></>}>
        <TableMessage cols={9} loading={q.isLoading} error={q.isError} empty={!q.isLoading && !q.isError && items.length === 0}>
          {q.isError ? apiError(q.error, 'Không tải được sổ cái.') : 'Sổ cái chưa có dòng nào phù hợp.'}
        </TableMessage>
        {items.map((l) => {
          const m = lineStateMeta(l.state);
          return (
            <tr key={l.id}>
              <Td className="font-semibold">{l.payeeName}</Td><Td>{flowLabel(l.flow)}</Td><Td>{kindLabel(l.kind)}</Td>
              <Td className="text-right">{vnd(signedForKind(l.kind, l.gross))}</Td><Td className="text-right">{pct(l.commissionRate)}</Td>
              <Td className="text-right">{vnd(signedForKind(l.kind, l.commission))}</Td><Td className="text-right font-semibold">{vnd(l.net)}</Td>
              <Td><Badge tone={m.tone}>{m.label}</Badge></Td><Td>{fmtDateTime(l.availableAt)}</Td>
            </tr>
          );
        })}
      </DataTable>
      {q.data && <Pagination page={page} pageSize={q.data.pageSize} totalCount={q.data.totalCount} onPageChange={setPage} />}
    </Panel>
  );
}

export default function LedgerTab({ onGoPayees }) {
  return (
    <div className="flex flex-col gap-6">
      <CollectionsPanel onGoPayees={onGoPayees} />
      <LedgerPanel />
    </div>
  );
}
