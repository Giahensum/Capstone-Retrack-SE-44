import { Inbox, Loader2, AlertCircle, Search } from 'lucide-react';
import Pagination from '@/components/ui/Pagination';
import { TONES, Badge, Modal, ConfirmModal, Notice } from '@/components/liquidation/LiquidationUI';

export { TONES, Badge, Modal, ConfirmModal, Notice, Pagination };

// ── Bộ thành phần dùng chung cho khu Admin (cùng một khuôn ở mọi màn) ──
export const inputClass = 'w-full h-11 px-4 bg-white border border-d-outline-variant rounded-xl text-sm text-d-on-surface placeholder:text-d-outline focus:border-d-primary focus:ring-2 focus:ring-d-primary/20 transition-all outline-none disabled:opacity-60';
export const labelClass = 'text-xs font-semibold text-d-on-surface-variant';

export function PageHeader({ icon: Icon, title, description, children }) {
  return (
    <div className="flex flex-wrap items-start justify-between gap-4">
      <div className="flex min-w-0 items-start gap-3.5">
        {Icon && <span className="mt-0.5 flex size-12 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-[#446900] to-[#5c8a00] text-white shadow-md"><Icon size={23} aria-hidden="true" /></span>}
        <div className="min-w-0">
          <h1 className="text-2xl font-black tracking-tight text-d-on-surface">{title}</h1>
          {description && <p className="mt-1 text-sm text-d-on-surface-variant">{description}</p>}
        </div>
      </div>
      {children && <div className="flex flex-wrap items-center gap-2">{children}</div>}
    </div>
  );
}

export function PrimaryButton({ icon: Icon, children, className = '', ...props }) {
  return (
    <button type="button" {...props} className={`inline-flex items-center justify-center gap-2 rounded-full bg-d-primary px-5 py-2.5 text-sm font-bold text-white shadow-sm transition hover:bg-[#385500] active:scale-95 disabled:opacity-60 ${className}`}>
      {Icon && <Icon size={17} aria-hidden="true" />}{children}
    </button>
  );
}

export function GhostButton({ children, className = '', ...props }) {
  return <button type="button" {...props} className={`rounded-full border border-d-border-subtle bg-white px-5 py-2.5 text-sm font-semibold text-d-on-surface-variant transition hover:bg-d-surface-container disabled:opacity-60 ${className}`}>{children}</button>;
}

// Thẻ số liệu (KPI): nhãn nhỏ, số to, dòng phụ, icon bên phải.
export function StatCard({ label, value, hint, icon: Icon, tone = 'primary', loading = false }) {
  return (
    <div className="group relative overflow-hidden rounded-2xl border border-d-border-subtle bg-white p-5 shadow-sm transition hover:-translate-y-0.5 hover:shadow-md">
      <div className="absolute -right-5 -top-5 size-24 rounded-full bg-d-primary/5 transition group-hover:bg-d-primary/10" />
      <div className="relative flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="truncate text-xs font-semibold text-d-on-surface-variant">{label}</p>
          {loading ? <div className="mt-2 h-8 w-24 animate-pulse rounded-lg bg-d-surface-container" /> : <p className="mt-1.5 truncate text-2xl font-black tracking-tight text-d-on-surface">{value}</p>}
          {hint && !loading && <p className="mt-1 truncate text-xs text-d-on-surface-variant">{hint}</p>}
        </div>
        {Icon && <span className={`flex size-11 shrink-0 items-center justify-center rounded-xl ${TONES[tone].chip}`}><Icon size={21} aria-hidden="true" /></span>}
      </div>
    </div>
  );
}

// Dải nút lọc trạng thái trên đầu bảng.
export function FilterTabs({ tabs, value, onChange }) {
  return (
    <div className="flex flex-wrap items-center gap-1.5" role="tablist">
      {tabs.map((t) => {
        const active = value === t.key;
        return (
          <button key={t.key || 'all'} type="button" role="tab" aria-selected={active} onClick={() => onChange(t.key)}
            className={`inline-flex h-9 items-center gap-1.5 rounded-full border px-4 text-sm font-semibold transition ${active ? 'border-d-primary bg-d-primary text-white shadow-sm' : 'border-d-border-subtle bg-white text-d-on-surface-variant hover:bg-d-surface-container'}`}>
            {t.icon && <t.icon size={15} aria-hidden="true" />}{t.label}
          </button>
        );
      })}
    </div>
  );
}

// Khung bảng: tiêu đề + thanh công cụ + nội dung + (phân trang do nơi gọi truyền vào).
export function Panel({ icon: Icon, title, toolbar, children, className = '' }) {
  return (
    <section className={`overflow-hidden rounded-2xl border border-d-border-subtle bg-white shadow-sm ${className}`}>
      {(title || toolbar) && (
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-d-border-subtle bg-d-surface-accent/40 px-5 py-4">
          {title && <h2 className="flex items-center gap-2.5 text-sm font-bold text-d-on-surface">{Icon && <span className="flex size-8 items-center justify-center rounded-xl bg-d-primary/10 text-d-primary"><Icon size={17} /></span>}{title}</h2>}
          {toolbar && <div className="flex flex-wrap items-center gap-2.5">{toolbar}</div>}
        </div>
      )}
      {children}
    </section>
  );
}

