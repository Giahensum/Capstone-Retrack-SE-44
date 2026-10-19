import { useState } from 'react';
import { useDepotMutation, money, number } from '../depotApi';
import { Dialog, MutationError, buttonClass, inputClass } from './DepotUI';
import { StockExpenseFields } from './StockExpenseFields';
import StockCostAdjustments from './StockCostAdjustments';

export default function StockReceiptReview({ receipt, onClose }) {
  const [purchaseCost, setPurchaseCost] = useState(receipt.purchaseCost);
  const [confirmed, setConfirmed] = useState(receipt.costsConfirmed);
  const [lines, setLines] = useState(receipt.lines);
  const [expenses, setExpenses] = useState(receipt.expenses.map(x => ({ ...x, key: crypto.randomUUID() })));
  const [reason, setReason] = useState('');
  const [editing, setEditing] = useState(false);
  const [adjusting, setAdjusting] = useState(false);
  const save = useDepotMutation('put', `stock/receipts/${receipt.id}/costs`, onClose);
  const post = useDepotMutation('patch', `stock/receipts/${receipt.id}/post`, onClose);
  const reject = useDepotMutation('patch', `stock/receipts/${receipt.id}/reject`, onClose);
  const reverse = useDepotMutation('patch', `stock/receipts/${receipt.id}/void`, onClose);
  const busy = save.isPending || post.isPending || reject.isPending || reverse.isPending || adjusting;
  const canReview = ['DRAFT', 'SUBMITTED'].includes(receipt.status);
  return <Dialog title={`Phiếu nhập: ${receipt.reference}`} wide busy={busy} onClose={onClose}>
    <div className="space-y-4">
      <p className="rounded-xl bg-d-surface-accent p-3 text-sm">{canReview ? 'Phiếu chưa ghi sổ, chưa được tính vào tồn khả dụng.' : receipt.status === 'POSTED' ? 'Phiếu đã ghi sổ. Không sửa trực tiếp số lượng hay giá vốn đã duyệt.' : `${receipt.status === 'VOIDED' ? 'Đã đảo phiếu' : 'Phiếu bị từ chối'}: ${receipt.reviewNote ?? ''}`}</p>
      <dl className="grid sm:grid-cols-2 gap-3"><div><dt>Giá mua cả lô</dt><dd className="font-semibold">{money(receipt.purchaseCost)}</dd></div><div><dt>Chi phí phiếu gốc</dt><dd className="font-semibold">{money(receipt.totalCost)}</dd></div></dl>
      {receipt.lines.map(x => <div key={x.id} className="rounded-xl bg-white border border-d-border-subtle p-4"><p className="font-semibold">{x.name} · {number(x.quantity)} {x.unit}</p>
        <p className="text-sm mt-1">{x.conditionNote || 'Chưa ghi tình trạng'}</p><p className="text-sm mt-2">Chi phí phân bổ: {x.allocatedCost == null ? 'Chưa xác định' : money(x.allocatedCost)} · Giá vốn / {x.unit}: {x.allocatedCost == null ? 'Chưa xác định' : money(x.allocatedCost / x.quantity)}</p></div>)}
      {receipt.expenses.length > 0 && <ul className="text-sm space-y-2">{receipt.expenses.map((x, i) => <li key={i}>{x.description}: {money(x.amount)}</li>)}</ul>}
      {canReview && <>
        {!editing && <button type="button" className={buttonClass} disabled={busy} onClick={() => setEditing(true)}>Bổ sung / chỉnh chi phí trước ghi sổ</button>}
        {editing && <form className="space-y-4 rounded-2xl border border-d-border-subtle p-4" onSubmit={e => {
          e.preventDefault(); save.mutate({ body: { purchaseCost: Number(purchaseCost), costsConfirmed: confirmed,
            lines: lines.map(x => ({ id: x.id, referencePrice: Number(x.referencePrice || 0), directCost: Number(x.directCost || 0) })),
            expenses: expenses.map(({ key: _key, ...x }) => ({ ...x, amount: Number(x.amount || 0) })) } });
        }}>
          <fieldset disabled={busy} className="space-y-4"><label className="block">Giá mua cả lô (đ)<input required min="0" max="1000000000000" step="1" type="number" className={inputClass} value={purchaseCost} onChange={e => setPurchaseCost(e.target.value)} /></label>
            {lines.map(x => <div key={x.id} className="grid sm:grid-cols-2 gap-3"><label>Giá tham khảo / {x.unit}: {x.name}<input type="number" min={confirmed ? 1 : 0} required step="1" max="1000000000000" className={inputClass} value={x.referencePrice} onChange={e => setLines(lines.map(l => l.id === x.id ? { ...l, referencePrice: e.target.value } : l))} /></label>
              <label>Chi phí riêng: {x.name}<input required type="number" min="0" step="1" max="1000000000000" className={inputClass} value={x.directCost} onChange={e => setLines(lines.map(l => l.id === x.id ? { ...l, directCost: e.target.value } : l))} /></label></div>)}
            <StockExpenseFields expenses={expenses} setExpenses={setExpenses} />
            <label className="flex gap-2"><input type="checkbox" checked={confirmed} onChange={e => setConfirmed(e.target.checked)} />Đủ dữ liệu chi phí để phân bổ theo giá trị tham khảo</label>
            <button className={buttonClass}>Lưu chi phí và tính phân bổ</button>
          </fieldset><MutationError mutation={save} />
        </form>}
        {!editing && <div className="space-y-4 border-t border-d-border-subtle pt-4">
          <p className="text-sm">Duyệt xác nhận số hàng đã về kho. {receipt.costsConfirmed ? 'Giá vốn phân bổ sẽ được giữ làm căn cứ cho lô xuất.' : 'Giá vốn còn thiếu; báo cáo lợi nhuận sẽ hiện chưa xác định.'}</p>
          <button type="button" className={buttonClass} disabled={busy} onClick={() => post.mutate()}>{post.isPending ? 'Đang ghi sổ…' : 'Xác nhận hàng thực nhận và ghi sổ'}</button>
          <form className="space-y-2" onSubmit={e => { e.preventDefault(); reject.mutate({ body: { reason } }); }}><label className="block">Lý do từ chối phiếu<input required maxLength={1000} className={inputClass} value={reason} onChange={e => setReason(e.target.value)} /></label><button disabled={busy} className="rounded-full border border-d-error text-d-error px-5 py-2.5">Từ chối phiếu</button></form>
        </div>}
      </>}
      {receipt.status === 'POSTED' && <form className="space-y-3 border-t border-d-border-subtle pt-4" onSubmit={e => { e.preventDefault(); reverse.mutate({ body: { reason } }); }}><p className="text-sm">Chỉ đảo phiếu khi toàn bộ hàng chưa được giữ/xuất/bán. Thao tác loại số lượng khỏi tồn và giữ lịch sử phiếu.</p><label className="block">Lý do đảo phiếu<input required maxLength={1000} disabled={busy} className={inputClass} value={reason} onChange={e => setReason(e.target.value)} /></label><button className="rounded-full border border-d-error text-d-error px-5 py-2.5" disabled={busy}>Xác nhận đảo toàn bộ phiếu nhập</button></form>}
      <MutationError mutation={post} /><MutationError mutation={reject} /><MutationError mutation={reverse} />
      {receipt.status === 'POSTED' && <StockCostAdjustments receipt={receipt} onBusyChange={setAdjusting} />}
      <p className="text-sm text-d-on-surface-variant">Nguồn do kho tự khai báo. Tài liệu/phiếu nội bộ không thay hóa đơn thuế. Chi phí đã ghi không đồng nghĩa đã gồm mọi thuế và chi phí vận hành.</p>
    </div>
  </Dialog>;
}
