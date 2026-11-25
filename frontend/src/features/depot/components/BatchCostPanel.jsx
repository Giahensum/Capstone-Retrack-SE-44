import { Form, Input } from '@/components/forms/ValidatedForm';
import { useState } from 'react';
import { useDepotQuery, useDepotMutation, money, number } from '../depotApi';
import { QueryState, MutationError, Pager, inputClass, buttonClass } from './DepotUI';
import BatchDetailSection from './BatchDetailSection';

export default function BatchCostPanel({ id, quantity }) {
  const [sourcePage, setSourcePage] = useState(1);
  const [expensePage, setExpensePage] = useState(1);
  const query = useDepotQuery(`stock/batches/${id}/costs`, { sourcePage, expensePage });
  const [fee, setFee] = useState('0');
  const [margin, setMargin] = useState('');
  const [expenseOpen, setExpenseOpen] = useState(false);
  const [expense, setExpense] = useState({ operationId: crypto.randomUUID(), description: '', amount: '' });
  const save = useDepotMutation('post', `stock/batches/${id}/expenses`, () => {
    setExpense({ operationId: crypto.randomUUID(), description: '', amount: '' }); setExpenseOpen(false);
  });
  const d = query.data;
  const sourceCount = d?.sourceCount ?? d?.sources.length ?? 0;
  const expenseCount = d?.expenseCount ?? d?.expenses.length ?? 0;
  const denominator = 1 - Number(fee) / 100 - Number(margin || 0) / 100;
  const suggestion = d?.hasCompleteCost && !d.needsCostReconciliation && quantity > 0 && denominator > 0 && Number(fee) >= 0 && Number(margin || 0) >= 0
    ? Math.ceil(((d.adjustedCostOfGoods ?? d.costOfGoods) + d.sellingCosts) / denominator / quantity) : null;
  return <section className="space-y-4">
    <p className="text-sm text-d-on-surface-variant">Dùng để đối chiếu chi phí nội bộ, không bắt buộc để giao hàng hoặc nhận tiền. Không thay đổi giá đã thỏa thuận với nhà máy.</p>
    <QueryState query={query}>{d && <>
      <dl className="grid sm:grid-cols-2 gap-3"><div><dt className="text-sm">Giá vốn sau đối soát</dt><dd className="font-semibold">{(d.adjustedCostOfGoods ?? d.costOfGoods) == null ? 'Chưa đủ dữ liệu' : money(d.adjustedCostOfGoods ?? d.costOfGoods)}</dd></div><div><dt className="text-sm">Chi phí giao/bán đã ghi</dt><dd className="font-semibold">{money(d.sellingCosts)}</dd></div><div><dt className="text-sm">Tiền hàng trước VAT</dt><dd>{d.revenue == null ? 'Chưa quyết toán' : money(d.revenue)}</dd></div><div><dt className="text-sm">Chênh lệch tiền hàng, chưa VAT</dt><dd className="font-semibold">{d.pretaxDifference == null ? 'Chưa đủ dữ liệu để kết luận' : money(d.pretaxDifference)}</dd></div></dl>
      {!d.hasCompleteCost && <p role="status" className="rounded-xl bg-d-surface-accent p-3 text-sm">Chưa đủ dữ liệu nguồn giá vốn. Lô vẫn được xử lý giao nhận và thanh toán theo trạng thái hiện tại.</p>}
      {d.needsCostReconciliation && <p role="status" className="rounded-xl border border-d-border-subtle p-3 text-d-error">Cần đối soát chi phí bổ sung trước khi kết luận chênh lệch.</p>}
      <BatchDetailSection title="Xem nguồn hàng và cách tính chênh lệch">
      <p className="text-sm text-d-on-surface-variant">{d.note}</p>
      {(d.hasReconciliation ?? d.sources.some(x => x.hasReconciliation)) && <p className="rounded-xl bg-d-surface-accent p-3 text-sm">Giá vốn sau đối soát: <strong>{d.adjustedCostOfGoods == null ? 'Chưa đủ dữ liệu' : money(d.adjustedCostOfGoods)}</strong>. Các dòng bên dưới giữ snapshot ban đầu; phần chênh do chi phí bổ sung được lưu riêng, không thay đổi giá mua/bán đã chốt.</p>}
      {d.needsCostReconciliation && <p role="status" className="rounded-xl border border-d-border-subtle p-3 text-d-error">Nguồn hàng có điều chỉnh chi phí sau khi tạo lô. Giá vốn bên dưới là snapshot đã chốt; cần đối soát khoản điều chỉnh trước khi kết luận chênh lệch.</p>}
      {d.sources.length ? <div className="overflow-x-auto"><table className="w-full text-left text-sm"><thead><tr>{['Nguồn phân bổ', 'Lượng', 'Giá vốn / kg', 'Thành tiền'].map(x => <th key={x} className="py-2 pr-4 whitespace-nowrap">{x}</th>)}</tr></thead><tbody>{d.sources.map((x, i) => <tr key={i} className="border-t border-d-border-subtle"><td className="py-3 pr-4 break-words">{x.sourceLabel}</td><td className="pr-4 whitespace-nowrap">{number(x.quantity)} kg</td><td className="pr-4 whitespace-nowrap">{x.unitCost == null ? 'Chưa xác định' : money(x.unitCost)}</td><td className="whitespace-nowrap">{x.unitCost == null ? 'Chưa xác định' : money(x.quantity * x.unitCost)}</td></tr>)}</tbody></table></div>
        : sourceCount === 0 && <p role="status" className="text-sm">Lô cũ chưa có snapshot nguồn giá vốn; không tự dựng số liệu lịch sử.</p>}
      {sourceCount > 20 && <nav aria-label="Phân trang nguồn giá vốn"><Pager page={sourcePage} setPage={setSourcePage} total={sourceCount} /></nav>}
      <p className="rounded-xl bg-d-surface-accent p-3 text-sm">Tiền theo quyết toán {d.settlementAmount == null ? 'Chưa quyết toán' : money(d.settlementAmount)} · VAT đã khai {d.outputVat == null ? 'Chưa có thông tin' : money(d.outputVat)}. Chênh lệch bằng tiền theo quyết toán trừ VAT đã khai, giá vốn sau đối soát và chi phí bán. Chưa tự tách VAT đầu vào hoặc tính thuế thu nhập.</p>
      </BatchDetailSection>
      {d.hasCompleteCost && !d.needsCostReconciliation && quantity > 0 && <BatchDetailSection title="Ước tính giá bán / kg (tham khảo)">
        <p className="text-sm">Tính trên {number(quantity)} kg khai báo của lô. Giá tham khảo không thay đổi giá thỏa thuận hoặc số tiền nhà máy phải trả.</p>
        <label className="block">Biên lợi nhuận mong muốn trên doanh thu (%)<Input type="number" min="0" max="99.99" step="0.01" className={inputClass} value={margin} onChange={e => setMargin(e.target.value)} /></label>
        <label className="block">Khoản khấu trừ doanh thu khác giả định (%)<Input type="number" min="0" max="99.99" step="0.01" className={inputClass} value={fee} onChange={e => setFee(e.target.value)} /></label>
        <p className="text-sm">Mặc định 0%. Với chính sách Factory chịu phí nền tảng riêng, không nhập phí đó vào ô này. Đây không phải ô khai VAT.</p>
        <p aria-live="polite">Giá {Number(margin || 0) > 0 ? 'đạt biên mục tiêu' : 'hòa vốn'} dự kiến: <strong>{suggestion == null ? 'Các tỷ lệ phải không âm và có tổng nhỏ hơn 100%.' : `${money(suggestion)} / kg`}</strong></p>
        <p className="rounded-xl bg-d-surface-accent p-3 text-sm">Công thức: (giá vốn sau đối soát + chi phí giao/bán) ÷ [khối lượng khai báo × (1 − tỷ lệ khấu trừ − biên lợi nhuận)]. Tỷ lệ tính dưới dạng thập phân, ví dụ 10% = 0,1. Giá làm tròn lên đến đồng; chưa gồm khoản chưa ghi và không phải báo giá đã gồm VAT.</p>
      </BatchDetailSection>}
      {expenseCount > 0 && <section className="space-y-3" aria-label="Chi phí giao và bán">
        <h4 className="font-semibold">Chi phí giao và bán đã ghi</h4>
        <ul className="space-y-2 text-sm">{d.expenses.map(x => <li key={x.id}>{x.description}: <strong>{money(x.amount)}</strong></li>)}</ul>
        {expenseCount > 20 && <nav aria-label="Phân trang chi phí giao và bán"><Pager page={expensePage} setPage={setExpensePage} total={expenseCount} /></nav>}
      </section>}
      {!expenseOpen && <button type="button" className={buttonClass} onClick={() => setExpenseOpen(true)}>Thêm chi phí giao/bán</button>}
      {save.isSuccess && !expenseOpen && <p role="status" className="text-sm text-d-primary">Đã ghi chi phí giao/bán.</p>}
      {expenseOpen && <Form className="space-y-3 border-t border-d-border-subtle pt-4" onSubmit={e => { e.preventDefault(); save.mutate({ body: { ...expense, amount: Number(expense.amount) } }); }}><h4 className="font-semibold">Ghi thêm chi phí giao/bán thực tế</h4><fieldset disabled={save.isPending} className="space-y-3"><label className="block">Mô tả chi phí / căn cứ<Input required maxLength={255} className={inputClass} value={expense.description} onChange={e => setExpense({ ...expense, description: e.target.value })} placeholder="Công giao, xăng xe, bốc dỡ…" /></label><label className="block">Số tiền (đ)<Input required type="number" min="0" max="1000000000000" step="1" className={inputClass} value={expense.amount} onChange={e => setExpense({ ...expense, amount: e.target.value })} /></label><p className="text-sm">Mỗi khoản chỉ ghi một lần; chi phí đầu vào đã nằm trong giá vốn không ghi lại ở đây. Khoản bổ sung sẽ làm báo cáo thay đổi và được lưu ngày ghi nhận.</p><div className="flex flex-wrap gap-3"><button className={buttonClass}>{save.isPending ? 'Đang ghi…' : 'Ghi chi phí'}</button><button type="button" className="min-h-11 rounded-full border border-d-border-subtle px-5 py-2 text-sm" onClick={() => setExpenseOpen(false)}>Đóng biểu mẫu</button></div></fieldset><MutationError mutation={save} /></Form>}
    </>}</QueryState>
  </section>;
}
