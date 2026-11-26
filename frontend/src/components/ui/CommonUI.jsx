// Giữ API cũ (Input/Badge/Card/StatCard) nhưng dùng bộ thành phần chuẩn RETRACK.
import { cn } from '@/lib/cn';
import { ui } from '@/components/ui/kit';
export { Input, StatCard } from '@/components/ui/kit';

export function Badge({ children, className }) {
  return <span className={cn('inline-flex items-center rounded-full border border-d-border-subtle bg-d-surface-container px-2.5 py-0.5 text-xs font-medium text-d-on-surface-variant', className)}>{children}</span>;
}

export function Card({ children, className, title }) {
  return (
    <div className={cn(ui.card, 'p-5', className)}>
      {title && <h3 className="mb-4 text-base font-bold text-d-on-surface">{title}</h3>}
      {children}
    </div>
  );
}
