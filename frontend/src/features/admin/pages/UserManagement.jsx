import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import {
  Users, UserPlus, Eye, Pencil, Power, Trash2, Mail, Phone, Warehouse, Factory, UserCog, Check, CircleCheck, CircleSlash, CalendarClock,
} from 'lucide-react';
import AdminLayout from '@/components/layout/AdminLayout';
import { adminApi, apiErrorMessage } from '../api';
import { ROLE_LABEL, formatDate } from '@/lib/utils';
import {
  PageHeader, PrimaryButton, FilterTabs, Panel, SearchBox, DataTable, Th, Td, TableMessage, IconAction, Field, Avatar, Badge,
  Modal, ConfirmModal, FormFooter, DetailList, Pagination, inputClass,
} from '../components/AdminUI';

const ROLE_OPTIONS = Object.keys(ROLE_LABEL);
const ROLE_TONE = { ADMIN: 'red', SELLER: 'teal', DEPOT_OWNER: 'primary', DEPOT_EMPLOYEE: 'blue', DRIVER: 'amber', FACTORY: 'slate' };
const PAGE_SIZE = 10;
const STATUS_TABS = [
  { key: '', label: 'Tất cả' },
  { key: 'true', label: 'Đang hoạt động', icon: CircleCheck },
  { key: 'false', label: 'Đã vô hiệu hóa', icon: CircleSlash },
];

const emptyCreateForm = { email: '', password: '', fullName: '', phone: '', role: 'SELLER' };

