import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import AdminLayout from '@/components/layout/AdminLayout';
import { MaterialIcon } from '@/components/ui/MaterialIcon';
import Pagination from '@/components/ui/Pagination';
import { adminApi } from '../api';
import { formatDate } from '@/lib/utils';

const PAGE_SIZE = 20;
const ENTITY_OPTIONS = ['User', 'MarketPrice', 'SystemConfig', 'PlatformInvoice'];

const ACTION_STYLE = {
  CREATE: 'bg-d-surface-accent text-d-secondary border-d-secondary-container',
  UPDATE: 'bg-[#FFF8E1] text-[#F57F17] border-[#FFECB3]',
  DELETE: 'bg-d-error-container text-d-on-error-container border-transparent',
  ACTIVATE: 'bg-d-surface-accent text-d-secondary border-d-secondary-container',
  DEACTIVATE: 'bg-d-error-container text-d-on-error-container border-transparent',
  MARK_PAID: 'bg-d-surface-accent text-d-secondary border-d-secondary-container',
  GENERATE: 'bg-d-primary-container text-d-on-primary-container border-transparent',
};

export default function AuditLogs() {
  const [entityName, setEntityName] = useState('');
  const [page, setPage] = useState(1);

  const { data, isLoading } = useQuery({
    queryKey: ['admin', 'audit-logs', entityName, page],
    queryFn: () => adminApi.getAuditLogs({ entityName: entityName || undefined, page, pageSize: PAGE_SIZE }),
    placeholderData: (prev) => prev,
  });

  return (
    <AdminLayout crumb="Audit logs">
      <div className="p-4 md:p-6 w-full flex flex-col gap-8">
        <section>
          <h2 className="font-d-headline-xl text-d-headline-xl text-d-on-surface">Audit Logs</h2>
          <p className="font-d-body-md text-d-body-md text-d-on-surface-variant mt-2">Lịch sử hành động của quản trị viên trên hệ thống.</p>
        </section>

        <section className="bg-white border border-d-border-subtle rounded-[20px] shadow-sm overflow-hidden flex flex-col">
          <div className="p-6 border-b border-d-border-subtle flex items-center justify-between bg-d-surface-container-lowest">
            <h3 className="font-d-headline-md text-d-headline-md text-d-on-background">Lịch sử hành động hệ thống</h3>
            <select
              value={entityName}
              onChange={(e) => { setEntityName(e.target.value); setPage(1); }}
              className="h-10 px-4 bg-white border border-d-outline-variant rounded-lg font-d-body-sm text-d-body-sm outline-none focus:border-d-primary cursor-pointer"
            >
              <option value="">Tất cả đối tượng</option>
              {ENTITY_OPTIONS.map((e) => <option key={e} value={e}>{e}</option>)}
            </select>
          </div>

          <div className="overflow-x-auto w-full">
            <table className="w-full text-left border-collapse min-w-[900px]">
              <thead>
                <tr className="bg-d-surface-container-low text-d-on-surface-variant font-d-label-md text-d-label-md uppercase tracking-wider">
                  <th className="px-6 py-4 font-medium">Thời gian</th>
                  <th className="px-6 py-4 font-medium">Người thực hiện</th>
                  <th className="px-6 py-4 font-medium">Hành động</th>
                  <th className="px-6 py-4 font-medium">Đối tượng</th>
                  <th className="px-6 py-4 font-medium">Chi tiết</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-d-border-subtle font-d-body-sm text-d-body-sm text-d-on-surface">
                {isLoading && <tr><td colSpan={5} className="px-6 py-10 text-center text-d-on-surface-variant">Đang tải...</td></tr>}
                {!isLoading && data?.items.length === 0 && (
                  <tr><td colSpan={5} className="px-6 py-14 text-center text-d-on-surface-variant">
                    <MaterialIcon name="history_edu" className="text-[32px] mb-2 opacity-30 block mx-auto" />
                    Chưa có nhật ký nào.
                  </td></tr>
                )}
                {data?.items.map((log) => (
                  <tr key={log.id} className="hover:bg-d-surface-accent/20 transition-colors align-top">
                    <td className="px-6 py-4 text-d-on-surface-variant whitespace-nowrap">{formatDate(log.createdAt)}</td>
                    <td className="px-6 py-4 text-d-on-surface">{log.userName ?? 'Hệ thống'}</td>
                    <td className="px-6 py-4">
                      <span className={`px-2.5 py-1 rounded-full border font-d-label-sm text-d-label-sm ${ACTION_STYLE[log.action] ?? 'bg-d-surface-variant/50 text-d-on-surface-variant border-transparent'}`}>
                        {log.action}
                      </span>
                    </td>
                    <td className="px-6 py-4 text-d-on-surface-variant">{log.entityName}</td>
                    <td className="px-6 py-4 text-d-on-surface-variant max-w-md">
                      {log.oldData && <p className="truncate" title={log.oldData}>Cũ: {log.oldData}</p>}
                      {log.newData && <p className="truncate" title={log.newData}>Mới: {log.newData}</p>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Pagination page={page} pageSize={PAGE_SIZE} totalCount={data?.totalCount ?? 0} onPageChange={setPage} />
        </section>
      </div>
    </AdminLayout>
  );
}