export function SearchBox({ value, onChange, placeholder, className = '' }) {
  return (
    <div className={`relative ${className}`}>
      <Search size={17} className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 text-d-on-surface-variant" aria-hidden="true" />
      <input type="search" value={value} onChange={onChange} placeholder={placeholder} aria-label={placeholder}
        className="h-11 w-full rounded-full border border-d-border-subtle bg-white pl-10 pr-4 text-sm outline-none transition focus:border-d-primary focus:ring-2 focus:ring-d-primary/20" />
    </div>
  );
}

export const Th = ({ children, className = '' }) => <th className={`px-5 py-3.5 text-[11px] font-bold uppercase tracking-wider ${className}`}>{children}</th>;
export const Td = ({ children, className = '' }) => <td className={`px-5 py-3.5 ${className}`}>{children}</td>;

export function DataTable({ head, minWidth = 800, children }) {
  return (
    <div className="w-full overflow-x-auto">
      <table className="w-full border-collapse text-left text-sm" style={{ minWidth }}>
        <thead><tr className="bg-d-surface-container-low text-d-on-surface-variant">{head}</tr></thead>
        <tbody className="divide-y divide-d-border-subtle text-d-on-surface">{children}</tbody>
      </table>
    </div>
  );
}

// Dòng trạng thái của bảng: đang tải / rỗng / lỗi.
export function TableMessage({ cols, loading, empty, error, icon: Icon = Inbox, children }) {
  if (!loading && !empty && !error) return null;
  return (
    <tr><td colSpan={cols} className="px-6 py-14 text-center text-d-on-surface-variant">
      {loading ? <span className="inline-flex items-center gap-2"><Loader2 size={18} className="animate-spin" />Đang tải…</span>
        : error ? <span className="inline-flex items-center gap-2 text-d-error"><AlertCircle size={18} />{children}</span>
          : <><Icon size={34} className="mx-auto mb-2 opacity-30" aria-hidden="true" />{children}</>}
    </td></tr>
  );
}

export function IconAction({ icon: Icon, title, onClick, tone = 'default', disabled }) {
  const hover = { default: 'hover:bg-d-primary/10 hover:text-d-primary', danger: 'hover:bg-d-error-container/60 hover:text-d-error', warn: 'hover:bg-amber-50 hover:text-amber-600' }[tone];
  return (
    <button type="button" title={title} aria-label={title} onClick={onClick} disabled={disabled}
      className={`flex size-9 items-center justify-center rounded-full text-d-on-surface-variant transition disabled:opacity-40 ${hover}`}>
      <Icon size={18} />
    </button>
  );
}

export function Field({ label, hint, children }) {
  return (
    <label className="flex flex-col gap-1.5">
      <span className={labelClass}>{label}</span>
      {children}
      {hint && <span className="text-xs text-d-on-surface-variant">{hint}</span>}
    </label>
  );
}

export function DetailList({ rows }) {
  return (
    <dl className="grid gap-x-6 gap-y-3 sm:grid-cols-2">
      {rows.map((r) => (
        <div key={r.label} className="min-w-0">
          <dt className="text-xs font-semibold text-d-on-surface-variant">{r.label}</dt>
          <dd className="mt-0.5 break-words text-sm font-semibold text-d-on-surface">{r.value ?? '—'}</dd>
        </div>
      ))}
    </dl>
  );
}

export function Avatar({ name, size = 36 }) {
  const initials = (name ?? '?').split(' ').filter(Boolean).slice(-2).map((s) => s[0]).join('').toUpperCase();
  return <span className="flex shrink-0 items-center justify-center rounded-full bg-gradient-to-br from-d-primary-container to-[#8bc92a] font-black text-[#2d4700]" style={{ width: size, height: size, fontSize: size * 0.36 }}>{initials}</span>;
}

// Footer chuẩn cho form trong Modal (nút luôn nhìn thấy).
export function FormFooter({ onCancel, submitting, submitLabel, formId, icon: Icon }) {
  return (
    <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
      <GhostButton onClick={onCancel} disabled={submitting}>Hủy</GhostButton>
      <button type="submit" form={formId} disabled={submitting} className="inline-flex items-center justify-center gap-2 rounded-full bg-d-primary px-6 py-2.5 text-sm font-bold text-white shadow-sm transition hover:bg-[#385500] active:scale-95 disabled:opacity-60">
        {submitting ? <Loader2 size={17} className="animate-spin" /> : Icon && <Icon size={17} />}{submitLabel}
      </button>
    </div>
  );
}
