import { useEffect, useRef } from 'react';
import { createPortal } from 'react-dom';
import { Gavel, X } from 'lucide-react';
import { TONES } from './modalTheme';

// Dùng chung khung popup đấu giá: header/footer cố định, nội dung cuộn độc lập.
export default function Modal({ icon: Icon = Gavel, tone = 'primary', title, subtitle, badge, onClose,
  busy = false, footer, size = 'lg', closeLabel = 'Đóng', children }) {
  const ref = useRef(null);
  useEffect(() => {
    const dialog = ref.current;
    const overflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    dialog.showModal();
    return () => { dialog.close(); document.body.style.overflow = overflow; };
  }, []);
  const width = { sm: 'sm:max-w-md', md: 'sm:max-w-xl', lg: 'sm:max-w-3xl', xl: 'sm:max-w-5xl' }[size];
  const colors = TONES[tone] ?? TONES.primary;
  return createPortal(
    <dialog ref={ref} aria-label={title} onCancel={e => { e.preventDefault(); if (!busy) onClose?.(); }}
      onClick={e => { if (e.target === e.currentTarget && !busy) onClose?.(); }}
      className={`fixed inset-0 m-0 mt-auto sm:m-auto w-full sm:w-[calc(100%-2rem)] max-w-none ${width} max-h-[94dvh] sm:max-h-[90dvh] overflow-hidden border-0 bg-transparent p-0 text-d-on-surface backdrop:bg-slate-900/50 backdrop:backdrop-blur-[2px] outline-none`}>
      <div className="flex max-h-[94dvh] sm:max-h-[90dvh] flex-col overflow-hidden rounded-t-3xl sm:rounded-3xl bg-d-surface font-d-body-md shadow-2xl">
        <header className="shrink-0 border-b border-d-border-subtle bg-white">
          <div className={`h-1.5 w-full ${colors.bar}`} />
          <div className="flex items-start gap-3 px-4 sm:px-6 py-4">
            <span className={`flex size-11 shrink-0 items-center justify-center rounded-2xl ${colors.chip}`}><Icon size={22} aria-hidden="true" /></span>
            <div className="min-w-0 flex-1"><h2 className="text-lg font-bold leading-snug break-words">{title}</h2>
              {(subtitle || badge) && <div className="mt-1 flex flex-wrap items-center gap-2 text-sm text-d-on-surface-variant">{badge}{subtitle && <span className="break-words">{subtitle}</span>}</div>}
            </div>
            <button type="button" disabled={busy} aria-label={closeLabel} onClick={onClose}
              className="flex size-11 shrink-0 items-center justify-center rounded-full bg-d-surface-container text-d-on-surface-variant transition hover:bg-d-error-container hover:text-d-error focus-visible:outline-2 focus-visible:outline-d-primary disabled:opacity-40"><X size={20} /></button>
          </div>
        </header>
        <div data-modal-body className="min-h-0 flex-1 overflow-y-auto overscroll-contain px-4 sm:px-6 py-5 space-y-5">{children}</div>
        {footer && <footer className="shrink-0 border-t border-d-border-subtle bg-white px-4 sm:px-6 py-3 pb-[max(0.75rem,env(safe-area-inset-bottom))]">{footer}</footer>}
      </div>
    </dialog>, document.body);
}
