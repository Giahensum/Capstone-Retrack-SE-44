import { useEffect, useId, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Calculator, Info } from 'lucide-react';
import api from '@/lib/axios';
import { useDepot, depotError } from '../DepotContext';
import { money, number } from '../depotApi';
import { needsReferencePrice } from './stockIntake';
import { Section } from '@/components/liquidation/LiquidationUI';

const formula = 'Vốn dòng = (giá mua cả lô + chi phí chung) × (số lượng dòng × giá bán tham khảo / tổng giá trị tham khảo) + chi phí riêng. Vốn mỗi đơn vị = vốn dòng / số lượng.';
export function CostFormulaHelp() {
  const [open, setOpen] = useState(false);
  const id = useId();
  return <div className="text-sm">
    <button type="button" title={formula} aria-expanded={open} aria-controls={id} onClick={() => setOpen(!open)}
      className="inline-flex min-h-11 items-center gap-2 rounded-full px-3 font-semibold text-d-primary hover:bg-d-primary/10 focus-visible:outline-2 focus-visible:outline-d-primary">
      <Info size={17} aria-hidden="true" />Cách tính giá vốn
    </button>
    {open && <div id={id} className="space-y-2 rounded-2xl border border-d-primary/20 bg-d-surface-container-low p-4">
      <p className="font-semibold">Giá bán tham khảo khác giá vốn</p>
      <p>{formula}</p>
      <p>Không cộng kg và số cái để chia đều. Hệ thống đổi từng dòng sang giá trị tham khảo bằng tiền rồi tính tỷ trọng; lô nhiều loại hàng vẫn dùng cùng công thức.</p>
      <p>Giá tham khảo do kho ước tính, không phải giá mua riêng hoặc giá bán được bảo đảm. Chi phí riêng chỉ cộng vào đúng dòng; không nhập lại khoản đã nằm trong giá mua/chi phí chung.</p>
      <p>Làm tròn vốn từng dòng đến đồng; dòng cuối nhận phần dư để tổng vốn luôn khớp. Chưa đủ dữ liệu thì để giá vốn chưa xác định. Phiếu đã ghi sổ giữ snapshot, bổ sung chi phí qua điều chỉnh riêng.</p>
    </div>}
  </div>;
}

