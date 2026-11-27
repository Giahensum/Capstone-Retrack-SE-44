import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { Flag, Info, CheckCircle2, XCircle } from 'lucide-react';
import AdminLayout from '@/components/layout/AdminLayout';
import { adminApi, apiErrorMessage } from '../api';
import { PageHeader, Notice, Panel, FilterTabs, DataTable, Th, Td, TableMessage, Badge, Modal, Pagination, Field, inputClass, FormFooter } from '../components/AdminUI';

const STAGE = { WAITING_BUYER_PAYMENT: 'Chờ kho thanh toán', WAITING_SELLER_CONFIRM: 'Chờ Seller xác nhận tiền', WAITING_HANDOVER: 'Chờ bàn giao',
  WAITING_OWNER_RECEIVE: 'Chờ kho nhận hàng', COMPLETED: 'Hoàn tất', DISPUTED: 'Chờ Admin xử lý', CANCELLED: 'Đã hủy' };
const money = (n) => Number(n ?? 0).toLocaleString('vi-VN') + ' đ';
const local = (v) => (v ? new Date(v).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' }) : '—');

/** Admin xử lý báo bỏ mua của phiên nhiều hạng mục: chỉ Tiếp tục hoặc Hủy, có ghi chú; không phạt, không chuyển người thắng. */
export default function LiquidationDisputes() {
  const [openOnly, setOpenOnly] = useState(true);
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState(null);
  const query = useQuery({ queryKey: ['admin', 'liquidation-disputes', openOnly, page], queryFn: () => adminApi.getLiquidationDisputes({ openOnly, page, pageSize: 20 }) });
  const rows = query.data?.items ?? [];
  return (
    <AdminLayout crumb="Báo bỏ mua thanh lý">
      <div className="flex w-full flex-col gap-6 p-4 md:p-6">
        <PageHeader icon={Flag} title="Báo bỏ mua phiên thanh lý" description="Seller hoặc chủ kho báo bên kia không thực hiện thỏa thuận; Admin xem lý do và quyết định." />
        <Notice tone="blue" icon={Info}>Hệ thống không tự phạt tiền, tịch thu cọc hay chuyển hạng mục cho người trả giá thứ hai. Hủy chỉ áp dụng khi hàng chưa bàn giao; tiền đã giao (nếu có) do hai bên tự hoàn trả.</Notice>
        <Panel icon={Flag} title="Danh sách báo cáo" toolbar={<FilterTabs value={openOnly ? 'open' : 'all'} onChange={(k) => { setOpenOnly(k === 'open'); setPage(1); }} tabs={[{ key: 'open', label: 'Chờ xử lý' }, { key: 'all', label: 'Tất cả' }]} />}>
          <DataTable minWidth={960} head={<><Th>Phiên / kho</Th><Th>Người báo</Th><Th>Lý do</Th><Th className="text-right">Giá trị</Th><Th>Trạng thái</Th><Th>Thao tác</Th></>}>
            <TableMessage cols={6} loading={query.isLoading} error={query.isError} empty={!query.isLoading && !query.isError && rows.length === 0}>
              {query.isError ? apiErrorMessage(query.error) : 'Không có báo cáo nào.'}
            </TableMessage>
            {rows.map((r) => <tr key={r.agreementId} className="align-top">
              <Td><p className="font-semibold">{r.title}</p><p className="text-xs text-d-on-surface-variant">Seller {r.sellerName} · Kho {r.depotName}</p></Td>
              <Td><p>{r.abandonReporterRole === 'SELLER' ? 'Seller' : 'Chủ kho'}</p><p className="text-xs text-d-on-surface-variant">{local(r.abandonReportedAt)}</p></Td>
              <Td className="max-w-xs whitespace-pre-wrap break-words">{r.abandonReason}{r.resolutionNote && <p className="mt-1 text-xs text-d-on-surface-variant">Xử lý: {r.resolutionNote}</p>}</Td>
              <Td className="text-right font-semibold">{money(r.totalAmount)}</Td>
              <Td><Badge tone={r.resolution === 'CANCELLED' ? 'red' : r.resolution === 'CONTINUE' ? 'primary' : 'amber'}>{r.resolution ? (r.resolution === 'CANCELLED' ? 'Đã hủy' : 'Tiếp tục') : STAGE[r.stage] ?? r.stage}</Badge></Td>
              <Td>{!r.resolution && <button type="button" className="rounded-full bg-d-primary px-4 py-2 text-sm font-semibold text-white" onClick={() => setSelected(r)}>Xử lý</button>}</Td>
            </tr>)}
          </DataTable>
          <Pagination page={page} pageSize={20} totalCount={query.data?.totalCount ?? 0} onPageChange={setPage} />
        </Panel>
      </div>
      {selected && <ResolveModal row={selected} onClose={() => setSelected(null)} />}
    </AdminLayout>
  );
}

function ResolveModal({ row, onClose }) {
  const client = useQueryClient();
  const [resolution, setResolution] = useState('CONTINUE');
  const [note, setNote] = useState('');
  const save = useMutation({ mutationFn: () => adminApi.resolveLiquidationDispute(row.agreementId, { resolution, note: note.trim() }),
    onSuccess: () => { toast.success('Đã lưu quyết định.'); client.invalidateQueries({ queryKey: ['admin', 'liquidation-disputes'] }); onClose(); },
    onError: (e) => toast.error(apiErrorMessage(e)) });
  return <Modal icon={Flag} tone="amber" size="md" title="Xử lý báo bỏ mua" subtitle={`${row.title} · ${row.depotName}`} onClose={onClose} busy={save.isPending}
    footer={<FormFooter formId="resolve-dispute" onCancel={onClose} submitting={save.isPending} submitLabel="Lưu quyết định" icon={CheckCircle2} />}>
    <form id="resolve-dispute" className="space-y-4" onSubmit={(e) => { e.preventDefault(); if (note.trim().length >= 5) save.mutate(); else toast.error('Cần ghi chú từ 5 ký tự.'); }}>
      <p className="rounded-xl bg-d-surface-container-low p-3 text-sm"><strong>Lý do báo:</strong> {row.abandonReason}</p>
      <fieldset className="grid gap-2 sm:grid-cols-2">
        <legend className="mb-1 text-xs font-semibold text-d-on-surface-variant">Quyết định</legend>
        {[['CONTINUE', 'Tiếp tục thực hiện', CheckCircle2], ['CANCELLED', 'Hủy thỏa thuận', XCircle]].map(([key, text, Icon]) =>
          <label key={key} className={`flex cursor-pointer items-center gap-2 rounded-xl border p-3 text-sm font-semibold ${resolution === key ? 'border-d-primary bg-d-surface-accent' : 'border-d-border-subtle'}`}>
            <input type="radio" name="resolution" className="accent-[#446900]" checked={resolution === key} onChange={() => setResolution(key)} /><Icon size={16} />{text}</label>)}
      </fieldset>
      <Field label="Ghi chú xử lý (5–1000 ký tự, gửi cho cả hai bên)">
        <textarea rows={3} maxLength={1000} className={`${inputClass} h-auto py-2.5`} value={note} onChange={(e) => setNote(e.target.value)} required />
      </Field>
    </form>
  </Modal>;
}
