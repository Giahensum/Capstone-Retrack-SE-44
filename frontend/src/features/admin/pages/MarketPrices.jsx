import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { Tags, Plus, Pencil, Trash2, Check, Layers, TrendingUp, CalendarClock, Coins } from 'lucide-react';
import AdminLayout from '@/components/layout/AdminLayout';
import { adminApi, apiErrorMessage } from '../api';
import { formatCurrency, formatDate, MATERIAL_TYPE_LABEL } from '@/lib/utils';
import {
  PageHeader, PrimaryButton, StatCard, Panel, DataTable, Th, Td, TableMessage, IconAction, Field, Modal, ConfirmModal, FormFooter, Badge, inputClass,
} from '../components/AdminUI';

const MATERIAL_OPTIONS = Object.keys(MATERIAL_TYPE_LABEL);
const materialLabel = (value) => MATERIAL_TYPE_LABEL[value] ?? value;
const emptyForm = { materialType: '', pricePerKg: '', effectiveDate: new Date().toISOString().slice(0, 10), source: '' };

export default function MarketPrices() {
  const queryClient = useQueryClient();
  const [modalOpen, setModalOpen] = useState(false);
  const [editingId, setEditingId] = useState(null);
  const [form, setForm] = useState(emptyForm);
  const [deleteTarget, setDeleteTarget] = useState(null);

  const { data, isLoading } = useQuery({ queryKey: ['admin', 'market-prices'], queryFn: () => adminApi.getMarketPrices() });
  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['admin', 'market-prices'] });
  const closeModal = () => setModalOpen(false);

  const upsertMutation = useMutation({
    mutationFn: (dto) => (editingId ? adminApi.updateMarketPrice(editingId, dto) : adminApi.createMarketPrice(dto)),
    onSuccess: () => { toast.success(editingId ? 'Đã cập nhật giá.' : 'Đã thêm giá tham khảo.'); closeModal(); invalidate(); },
    onError: (err) => toast.error(apiErrorMessage(err, 'Không thể lưu giá tham khảo.')),
  });

  const deleteMutation = useMutation({
    mutationFn: (id) => adminApi.deleteMarketPrice(id),
    onSuccess: () => { toast.success('Đã xóa.'); setDeleteTarget(null); invalidate(); },
    onError: (err) => { toast.error(apiErrorMessage(err, 'Không thể xóa.')); setDeleteTarget(null); },
  });

  const openCreate = () => { setEditingId(null); setForm(emptyForm); setModalOpen(true); };
  const openEdit = (p) => { setEditingId(p.id); setForm({ materialType: p.materialType, pricePerKg: p.pricePerKg, effectiveDate: p.effectiveDate.slice(0, 10), source: p.source ?? '' }); setModalOpen(true); };

  const handleSubmit = (e) => {
    e.preventDefault();
    upsertMutation.mutate({ materialType: form.materialType, pricePerKg: Number(form.pricePerKg), effectiveDate: new Date(form.effectiveDate).toISOString(), source: form.source || null });
  };

  const rows = data ?? [];
  const top = rows.reduce((best, p) => (!best || p.pricePerKg > best.pricePerKg ? p : best), null);
  const latest = rows.reduce((best, p) => (!best || new Date(p.effectiveDate) > new Date(best.effectiveDate) ? p : best), null);

  return (
    <AdminLayout crumb="Bảng giá thị trường">
      <div className="flex w-full flex-col gap-6 p-4 md:p-6">
        <PageHeader icon={Tags} title="Bảng giá thị trường" description="Giá tham khảo phế liệu theo loại vật liệu, dùng làm cơ sở định giá.">
          <PrimaryButton icon={Plus} onClick={openCreate}>Thêm giá tham khảo</PrimaryButton>
        </PageHeader>

        <section className="grid grid-cols-1 gap-4 sm:grid-cols-3" aria-label="Tóm tắt">
          <StatCard icon={Layers} tone="primary" label="Số mục giá" value={rows.length} loading={isLoading} />
          <StatCard icon={TrendingUp} tone="amber" label="Giá cao nhất" value={top ? formatCurrency(top.pricePerKg) : '—'} hint={top ? `${materialLabel(top.materialType)} / kg` : undefined} loading={isLoading} />
          <StatCard icon={CalendarClock} tone="teal" label="Hiệu lực gần nhất" value={latest ? formatDate(latest.effectiveDate) : '—'} hint={latest ? materialLabel(latest.materialType) : undefined} loading={isLoading} />
        </section>

        <Panel icon={Coins} title="Danh sách giá tham khảo">
          <DataTable minWidth={700} head={<><Th>Loại vật liệu</Th><Th>Giá / kg</Th><Th>Ngày hiệu lực</Th><Th>Nguồn</Th><Th className="text-center">Thao tác</Th></>}>
            <TableMessage cols={5} loading={isLoading} empty={!isLoading && rows.length === 0} icon={Tags}>Chưa có giá tham khảo nào.</TableMessage>
            {rows.map((p) => (
              <tr key={p.id} className="transition-colors hover:bg-d-surface-accent/40">
                <Td><Badge tone="primary">{materialLabel(p.materialType)}</Badge></Td>
                <Td className="font-black text-d-secondary">{formatCurrency(p.pricePerKg)}</Td>
                <Td className="whitespace-nowrap text-d-on-surface-variant">{formatDate(p.effectiveDate)}</Td>
                <Td className="text-d-on-surface-variant">{p.source || '—'}</Td>
                <Td>
                  <div className="flex items-center justify-center gap-0.5">
                    <IconAction icon={Pencil} title="Sửa" onClick={() => openEdit(p)} />
                    <IconAction icon={Trash2} tone="danger" title="Xóa" onClick={() => setDeleteTarget(p)} />
                  </div>
                </Td>
              </tr>
            ))}
          </DataTable>
        </Panel>
      </div>

      {modalOpen && (
        <Modal size="md" icon={Tags} title={editingId ? 'Cập nhật giá tham khảo' : 'Thêm giá tham khảo'} onClose={closeModal} busy={upsertMutation.isPending}
          footer={<FormFooter formId="price-form" onCancel={closeModal} submitting={upsertMutation.isPending} submitLabel={editingId ? 'Lưu thay đổi' : 'Thêm giá'} icon={Check} />}>
          <form id="price-form" className="flex flex-col gap-4" onSubmit={handleSubmit}>
            <Field label="Loại vật liệu">
              <select required value={form.materialType} onChange={(e) => setForm((f) => ({ ...f, materialType: e.target.value }))} className={`${inputClass} cursor-pointer`}>
                <option value="" disabled>-- Chọn loại vật liệu --</option>
                {MATERIAL_OPTIONS.map((m) => <option key={m} value={m}>{MATERIAL_TYPE_LABEL[m]}</option>)}
              </select>
            </Field>
            <div className="grid gap-4 sm:grid-cols-2">
              <Field label="Giá / kg (VNĐ)"><input type="number" min="0" step="100" required value={form.pricePerKg} onChange={(e) => setForm((f) => ({ ...f, pricePerKg: e.target.value }))} className={inputClass} /></Field>
              <Field label="Ngày hiệu lực"><input type="date" required value={form.effectiveDate} onChange={(e) => setForm((f) => ({ ...f, effectiveDate: e.target.value }))} className={inputClass} /></Field>
            </div>
            <Field label="Nguồn (tùy chọn)"><input placeholder="VD: Thị trường HCM" value={form.source} onChange={(e) => setForm((f) => ({ ...f, source: e.target.value }))} className={inputClass} /></Field>
          </form>
        </Modal>
      )}

      {deleteTarget && (
        <ConfirmModal icon={Trash2} tone="red" title="Xóa giá tham khảo?" confirmLabel="Xóa" cancelLabel="Giữ lại"
          description={`Giá "${materialLabel(deleteTarget.materialType)}" (${formatCurrency(deleteTarget.pricePerKg)}/kg) sẽ bị xóa.`}
          busy={deleteMutation.isPending} onCancel={() => setDeleteTarget(null)} onConfirm={() => deleteMutation.mutate(deleteTarget.id)} />
      )}
    </AdminLayout>
  );
}