export default function UserManagement() {
  const queryClient = useQueryClient();
  const [filters, setFilters] = useState({ keyword: '', role: '', isActive: '', page: 1 });
  const [createOpen, setCreateOpen] = useState(false);
  const [createForm, setCreateForm] = useState(emptyCreateForm);
  const [editUser, setEditUser] = useState(null);
  const [detailId, setDetailId] = useState(null);
  const [deleteTarget, setDeleteTarget] = useState(null);

  const queryParams = {
    keyword: filters.keyword || undefined,
    role: filters.role || undefined,
    isActive: filters.isActive === '' ? undefined : filters.isActive === 'true',
    page: filters.page,
    pageSize: PAGE_SIZE,
  };

  const { data, isLoading } = useQuery({
    queryKey: ['admin', 'users', queryParams],
    queryFn: () => adminApi.searchUsers(queryParams),
    placeholderData: (prev) => prev,
  });

  const { data: detail, isLoading: detailLoading } = useQuery({
    queryKey: ['admin', 'users', 'detail', detailId],
    queryFn: () => adminApi.getUser(detailId),
    enabled: !!detailId,
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['admin', 'users'] });

  const createMutation = useMutation({
    mutationFn: (dto) => adminApi.createUser(dto),
    onSuccess: () => { toast.success('Đã tạo người dùng.'); setCreateOpen(false); setCreateForm(emptyCreateForm); invalidate(); },
    onError: (err) => toast.error(apiErrorMessage(err, 'Tạo người dùng thất bại.')),
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, dto }) => adminApi.updateUser(id, dto),
    onSuccess: () => { toast.success('Đã cập nhật.'); setEditUser(null); invalidate(); },
    onError: (err) => toast.error(apiErrorMessage(err, 'Cập nhật thất bại.')),
  });

  const statusMutation = useMutation({
    mutationFn: ({ id, isActive }) => adminApi.setUserActive(id, isActive),
    onSuccess: () => { toast.success('Đã cập nhật trạng thái.'); invalidate(); },
    onError: (err) => toast.error(apiErrorMessage(err, 'Không thể cập nhật trạng thái.')),
  });

  const deleteMutation = useMutation({
    mutationFn: (id) => adminApi.deleteUser(id),
    onSuccess: () => { toast.success('Đã xóa người dùng.'); setDeleteTarget(null); invalidate(); },
    onError: (err) => { toast.error(apiErrorMessage(err, 'Không thể xóa người dùng.')); setDeleteTarget(null); },
  });

  const items = data?.items ?? [];
  const total = data?.totalCount ?? 0;

  return (
    <AdminLayout crumb="Người dùng">
      <div className="flex w-full flex-col gap-6 p-4 md:p-6">
        <PageHeader icon={Users} title="Quản lý người dùng" description="Tìm kiếm, tạo mới và quản lý tài khoản trên toàn hệ thống.">
          <PrimaryButton icon={UserPlus} onClick={() => setCreateOpen(true)}>Tạo người dùng</PrimaryButton>
        </PageHeader>

        <FilterTabs tabs={STATUS_TABS} value={filters.isActive} onChange={(v) => setFilters((f) => ({ ...f, isActive: v, page: 1 }))} />

        <Panel
          toolbar={(
            <>
              <SearchBox className="w-full sm:w-72" placeholder="Tên, email hoặc số điện thoại…" value={filters.keyword} onChange={(e) => setFilters((f) => ({ ...f, keyword: e.target.value, page: 1 }))} />
              <select aria-label="Lọc theo vai trò" value={filters.role} onChange={(e) => setFilters((f) => ({ ...f, role: e.target.value, page: 1 }))}
                className="h-11 cursor-pointer rounded-full border border-d-border-subtle bg-white px-4 text-sm outline-none focus:border-d-primary">
                <option value="">Tất cả vai trò</option>
                {ROLE_OPTIONS.map((r) => <option key={r} value={r}>{ROLE_LABEL[r]}</option>)}
              </select>
            </>
          )}
        >
          <DataTable minWidth={900} head={<><Th>Người dùng</Th><Th>Liên hệ</Th><Th>Vai trò</Th><Th>Trạng thái</Th><Th>Ngày tạo</Th><Th className="text-center">Thao tác</Th></>}>
            <TableMessage cols={6} loading={isLoading && !data} empty={!isLoading && items.length === 0} icon={Users}>Không tìm thấy người dùng nào.</TableMessage>
            {items.map((u) => (
              <tr key={u.id} className="transition-colors hover:bg-d-surface-accent/40">
                <Td>
                  <div className="flex items-center gap-3">
                    <Avatar name={u.fullName} />
                    <span className="font-semibold">{u.fullName}</span>
                  </div>
                </Td>
                <Td className="text-d-on-surface-variant">
                  <p className="flex items-center gap-1.5"><Mail size={13} aria-hidden="true" />{u.email}</p>
                  <p className="mt-0.5 flex items-center gap-1.5"><Phone size={13} aria-hidden="true" />{u.phone}</p>
                </Td>
                <Td><Badge tone={ROLE_TONE[u.role] ?? 'slate'}>{ROLE_LABEL[u.role] ?? u.role}</Badge></Td>
                <Td><Badge tone={u.isActive ? 'primary' : 'red'} icon={u.isActive ? CircleCheck : CircleSlash}>{u.isActive ? 'Hoạt động' : 'Vô hiệu hóa'}</Badge></Td>
                <Td className="whitespace-nowrap text-d-on-surface-variant">{formatDate(u.createdAt)}</Td>
                <Td>
                  <div className="flex items-center justify-center gap-0.5">
                    <IconAction icon={Eye} title="Xem chi tiết" onClick={() => setDetailId(u.id)} />
                    <IconAction icon={Pencil} title="Sửa" onClick={() => setEditUser({ id: u.id, fullName: u.fullName, phone: u.phone })} />
                    <IconAction icon={Power} tone={u.isActive ? 'warn' : 'default'} title={u.isActive ? 'Vô hiệu hóa' : 'Kích hoạt'} disabled={statusMutation.isPending} onClick={() => statusMutation.mutate({ id: u.id, isActive: !u.isActive })} />
                    <IconAction icon={Trash2} tone="danger" title="Xóa" onClick={() => setDeleteTarget(u)} />
                  </div>
                </Td>
              </tr>
            ))}
          </DataTable>
          <Pagination page={filters.page} pageSize={PAGE_SIZE} totalCount={total} onPageChange={(p) => setFilters((f) => ({ ...f, page: p }))} />
        </Panel>
      </div>

      {createOpen && (
        <Modal size="md" icon={UserPlus} title="Tạo người dùng mới" subtitle="Tài khoản được kích hoạt ngay sau khi tạo" onClose={() => setCreateOpen(false)} busy={createMutation.isPending}
          footer={<FormFooter formId="create-user" onCancel={() => setCreateOpen(false)} submitting={createMutation.isPending} submitLabel="Tạo người dùng" icon={Check} />}>
          <form id="create-user" className="flex flex-col gap-4" onSubmit={(e) => { e.preventDefault(); createMutation.mutate(createForm); }}>
            <Field label="Email"><input type="email" required className={inputClass} value={createForm.email} onChange={(e) => setCreateForm((f) => ({ ...f, email: e.target.value }))} /></Field>
            <Field label="Mật khẩu" hint="Tối thiểu 6 ký tự"><input type="password" required minLength={6} className={inputClass} value={createForm.password} onChange={(e) => setCreateForm((f) => ({ ...f, password: e.target.value }))} /></Field>
            <div className="grid gap-4 sm:grid-cols-2">
              <Field label="Họ tên"><input required className={inputClass} value={createForm.fullName} onChange={(e) => setCreateForm((f) => ({ ...f, fullName: e.target.value }))} /></Field>
              <Field label="Số điện thoại"><input required className={inputClass} value={createForm.phone} onChange={(e) => setCreateForm((f) => ({ ...f, phone: e.target.value }))} /></Field>
            </div>
            <Field label="Vai trò">
              <select value={createForm.role} onChange={(e) => setCreateForm((f) => ({ ...f, role: e.target.value }))} className={`${inputClass} cursor-pointer`}>
                {ROLE_OPTIONS.map((r) => <option key={r} value={r}>{ROLE_LABEL[r]}</option>)}
              </select>
            </Field>
          </form>
        </Modal>
      )}

      {editUser && (
        <Modal size="md" icon={Pencil} tone="blue" title="Cập nhật người dùng" onClose={() => setEditUser(null)} busy={updateMutation.isPending}
          footer={<FormFooter formId="edit-user" onCancel={() => setEditUser(null)} submitting={updateMutation.isPending} submitLabel="Lưu thay đổi" icon={Check} />}>
          <form id="edit-user" className="flex flex-col gap-4" onSubmit={(e) => { e.preventDefault(); updateMutation.mutate({ id: editUser.id, dto: { fullName: editUser.fullName, phone: editUser.phone } }); }}>
            <Field label="Họ tên"><input required className={inputClass} value={editUser.fullName} onChange={(e) => setEditUser((u) => ({ ...u, fullName: e.target.value }))} /></Field>
            <Field label="Số điện thoại"><input required className={inputClass} value={editUser.phone} onChange={(e) => setEditUser((u) => ({ ...u, phone: e.target.value }))} /></Field>
          </form>
        </Modal>
      )}

      {detailId && (
        <Modal size="md" icon={UserCog} tone="teal" title={detail?.fullName ?? 'Chi tiết người dùng'} subtitle={detail?.email} onClose={() => setDetailId(null)}
          badge={detail && <Badge tone={ROLE_TONE[detail.role] ?? 'slate'}>{ROLE_LABEL[detail.role] ?? detail.role}</Badge>}>
          {detailLoading && <p className="text-sm text-d-on-surface-variant">Đang tải…</p>}
          {detail && (
            <>
              <DetailList rows={[
                { label: 'Họ tên', value: detail.fullName }, { label: 'Email', value: detail.email }, { label: 'Số điện thoại', value: detail.phone },
                { label: 'Vai trò', value: ROLE_LABEL[detail.role] ?? detail.role }, { label: 'Ngày tạo', value: formatDate(detail.createdAt) }, { label: 'Cập nhật', value: formatDate(detail.updatedAt) },
              ]} />
              {detail.depotProfile && <ProfileCard icon={Warehouse} title="Hồ sơ kho vựa" name={detail.depotProfile.name} address={detail.depotProfile.address} rating={detail.depotProfile.rating} />}
              {detail.factoryProfile && <ProfileCard icon={Factory} title="Hồ sơ nhà máy" name={detail.factoryProfile.name} address={detail.factoryProfile.address} rating={detail.factoryProfile.rating} />}
              {detail.depotStaffProfile && (
                <div className="rounded-2xl border border-d-border-subtle bg-d-surface-container-low p-4">
                  <p className="mb-1 flex items-center gap-2 text-xs font-bold uppercase text-d-on-surface-variant"><CalendarClock size={14} />Hồ sơ nhân sự kho</p>
                  <p className="font-semibold">{detail.depotStaffProfile.depotName}</p>
                  <p className="text-sm text-d-on-surface-variant">Loại: {detail.depotStaffProfile.staffType === 'DRIVER' ? 'Tài xế' : 'Nhân viên thu gom'}</p>
                </div>
              )}
            </>
          )}
        </Modal>
      )}

      {deleteTarget && (
        <ConfirmModal icon={Trash2} tone="red" title="Xóa vĩnh viễn tài khoản?" confirmLabel="Xóa tài khoản" cancelLabel="Giữ lại"
          description={`Tài khoản "${deleteTarget.fullName}" (${deleteTarget.email}) sẽ bị xóa và không thể khôi phục.`}
          busy={deleteMutation.isPending} onCancel={() => setDeleteTarget(null)} onConfirm={() => deleteMutation.mutate(deleteTarget.id)} />
      )}
    </AdminLayout>
  );
}

function ProfileCard({ icon: Icon, title, name, address, rating }) {
  return (
    <div className="rounded-2xl border border-d-border-subtle bg-d-surface-container-low p-4">
      <p className="mb-1 flex items-center gap-2 text-xs font-bold uppercase text-d-on-surface-variant"><Icon size={14} />{title}</p>
      <p className="font-semibold">{name}</p>
      <p className="text-sm text-d-on-surface-variant">{address}</p>
      <p className="text-sm text-d-on-surface-variant">Đánh giá: {rating} ⭐</p>
    </div>
  );
}
