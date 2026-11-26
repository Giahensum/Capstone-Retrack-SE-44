import { Form, Input } from '@/components/forms/ValidatedForm';
import { useState } from 'react';
import { useDepotMutation, money, number } from '../depotApi';
import { Dialog, MutationError, buttonClass, inputClass } from './DepotUI';
import StockReceiptCosts from './StockReceiptCosts';
import StockCostAdjustments from './StockCostAdjustments';
import StockReceiptForm from './StockReceiptForm';
import CostAllocationPanel from './CostAllocationPanel';
import { ReceiptText } from 'lucide-react';
import BatchDetailSection from './BatchDetailSection';

export default function StockReceiptReview({ receipt, onClose }) {
  const [purchaseCost, setPurchaseCost] = useState(receipt.purchaseCost);
  const [confirmed, setConfirmed] = useState(receipt.costsConfirmed);
  const [lines, setLines] = useState(receipt.lines);
  const [expenses, setExpenses] = useState(receipt.expenses.map(x => ({ ...x, key: crypto.randomUUID() })));
  const [reason, setReason] = useState('');
  const [editing, setEditing] = useState(false);
  const [adjusting, setAdjusting] = useState(false);
  const [editingDraft, setEditingDraft] = useState(false);
  const save = useDepotMutation('put', `stock/receipts/${receipt.id}/costs`, onClose);
  const post = useDepotMutation('patch', `stock/receipts/${receipt.id}/post`, onClose);
  const reject = useDepotMutation('patch', `stock/receipts/${receipt.id}/reject`, onClose);
  const reverse = useDepotMutation('patch', `stock/receipts/${receipt.id}/void`, onClose);
  const busy = save.isPending || post.isPending || reject.isPending || reverse.isPending || adjusting;
  const canReview = ['DRAFT', 'SUBMITTED'].includes(receipt.status);
  if (editingDraft) return <StockReceiptForm receipt={receipt} onClose={onClose} />;
  return <Dialog icon={ReceiptText} title={`Phiếu nhập: ${receipt.reference}`} wide busy={busy} onClose={onClose}>
    <div className="space-y-4">
      <p className="rounded-xl bg-d-surface-accent p-3 text-sm">{canReview ? 'Phiếu chưa ghi sổ, chưa được tính vào tồn khả dụng.' : receipt.status === 'POSTED' ? 'Phiếu đã ghi sổ. Không sửa trực tiếp số lượng hay giá vốn đã duyệt.' : `${receipt.status === 'VOIDED' ? 'Đã đảo phiếu' : 'Phiếu bị từ chối'}: ${receipt.reviewNote ?? ''}`}</p>
      {receipt.lines.map(x => <div key={x.id} className="rounded-xl bg-white border border-d-border-subtle p-4"><p className="font-semibold">{x.name} · {number(x.quantity)} {x.unit}</p>
        <p className="text-sm mt-1 text-d-primary">Vốn ban đầu: {x.allocatedCost == null ? 'Chưa xác định' : money(x.allocatedCost / x.quantity) + ' / ' + x.unit}</p>
        {x.conditionNote && <p className="text-sm mt-1">{x.conditionNote}</p>}</div>)}
      <BatchDetailSection title="Chi phí và phân bổ giá vốn (nâng cao)">
      <dl className="grid sm:grid-cols-2 gap-3"><div><dt>Giá mua cả lô đã ghi</dt><dd className="font-semibold">{money(receipt.purchaseCost)}</dd></div><div><dt>Chi phí phiếu gốc đã ghi</dt><dd className="font-semibold">{money(receipt.totalCost)}</dd></div></dl>
      <CostAllocationPanel purchaseCost={receipt.purchaseCost} lines={receipt.lines} expenses={receipt.expenses} enabled={receipt.costsConfirmed} snapshot />
      {receipt.expenses.length > 0 && <ul className="text-sm space-y-2">{receipt.expenses.map((x, i) => <li key={i}>{x.description}: {money(x.amount)}</li>)}</ul>}
      </BatchDetailSection>
      {canReview && <>
        {receipt.status === 'DRAFT' && <button type="button" className={buttonClass} disabled={busy} onClick={() => setEditingDraft(true)}>Sửa phiếu nháp</button>}
        {receipt.status === 'SUBMITTED' && !editing && <button type="button" className={buttonClass} disabled={busy} onClick={() => setEditing(true)}>Kiểm tra / bổ sung chi phí trước duyệt</button>}
        {editing && <Form className="space-y-4 rounded-2xl border border-d-border-subtle p-4" onSubmit={e => {
          e.preventDefault(); save.mutate({ body: { purchaseCost: Number(purchaseCost), costsConfirmed: confirmed,
            lines: lines.map(x => ({ id: x.id, referencePrice: Number(x.referencePrice || 0), directCost: Number(x.directCost || 0) })),
            expenses: expenses.map(({ key: _key, ...x }) => ({ ...x, amount: Number(x.amount || 0) })) } });
        }}>
          <fieldset disabled={busy} className="space-y-4"><StockReceiptCosts purchaseCost={purchaseCost} setPurchaseCost={setPurchaseCost} linked={!!receipt.bulkAgreementId}
            confirmed={confirmed} setConfirmed={setConfirmed} lines={lines} setLines={setLines} expenses={expenses} setExpenses={setExpenses} />
            <button className={buttonClass}>Lưu chi phí và tính phân bổ</button>
          </fieldset><MutationError mutation={save} />
        </Form>}
        {!editing && <div className="space-y-4 border-t border-d-border-subtle pt-4">
          <p className="text-sm">Duyệt xác nhận số hàng đã về kho. {receipt.costsConfirmed ? 'Giá vốn phân bổ sẽ được giữ làm căn cứ cho lô xuất.' : 'Giá vốn còn thiếu; báo cáo lợi nhuận sẽ hiện chưa xác định.'}</p>
          <button type="button" className={buttonClass} disabled={busy} onClick={() => post.mutate()}>{post.isPending ? 'Đang ghi sổ…' : 'Xác nhận hàng thực nhận và ghi sổ'}</button>
          <BatchDetailSection title="Không nhận phiếu này"><Form className="space-y-2" onSubmit={e => { e.preventDefault(); reject.mutate({ body: { reason } }); }}><label className="block">Lý do từ chối phiếu<Input required maxLength={1000} className={inputClass} value={reason} onChange={e => setReason(e.target.value)} /></label><button disabled={busy} className="inline-flex items-center justify-center gap-2 rounded-full font-semibold transition active:scale-[.98] disabled:pointer-events-none disabled:opacity-50 outline-none focus-visible:ring-2 focus-visible:ring-d-primary/30 focus-visible:ring-offset-1 min-h-11 px-5 py-2 text-sm border border-d-error/40 bg-white text-d-error hover:bg-d-error-container/50">Từ chối phiếu</button></Form></BatchDetailSection>
        </div>}
      </>}
      {receipt.status === 'POSTED' && <BatchDetailSection title="Thao tác khác: sửa sai và điều chỉnh">
      <BatchDetailSection title="Đảo phiếu nhập còn nguyên hàng"><Form className="space-y-3" onSubmit={e => { e.preventDefault(); reverse.mutate({ body: { reason } }); }}><p className="text-sm">Chỉ đảo phiếu khi toàn bộ hàng chưa được giữ/xuất/bán. Thao tác loại số lượng khỏi tồn và giữ lịch sử phiếu.</p><label className="block">Lý do đảo phiếu<Input required maxLength={1000} disabled={busy} className={inputClass} value={reason} onChange={e => setReason(e.target.value)} /></label><button className="inline-flex items-center justify-center gap-2 rounded-full font-semibold transition active:scale-[.98] disabled:pointer-events-none disabled:opacity-50 outline-none focus-visible:ring-2 focus-visible:ring-d-primary/30 focus-visible:ring-offset-1 min-h-11 px-5 py-2 text-sm border border-d-error/40 bg-white text-d-error hover:bg-d-error-container/50" disabled={busy}>Xác nhận đảo toàn bộ phiếu nhập</button></Form></BatchDetailSection>
      <BatchDetailSection title="Điều chỉnh chi phí có lịch sử"><StockCostAdjustments receipt={receipt} onBusyChange={setAdjusting} /></BatchDetailSection>
      </BatchDetailSection>}
      <MutationError mutation={post} /><MutationError mutation={reject} /><MutationError mutation={reverse} />
      <p className="text-sm text-d-on-surface-variant">{receipt.bulkAgreementId ? 'Phiếu liên kết với thỏa thuận mua; lượng thực nhập do kho xác nhận.' : 'Nguồn do kho tự khai báo.'} Tài liệu/phiếu nội bộ không thay hóa đơn thuế. Chi phí đã ghi không đồng nghĩa đã gồm mọi thuế và chi phí vận hành.</p>
    </div>
  </Dialog>;
}
