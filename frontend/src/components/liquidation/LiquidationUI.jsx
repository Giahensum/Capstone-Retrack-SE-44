import { useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import {
  X, Check, Gavel, Clock, Timer, TrendingUp, Trophy, Wallet, Crown, ChevronRight,
} from 'lucide-react';
import { formatCurrency } from '@/lib/utils';

// ── Màu theo "tông" (dựa trên bảng màu Retrack: xanh lá chủ đạo + vài màu trạng thái) ──
export const TONES = {
  primary: { chip: 'bg-d-primary/10 text-d-primary', solid: 'bg-d-primary text-d-on-primary', ring: 'ring-d-primary/30', bar: 'bg-d-primary' },
  lime: { chip: 'bg-d-primary-container/40 text-d-on-primary-container', solid: 'bg-d-primary-container text-d-on-primary-container', ring: 'ring-d-primary-container', bar: 'bg-d-primary-container' },
  teal: { chip: 'bg-teal-50 text-teal-700', solid: 'bg-teal-600 text-white', ring: 'ring-teal-200', bar: 'bg-teal-500' },
  blue: { chip: 'bg-blue-50 text-blue-700', solid: 'bg-blue-600 text-white', ring: 'ring-blue-200', bar: 'bg-blue-500' },
  amber: { chip: 'bg-amber-50 text-amber-700', solid: 'bg-amber-500 text-white', ring: 'ring-amber-200', bar: 'bg-amber-500' },
  red: { chip: 'bg-red-50 text-red-700', solid: 'bg-red-600 text-white', ring: 'ring-red-200', bar: 'bg-red-500' },
  slate: { chip: 'bg-slate-100 text-slate-600', solid: 'bg-slate-600 text-white', ring: 'ring-slate-200', bar: 'bg-slate-400' },
};

export const PHASE_TONE = {
  UPCOMING: 'slate', SURVEY: 'blue', BIDDING: 'primary', CLOSING: 'amber', AWARDED: 'teal', NO_SALE: 'slate', CANCELLED: 'red',
};

export function Badge({ tone = 'slate', icon: Icon, children, className = '' }) {
  return (
    <span className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-bold ${TONES[tone].chip} ${className}`}>
      {Icon && <Icon size={13} aria-hidden="true" />}
      {children}
    </span>
  );
}

// ── Modal: header + footer cố định, chỉ phần thân cuộn → nút X luôn nhìn thấy ──
// Đóng bằng: nút X, phím Esc, bấm vùng nền tối. Khi `busy` (đang gửi) thì khóa việc đóng.
export function Modal({ icon: Icon = Gavel, tone = 'primary', title, subtitle, badge, onClose, busy = false, footer, size = 'lg', children }) {
  const panel = useRef(null);
  const closeRef = useRef(onClose);
  closeRef.current = onClose;

  useEffect(() => {
    const previous = document.activeElement;
    const overflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    panel.current?.focus();
    const onKey = (e) => { if (e.key === 'Escape' && !busy) { e.stopPropagation(); closeRef.current?.(); } };
    document.addEventListener('keydown', onKey);
    return () => {
      document.body.style.overflow = overflow;
      document.removeEventListener('keydown', onKey);
      previous?.focus?.();
    };
  }, [busy]);

  const width = { sm: 'sm:max-w-md', md: 'sm:max-w-xl', lg: 'sm:max-w-3xl', xl: 'sm:max-w-5xl' }[size];
  return createPortal(
    <div className="fixed inset-0 z-[100] flex items-end sm:items-center justify-center sm:p-4">
      <div className="absolute inset-0 bg-slate-900/50 backdrop-blur-[2px] animate-[fadeIn_.15s_ease-out]" onMouseDown={() => !busy && onClose?.()} aria-hidden="true" />
      <div
        ref={panel}
        role="dialog"
        aria-modal="true"
        aria-label={title}
        tabIndex={-1}
        className={`relative flex w-full ${width} max-h-[94dvh] sm:max-h-[90dvh] flex-col overflow-hidden rounded-t-3xl sm:rounded-3xl bg-d-surface font-d-body-md text-d-on-surface shadow-2xl outline-none animate-[popIn_.18s_ease-out]`}
      >
        <header className="shrink-0 border-b border-d-border-subtle bg-white">
          <div className={`h-1.5 w-full ${TONES[tone].bar}`} />
          <div className="flex items-start gap-3 px-4 sm:px-6 py-4">
            <span className={`flex size-11 shrink-0 items-center justify-center rounded-2xl ${TONES[tone].chip}`}>
              <Icon size={22} aria-hidden="true" />
            </span>
            <div className="min-w-0 flex-1">
              <h2 className="text-lg font-bold leading-snug text-d-on-surface break-words">{title}</h2>
              {(subtitle || badge) && (
                <div className="mt-1 flex flex-wrap items-center gap-2 text-sm text-d-on-surface-variant">
                  {badge}
                  {subtitle && <span className="break-words">{subtitle}</span>}
                </div>
              )}
            </div>
            <button
              type="button"
              onClick={onClose}
              disabled={busy}
              aria-label="Đóng"
              className="flex size-10 shrink-0 items-center justify-center rounded-full bg-d-surface-container text-d-on-surface-variant transition hover:bg-d-error-container hover:text-d-error disabled:opacity-40"
            >
              <X size={20} />
            </button>
          </div>
        </header>
        <div className="min-h-0 flex-1 overflow-y-auto overscroll-contain px-4 sm:px-6 py-5 space-y-5">{children}</div>
        {footer && <footer className="shrink-0 border-t border-d-border-subtle bg-white px-4 sm:px-6 py-3 pb-[max(0.75rem,env(safe-area-inset-bottom))]">{footer}</footer>}
      </div>
    </div>,
    document.body,
  );
}

// Hộp xác nhận nhỏ gọn thay cho nút "Đồng ý / Không" nằm rải trong trang.
export function ConfirmModal({ icon: Icon = Check, tone = 'primary', title, description, confirmLabel = 'Xác nhận', cancelLabel = 'Để sau', onConfirm, onCancel, busy = false, disabled = false, children }) {
  return (
    <Modal
      size="sm" icon={Icon} tone={tone} title={title} onClose={onCancel} busy={busy}
      footer={(
        <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
          <button type="button" onClick={onCancel} disabled={busy} className="rounded-full border border-d-border-subtle bg-white px-5 py-2.5 text-sm font-semibold text-d-on-surface-variant hover:bg-d-surface-container disabled:opacity-50">{cancelLabel}</button>
          <button type="button" onClick={onConfirm} disabled={busy || disabled} className={`rounded-full px-6 py-2.5 text-sm font-bold shadow-sm transition active:scale-95 disabled:opacity-50 ${TONES[tone].solid}`}>
            {busy ? 'Đang xử lý…' : confirmLabel}
          </button>
        </div>
      )}
    >
      {description && <p className="text-sm leading-relaxed text-d-on-surface-variant">{description}</p>}
      {children}
    </Modal>
  );
}

// ── Khối nội dung ──
export function Section({ icon: Icon, title, tone = 'primary', action, children, className = '' }) {
  return (
    <section className={`rounded-2xl border border-d-border-subtle bg-white p-4 sm:p-5 ${className}`}>
      <div className="mb-3 flex items-center gap-2.5">
        {Icon && <span className={`flex size-8 items-center justify-center rounded-xl ${TONES[tone].chip}`}><Icon size={17} aria-hidden="true" /></span>}
        <h3 className="flex-1 text-sm font-bold text-d-on-surface">{title}</h3>
        {action}
      </div>
      {children}
    </section>
  );
}

export function Tile({ icon: Icon, label, value, hint, tone = 'slate', className = '' }) {
  return (
    <div className={`flex items-start gap-3 rounded-2xl border border-d-border-subtle bg-white p-3.5 ${className}`}>
      {Icon && <span className={`flex size-9 shrink-0 items-center justify-center rounded-xl ${TONES[tone].chip}`}><Icon size={17} aria-hidden="true" /></span>}
      <div className="min-w-0">
        <p className="text-xs font-medium text-d-on-surface-variant">{label}</p>
        <p className="mt-0.5 break-words text-sm font-bold text-d-on-surface">{value}</p>
        {hint && <p className="mt-0.5 text-xs text-d-on-surface-variant">{hint}</p>}
      </div>
    </div>
  );
}

export function Notice({ icon: Icon, tone = 'blue', children }) {
  return (
    <div role="status" className={`flex items-start gap-2.5 rounded-2xl px-4 py-3 text-sm ring-1 ${TONES[tone].chip} ${TONES[tone].ring}`}>
      {Icon && <Icon size={17} className="mt-0.5 shrink-0" aria-hidden="true" />}
      <div className="min-w-0 break-words">{children}</div>
    </div>
  );
}

// ── Thanh tiến trình các bước (ví dụ: ký hợp đồng → cọc → thanh toán → bàn giao) ──
export function Steps({ steps }) {
  return (
    <ol className="flex items-start gap-1 overflow-x-auto pb-1" aria-label="Tiến trình">
      {steps.map((s, i) => {
        const done = s.state === 'done', current = s.state === 'current';
        const Icon = s.icon;
        return (
          <li key={s.key} className="flex min-w-[76px] flex-1 flex-col items-center text-center">
            <div className="flex w-full items-center">
              <span className={`h-0.5 flex-1 ${i === 0 ? 'opacity-0' : steps[i - 1].state === 'done' ? 'bg-d-primary' : 'bg-d-border-subtle'}`} />
              <span
                className={`flex size-10 shrink-0 items-center justify-center rounded-full border-2 transition ${
                  done ? 'border-d-primary bg-d-primary text-white'
                    : current ? 'border-d-primary bg-d-primary-container text-d-on-primary-container ring-4 ring-d-primary/15'
                      : 'border-d-border-subtle bg-white text-d-outline'
                }`}
                aria-current={current ? 'step' : undefined}
              >
                {done ? <Check size={18} /> : <Icon size={17} aria-hidden="true" />}
              </span>
              <span className={`h-0.5 flex-1 ${i === steps.length - 1 ? 'opacity-0' : done ? 'bg-d-primary' : 'bg-d-border-subtle'}`} />
            </div>
            <span className={`mt-1.5 text-[11px] leading-tight ${current ? 'font-bold text-d-primary' : done ? 'font-semibold text-d-on-surface' : 'text-d-on-surface-variant'}`}>{s.label}</span>
          </li>
        );
      })}
    </ol>
  );
}

// ── Đồng hồ đếm ngược theo giờ server (bù lệch đồng hồ máy) ──
export function useCountdown(target, serverTime, fetchedAt) {
  const [, setTick] = useState(0);
  useEffect(() => {
    const timer = setInterval(() => setTick((t) => t + 1), 1000);
    return () => clearInterval(timer);
  }, []);
  if (!target || !serverTime) return null;
  const offset = new Date(serverTime).getTime() - fetchedAt;
  return Math.max(0, new Date(target).getTime() - (Date.now() + offset));
}
export function formatRemaining(ms) {
  const total = Math.floor(ms / 1000);
  const d = Math.floor(total / 86400), h = Math.floor((total % 86400) / 3600), m = Math.floor((total % 3600) / 60), s = total % 60;
  return `${d > 0 ? `${d} ngày ` : ''}${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
}

// ── Khối giá hiện tại: dùng cho cả Seller và Chủ kho ──
export function PriceHero({ lot, fetchedAt, leading = false, perspective = 'seller' }) {
  const current = lot.highestPrice ?? lot.startingPrice;
  const nextMin = lot.highestPrice ? lot.highestPrice + lot.bidIncrement : lot.startingPrice;
  const raise = lot.highestPrice ? ((lot.highestPrice - lot.startingPrice) / lot.startingPrice) * 100 : 0;
  const bidding = lot.phase === 'BIDDING' || lot.phase === 'CLOSING';
  const waiting = ['UPCOMING', 'SURVEY', 'BIDDING', 'CLOSING'].includes(lot.phase);
  const remaining = useCountdown(bidding ? lot.biddingEnd : lot.biddingStart, lot.serverTime, fetchedAt);
  const deposit = Math.round((current * (lot.depositPercent ?? 0)) / 100);
  return (
    <section aria-label="Giá hiện tại" aria-live="polite" className="relative overflow-hidden rounded-3xl bg-gradient-to-br from-[#446900] to-[#2d4700] p-5 sm:p-6 text-white shadow-lg">
      <div className="pointer-events-none absolute -right-10 -top-10 size-44 rounded-full bg-white/5" />
      <div className="pointer-events-none absolute -bottom-12 left-10 size-32 rounded-full bg-[#a3e635]/10" />
      <div className="relative flex flex-wrap items-end justify-between gap-5">
        <div>
          <p className="flex items-center gap-2 text-xs font-semibold text-[#a3e635]">
            <TrendingUp size={14} aria-hidden="true" />
            {lot.phase === 'AWARDED' ? `Giá trúng • ${lot.bidCount} lượt` : lot.highestPrice ? `Giá cao nhất hiện tại • ${lot.bidCount} lượt` : 'Giá khởi điểm • chưa có lượt trả giá'}
          </p>
          <p className="mt-1 text-3xl font-black tracking-tight sm:text-4xl">{formatCurrency(current)}</p>
          <p className="mt-1 text-xs text-white/75">
            {lot.highestPrice ? `Cao hơn khởi điểm ${raise.toFixed(1)}%` : 'Giá trọn lô (VND)'}
            {lot.depositPercent > 0 && ` • Cọc ${lot.depositPercent}% ≈ ${formatCurrency(deposit)}`}
          </p>
          {leading && perspective === 'depot' && (
            <span className="mt-3 inline-flex items-center gap-1.5 rounded-full bg-[#a3e635] px-3 py-1 text-xs font-black text-[#2d4700]">
              <Crown size={13} aria-hidden="true" /> Bạn đang dẫn đầu
            </span>
          )}
        </div>
        {waiting && <div className="rounded-2xl bg-white/10 px-4 py-3 text-right backdrop-blur-sm">
          <p className="text-[11px] font-semibold text-white/75">Mức tối thiểu lượt tiếp theo</p>
          <p className="text-xl font-black">{formatCurrency(nextMin)}</p>
          {remaining !== null && (
            <p className="mt-1.5 flex items-center justify-end gap-1.5 text-xs font-semibold text-[#a3e635]">
              <Timer size={13} aria-hidden="true" />
              {bidding ? 'Còn' : 'Mở sau'} <span className="tabular-nums text-sm font-black text-white">{formatRemaining(remaining)}</span>
            </p>
          )}
        </div>}
      </div>
    </section>
  );
}

// Thang giá: thanh tỷ lệ so với giá cao nhất
export function PriceBar({ amount, top, leading }) {
  const pct = Math.min(100, (amount / top) * 100);
  return (
    <div className="h-2 overflow-hidden rounded-full bg-d-surface-container" role="img" aria-label={`${Math.round(pct)}% so với giá cao nhất`}>
      <div className={`h-full rounded-full ${leading ? 'bg-gradient-to-r from-[#446900] to-[#a3e635]' : 'bg-d-primary/45'}`} style={{ width: `${pct}%` }} />
    </div>
  );
}

export const RankIcon = ({ rank }) => rank === 1
  ? <span className="flex size-7 items-center justify-center rounded-full bg-amber-100 text-amber-600"><Trophy size={14} aria-hidden="true" /></span>
  : <span className="flex size-7 items-center justify-center rounded-full bg-d-surface-container text-xs font-bold text-d-on-surface-variant">{rank}</span>;

export { Clock, Wallet, ChevronRight };
