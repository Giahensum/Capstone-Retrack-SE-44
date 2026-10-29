import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { ScrollText, Clock, User, Filter } from 'lucide-react';
import AdminLayout from '@/components/layout/AdminLayout';
import { adminApi } from '../api';
import { formatDate } from '@/lib/utils';
import { PageHeader, FilterTabs, Panel, DataTable, Th, Td, TableMessage, Badge, Pagination } from '../components/AdminUI';

const PAGE_SIZE = 20;
const ENTITY_TABS = [{ key: '', label: 'Tất cả đối tượng' }, ...['User', 'MarketPrice', 'SystemConfig', 'PlatformInvoice'].map((e) => ({ key: e, label: e }))];

const ACTION_TONE = {
  CREATE: 'primary', UPDATE: 'amber', DELETE: 'red', ACTIVATE: 'teal', DEACTIVATE: 'red', MARK_PAID: 'teal', GENERATE: 'lime',
};

export default function AuditLogs() {
  const [entityName, setEntityName] = useState('');
  const [page, setPage] = useState(1);

  const { data, isLoading } = useQuery({
    queryKey: ['admin', 'audit-logs', entityName, page],
    queryFn: () => adminApi.getAuditLogs({ entityName: entityName || undefined, page, pageSize: PAGE_SIZE }),
    placeholderData: (prev) => prev,
  });
  const items = data?.items ?? [];

  return (
    <AdminLayout crumb="Audit logs">
      <div className="flex w-full flex-col gap-6 p-4 md:p-6">
        <PageHeader icon={ScrollText} title="Audit Logs" description="Lịch sử hành động của quản trị viên trên hệ thống." />

        <FilterTabs tabs={ENTITY_TABS} value={entityName} onChange={(v) => { setEntityName(v); setPage(1); }} />

        <Panel icon={Filter} title="Lịch sử hành động hệ thống">
          <DataTable minWidth={900} head={<><Th>Thời gian</Th><Th>Người thực hiện</Th><Th>Hành động</Th><Th>Đối tượng</Th><Th>Chi tiết</Th></>}>
            <TableMessage cols={5} loading={isLoading && !data} empty={!isLoading && items.length === 0} icon={ScrollText}>Chưa có nhật ký nào.</TableMessage>
            {items.map((log) => (
              <tr key={log.id} className="align-top transition-colors hover:bg-d-surface-accent/40">
                <Td className="whitespace-nowrap text-d-on-surface-variant"><span className="inline-flex items-center gap-1.5"><Clock size={13} aria-hidden="true" />{formatDate(log.createdAt)}</span></Td>
                <Td><span className="inline-flex items-center gap-1.5 font-semibold"><User size={13} className="text-d-on-surface-variant" aria-hidden="true" />{log.userName ?? 'Hệ thống'}</span></Td>
                <Td><Badge tone={ACTION_TONE[log.action] ?? 'slate'}>{log.action}</Badge></Td>
                <Td className="text-d-on-surface-variant">{log.entityName}</Td>
                <Td className="max-w-md text-d-on-surface-variant">
                  {log.oldData && <p className="truncate" title={log.oldData}><span className="font-semibold text-red-600">Cũ:</span> {log.oldData}</p>}
                  {log.newData && <p className="truncate" title={log.newData}><span className="font-semibold text-d-primary">Mới:</span> {log.newData}</p>}
                </Td>
              </tr>
            ))}
          </DataTable>
          <Pagination page={page} pageSize={PAGE_SIZE} totalCount={data?.totalCount ?? 0} onPageChange={setPage} />
        </Panel>
      </div>
    </AdminLayout>
  );
}
