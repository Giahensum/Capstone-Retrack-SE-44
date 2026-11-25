import { Form, Input, Select } from '@/components/forms/ValidatedForm';
import { useId, useState } from 'react';
import { Warehouse } from 'lucide-react';
import CostAllocationPanel from './CostAllocationPanel';
import { useDepotMutation } from '../depotApi';
import { materialLabel } from '../materialLabels';
import { Dialog, MutationError, inputClass, buttonClass } from './DepotUI';
import { StockExpenseFields } from './StockExpenseFields';

const materials = ['PET', 'HDPE', 'PVC', 'PAPER', 'CARDBOARD', 'ALUMINUM', 'IRON', 'STEEL', 'COPPER', 'OTHER'];
const emptyLine = (kind = 'MATERIAL') => ({ key: crypto.randomUUID(), kind, materialType: 'IRON', name: kind === 'MATERIAL' ? 'Sắt' : '', unit: kind === 'MATERIAL' ? 'kg' : 'cái', quantity: '', referencePrice: '', directCost: '', conditionNote: '' });
export default function StockReceiptForm({ onClose, agreement, receipt, confirmBulkReceived = false }) {
  const formId = useId();
  const [operationId] = useState(() => crypto.randomUUID());
  const [receivedAt] = useState(() => receipt?.receivedAt ?? new Date().toISOString());
  const linkedAgreement = !!(agreement || receipt?.bulkAgreementId);
  const [form, setForm] = useState({ sourceType: receipt?.sourceType ?? (agreement ? 'BULK_PURCHASE' : 'EXTERNAL_PURCHASE'), reference: receipt?.reference ?? (agreement ? `Lô trúng: ${agreement.title}`.slice(0, 255) : ''), purchaseCost: receipt?.purchaseCost ?? agreement?.amount ?? '', costsConfirmed: receipt?.costsConfirmed ?? false });
  const [lines, setLines] = useState(() => receipt ? receipt.lines.map(x => ({ ...x, key: x.id, materialType: x.materialType ?? 'IRON', conditionNote: x.conditionNote ?? '' })) : [emptyLine()]);
  const [expenses, setExpenses] = useState(() => receipt?.expenses.map(x => ({ ...x, key: crypto.randomUUID() })) ?? []);
  const [stockConfirmed, setStockConfirmed] = useState(false);
  const save = useDepotMutation(receipt ? 'put' : 'post', receipt ? `stock/receipts/${receipt.id}/draft` : 'stock/receipts', onClose);
  const update = (key, value) => setForm({ ...form, [key]: value });
  const lineField = (key, patch) => setLines(lines.map(x => x.key === key ? { ...x, ...patch } : x));
  const footer = (<div className="flex flex-wrap justify-end gap-3"><button type="button" disabled={save.isPending} className="rounded-full border border-d-border-subtle px-5 py-2.5" onClick={onClose}>Hủy bỏ</button>{!confirmBulkReceived && <button type="submit" form={formId} value="draft" className={receipt ? buttonClass : 'rounded-full border border-d-border-subtle px-5 py-2.5'} disabled={save.isPending}>{save.isPending ? 'Đang lưu…' : receipt ? 'Lưu thay đổi phiếu nháp' : 'Lưu phiếu nháp'}</button>}{!receipt && <button type="submit" form={formId} value="post" className={buttonClass} disabled={save.isPending || !stockConfirmed}>{save.isPending ? 'Đang lưu…' : confirmBulkReceived ? 'Xác nhận nhận đủ và nhập kho' : 'Ghi nhận vào kho'}</button>}</div>);
  return <Dialog icon={Warehouse} footer={footer} title={receipt ? 'Sửa phiếu nhập nháp' : confirmBulkReceived ? 'Nhận hàng và nhập kho từ lô trúng' : 'Lập phiếu nhập kho đa nguồn'} wide busy={save.isPending} onClose={onClose}>
    <p className="text-sm text-d-on-surface-variant mb-4">{confirmBulkReceived ? 'Nhập số lượng từng loại hàng sau phân loại. Giá mua cả lô đã được lấy từ thỏa thuận.' : 'Chỉ ghi hàng đã thực nhận; không nhập lại đơn thu gom đã hoàn tất.'} {receipt && 'Phiếu nháp cho phép sửa hàng và chi phí trước khi ghi sổ.'}</p>
    <Form id={formId} className="space-y-5" onSubmit={e => {
      e.preventDefault();
      const postImmediately = !receipt && e.nativeEvent.submitter?.value === 'post';
      if (postImmediately && !stockConfirmed) return;
      save.mutate({ body: { ...form, operationId, receivedAt, postImmediately, confirmBulkReceived, expectedRevision: receipt?.revision, bulkAgreementId: agreement?.id, purchaseCost: Number(form.purchaseCost || 0),
        expenses: expenses.map(({ key: _key, ...x }) => ({ ...x, amount: Number(x.amount || 0) })),
        lines: lines.map(({ key: _key, ...x }) => ({ ...x, materialType: x.kind === 'MATERIAL' ? x.materialType : null,
          quantity: Number(x.quantity), referencePrice: Number(x.referencePrice || 0), directCost: Number(x.directCost || 0) })) } });
    }}>
      <fieldset disabled={save.isPending} className="space-y-5">
        {linkedAgreement && !receipt ? <details className="rounded-xl border border-d-border-subtle bg-white p-3"><summary className="min-h-11 cursor-pointer font-medium text-d-primary">Nguồn: {form.reference}</summary><p className="text-sm">Mua lô trúng giá · nguồn liên kết tự động. Ngày nhận ghi theo thời điểm lập phiếu.</p></details> : <div className="grid sm:grid-cols-2 gap-4">
          <label>Nguồn hàng<Select className={inputClass} disabled={linkedAgreement || !!receipt} value={form.sourceType} onChange={e => update('sourceType', e.target.value)}>
            <option value="EXTERNAL_PURCHASE">Mua ngoài hệ thống</option><option value="OPENING_BALANCE">Tồn đầu kỳ</option><option value="BULK_PURCHASE">{linkedAgreement ? 'Mua lô trúng giá — liên kết thỏa thuận' : 'Mua lô thanh lý ngoài phiên giá'}</option>
          </Select></label>
          <label>Chứng từ / tham chiếu nguồn<Input required maxLength={255} className={inputClass} value={form.reference} onChange={e => update('reference', e.target.value)} placeholder="Ví dụ: Phiếu mua khách sạn, ngày nhận" /></label>
        </div>}
        {!linkedAgreement && <p className="text-sm text-d-on-surface-variant">Nguồn ngoài do kho khai báo; ngày nhận theo thời điểm lập phiếu.</p>}
        <div className="flex flex-wrap items-center justify-between gap-3"><h3 className="font-semibold">Hàng thực nhận</h3><div className="flex flex-wrap gap-2"><button type="button" className={buttonClass} disabled={lines.length >= 50} onClick={() => setLines([...lines, emptyLine()])}>Thêm phế liệu</button><button type="button" className="min-h-11 rounded-full border border-d-border-subtle px-4 py-2 font-semibold text-d-primary" disabled={lines.length >= 50} onClick={() => setLines([...lines, emptyLine('REUSABLE')])}>Thêm đồ cũ</button></div></div>
        {lines.map((line, index) => <section key={line.key} className="rounded-2xl border border-d-border-subtle bg-white p-4 space-y-3">
          <div className="flex justify-between items-center gap-2"><h4 className="font-semibold">Dòng {index + 1}</h4><button type="button" className="min-h-11 px-3 text-d-error rounded-full border border-d-border-subtle disabled:opacity-40" disabled={lines.length === 1} onClick={() => setLines(lines.filter(x => x.key !== line.key))}>Bỏ dòng</button></div>
          <div className="grid sm:grid-cols-2 gap-3">
            <label>Nhóm hàng<Select className={inputClass} value={line.kind} onChange={e => lineField(line.key, { kind: e.target.value, unit: e.target.value === 'MATERIAL' ? 'kg' : 'cái', name: e.target.value === 'MATERIAL' ? materialLabel(line.materialType) : '', quantity: '' })}>
              <option value="MATERIAL">Nguyên liệu tái chế</option><option value="REUSABLE">Đồ còn sử dụng được</option></Select></label>
            {line.kind === 'MATERIAL' ? <label>Vật liệu<Select className={inputClass} value={line.materialType} onChange={e => lineField(line.key, { materialType: e.target.value, name: materialLabel(e.target.value) })}>{materials.map(m => <option key={m} value={m}>{materialLabel(m)}</option>)}</Select></label>
              : <label>Tên đồ / hãng / model<Input data-field-label={`Tên đồ / hãng / model — dòng ${index + 1}`} required maxLength={255} className={inputClass} value={line.name} onChange={e => lineField(line.key, { name: e.target.value })} placeholder="Tivi, tủ lạnh, điều hòa, bàn ghế…" /></label>}
            <label>Số lượng ({line.unit})<Input data-field-label={`Số lượng (${line.unit}) — dòng ${index + 1}`} required type="number" min={line.kind === 'MATERIAL' ? '0.001' : '1'} step={line.kind === 'MATERIAL' ? '0.001' : '1'} max="1000000000" className={inputClass} value={line.quantity} onChange={e => lineField(line.key, { quantity: e.target.value })} /></label>
            {line.kind === 'REUSABLE' && <label>Đơn vị<Select className={inputClass} value={line.unit} onChange={e => lineField(line.key, { unit: e.target.value })}><option>cái</option><option>bộ</option></Select></label>}
            {line.kind === 'REUSABLE' && <label className="sm:col-span-2">Tình trạng / lỗi đã biết (tùy chọn)<Input maxLength={1000} className={inputClass} value={line.conditionNote} onChange={e => lineField(line.key, { conditionNote: e.target.value })} /></label>}
          </div>
        </section>)}
        {linkedAgreement && <p className="rounded-xl bg-d-surface-accent p-3 text-sm">Giá mua cả lô được lấy từ thỏa thuận: <strong>{Number(form.purchaseCost).toLocaleString('vi-VN')} đ</strong>. Không tính lại theo kg.</p>}
        <details className="rounded-2xl border border-d-border-subtle bg-white p-4">
          <summary className="cursor-pointer min-h-11 font-semibold text-d-primary">Chi phí và giá vốn (tùy chọn nâng cao)</summary>
          <div className="space-y-4 pt-3">
            <p className="text-sm text-d-on-surface-variant">Có thể ghi hàng vào kho trước mà chưa phân bổ vốn. Nếu chưa đủ dữ liệu, báo cáo sẽ ghi rõ chưa xác định; đóng mục này không xóa các khoản đã nhập.</p>
            <label className="block">Giá mua cả lô / giá trị tồn đầu kỳ (đ)<Input type="number" min="0" step="1" max="1000000000000" className={inputClass} readOnly={linkedAgreement} value={form.purchaseCost} onChange={e => update('purchaseCost', e.target.value)} /></label>
            <StockExpenseFields expenses={expenses} setExpenses={setExpenses} />
            <label className="flex gap-3 items-start"><Input type="checkbox" className="mt-1" checked={form.costsConfirmed} onChange={e => update('costsConfirmed', e.target.checked)} /><span>Tính giá vốn từng loại hàng để xem lãi khi bán.<span className="block text-sm text-d-on-surface-variant">Cần giá bán tham khảo làm tỷ trọng chia tổng vốn. Giá tham khảo không phải giá mua thực tế. Bỏ chọn: vốn chưa xác định, không coi là 0.</span></span></label>
            {form.costsConfirmed && lines.map(line => <div key={line.key} className="grid sm:grid-cols-2 gap-3 rounded-xl border border-d-border-subtle p-3"><p className="sm:col-span-2 font-medium">{line.name || 'Dòng hàng chưa đặt tên'}</p><label>Giá bán tham khảo / {line.unit} (đ)<Input required type="number" min="1" step="1" max="1000000000000" className={inputClass} value={line.referencePrice} onChange={e => lineField(line.key, { referencePrice: e.target.value })} /></label><label>Chi phí riêng dòng này (đ)<Input type="number" min="0" step="1" max="1000000000000" className={inputClass} value={line.directCost} onChange={e => lineField(line.key, { directCost: e.target.value })} /></label></div>)}
            {form.costsConfirmed && <CostAllocationPanel purchaseCost={form.purchaseCost} lines={lines} expenses={expenses} enabled />}
            <p className="text-sm text-d-on-surface-variant">Chi phí nhập đã nằm trong giá vốn không ghi lại khi bán. Thuế chỉ ghi khoản không thu hồi đã xác định, không tự áp thuế suất.</p>
          </div>
        </details>
        <p className="text-sm text-d-on-surface-variant">Đồ còn dùng được ghi cái/bộ; phế liệu ghi kg. Không nhập trùng cùng một món đồ vào cả hai nhóm.</p>
        {!receipt && <label className="flex gap-3 items-start rounded-2xl border border-d-border-subtle bg-white p-4"><Input type="checkbox" className="mt-1" checked={stockConfirmed} onChange={e => setStockConfirmed(e.target.checked)} /><span>{confirmBulkReceived ? 'Tôi đã nhận đủ toàn bộ lô về kho, kiểm tra phân loại và đồng ý ghi nhận số hàng này.' : 'Tôi đã kiểm tra hàng thực nhận và đồng ý ghi nhận vào kho.'}<span className="block text-sm text-d-on-surface-variant">{confirmBulkReceived ? 'Một lần xác nhận sẽ ghi nhận đã nhận đủ lô và tăng tồn. Nếu chưa nhận đủ, hãy đóng và quay lại sau.' : 'Ghi nhận sẽ tăng tồn ngay. Nếu cần kiểm tra tiếp, chọn Lưu phiếu nháp; phiếu đã ghi sổ không sửa trực tiếp số lượng.'}</span></span></label>}
      </fieldset>
      <MutationError mutation={save} />

    </Form>
  </Dialog>;
}