export default function CostAllocationPanel({ purchaseCost, lines, expenses, enabled, snapshot = false, compact = false, bulkAgreementId, sourceRevision }) {
  const { depotId, userId } = useDepot();
  const byItem = lines.some(x => x.agreementItemId);
  const needsValues = lines.some(x => needsReferencePrice(lines, x));
  const body = JSON.stringify({ bulkAgreementId, sourceRevision, purchaseCost: Number(purchaseCost || 0),
    lines: lines.map(x => ({ agreementItemId: x.agreementItemId, kind: x.kind, materialType: x.kind === 'MATERIAL' ? x.materialType : null, name: x.name,
      unit: x.unit, quantity: Number(x.quantity), referencePrice: Number(x.referencePrice || 0), directCost: Number(x.directCost || 0), conditionNote: x.conditionNote || null })),
    expenses: expenses.map(x => ({ category: x.category, description: x.description, amount: Number(x.amount || 0) })) });
  const [debounced, setDebounced] = useState(null);
  useEffect(() => { const timer = setTimeout(() => setDebounced(body), 350); return () => clearTimeout(timer); }, [body]);
  const valid = enabled && purchaseCost !== '' && Number.isFinite(Number(purchaseCost)) && Number(purchaseCost) >= 0 && lines.length > 0 && lines.every(x => x.name?.trim() && Number(x.quantity) > 0 && (!needsReferencePrice(lines, x) || Number(x.referencePrice) > 0))
    && expenses.every(x => x.description?.trim() && x.amount !== '' && Number.isFinite(Number(x.amount)) && Number(x.amount) >= 0);
  const query = useQuery({ queryKey: ['depot', userId, depotId, 'cost-preview', debounced], enabled: !!depotId && valid && debounced === body,
    queryFn: async () => (await api.post('/depot/stock/cost-preview', JSON.parse(debounced), { params: { depotId } })).data.data,
    retry: false, staleTime: 30000 });
  const d = query.data;
  if (compact) return <div className="rounded-xl bg-d-surface-accent p-3 text-sm">
    {!enabled ? <p>Giá vốn chưa xác định.</p> : !valid ? <p>Điền lượng hàng{needsValues ? ' và giá trị ước tính cho phần tách nguồn' : ''} để xem vốn.</p>
      : query.isError ? <p role="alert" className="text-d-error">{depotError(query.error)}</p>
      : !d || debounced !== body ? <p role="status">Đang tính giá vốn…</p>
      : <div className="space-y-2">
        <p>Tổng vốn: <strong>{money(d.totalCost)}</strong></p>
        {d.lines.map((x, i) => <p key={i}>{x.name}: <strong>{money(x.unitCost)} / {x.unit}</strong></p>)}
        <details><summary className="min-h-11 cursor-pointer inline-flex items-center gap-2 font-semibold text-d-primary"><Info size={17} aria-hidden="true" />Cách tính</summary>
          <div className="space-y-2 pt-2">
            <p>{money(d.purchaseCost)} tiền mua + {money(d.commonCost)} chi phí chung + {money(d.directCost)} chi phí riêng = {money(d.totalCost)} tổng vốn.</p>
            {d.lines.map((x, i) => <p key={i}>{x.name}: {!byItem && lines.length > 1 && <>{number(x.sharePercent)}% phần vốn chung, </>}{money(x.allocatedCost)} ÷ {number(x.quantity)} {x.unit} = {money(x.unitCost)} / {x.unit}.</p>)}
            {byItem ? <p>Tiền mua giữ riêng từng hạng mục; chi phí chung chia theo tiền mua. Chỉ phần tách nhiều đầu ra trong cùng hạng mục mới chia theo giá trị ước tính.</p> : lines.length > 1 && <p>Tỷ trọng theo số lượng × giá trị ước tính, không chia đều kg với số cái.</p>}
            <p>Chưa gồm chi phí giao bán cho nhà máy. Xem trước chưa tăng tồn; máy chủ kiểm tra và tính lại khi lưu.</p>
          </div>
        </details>
      </div>}
  </div>;
  return <Section icon={Calculator} title={snapshot ? 'Giải thích giá vốn phiếu gốc' : 'Xem trước phân bổ giá vốn'}>
    {byItem ? <p className="text-sm text-d-on-surface-variant">Tiền mua theo từng hạng mục đã chốt. Chi phí chung tự chia theo tiền mua; vốn mỗi đơn vị dựa trên lượng thực nhập.</p> : lines.length > 1 && <CostFormulaHelp />}
    {!enabled ? <p className="text-sm text-d-on-surface-variant">Giá vốn chưa xác định. Khi có đủ giá tham khảo và chi phí, có thể tính phân bổ; không coi phần thiếu là 0 để báo lãi.</p>
      : !valid ? <p className="text-sm text-d-on-surface-variant">Điền lượng hàng{needsValues ? ' và giá trị ước tính của phần tách nhiều đầu ra' : ''}, cùng chi phí để xem giá vốn.</p>
      : query.isError ? <p role="alert" className="text-sm text-d-error">{depotError(query.error)}</p>
      : !d || debounced !== body ? <p role="status" className="text-sm">Đang tính phân bổ…</p>
      : <div className="space-y-4 text-sm">
        <p><strong>{money(d.purchaseCost)}</strong> giá mua + <strong>{money(d.commonCost)}</strong> chi phí chung = <strong>{money(d.purchaseCost + d.commonCost)}</strong> cần chia. Chi phí riêng: {money(d.directCost)}. Tổng vốn: <strong>{money(d.totalCost)}</strong>.</p>
        {!byItem && lines.length > 1 && <p>Tổng giá trị tham khảo: <strong>{money(d.referenceTotal)}</strong>. Đây là mẫu số để chia vốn, không phải số tiền đã mua hoặc doanh thu thực nhận.</p>}
        {d.lines.map((x, i) => <div key={i} className="space-y-2 rounded-xl border border-d-border-subtle bg-d-surface p-3">
          <p className="font-bold">{x.name} · {number(x.quantity)} {x.unit}</p>
          {needsReferencePrice(lines, lines[i] ?? {}) && <><p>Giá trị ước tính đã nhập: <strong>{money(x.referencePrice)} / {x.unit}</strong>.</p>
          <p>Giá trị đầu ra: {number(x.quantity)} × {money(x.referencePrice)} = {money(x.referenceValue)}.</p></>}
          <p>Vốn phân bổ: {money(x.sharedCost)} + {money(x.directCost)} chi phí riêng = <strong>{money(x.allocatedCost)}</strong>.</p>
          <p className="font-semibold text-d-primary">Giá vốn / {x.unit}: {money(x.allocatedCost)} ÷ {number(x.quantity)} = {money(x.unitCost)}.</p>
          {snapshot && lines[i]?.allocatedCost != null && Number(lines[i].allocatedCost) !== x.allocatedCost &&
            <p className="text-d-error">Snapshot đã lưu khác kết quả giải thích hiện tại. Vốn gốc vẫn là {money(lines[i].allocatedCost)}; cần đối soát, không tự thay đổi lịch sử.</p>}
        </div>)}
        <p className="text-d-on-surface-variant">{snapshot ? 'Giải thích theo giá/chi phí phiếu gốc; khoản điều chỉnh sau ghi sổ xem trong lịch sử đối soát.' : 'Kết quả xem trước từ máy chủ, chưa ghi nhận tồn. Khi lưu, máy chủ kiểm tra và tính lại.'}</p>
      </div>}
  </Section>;
}
