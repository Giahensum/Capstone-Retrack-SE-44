import { useState } from 'react';
import { useDepotQuery, useDepotMutation, money, number } from '../depotApi';
import { QueryState, MutationError, inputClass, buttonClass } from './DepotUI';

export default function BatchCostPanel({ id, quantity }) {
  const query = useDepotQuery(`stock/batches/${id}/costs`);
  const [fee, setFee] = useState('0');
  const [margin, setMargin] = useState('');
  const [expense, setExpense] = useState({ operationId: crypto.randomUUID(), description: '', amount: '' });
  const save = useDepotMutation('post', `stock/batches/${id}/expenses`, () => setExpense({ operationId: crypto.randomUUID(), description: '', amount: '' }));
  const d = query.data;
  const denominator = 1 - Number(fee) / 100 - Number(margin || 0) / 100;
  const suggestion = d?.hasCompleteCost && !d.needsCostReconciliation && quantity > 0 && denominator > 0 && Number(fee) >= 0 && Number(margin || 0) >= 0
    ? Math.ceil((d.costOfGoods + d.sellingCosts) / denominator / quantity) : null;
  return <section className="rounded-2xl border border-d-border-subtle bg-white p-4 space-y-4">
    <h3 className="font-semibold text-d-primary">Nguồn giá vốn và chi phí riêng của kho</h3>
    <QueryState query={query}>{d && <>
      <p className="text-sm text-d-on-surface-variant">{d.note}</p>
      {d.needsCostReconciliation && <p role="status" className="rounded-xl border border-d-border-subtle p-3 text-d-error">Nguồn hàng có điều chỉnh chi phí sau khi tạo lô. Giá vốn bên dưới là snapshot đã chốt; cần đối soát khoản điều chỉnh trước khi kết luận chênh lệch.</p>}
      {d.sources.length ? <div className="overflow-x-auto"><table className="w-full text-left text-sm"><thead><tr>{['Nguồn phân bổ', 'Lượng', 'Giá vốn / kg', 'Thành tiền'].map(x => <th key={x} className="py-2 pr-4 whitespace-nowrap">{x}</th>)}</tr></thead><tbody>{d.sources.map((x, i) => <tr key={i} className="border-t border-d-border-subtle"><td className="py-3 pr-4 break-words">{x.sourceLabel}</td><td className="pr-4 whitespace-nowrap">{number(x.quantity)} kg</td><td className="pr-4 whitespace-nowrap">{x.unitCost == null ? 'Chưa xác định' : money(x.unitCost)}</td><td className="whitespace-nowrap">{x.unitCost == null ? 'Chưa xác định' : money(x.quantity * x.unitCost)}</td></tr>)}</tbody></table></div>
        : <p role="status" className="text-sm">Lô cũ chưa có snapshot nguồn giá vốn; không tự dựng số liệu lịch sử.</p>}
      <dl className="grid sm:grid-cols-2 gap-3"><div><dt className="text-sm">Giá vốn phần xuất</dt><dd className="font-semibold">{d.costOfGoods == null ? 'Chưa đủ dữ liệu' : money(d.costOfGoods)}</dd></div><div><dt className="text-sm">Chi phí bán đã ghi</dt><dd className="font-semibold">{money(d.sellingCosts)}</dd></div><div><dt className="text-sm">Doanh thu quyết toán</dt><dd>{d.revenue == null ? 'Chưa quyết toán' : money(d.revenue)}</dd></div><div><dt className="text-sm">Chênh lệch trước thuế thu nhập</dt><dd className="font-semibold">{d.pretaxDifference == null ? 'Chưa đủ giá vốn / chưa quyết toán' : money(d.pretaxDifference)}</dd></div></dl>
      <div className="rounded-xl bg-d-surface-accent p-3 space-y-3"><h4 className="font-semibold">Máy tính giá dự kiến / kg</h4><p className="text-sm">Dùng giá vốn và chi phí đã ghi. Tỷ lệ do bạn nhập, không phải thuế suất hay mức phí hệ thống mặc định; chưa gồm khoản chưa khai.</p><div className="grid sm:grid-cols-2 gap-3"><label>Phí trên doanh thu giả định (%)<input type="number" min="0" max="99.99" step="0.01" className={inputClass} value={fee} onChange={e => setFee(e.target.value)} /></label><label>Biên lợi nhuận mong muốn (%)<input type="number" min="0" max="99.99" step="0.01" className={inputClass} value={margin} onChange={e => setMargin(e.target.value)} /></label></div><p>Giá {Number(margin || 0) > 0 ? 'đạt biên mục tiêu' : 'hòa vốn'} dự kiến: <strong>{suggestion == null ? 'Chưa đủ dữ liệu hoặc tổng tỷ lệ không hợp lệ' : `${money(suggestion)} / kg`}</strong></p></div>
      {d.expenses.length > 0 && <ul className="text-sm space-y-2">{d.expenses.map(x => <li key={x.id}>{x.description}: <strong>{money(x.amount)}</strong></li>)}</ul>}
      <form className="space-y-3 border-t border-d-border-subtle pt-4" onSubmit={e => { e.preventDefault(); save.mutate({ body: { ...expense, amount: Number(expense.amount) } }); }}><h4 className="font-semibold">Ghi thêm chi phí giao/bán thực tế</h4><fieldset disabled={save.isPending} className="space-y-3"><label className="block">Mô tả chi phí / căn cứ<input required maxLength={255} className={inputClass} value={expense.description} onChange={e => setExpense({ ...expense, description: e.target.value })} placeholder="Công giao, xăng xe, bốc dỡ…" /></label><label className="block">Số tiền (đ)<input required type="number" min="0" max="1000000000000" step="1" className={inputClass} value={expense.amount} onChange={e => setExpense({ ...expense, amount: e.target.value })} /></label><p className="text-sm">Mỗi khoản chỉ ghi một lần; chi phí đầu vào đã nằm trong giá vốn không ghi lại ở đây. Khoản bổ sung sẽ làm báo cáo thay đổi và được lưu ngày ghi nhận.</p><button className={buttonClass}>{save.isPending ? 'Đang ghi…' : 'Ghi chi phí'}</button></fieldset><MutationError mutation={save} /></form>
    </>}</QueryState>
  </section>;
}
