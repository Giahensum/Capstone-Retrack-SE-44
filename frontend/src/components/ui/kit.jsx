import { forwardRef, isValidElement, useId } from 'react';
import { Inbox, Loader2, AlertCircle, Search } from 'lucide-react';
import { cn } from '@/lib/cn';
import { TONES } from '@/components/ui/modalTheme';
import { useShellPage } from '@/components/layout/AppShell';

/*
 * Bộ thành phần giao diện chuẩn của RETRACK, dùng chung cho mọi vai trò.
 * Phong cách theo khu quản trị FITLY (bề mặt trắng, viền mảnh, bo tròn lớn, nút dạng viên thuốc),
 * màu theo nhận diện RETRACK: xanh lá #446900, điểm nhấn lime #a3e635.
 * Các bộ riêng (AdminUI, DepotUI, LiquidationUI) re-export hoặc dựng trên các token ở đây
 * để mọi màn hình có cùng một khuôn.
 */

// ── Token class: dùng khi cần ghép tay trên phần tử gốc (input, nút, ô bảng) ──
export const ui = {
  focus: 'outline-none focus-visible:ring-2 focus-visible:ring-d-primary/30 focus-visible:ring-offset-1',
  input: 'block w-full min-h-11 px-4 py-2.5 bg-white border border-d-outline-variant rounded-xl text-sm text-d-on-surface placeholder:text-d-outline focus:border-d-primary focus:ring-2 focus:ring-d-primary/20 transition-all outline-none disabled:cursor-not-allowed disabled:opacity-60 aria-invalid:border-d-error aria-invalid:ring-d-error/20',
  textarea: 'w-full min-h-24 px-4 py-3 bg-white border border-d-outline-variant rounded-xl text-sm text-d-on-surface placeholder:text-d-outline focus:border-d-primary focus:ring-2 focus:ring-d-primary/20 transition-all outline-none disabled:opacity-60',
  label: 'text-xs font-semibold text-d-on-surface-variant',
  card: 'rounded-2xl border border-d-border-subtle bg-white shadow-sm',
  th: 'px-5 py-3.5 text-[11px] font-bold uppercase tracking-wider text-d-on-surface-variant whitespace-nowrap',
  td: 'px-5 py-3.5',
};

const BUTTON_VARIANTS = {
  primary: 'bg-d-primary text-white shadow-sm hover:bg-[#385500]',
  accent: 'bg-d-primary-container text-[#1b2d00] shadow-sm hover:bg-[#b2f746]',
  secondary: 'border border-d-border-subtle bg-white text-d-on-surface hover:bg-d-surface-container',
  ghost: 'text-d-on-surface-variant hover:bg-d-surface-container hover:text-d-on-surface',
  soft: 'bg-d-primary/10 text-d-primary hover:bg-d-primary/15',
  danger: 'bg-d-error text-white shadow-sm hover:bg-[#93000a]',
  'danger-soft': 'bg-d-error-container/60 text-d-error hover:bg-d-error-container',
  link: 'text-d-primary underline-offset-4 hover:underline px-0',
};
const BUTTON_SIZES = {
  sm: 'h-9 px-4 text-[13px] gap-1.5',
  md: 'h-11 px-5 text-sm gap-2',
  lg: 'h-12 px-6 text-[15px] gap-2',
  icon: 'size-10 p-0',
};
export function buttonClass({ variant = 'primary', size = 'md', className } = {}) {
  return cn(
    'inline-flex shrink-0 items-center justify-center rounded-full font-semibold whitespace-nowrap transition active:scale-[.98] disabled:pointer-events-none disabled:opacity-50',
    ui.focus, BUTTON_VARIANTS[variant] ?? BUTTON_VARIANTS.primary, BUTTON_SIZES[size] ?? BUTTON_SIZES.md, className,
  );
}

export const Button = forwardRef(function Button({ variant, size, icon: Icon, loading = false, className, children, type = 'button', disabled, ...props }, ref) {
  return (
    <button ref={ref} type={type} disabled={disabled || loading} aria-busy={loading || undefined} className={buttonClass({ variant, size, className })} {...props}>
      {loading ? <Loader2 size={17} className="animate-spin" aria-hidden="true" /> : Icon && <Icon size={17} aria-hidden="true" />}
      {children}
    </button>
  );
});

