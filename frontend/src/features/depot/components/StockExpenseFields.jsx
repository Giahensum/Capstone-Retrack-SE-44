import { inputClass, buttonClass } from './DepotUI';

const categories = { LABOR: 'Nhân công', FUEL: 'Xăng xe', TRANSPORT: 'Vận chuyển', HANDLING: 'Bốc xếp', NON_RECOVERABLE_TAX: 'Thuế không được thu hồi', OTHER: 'Khác' };
export function StockExpenseFields({ expenses, setExpenses }) {
  const update = (key, patch) => setExpenses(expenses.map(x => x.key === key ? { ...x, ...patch } : x));
  return <section className="space-y-3">
    <div className="flex flex-wrap justify-between gap-3 items-center"><h3 className="font-semibold">Chi phí chung đầu vào</h3><button type="button" className={buttonClass} disabled={expenses.length >= 30} onClick={() => setExpenses([...expenses, { key: crypto.randomUUID(), category: 'LABOR', description: '', amount: '' }])}>Thêm chi phí</button></div>
    {!expenses.length && <p className="text-sm text-d-on-surface-variant">Chưa ghi chi phí chung. Không tính lại khoản đã nằm trong giá mua hoặc chi phí riêng.</p>}
    {expenses.map((x, index) => <div key={x.key} className="grid sm:grid-cols-2 gap-3 rounded-xl border border-d-border-subtle p-3 bg-white">
      <label>Nhóm chi phí {index + 1}<select className={inputClass} value={x.category} onChange={e => update(x.key, { category: e.target.value })}>{Object.entries(categories).map(([key, label]) => <option key={key} value={key}>{label}</option>)}</select></label>
      <label>Số tiền (đ)<input required type="number" min="0" max="1000000000000" step="1" className={inputClass} value={x.amount} onChange={e => update(x.key, { amount: e.target.value })} /></label>
      <label className="sm:col-span-2">Mô tả / căn cứ phân bổ<input required maxLength={255} className={inputClass} value={x.description} onChange={e => update(x.key, { description: e.target.value })} /></label>
      <button type="button" className="justify-self-start min-h-11 rounded-full px-3 border border-d-border-subtle text-d-error" onClick={() => setExpenses(expenses.filter(e => e.key !== x.key))}>Bỏ chi phí {index + 1}</button>
    </div>)}
  </section>;
}
