import { useEffect, useId, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Calculator, Info } from 'lucide-react';
import api from '@/lib/axios';
import { useDepot, depotError } from '../DepotContext';
import { money, number } from '../depotApi';
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

export default function CostAllocationPanel({ purchaseCost, lines, expenses, enabled, snapshot = false }) {
  const { depotId, userId } = useDepot();
  const body = JSON.stringify({ purchaseCost: Number(purchaseCost || 0),
    lines: lines.map(x => ({ kind: x.kind, materialType: x.kind === 'MATERIAL' ? x.materialType : null, name: x.name,
      unit: x.unit, quantity: Number(x.quantity), referencePrice: Number(x.referencePrice || 0), directCost: Number(x.directCost || 0), conditionNote: x.conditionNote || null })),
    expenses: expenses.map(x => ({ category: x.category, description: x.description, amount: Number(x.amount || 0) })) });
  const [debounced, setDebounced] = useState(null);
  useEffect(() => { const timer = setTimeout(() => setDebounced(body), 350); return () => clearTimeout(timer); }, [body]);
  const valid = enabled && lines.length > 0 && lines.every(x => x.name?.trim() && Number(x.quantity) > 0 && Number(x.referencePrice) > 0)
    && expenses.every(x => x.description?.trim());
  const query = useQuery({ queryKey: ['depot', userId, depotId, 'cost-preview', debounced], enabled: !!depotId && valid && debounced === body,
    queryFn: async () => (await api.post('/depot/stock/cost-preview', JSON.parse(debounced), { params: { depotId } })).data.data,
    retry: false, staleTime: 30000 });
  const d = query.data;
  return <Section icon={Calculator} title={snapshot ? 'Giải thích giá vốn phiếu gốc' : 'Xem trước phân bổ giá vốn'} action={<CostFormulaHelp />}>
    {!enabled ? <p className="text-sm text-d-on-surface-variant">Giá vốn chưa xác định. Khi có đủ giá tham khảo và chi phí, có thể tính phân bổ; không coi phần thiếu là 0 để báo lãi.</p>
      : !valid ? <p className="text-sm text-d-on-surface-variant">Điền số lượng và giá bán tham khảo dương cho tất cả dòng, cùng mô tả chi phí để xem cách chia tổng tiền.</p>
      : query.isError ? <p role="alert" className="text-sm text-d-error">{depotError(query.error)}</p>
      : !d || debounced !== body ? <p role="status" className="text-sm">Đang tính phân bổ…</p>
      : <div className="space-y-4 text-sm">
        <p><strong>{money(d.purchaseCost)}</strong> giá mua + <strong>{money(d.commonCost)}</strong> chi phí chung = <strong>{money(d.purchaseCost + d.commonCost)}</strong> cần chia. Chi phí riêng: {money(d.directCost)}. Tổng vốn: <strong>{money(d.totalCost)}</strong>.</p>
        <p>Tổng giá trị tham khảo: <strong>{money(d.referenceTotal)}</strong>. Đây là mẫu số để chia vốn, không phải số tiền đã mua hoặc doanh thu thực nhận.</p>
        {d.lines.map((x, i) => <div key={i} className="space-y-2 rounded-xl border border-d-border-subtle bg-d-surface p-3">
          <p className="font-bold">{x.name} · {number(x.quantity)} {x.unit}</p>
          <p>Giá bán tham khảo đã nhập: <strong>{money(x.referencePrice)} / {x.unit}</strong>.</p>
          <p>Trọng số: {number(x.quantity)} × {money(x.referencePrice)} = {money(x.referenceValue)} → {number(x.sharePercent)}% tổng giá trị tham khảo.</p>
          <p>Vốn phân bổ: {money(x.sharedCost)} + {money(x.directCost)} chi phí riêng = <strong>{money(x.allocatedCost)}</strong>.</p>
          <p className="font-semibold text-d-primary">Giá vốn / {x.unit}: {money(x.allocatedCost)} ÷ {number(x.quantity)} = {money(x.unitCost)}.</p>
          {snapshot && lines[i]?.allocatedCost != null && Number(lines[i].allocatedCost) !== x.allocatedCost &&
            <p className="text-d-error">Snapshot đã lưu khác kết quả giải thích hiện tại. Vốn gốc vẫn là {money(lines[i].allocatedCost)}; cần đối soát, không tự thay đổi lịch sử.</p>}
        </div>)}
        <p className="text-d-on-surface-variant">{snapshot ? 'Giải thích theo giá/chi phí phiếu gốc; khoản điều chỉnh sau ghi sổ xem trong lịch sử đối soát.' : 'Kết quả xem trước từ máy chủ, chưa ghi nhận tồn. Khi lưu, máy chủ kiểm tra và tính lại.'}</p>
      </div>}
  </Section>;
}