export function PrimaryButton({ className = '', ...props }) { return <Button variant="primary" className={className} {...props} />; }
export function GhostButton({ className = '', ...props }) { return <Button variant="secondary" className={className} {...props} />; }

// ── Bề mặt ──
export function Card({ className, children, ...props }) {
  return <div className={cn(ui.card, className)} {...props}>{children}</div>;
}
export function CardHeader({ icon: Icon, title, description, action, className }) {
  return (
    <div className={cn('flex flex-wrap items-start justify-between gap-3 border-b border-d-border-subtle px-5 py-4', className)}>
      <div className="flex min-w-0 items-center gap-2.5">
        {Icon && <span className="flex size-8 shrink-0 items-center justify-center rounded-xl bg-d-primary/10 text-d-primary"><Icon size={17} aria-hidden="true" /></span>}
        <div className="min-w-0">
          <h2 className="text-sm font-bold text-d-on-surface">{title}</h2>
          {description && <p className="mt-0.5 text-xs text-d-on-surface-variant">{description}</p>}
        </div>
      </div>
      {action && <div className="flex flex-wrap items-center gap-2">{action}</div>}
    </div>
  );
}
export function CardContent({ className, children }) { return <div className={cn('p-5', className)}>{children}</div>; }

// Khung bảng/khối: tiêu đề + thanh công cụ + nội dung.
export function Panel({ icon: Icon, title, toolbar, children, className = '' }) {
  return (
    <section className={cn('overflow-hidden', ui.card, className)}>
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

// ── Đầu trang ──
// icon mặc định lấy từ mục menu đang chọn (AppShell); truyền icon={false} để ẩn.
export function PageHeader({ icon, eyebrow, title, description, children }) {
  const shell = useShellPage();
  const Icon = icon === false ? null : icon ?? shell?.icon;
  return (
    <div className="flex flex-wrap items-start justify-between gap-4">
      <div className="flex min-w-0 items-start gap-3.5">
        {Icon && <span className="mt-0.5 flex size-12 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-[#446900] to-[#5c8a00] text-white shadow-md"><Icon size={23} aria-hidden="true" /></span>}
        <div className="min-w-0">
          {eyebrow && <p className="text-[11px] font-bold uppercase tracking-[.14em] text-d-primary">{eyebrow}</p>}
          <h1 className="text-2xl font-black tracking-tight text-d-on-surface">{title}</h1>
          {description && <p className="mt-1 text-sm text-d-on-surface-variant">{description}</p>}
        </div>
      </div>
      {children && <div className="flex flex-wrap items-center gap-2">{children}</div>}
    </div>
  );
}

// Khung nội dung trang: canh giữa, khoảng cách chuẩn giữa các khối.
export function PageBody({ className, children }) {
  return <div className={cn('mx-auto flex w-full max-w-7xl flex-col gap-6 p-4 md:p-6', className)}>{children}</div>;
}

// Thẻ số liệu (KPI): nhãn nhỏ, số to, dòng phụ, icon bên phải.
export function StatCard({ label, title, value, hint, subtitle, icon: Icon, tone = 'primary', loading = false }) {
  const chip = (TONES[tone] ?? TONES.primary).chip;
  return (
    <div className="group relative overflow-hidden rounded-2xl border border-d-border-subtle bg-white p-5 shadow-sm transition hover:-translate-y-0.5 hover:shadow-md">
      <div className="absolute -right-5 -top-5 size-24 rounded-full bg-d-primary/5 transition group-hover:bg-d-primary/10" />
      <div className="relative flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="truncate text-xs font-semibold text-d-on-surface-variant">{label ?? title}</p>
          {loading ? <div className="mt-2 h-8 w-24 animate-pulse rounded-lg bg-d-surface-container" /> : <p className="mt-1.5 truncate text-2xl font-black tracking-tight text-d-on-surface">{value}</p>}
          {(hint ?? subtitle) && !loading && <p className="mt-1 truncate text-xs text-d-on-surface-variant">{hint ?? subtitle}</p>}
        </div>
        {Icon && <span className={`flex size-11 shrink-0 items-center justify-center rounded-xl ${chip}`}>{isValidElement(Icon) ? Icon : <Icon size={21} aria-hidden="true" />}</span>}
      </div>
    </div>
  );
}

// ── Nhãn trạng thái ──
export function Badge({ tone = 'slate', icon: Icon, children, className = '' }) {
  return (
    <span className={cn('inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-bold', (TONES[tone] ?? TONES.slate).chip, className)}>
      {Icon && <Icon size={13} aria-hidden="true" />}
      {children}
    </span>
  );
}

export function Notice({ icon: Icon, tone = 'blue', children, className = '' }) {
  const t = TONES[tone] ?? TONES.blue;
  return (
    <div role="status" className={cn('flex items-start gap-2.5 rounded-2xl px-4 py-3 text-sm ring-1', t.chip, t.ring, className)}>
      {Icon && <Icon size={17} className="mt-0.5 shrink-0" aria-hidden="true" />}
      <div className="min-w-0 break-words">{children}</div>
    </div>
  );
}

// ── Biểu mẫu ──
export const Input = forwardRef(function Input({ label, error, hint, className, id, ...props }, ref) {
  const generated = useId();
  const inputId = id ?? generated;
  const control = <input id={inputId} ref={ref} aria-invalid={error ? true : undefined} className={cn(ui.input, className)} {...props} />;
  if (!label && !error && !hint) return control;
  return (
    <div className="flex flex-col gap-1.5">
      {label && <label htmlFor={inputId} className={ui.label}>{label}</label>}
      {control}
      {error ? <span role="alert" className="text-xs text-d-error">{error}</span> : hint && <span className="text-xs text-d-on-surface-variant">{hint}</span>}
    </div>
  );
});

export const Select = forwardRef(function Select({ className, children, ...props }, ref) {
  return <select ref={ref} className={cn(ui.input, 'pr-9', className)} {...props}>{children}</select>;
});

export const Textarea = forwardRef(function Textarea({ className, ...props }, ref) {
  return <textarea ref={ref} className={cn(ui.textarea, className)} {...props} />;
});

export function Field({ label, hint, error, children, className }) {
  return (
    <label className={cn('flex flex-col gap-1.5', className)}>
      <span className={ui.label}>{label}</span>
      {children}
      {error ? <span role="alert" className="text-xs text-d-error">{error}</span> : hint && <span className="text-xs text-d-on-surface-variant">{hint}</span>}
    </label>
  );
}

export function SearchBox({ value, onChange, placeholder, className = '' }) {
  return (
    <div className={cn('relative', className)}>
      <Search size={17} className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 text-d-on-surface-variant" aria-hidden="true" />
      <input type="search" value={value} onChange={onChange} placeholder={placeholder} aria-label={placeholder}
        className="h-11 w-full rounded-full border border-d-border-subtle bg-white pl-10 pr-4 text-sm outline-none transition focus:border-d-primary focus:ring-2 focus:ring-d-primary/20" />
    </div>
  );
}

// Dải nút lọc trạng thái / tab dạng viên thuốc.
export function FilterTabs({ tabs, value, onChange }) {
  return (
    <div className="flex flex-wrap items-center gap-1.5" role="tablist">
      {tabs.map((t) => {
        const active = value === t.key;
        return (
          <button key={t.key || 'all'} type="button" role="tab" aria-selected={active} onClick={() => onChange(t.key)}
            className={`inline-flex h-9 items-center gap-1.5 rounded-full border px-4 text-sm font-semibold transition ${active ? 'border-d-primary bg-d-primary text-white shadow-sm' : 'border-d-border-subtle bg-white text-d-on-surface-variant hover:bg-d-surface-container'}`}>
            {t.icon && <t.icon size={15} aria-hidden="true" />}{t.label}
            {t.badge > 0 && (
              <span aria-label={`${t.badge} mục cần xử lý`} className={`ml-0.5 inline-flex min-w-5 items-center justify-center rounded-full px-1.5 text-[11px] font-black ${active ? 'bg-white text-d-primary' : 'bg-red-600 text-white'}`}>{t.badge > 99 ? '99+' : t.badge}</span>
            )}
          </button>
        );
      })}
    </div>
  );
}

// Tab dạng gạch chân cho chuyển nội dung trong một trang.
export function UnderlineTabs({ tabs, value, onChange, className }) {
  return (
    <div role="tablist" className={cn('flex gap-1 overflow-x-auto border-b border-d-border-subtle', className)}>
      {tabs.map((t) => {
        const active = value === t.key;
        return (
          <button key={t.key} type="button" role="tab" aria-selected={active} onClick={() => onChange(t.key)}
            className={`-mb-px inline-flex items-center gap-1.5 whitespace-nowrap border-b-2 px-4 pb-3 pt-1 text-sm font-semibold transition ${active ? 'border-d-primary text-d-primary' : 'border-transparent text-d-on-surface-variant hover:text-d-on-surface'}`}>
            {t.icon && <t.icon size={15} aria-hidden="true" />}{t.label}
          </button>
        );
      })}
    </div>
  );
}

// ── Bảng ──
export const Th = ({ children, className = '' }) => <th className={cn(ui.th, className)}>{children}</th>;
export const Td = ({ children, className = '' }) => <td className={cn(ui.td, className)}>{children}</td>;

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

// ── Trạng thái tải / rỗng / lỗi ──
export function Spinner({ size = 20, className, label = 'Đang tải…' }) {
  return <Loader2 size={size} role="status" aria-label={label} className={cn('animate-spin text-d-primary', className)} />;
}

export function LoadingState({ children = 'Đang tải dữ liệu…', className }) {
  return <div role="status" className={cn('flex items-center justify-center gap-2.5 p-10 text-sm text-d-on-surface-variant', className)}><Loader2 size={18} className="animate-spin text-d-primary" />{children}</div>;
}

export function EmptyState({ icon: Icon = Inbox, title, description, action, className }) {
  return (
    <div role="status" className={cn('flex flex-col items-center gap-2 px-6 py-12 text-center', className)}>
      <span className="mb-1 flex size-14 items-center justify-center rounded-2xl bg-d-surface-container text-d-on-surface-variant"><Icon size={26} aria-hidden="true" /></span>
      {title && <p className="font-bold text-d-on-surface">{title}</p>}
      {description && <p className="max-w-md text-sm text-d-on-surface-variant">{description}</p>}
      {action && <div className="mt-2">{action}</div>}
    </div>
  );
}

export function ErrorState({ children, onRetry, className }) {
  return (
    <div role="alert" className={cn('flex flex-wrap items-center gap-3 rounded-2xl border border-d-error/20 bg-d-error-container/40 p-4 text-sm text-d-on-error-container', className)}>
      <AlertCircle size={18} className="shrink-0 text-d-error" aria-hidden="true" />
      <span className="min-w-0 flex-1">{children}</span>
      {onRetry && <Button variant="secondary" size="sm" onClick={onRetry}>Thử lại</Button>}
    </div>
  );
}

// ── Khác ──
export function IconAction({ icon: Icon, title, onClick, tone = 'default', disabled }) {
  const hover = { default: 'hover:bg-d-primary/10 hover:text-d-primary', danger: 'hover:bg-d-error-container/60 hover:text-d-error', warn: 'hover:bg-amber-50 hover:text-amber-600' }[tone];
  return (
    <button type="button" title={title} aria-label={title} onClick={onClick} disabled={disabled}
      className={`flex size-9 items-center justify-center rounded-full text-d-on-surface-variant transition disabled:opacity-40 ${hover}`}>
      <Icon size={18} />
    </button>
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
export function FormFooter({ onCancel, submitting, submitLabel, formId, icon }) {
  return (
    <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
      <Button variant="secondary" onClick={onCancel} disabled={submitting}>Hủy</Button>
      <Button type="submit" form={formId} loading={submitting} icon={icon}>{submitLabel}</Button>
    </div>
  );
}

export { TONES };
