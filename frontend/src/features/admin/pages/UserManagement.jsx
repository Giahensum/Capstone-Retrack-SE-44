import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import AdminLayout from '@/components/layout/AdminLayout';
import { MaterialIcon } from '@/components/ui/MaterialIcon';
import Modal from '@/components/ui/Modal';
import Pagination from '@/components/ui/Pagination';
import { adminApi, apiErrorMessage } from '../api';
import { ROLE_LABEL, formatDate } from '@/lib/utils';

const ROLE_OPTIONS = Object.keys(ROLE_LABEL);
const PAGE_SIZE = 10;

const emptyCreateForm = { email: '', password: '', fullName: '', phone: '', role: 'SELLER' };

const inputClass = 'w-full h-11 px-4 bg-white border border-d-outline-variant rounded-lg font-d-body-md text-d-body-md text-d-on-surface placeholder:text-d-outline focus:border-d-primary focus:ring-1 focus:ring-d-primary transition-all shadow-sm outline-none';
const labelClass = 'font-d-body-sm text-d-body-sm font-medium text-d-on-surface';

export default function UserManagement() {
  const queryClient = useQueryClient();
  const [filters, setFilters] = useState({ keyword: '', role: '', isActive: '', page: 1 });
  const [createOpen, setCreateOpen] = useState(false);
  const [createForm, setCreateForm] = useState(emptyCreateForm);
  const [editUser, setEditUser] = useState(null);
  const [detailId, setDetailId] = useState(null);

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
    onSuccess: () => { toast.success('Đã xóa người dùng.'); invalidate(); },
    onError: (err) => toast.error(apiErrorMessage(err, 'Không thể xóa người dùng.')),
  });

  const handleDelete = (user) => {
    if (window.confirm(`Xóa vĩnh viễn tài khoản "${user.fullName}" (${user.email})?`)) deleteMutation.mutate(user.id);
  };

  return (
    <AdminLayout crumb="Người dùng">
      <div className="p-4 md:p-6 w-full flex flex-col gap-8">
        {/* Header */}
        <section className="flex flex-col md:flex-row md:items-end justify-between gap-4">
          <div>
            <h2 className="font-d-headline-xl text-d-headline-xl text-d-on-surface">Quản lý người dùng</h2>
            <p className="font-d-body-md text-d-body-md text-d-on-surface-variant mt-2">Tìm kiếm, tạo mới và quản lý tài khoản trên toàn hệ thống.</p>
          </div>
          <button
            onClick={() => setCreateOpen(true)}
            className="bg-d-primary-fixed text-d-on-primary-fixed font-d-label-md text-d-label-md py-3 px-6 rounded-full hover:-translate-y-0.5 transition-all duration-200 shadow-sm flex items-center gap-2 shrink-0"
          >
            <MaterialIcon name="add" className="text-[20px]" />
            Tạo người dùng
          </button>
        </section>

        {/* Table + filters */}
        <section className="bg-white border border-d-border-subtle rounded-[20px] shadow-sm overflow-hidden flex flex-col">
          <div className="p-6 border-b border-d-border-subtle flex flex-col md:flex-row gap-4 items-center justify-between bg-d-surface-accent/30">
            <div className="relative w-full md:w-[40%]">
              <MaterialIcon name="search" className="absolute left-3 top-1/2 -translate-y-1/2 text-d-on-surface-variant" />
              <input
                className="w-full bg-white border border-d-border-subtle rounded-full py-2.5 pl-10 pr-4 font-d-body-sm text-d-body-sm focus:border-d-secondary focus:outline-none transition-colors"
                placeholder="Tên, email hoặc số điện thoại..."
                value={filters.keyword}
                onChange={(e) => setFilters((f) => ({ ...f, keyword: e.target.value, page: 1 }))}
              />
            </div>
            <div className="flex gap-4 w-full md:w-auto">
              <select
                value={filters.role}
                onChange={(e) => setFilters((f) => ({ ...f, role: e.target.value, page: 1 }))}
                className="flex-1 md:w-44 bg-white border border-d-border-subtle rounded-full py-2.5 px-4 font-d-body-sm text-d-body-sm appearance-none focus:border-d-secondary focus:outline-none cursor-pointer"
              >
                <option value="">Tất cả vai trò</option>
                {ROLE_OPTIONS.map((r) => <option key={r} value={r}>{ROLE_LABEL[r]}</option>)}
              </select>
              <select
                value={filters.isActive}
                onChange={(e) => setFilters((f) => ({ ...f, isActive: e.target.value, page: 1 }))}
                className="flex-1 md:w-40 bg-white border border-d-border-subtle rounded-full py-2.5 px-4 font-d-body-sm text-d-body-sm appearance-none focus:border-d-secondary focus:outline-none cursor-pointer"
              >
                <option value="">Tất cả trạng thái</option>
                <option value="true">Đang hoạt động</option>
                <option value="false">Đã vô hiệu hóa</option>
              </select>
            </div>
          </div>

          <div className="overflow-x-auto w-full">
            <table className="w-full text-left border-collapse min-w-[900px]">
              <thead>
                <tr className="bg-d-surface-container-low text-d-on-surface-variant font-d-label-md text-d-label-md uppercase tracking-wider">
                  <th className="px-6 py-4 font-medium">Họ tên</th>
                  <th className="px-6 py-4 font-medium">Email</th>
                  <th className="px-6 py-4 font-medium">SĐT</th>
                  <th className="px-6 py-4 font-medium">Vai trò</th>
                  <th className="px-6 py-4 font-medium">Trạng thái</th>
                  <th className="px-6 py-4 font-medium">Ngày tạo</th>
                  <th className="px-6 py-4 font-medium text-center">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-d-border-subtle font-d-body-sm text-d-body-sm text-d-on-surface">
                {isLoading && (
                  <tr><td colSpan={7} className="px-6 py-10 text-center text-d-on-surface-variant">Đang tải...</td></tr>
                )}
                {!isLoading && data?.items.length === 0 && (
                  <tr><td colSpan={7} className="px-6 py-10 text-center text-d-on-surface-variant">Không tìm thấy người dùng nào.</td></tr>
                )}
                {data?.items.map((u) => (
                  <tr key={u.id} className="hover:bg-d-surface-accent/20 transition-colors group">
                    <td className="px-6 py-4">
                      <div className="flex items-center gap-3">
                        <div className="w-9 h-9 rounded-full bg-d-surface-container flex items-center justify-center text-d-primary font-bold text-xs shrink-0">
                          {u.fullName.split(' ').filter(Boolean).slice(-2).map((s) => s[0]).join('').toUpperCase()}
                        </div>
                        <span className="font-medium text-d-on-surface">{u.fullName}</span>
                      </div>
                    </td>
                    <td className="px-6 py-4 text-d-on-surface-variant">{u.email}</td>
                    <td className="px-6 py-4 text-d-on-surface-variant">{u.phone}</td>
                    <td className="px-6 py-4">
                      <span className="bg-d-surface-variant/50 text-d-on-surface-variant px-2.5 py-1 rounded-md font-d-label-sm text-d-label-sm">{ROLE_LABEL[u.role] ?? u.role}</span>
                    </td>
                    <td className="px-6 py-4">
                      <div className="flex items-center gap-2">
                        <div className={`w-2 h-2 rounded-full ${u.isActive ? 'bg-d-secondary' : 'bg-d-error'}`} />
                        <span>{u.isActive ? 'Hoạt động' : 'Vô hiệu hóa'}</span>
                      </div>
                    </td>
                    <td className="px-6 py-4 text-d-on-surface-variant">{formatDate(u.createdAt)}</td>
                    <td className="px-6 py-4">
                      <div className="flex items-center justify-center gap-1">
                        <button title="Xem chi tiết" onClick={() => setDetailId(u.id)} className="p-1.5 text-d-on-surface-variant hover:text-d-primary rounded-full hover:bg-d-surface-variant transition-colors">
                          <MaterialIcon name="visibility" className="text-[20px]" />
                        </button>
                        <button title="Sửa" onClick={() => setEditUser({ id: u.id, fullName: u.fullName, phone: u.phone })} className="p-1.5 text-d-on-surface-variant hover:text-d-primary rounded-full hover:bg-d-surface-variant transition-colors">
                          <MaterialIcon name="edit" className="text-[20px]" />
                        </button>
                        <button
                          title={u.isActive ? 'Vô hiệu hóa' : 'Kích hoạt'}
                          onClick={() => statusMutation.mutate({ id: u.id, isActive: !u.isActive })}
                          className="p-1.5 text-d-on-surface-variant hover:text-d-primary rounded-full hover:bg-d-surface-variant transition-colors"
                        >
                          <MaterialIcon name="power_settings_new" className="text-[20px]" />
                        </button>
                        <button title="Xóa" onClick={() => handleDelete(u)} className="p-1.5 text-d-on-surface-variant hover:text-d-error rounded-full hover:bg-d-error-container/40 transition-colors">
                          <MaterialIcon name="delete" className="text-[20px]" />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <Pagination page={filters.page} pageSize={PAGE_SIZE} totalCount={data?.totalCount ?? 0} onPageChange={(p) => setFilters((f) => ({ ...f, page: p }))} />
        </section>
      </div>

      {/* Create modal */}
      <Modal open={createOpen} onClose={() => setCreateOpen(false)} title="Tạo người dùng mới">
        <form className="flex flex-col gap-5" onSubmit={(e) => { e.preventDefault(); createMutation.mutate(createForm); }}>
          <div className="flex flex-col gap-1.5">
            <label className={labelClass}>Email</label>
            <input type="email" required className={inputClass} value={createForm.email} onChange={(e) => setCreateForm((f) => ({ ...f, email: e.target.value }))} />
          </div>
          <div className="flex flex-col gap-1.5">
            <label className={labelClass}>Mật khẩu</label>
            <input type="password" required minLength={6} className={inputClass} value={createForm.password} onChange={(e) => setCreateForm((f) => ({ ...f, password: e.target.value }))} />
          </div>
          <div className="flex flex-col gap-1.5">
            <label className={labelClass}>Họ tên</label>
            <input required className={inputClass} value={createForm.fullName} onChange={(e) => setCreateForm((f) => ({ ...f, fullName: e.target.value }))} />
          </div>
          <div className="flex flex-col gap-1.5">
            <label className={labelClass}>Số điện thoại</label>
            <input required className={inputClass} value={createForm.phone} onChange={(e) => setCreateForm((f) => ({ ...f, phone: e.target.value }))} />
          </div>
          <div className="flex flex-col gap-1.5">
            <label className={labelClass}>Vai trò</label>
            <div className="relative">
              <select
                value={createForm.role}
                onChange={(e) => setCreateForm((f) => ({ ...f, role: e.target.value }))}
                className={`${inputClass} appearance-none pr-10 cursor-pointer`}
              >
                {ROLE_OPTIONS.map((r) => <option key={r} value={r}>{ROLE_LABEL[r]}</option>)}
              </select>
              <MaterialIcon name="keyboard_arrow_down" className="absolute right-3 top-1/2 -translate-y-1/2 text-d-outline pointer-events-none" />
            </div>
          </div>
          <div className="flex justify-end gap-3 pt-2 border-t border-d-border-subtle mt-1">
            <button type="button" onClick={() => setCreateOpen(false)} className="h-10 px-6 rounded-full border border-d-on-surface text-d-on-surface font-d-label-md text-d-label-md hover:bg-d-surface-variant/30 transition-colors">Hủy</button>
            <button type="submit" disabled={createMutation.isPending} className="h-10 px-6 rounded-full bg-d-primary-fixed text-d-on-primary-fixed font-d-label-md text-d-label-md font-bold hover:-translate-y-0.5 transition-all flex items-center gap-2 shadow-sm disabled:opacity-60">
              {createMutation.isPending ? <MaterialIcon name="progress_activity" className="animate-spin text-[18px]" /> : <MaterialIcon name="check" className="text-[18px]" />}
              Tạo người dùng
            </button>
          </div>
        </form>
      </Modal>

      {/* Edit modal */}
      <Modal open={!!editUser} onClose={() => setEditUser(null)} title="Cập nhật người dùng">
        {editUser && (
          <form className="flex flex-col gap-5" onSubmit={(e) => { e.preventDefault(); updateMutation.mutate({ id: editUser.id, dto: { fullName: editUser.fullName, phone: editUser.phone } }); }}>
            <div className="flex flex-col gap-1.5">
              <label className={labelClass}>Họ tên</label>
              <input required className={inputClass} value={editUser.fullName} onChange={(e) => setEditUser((u) => ({ ...u, fullName: e.target.value }))} />
            </div>
            <div className="flex flex-col gap-1.5">
              <label className={labelClass}>Số điện thoại</label>
              <input required className={inputClass} value={editUser.phone} onChange={(e) => setEditUser((u) => ({ ...u, phone: e.target.value }))} />
            </div>
            <div className="flex justify-end gap-3 pt-2 border-t border-d-border-subtle mt-1">
              <button type="button" onClick={() => setEditUser(null)} className="h-10 px-6 rounded-full border border-d-on-surface text-d-on-surface font-d-label-md text-d-label-md hover:bg-d-surface-variant/30 transition-colors">Hủy</button>
              <button type="submit" disabled={updateMutation.isPending} className="h-10 px-6 rounded-full bg-d-primary-fixed text-d-on-primary-fixed font-d-label-md text-d-label-md font-bold hover:-translate-y-0.5 transition-all flex items-center gap-2 shadow-sm disabled:opacity-60">
                Lưu thay đổi
              </button>
            </div>
          </form>
        )}
      </Modal>

      {/* Detail modal */}
      <Modal open={!!detailId} onClose={() => setDetailId(null)} title="Chi tiết người dùng">
        {detailLoading && <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant">Đang tải...</p>}
        {detail && (
          <div className="flex flex-col gap-4 font-d-body-sm text-d-body-sm">
            <div className="grid grid-cols-2 gap-4">
              <div><span className="text-d-on-surface-variant">Họ tên</span><p className="text-d-on-surface font-medium">{detail.fullName}</p></div>
              <div><span className="text-d-on-surface-variant">Email</span><p className="text-d-on-surface">{detail.email}</p></div>
              <div><span className="text-d-on-surface-variant">SĐT</span><p className="text-d-on-surface">{detail.phone}</p></div>
              <div><span className="text-d-on-surface-variant">Vai trò</span><p className="text-d-on-surface">{ROLE_LABEL[detail.role] ?? detail.role}</p></div>
              <div><span className="text-d-on-surface-variant">Ngày tạo</span><p className="text-d-on-surface">{formatDate(detail.createdAt)}</p></div>
              <div><span className="text-d-on-surface-variant">Cập nhật</span><p className="text-d-on-surface">{formatDate(detail.updatedAt)}</p></div>
            </div>

            {detail.depotProfile && (
              <div className="border-t border-d-border-subtle pt-4">
                <p className="font-d-label-sm text-d-label-sm uppercase text-d-on-surface-variant mb-2">Hồ sơ kho vựa</p>
                <p className="text-d-on-surface font-medium">{detail.depotProfile.name}</p>
                <p className="text-d-on-surface-variant">{detail.depotProfile.address}</p>
                <p className="text-d-on-surface-variant">Đánh giá: {detail.depotProfile.rating} ⭐</p>
              </div>
            )}
            {detail.factoryProfile && (
              <div className="border-t border-d-border-subtle pt-4">
                <p className="font-d-label-sm text-d-label-sm uppercase text-d-on-surface-variant mb-2">Hồ sơ nhà máy</p>
                <p className="text-d-on-surface font-medium">{detail.factoryProfile.name}</p>
                <p className="text-d-on-surface-variant">{detail.factoryProfile.address}</p>
                <p className="text-d-on-surface-variant">Đánh giá: {detail.factoryProfile.rating} ⭐</p>
              </div>
            )}
            {detail.depotStaffProfile && (
              <div className="border-t border-d-border-subtle pt-4">
                <p className="font-d-label-sm text-d-label-sm uppercase text-d-on-surface-variant mb-2">Hồ sơ nhân sự kho</p>
                <p className="text-d-on-surface font-medium">{detail.depotStaffProfile.depotName}</p>
                <p className="text-d-on-surface-variant">Loại: {detail.depotStaffProfile.staffType === 'DRIVER' ? 'Tài xế' : 'Nhân viên thu gom'}</p>
              </div>
            )}
          </div>
        )}
      </Modal>
    </AdminLayout>
  );
}
