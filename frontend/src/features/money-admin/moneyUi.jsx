import { useEffect, useId, useMemo, useState } from 'react';
import { Badge } from '@/components/liquidation/LiquidationUI';
import { inputClass, labelClass } from '@/features/admin/components/AdminUI';
import { payoutStatusMeta, bankStatusMeta, outcomeMeta } from './moneyLabels';

// Thành phần nhỏ dùng chung cho màn Thu chi (Admin) và Thu nhập (người nhận).

export function PayoutStatusChip({ status }) {
  const m = payoutStatusMeta(status);
  return <Badge tone={m.tone}>{m.label}</Badge>;
}

export function BankStatusChip({ status }) {
  const m = bankStatusMeta(status);
  return <Badge tone={m.tone}>{m.label}</Badge>;
}

export function OutcomeChip({ outcome }) {
  const m = outcomeMeta(outcome);
  return <Badge tone={m.tone}>{m.label}</Badge>;
}

export function useDebounced(value, delay = 350) {
  const [v, setV] = useState(value);
  useEffect(() => {
    const t = setTimeout(() => setV(value), delay);
    return () => clearTimeout(t);
  }, [value, delay]);
  return v;
}

/** Ô lỗi dưới trường nhập. */
export function FieldError({ children }) {
  return children ? <span role="alert" className="text-xs font-semibold text-d-error">{children}</span> : null;
}

/**
 * Chọn ngân hàng có tìm kiếm: ô lọc theo tên hoặc mã BIN, danh sách chọn bên dưới.
 * banks: [{ bin, name }]. onChange nhận (bin, name).
 */
export function BankSelect({ banks, value, onChange, label = 'Ngân hàng', disabled, error, loading }) {
  const [q, setQ] = useState('');
  const id = useId();
  const filtered = useMemo(() => {
    const s = q.trim().toLowerCase();
    const list = s ? banks.filter((b) => b.name.toLowerCase().includes(s) || b.bin.includes(s)) : banks;
    // Luôn giữ ngân hàng đang chọn trong danh sách để select hiển thị đúng.
    const sel = banks.find((b) => b.bin === value);
    return sel && !list.includes(sel) ? [sel, ...list] : list;
  }, [banks, q, value]);
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={`${id}-s`} className={labelClass}>{label}</label>
      <input id={`${id}-q`} type="search" value={q} onChange={(e) => setQ(e.target.value)} disabled={disabled || loading}
        placeholder="Gõ tên ngân hàng hoặc mã BIN để lọc" aria-label={`${label}: lọc danh sách`} className={inputClass} />
      <select id={`${id}-s`} value={value ?? ''} disabled={disabled || loading} aria-invalid={!!error}
        onChange={(e) => { const b = banks.find((x) => x.bin === e.target.value); onChange(e.target.value, b?.name ?? ''); }}
        className={`${inputClass} cursor-pointer`}>
        <option value="">{loading ? 'Đang tải danh sách ngân hàng…' : '— Chọn ngân hàng —'}</option>
        {filtered.map((b) => <option key={b.bin} value={b.bin}>{b.name} ({b.bin})</option>)}
      </select>
      <FieldError>{error}</FieldError>
    </div>
  );
}

/** Thông báo trạng thái chung: tải / lỗi / rỗng. */
export function StateBox({ loading, error, empty, children }) {
  if (loading) return <p role="status" className="px-5 py-10 text-center text-sm text-d-on-surface-variant">Đang tải…</p>;
  if (error) return <p role="alert" className="px-5 py-10 text-center text-sm font-semibold text-d-error">{error}</p>;
  if (empty) return <p className="px-5 py-10 text-center text-sm text-d-on-surface-variant">{children}</p>;
  return null;
}
