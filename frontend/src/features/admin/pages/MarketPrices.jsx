import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import AdminLayout from '@/components/layout/AdminLayout';
import { MaterialIcon } from '@/components/ui/MaterialIcon';
import Modal from '@/components/ui/Modal';
import { adminApi, apiErrorMessage } from '../api';
import { formatCurrency, formatDate } from '@/lib/utils';

const COMMON_MATERIALS = ['Sắt vụn', 'Đồng cáp', 'Nhôm', 'Giấy Carton', 'Nhựa PET', 'Nhựa HDPE', 'Nhựa cứng', 'Rác điện tử'];
const emptyForm = { materialType: '', pricePerKg: '', effectiveDate: new Date().toISOString().slice(0, 10), source: '' };
const inputClass = 'w-full h-11 px-4 bg-white border border-d-outline-variant rounded-lg font-d-body-md text-d-body-md text-d-on-surface placeholder:text-d-outline focus:border-d-primary focus:ring-1 focus:ring-d-primary transition-all shadow-sm outline-none';
const labelClass = 'font-d-body-sm text-d-body-sm font-medium text-d-on-surface';

export default function MarketPrices() {
  const queryClient = useQueryClient();
  const [modalOpen, setModalOpen] = useState(false);
  const [editingId, setEditingId] = useState(null);
  const [form, setForm] = useState(emptyForm);

  const { data, isLoading } = useQuery({ queryKey: ['admin', 'market-prices'], queryFn: () => adminApi.getMarketPrices() });
  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['admin', 'market-prices'] });

  const upsertMutation = useMutation({
    mutationFn: (dto) => (editingId ? adminApi.updateMarketPrice(editingId, dto) : adminApi.createMarketPrice(dto)),
    onSuccess: () => { toast.success(editingId ? 'Đã cập nhật giá.' : 'Đã thêm giá tham khảo.'); closeModal(); invalidate(); },
    onError: (err) => toast.error(apiErrorMessage(err, 'Không thể lưu giá tham khảo.')),
  });

  const deleteMutation = useMutation({
    mutationFn: (id) => adminApi.deleteMarketPrice(id),
    onSuccess: () => { toast.success('Đã xóa.'); invalidate(); },
    onError: (err) => toast.error(apiErrorMessage(err, 'Không thể xóa.')),
  });

  const openCreate = () => { setEditingId(null); setForm(emptyForm); setModalOpen(true); };
  const openEdit = (p) => { setEditingId(p.id); setForm({ materialType: p.materialType, pricePerKg: p.pricePerKg, effectiveDate: p.effectiveDate.slice(0, 10), source: p.source ?? '' }); setModalOpen(true); };
  const closeModal = () => setModalOpen(false);

  const handleSubmit = (e) => {
    e.preventDefault();
    upsertMutation.mutate({ materialType: form.materialType, pricePerKg: Number(form.pricePerKg), effectiveDate: new Date(form.effectiveDate).toISOString(), source: form.source || null });
  };

  return (
    <AdminLayout crumb="Bảng giá thị trường">
      <div className="p-4 md:p-6 w-full flex flex-col gap-8">
        <section className="flex flex-col md:flex-row md:items-end justify-between gap-4">
          <div>
            <h2 className="font-d-headline-xl text-d-headline-xl text-d-on-surface">Bảng giá thị trường</h2>
            <p className="font-d-body-md text-d-body-md text-d-on-surface-variant mt-2">Giá tham khảo phế liệu theo loại vật liệu, dùng làm cơ sở định giá.</p>
          </div>
          <button onClick={openCreate} className="bg-d-primary-fixed text-d-on-primary-fixed font-d-label-md text-d-label-md py-3 px-6 rounded-full hover:-translate-y-0.5 transition-all duration-200 shadow-sm flex items-center gap-2 shrink-0">
            <MaterialIcon name="add" className="text-[20px]" />
            Thêm giá tham khảo
          </button>
        </section>

        <section className="bg-white border border-d-border-subtle rounded-[20px] shadow-sm overflow-hidden flex flex-col">
          <div className="overflow-x-auto w-full">
            <table className="w-full text-left border-collapse min-w-[700px]">
              <thead>
                <tr className="bg-d-surface-container-low text-d-on-surface-variant font-d-label-md text-d-label-md uppercase tracking-wider">
                  <th className="px-6 py-4 font-medium">Loại vật liệu</th>
                  <th className="px-6 py-4 font-medium">Giá / kg</th>
                  <th className="px-6 py-4 font-medium">Ngày hiệu lực</th>
                  <th className="px-6 py-4 font-medium">Nguồn</th>
                  <th className="px-6 py-4 font-medium text-center">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-d-border-subtle font-d-body-sm text-d-body-sm text-d-on-surface">
                {isLoading && <tr><td colSpan={5} className="px-6 py-10 text-center text-d-on-surface-variant">Đang tải...</td></tr>}
                {!isLoading && data?.length === 0 && (
                  <tr><td colSpan={5} className="px-6 py-14 text-center text-d-on-surface-variant">
                    <MaterialIcon name="sell" className="text-[32px] mb-2 opacity-30 block mx-auto" />
                    Chưa có giá tham khảo nào.
                  </td></tr>
                )}
                {data?.map((p) => (
                  <tr key={p.id} className="hover:bg-d-surface-accent/20 transition-colors">
                    <td className="px-6 py-4 font-medium text-d-on-surface">{p.materialType}</td>
                    <td className="px-6 py-4 font-semibold text-d-secondary">{formatCurrency(p.pricePerKg)}</td>
                    <td className="px-6 py-4 text-d-on-surface-variant">{formatDate(p.effectiveDate)}</td>
                    <td className="px-6 py-4 text-d-on-surface-variant">{p.source || '—'}</td>
                    <td className="px-6 py-4">
                      <div className="flex items-center justify-center gap-1">
                        <button title="Sửa" onClick={() => openEdit(p)} className="p-1.5 text-d-on-surface-variant hover:text-d-primary rounded-full hover:bg-d-surface-variant transition-colors">
                          <MaterialIcon name="edit" className="text-[20px]" />
                        </button>
                        <button title="Xóa" onClick={() => window.confirm(`Xóa giá "${p.materialType}"?`) && deleteMutation.mutate(p.id)} className="p-1.5 text-d-on-surface-variant hover:text-d-error rounded-full hover:bg-d-error-container/40 transition-colors">
                          <MaterialIcon name="delete" className="text-[20px]" />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      </div>

      <Modal open={modalOpen} onClose={closeModal} title={editingId ? 'Cập nhật giá tham khảo' : 'Thêm giá tham khảo'}>
        <form className="flex flex-col gap-5" onSubmit={handleSubmit}>
          <div className="flex flex-col gap-1.5">
            <label className={labelClass}>Loại vật liệu</label>
            <input list="material-types" required value={form.materialType} onChange={(e) => setForm((f) => ({ ...f, materialType: e.target.value }))} className={inputClass} />
            <datalist id="material-types">{COMMON_MATERIALS.map((m) => <option key={m} value={m} />)}</datalist>
          </div>
          <div className="flex flex-col gap-1.5">
            <label className={labelClass}>Giá / kg (VNĐ)</label>
            <input type="number" min="0" step="100" required value={form.pricePerKg} onChange={(e) => setForm((f) => ({ ...f, pricePerKg: e.target.value }))} className={inputClass} />
          </div>
          <div className="flex flex-col gap-1.5">
            <label className={labelClass}>Ngày hiệu lực</label>
            <input type="date" required value={form.effectiveDate} onChange={(e) => setForm((f) => ({ ...f, effectiveDate: e.target.value }))} className={inputClass} />
          </div>
          <div className="flex flex-col gap-1.5">
            <label className={labelClass}>Nguồn (tùy chọn)</label>
            <input placeholder="VD: Thị trường HCM" value={form.source} onChange={(e) => setForm((f) => ({ ...f, source: e.target.value }))} className={inputClass} />
          </div>
          <div className="flex justify-end gap-3 pt-2 border-t border-d-border-subtle mt-1">
            <button type="button" onClick={closeModal} className="h-10 px-6 rounded-full border border-d-on-surface text-d-on-surface font-d-label-md text-d-label-md hover:bg-d-surface-variant/30 transition-colors">Hủy</button>
            <button type="submit" disabled={upsertMutation.isPending} className="h-10 px-6 rounded-full bg-d-primary-fixed text-d-on-primary-fixed font-d-label-md text-d-label-md font-bold hover:-translate-y-0.5 transition-all shadow-sm disabled:opacity-60">
              {editingId ? 'Lưu thay đổi' : 'Thêm giá'}
            </button>
          </div>
        </form>
      </Modal>
    </AdminLayout>
  );
}
