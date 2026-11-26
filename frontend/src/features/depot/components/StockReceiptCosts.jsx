import { useId, useState } from 'react';
import { Input } from '@/components/forms/ValidatedForm';
import { inputClass } from './DepotUI';
import { StockExpenseFields } from './StockExpenseFields';
import CostAllocationPanel from './CostAllocationPanel';

export default function StockReceiptCosts({ purchaseCost, setPurchaseCost, linked, confirmed, setConfirmed, lines, setLines, expenses, setExpenses }) {
  const hintId = useId();
  const [details, setDetails] = useState(false);
  const total = expenses.reduce((sum, x) => sum + Number(x.amount || 0), 0);
  const multipleExpenses = expenses.length > 1;
  const setTotal = value => setExpenses([{ ...(expenses[0] ?? { key: crypto.randomUUID(), category: 'OTHER', description: 'Chi phí đưa hàng về và chuẩn bị hàng' }), amount: value }]);
  const field = (index, patch) => setLines(lines.map((x, i) => i === index ? { ...x, ...patch } : x));
  return <section className="space-y-4 rounded-2xl border border-d-border-subtle bg-white p-4">
    <h3 className="font-semibold">Tiền mua và chi phí</h3>
    <div className="grid gap-4 sm:grid-cols-2">
      <label>{linked ? 'Giá mua từ thỏa thuận (đ)' : 'Tổng tiền mua hàng (đ)'}<Input name="purchaseCost" required={confirmed} readOnly={linked} type="number" min="0" max="1000000000000" step="1" className={inputClass} value={purchaseCost} onChange={e => setPurchaseCost(e.target.value)} /></label>
      <label>{multipleExpenses ? 'Tổng chi phí đã lưu (đ)' : 'Chi phí đưa hàng về và chuẩn bị hàng (đ)'}<Input name="intakeExpenses" required={confirmed} readOnly={multipleExpenses} type="number" min="0" max="1000000000000" step="1" className={inputClass} value={multipleExpenses ? total : (expenses[0]?.amount ?? 0)} onChange={e => setTotal(e.target.value)} aria-describedby={hintId} /></label>
    </div>
    <p id={hintId} className="text-sm text-d-on-surface-variant">Tổng vận chuyển về kho, bốc dỡ, phân loại/cắt/ép; nhập 0 nếu không phát sinh. Chưa gồm giao bán cho nhà máy.</p>
    {linked && <p className="text-sm text-d-on-surface-variant">Tiền mua giữ theo thỏa thuận; số lượng nhập là hàng thực tế sau phân loại.</p>}
    {(expenses.length > 0 && (multipleExpenses || expenses[0].description !== 'Chi phí đưa hàng về và chuẩn bị hàng')) && <div>
      <button type="button" className="min-h-11 text-sm font-semibold text-d-primary" aria-expanded={details} onClick={() => setDetails(!details)}>{details ? 'Ẩn chi tiết đã lưu' : 'Xem / sửa các khoản đã lưu'}</button>
      {details && <StockExpenseFields expenses={expenses} setExpenses={setExpenses} />}
    </div>}
    <label className="flex items-start gap-3 text-sm"><Input type="checkbox" checked={!confirmed} onChange={e => setConfirmed(!e.target.checked)} className="mt-1" /><span>Chưa đủ thông tin tiền mua / chi phí<span className="block text-d-on-surface-variant">Vẫn có thể nhập hàng; giá vốn sẽ ghi chưa xác định.</span></span></label>
    {confirmed && lines.length > 1 && <div className="space-y-3">
      <p className="text-sm">Lô có nhiều loại hàng: nhập giá bán tham khảo để chia tổng vốn. Đây là tỷ trọng, không phải giá vốn.</p>
      {lines.map((line, i) => <label key={line.key ?? line.id} className="block">Giá bán tham khảo / {line.unit}: {line.name || 'Dòng ' + (i + 1)}<Input data-field-label={'Giá bán tham khảo — dòng ' + (i + 1)} required type="number" min="1" max="1000000000000" step="1" className={inputClass} value={line.referencePrice} onChange={e => field(i, { referencePrice: e.target.value })} /></label>)}
    </div>}
    {lines.some(x => Number(x.directCost) > 0) && <details className="rounded-xl border border-d-border-subtle p-3">
      <summary className="min-h-11 cursor-pointer text-sm font-semibold">Chi phí riêng từng dòng đã lưu</summary>
      <p className="mb-3 text-sm">Các khoản này được cộng thêm vào vốn, không nhập trùng vào chi phí chung.</p>
      {lines.map((line, i) => <label key={line.key ?? line.id} className="block">Chi phí riêng: {line.name}<Input type="number" min="0" max="1000000000000" step="1" className={inputClass} value={line.directCost} onChange={e => field(i, { directCost: e.target.value })} /></label>)}
    </details>}
    <CostAllocationPanel purchaseCost={purchaseCost} lines={lines} expenses={expenses} enabled={confirmed} compact />
  </section>;
}
