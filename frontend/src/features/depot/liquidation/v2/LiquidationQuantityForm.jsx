import { handoverAmount } from '@/components/liquidation/handoverQuantity';
import { useState } from 'react';
import { Scale } from 'lucide-react';
import { Form, Input, Textarea } from '@/components/forms/ValidatedForm';
import { Modal, Notice } from '@/components/liquidation/LiquidationUI';
import { MutationError, inputClass, buttonClass as primaryButton, secondaryButtonClass as secondaryButton } from '@/features/depot/components/DepotUI';
import { money } from '@/features/depot/depotApi';
import { newOperationId, quantity, unitLabel, useV2Mutation } from './liquidationV2Api';

export default function LiquidationQuantityForm({ agreement: a, onClose }) {
  const [operationId] = useState(newOperationId);
  const [expectedRevision] = useState(a.handoverRevision);
  const [lines, setLines] = useState(() => a.items.map(x => ({ agreementItemId: x.id, quantity: a.handover?.lines.find(y => y.agreementItemId === x.id)?.quantity ?? x.quantity })));
  const [note, setNote] = useState(a.handover?.note ?? '');
  const [confirmed, setConfirmed] = useState(false);
  const save = useV2Mutation('post', '/agreements/' + a.id + '/handover-quantity', onClose);
  const total = a.items.reduce((sum, x, i) => sum + handoverAmount(lines[i].quantity, x.unitPrice), 0);
  const valid = a.items.every((x, i) => Number(lines[i].quantity) > 0 && (x.unit === 'kg' || Number.isInteger(Number(lines[i].quantity))));
  return <Modal title="Chốt cân / kiểm đếm bàn giao" icon={Scale} onClose={onClose} busy={save.isPending}
    footer={<div className="flex flex-wrap justify-end gap-2"><button type="button" className={secondaryButton} onClick={onClose} disabled={save.isPending}>Quay lại</button><button form="handover-quantity" className={primaryButton} disabled={save.isPending || !confirmed}>{save.isPending ? 'Đang gửi…' : 'Gửi Seller xác nhận'}</button></div>}>
    <Form id="handover-quantity" className="space-y-4" onSubmit={e => { e.preventDefault(); if (confirmed && valid) save.mutate({ operationId, expectedRevision, lines: lines.map(x => ({ ...x, quantity: Number(x.quantity) })), note: note.trim() || null }); }}>
      <Notice tone="blue">Nhập số cân hoặc số cái/bộ thực tế tại nơi bàn giao. Đơn giá trúng giữ nguyên; chỉ thanh toán sau khi Seller xác nhận.</Notice>
      {a.items.map((x, i) => <div key={x.id} className="rounded-2xl border border-d-border-subtle bg-white p-4">
        <h3 className="font-bold">{x.name}</h3><p className="mb-3 text-sm text-d-on-surface-variant">Seller đăng {quantity(x.quantity, x.unit)} · Đơn giá {money(x.unitPrice)}/{unitLabel(x.unit)}</p>
        <label className="block text-sm font-semibold">Lượng bàn giao ({unitLabel(x.unit)})<Input required disabled={save.isPending} type="number" min={x.unit === 'kg' ? '0.001' : '1'} max={x.unit === 'kg' ? '1000000' : '100000'} step={x.unit === 'kg' ? '0.001' : '1'} value={lines[i].quantity} className={inputClass} onChange={e => setLines(lines.map((v, j) => j === i ? { ...v, quantity: e.target.value } : v))} /></label>
        <p className="mt-2 text-right text-sm font-bold">{money(handoverAmount(lines[i].quantity, x.unitPrice))}</p>
      </div>)}
      <p className="text-right text-lg font-bold">Tổng đề xuất: {money(total)}</p>
      <label className="block text-sm">Ghi chú (không bắt buộc)<Textarea rows={2} maxLength={1000} disabled={save.isPending} className={inputClass} value={note} onChange={e => setNote(e.target.value)} /></label>
      <label className="flex gap-3 text-sm"><Input type="checkbox" required checked={confirmed} disabled={save.isPending} onChange={e => setConfirmed(e.target.checked)} /><span>Tôi đã kiểm tra lượng bàn giao của tất cả hạng mục.</span></label>
      <MutationError mutation={save} />
    </Form>
  </Modal>;
}
