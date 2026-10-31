import { useState } from 'react';
import { useDepotMutation } from '../depotApi';
import { materialLabel } from '../materialLabels';
import { Dialog, MutationError, inputClass, buttonClass } from './DepotUI';
import { StockExpenseFields } from './StockExpenseFields';

const materials = ['PET', 'HDPE', 'PVC', 'PAPER', 'CARDBOARD', 'ALUMINUM', 'IRON', 'STEEL', 'COPPER', 'OTHER'];
const emptyLine = () => ({ key: crypto.randomUUID(), kind: 'MATERIAL', materialType: 'IRON', name: 'Sắt', unit: 'kg', quantity: '', referencePrice: '', directCost: '', conditionNote: '' });
export default function StockReceiptForm({ onClose, agreement }) {
  const [operationId] = useState(() => crypto.randomUUID());
  const [receivedAt] = useState(() => new Date().toISOString());
  const [form, setForm] = useState({ sourceType: agreement ? 'BULK_PURCHASE' : 'EXTERNAL_PURCHASE', reference: agreement ? `Lô trúng: ${agreement.title}`.slice(0, 255) : '', purchaseCost: agreement?.amount ?? '', costsConfirmed: false });
  const [lines, setLines] = useState([emptyLine()]);
  const [expenses, setExpenses] = useState([]);
  const save = useDepotMutation('post', 'stock/receipts', onClose);
  const update = (key, value) => setForm({ ...form, [key]: value });
  const lineField = (key, patch) => setLines(lines.map(x => x.key === key ? { ...x, ...patch } : x));
  return <Dialog title="Lập phiếu nhập kho đa nguồn" wide busy={save.isPending} onClose={onClose}>
    <p className="text-sm text-d-on-surface-variant mb-4">Chỉ ghi hàng đã thực nhận. Phiếu nháp chưa tăng tồn; chủ kho kiểm tra rồi duyệt ghi sổ. Đơn thu gom DONE đã tính tồn, không nhập lại ở đây.</p>
    <form className="space-y-5" onSubmit={e => {
      e.preventDefault();
      save.mutate({ body: { ...form, operationId, receivedAt, bulkAgreementId: agreement?.id, purchaseCost: Number(form.purchaseCost || 0),
        expenses: expenses.map(({ key: _key, ...x }) => ({ ...x, amount: Number(x.amount || 0) })),
        lines: lines.map(({ key: _key, ...x }) => ({ ...x, materialType: x.kind === 'MATERIAL' ? x.materialType : null,
          quantity: Number(x.quantity), referencePrice: Number(x.referencePrice || 0), directCost: Number(x.directCost || 0) })) } });
    }}>
      <fieldset disabled={save.isPending} className="space-y-5">
        <div className="grid sm:grid-cols-2 gap-4">
          <label>Nguồn hàng<select className={inputClass} disabled={!!agreement} value={form.sourceType} onChange={e => update('sourceType', e.target.value)}>
            <option value="EXTERNAL_PURCHASE">Mua ngoài hệ thống</option><option value="OPENING_BALANCE">Tồn đầu kỳ</option><option value="BULK_PURCHASE">{agreement ? 'Mua lô trúng giá — liên kết thỏa thuận' : 'Mua lô thanh lý ngoài phiên giá'}</option>
          </select></label>
          <label>Chứng từ / tham chiếu nguồn<input required maxLength={255} className={inputClass} value={form.reference} onChange={e => update('reference', e.target.value)} placeholder="Ví dụ: Phiếu mua khách sạn, ngày nhận" /></label>
        </div>
        <p className="text-sm text-d-on-surface-variant">Ngày nhận ghi theo thời điểm lập phiếu. {agreement ? "Phiếu liên kết thỏa thuận lô trúng, giữ nguyên tổng giá mua. Nhập hàng phân loại đã nhận và chi phí phát sinh thực tế." : "Nguồn này do kho tự khai báo; chưa phải dữ liệu phiên giá được hệ thống xác minh."}</p>
        <div className="flex items-center justify-between gap-3"><h3 className="font-semibold">Hàng thực nhận</h3><button type="button" className={buttonClass} disabled={lines.length >= 50} onClick={() => setLines([...lines, emptyLine()])}>Thêm dòng hàng</button></div>
        {lines.map((line, index) => <section key={line.key} className="rounded-2xl border border-d-border-subtle bg-white p-4 space-y-3">
          <div className="flex justify-between items-center gap-2"><h4 className="font-semibold">Dòng {index + 1}</h4><button type="button" className="min-h-11 px-3 text-d-error rounded-full border border-d-border-subtle disabled:opacity-40" disabled={lines.length === 1} onClick={() => setLines(lines.filter(x => x.key !== line.key))}>Bỏ dòng</button></div>
          <div className="grid sm:grid-cols-2 gap-3">
            <label>Nhóm hàng<select className={inputClass} value={line.kind} onChange={e => lineField(line.key, { kind: e.target.value, unit: e.target.value === 'MATERIAL' ? 'kg' : 'cái', name: e.target.value === 'MATERIAL' ? materialLabel(line.materialType) : '', quantity: '' })}>
              <option value="MATERIAL">Nguyên liệu tái chế</option><option value="REUSABLE">Đồ còn sử dụng được</option></select></label>
            {line.kind === 'MATERIAL' ? <label>Vật liệu<select className={inputClass} value={line.materialType} onChange={e => lineField(line.key, { materialType: e.target.value, name: materialLabel(e.target.value) })}>{materials.map(m => <option key={m} value={m}>{materialLabel(m)}</option>)}</select></label>
              : <label>Tên đồ / hãng / model<input required maxLength={255} className={inputClass} value={line.name} onChange={e => lineField(line.key, { name: e.target.value })} placeholder="Tivi, tủ lạnh, điều hòa, bàn ghế…" /></label>}
            <label>Số lượng ({line.unit})<input required type="number" min={line.kind === 'MATERIAL' ? '0.001' : '1'} step={line.kind === 'MATERIAL' ? '0.001' : '1'} max="1000000000" className={inputClass} value={line.quantity} onChange={e => lineField(line.key, { quantity: e.target.value })} /></label>
            {line.kind === 'REUSABLE' && <label>Đơn vị<select className={inputClass} value={line.unit} onChange={e => lineField(line.key, { unit: e.target.value })}><option>cái</option><option>bộ</option></select></label>}
            <label className="sm:col-span-2">Tình trạng / lỗi đã biết / serial nếu có<input maxLength={1000} className={inputClass} value={line.conditionNote} onChange={e => lineField(line.key, { conditionNote: e.target.value })} /></label>
            {form.costsConfirmed && <><label>Giá bán tham khảo / {line.unit} (đ)<input required type="number" min="1" step="1" max="1000000000000" className={inputClass} value={line.referencePrice} onChange={e => lineField(line.key, { referencePrice: e.target.value })} /></label>
              <label>Chi phí riêng dòng này (đ)<input type="number" min="0" step="1" max="1000000000000" className={inputClass} value={line.directCost} onChange={e => lineField(line.key, { directCost: e.target.value })} /></label></>}
          </div>
        </section>)}
        <label className="block">Giá mua cả lô / giá trị tồn đầu kỳ (đ)<input type="number" min="0" step="1" max="1000000000000" className={inputClass} readOnly={!!agreement} value={form.purchaseCost} onChange={e => update('purchaseCost', e.target.value)} /></label>
        <StockExpenseFields expenses={expenses} setExpenses={setExpenses} />
        <label className="flex gap-3 items-start"><input type="checkbox" className="mt-1" checked={form.costsConfirmed} onChange={e => update('costsConfirmed', e.target.checked)} /><span>Đủ dữ liệu chi phí để phân bổ theo giá trị bán tham khảo.<span className="block text-sm text-d-on-surface-variant">Không chọn: giá vốn chưa xác định, hệ thống không báo lợi nhuận bằng cách coi giá vốn là 0.</span></span></label>
        <p className="text-sm text-d-on-surface-variant">Một món đồ chỉ nhập theo cái/bộ hoặc vật liệu thực cân, không nhập trùng cả hai. Chi phí nhân công/xăng xe do kho khai thực tế; thuế chỉ ghi khoản không được thu hồi đã xác định, không tự áp thuế suất.</p>
      </fieldset>
      <MutationError mutation={save} />
      <div className="sticky bottom-0 -mx-4 sm:-mx-6 -mb-4 sm:-mb-6 px-4 sm:px-6 py-3 bg-d-surface flex flex-wrap justify-end gap-3 border-t border-d-border-subtle"><button type="button" disabled={save.isPending} className="rounded-full border border-d-border-subtle px-5 py-2.5" onClick={onClose}>Hủy bỏ</button><button className={buttonClass} disabled={save.isPending}>{save.isPending ? 'Đang lưu…' : 'Lưu phiếu nháp'}</button></div>
    </form>
  </Dialog>;
}
