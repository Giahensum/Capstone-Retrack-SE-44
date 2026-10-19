import { Link } from 'react-router-dom';
import { useDepotQuery, money, number } from '../depotApi';
import { Cards, QueryState, GridTable, cellClass } from './DepotUI';

const amount = value => value == null ? 'Chưa đủ dữ liệu' : money(value);
export default function CostReportPanel({ period }) {
  const query = useDepotQuery('reports/costs', period);
  const d = query.data;
  return <QueryState query={query}>{d && <div className="space-y-5">
    <Cards items={[
      { label: 'Tiền bán ròng đã ghi nhận', value: amount(d.netRevenue), icon: 'payments', description: 'Factory sau phí quyết toán + tiền bán đồ cũ ghi ngoài hệ thống.' },
      { label: 'Giá vốn đã xác định', value: money(d.knownCostOfGoods), icon: 'inventory_2', description: d.incompleteCount ? 'Mới là phần xác định được; còn giao dịch thiếu nguồn vốn hoặc quyết toán.' : 'Nguồn FIFO của lô xuất và phần giá vốn đồ cũ đã bán.' },
      { label: 'Chi phí bán đã ghi', value: money(d.sellingCosts), icon: 'receipt_long', description: 'Chi phí giao/bán gắn với giao dịch, gồm cả khoản bổ sung sau kỳ.' },
      { label: 'Chênh lệch trước thuế', value: amount(d.pretaxDifference), icon: 'analytics', description: 'Tiền bán ròng − giá vốn − chi phí bán đã ghi. Chưa gồm chi phí chưa nhập.' }
    ]} />
    <section className="rounded-[20px] border border-d-border-subtle bg-white p-4 sm:p-6 space-y-4" aria-labelledby="cost-report-title">
      <h2 id="cost-report-title" className="text-xl font-semibold">Đối chiếu giá vốn theo kênh bán</h2>
      <p className="text-sm">{number(d.transactionCount)} giao dịch trong kỳ · {number(d.incompleteCount)} giao dịch thiếu dữ liệu hoặc cần đối soát chi phí.</p>
      {d.incompleteCount > 0 && <p role="status" className="rounded-xl border border-d-border-subtle bg-d-surface-accent p-3 text-sm">Chưa thể kết luận tổng lãi/lỗ. Lô thiếu snapshot/vốn/tiền ròng hoặc nguồn có điều chỉnh sau xuất cần được đối soát trước.</p>}
      {!d.transactionCount && <p role="status">Chưa có lô Factory đã quyết toán hoặc đồ cũ đã ghi bán trong kỳ.</p>}
      <GridTable headers={['Kênh bán', 'Giao dịch', 'Tiền bán', 'Giá vốn đã xác định', 'Chi phí bán', 'Chênh lệch trước thuế']}>
        {d.channels.map(x => <tr key={x.channel}>
          <td className={cellClass}>{x.channel === 'FACTORY' ? 'Lô xuất nhà máy' : 'Đồ tái sử dụng bán ngoài'}</td>
          <td className={cellClass}><p>{number(x.transactionCount)}</p>{x.incompleteCount > 0 && <p className="text-xs text-d-on-surface-variant mt-1">{number(x.incompleteCount)} chưa đủ dữ liệu</p>}</td>
          <td className={cellClass}><p className="font-semibold">Ròng: {amount(x.netRevenue)}</p><p className="text-xs text-d-on-surface-variant mt-1">Gộp: {amount(x.grossRevenue)}</p><p className="text-xs text-d-on-surface-variant mt-1">Phí: {amount(x.platformFees)}</p></td>
          {[x.knownCostOfGoods, x.sellingCosts, x.pretaxDifference].map((value, i) => <td key={i} className={cellClass}>{amount(value)}</td>)}
        </tr>)}
      </GridTable>
      <p className="text-sm text-d-on-surface-variant leading-relaxed">{d.note}</p>
      <p className="text-sm text-d-on-surface-variant">Giá mua cả lô nhập chưa bán hết nằm trong tồn kho, không trừ toàn bộ vào kỳ bán một phần. Chênh lệch cân thực tế với nguồn xuất chưa được tự coi là hàng trả. Số liệu ở tab dòng tiền và tab này dùng thời điểm/cơ sở khác nhau nên không dùng phép trừ hai thẻ dòng tiền để tính lợi nhuận.</p>
      <div className="flex flex-wrap gap-4 text-sm font-semibold text-d-primary"><Link to="/depot/batches" className="underline underline-offset-4">Xem nguồn vốn / chi phí từng lô</Link><Link to="/depot/inventory?tab=reusable" className="underline underline-offset-4">Xem đồ tái sử dụng và lịch sử bán</Link></div>
    </section>
  </div>}</QueryState>;
}
