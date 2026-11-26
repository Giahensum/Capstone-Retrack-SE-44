import { useEffect } from 'react';
import Modal from '@/components/ui/Modal';
import { ClipboardList, ChevronLeft, ChevronRight } from 'lucide-react';
import { ui, PageHeader, LoadingState, ErrorState, EmptyState } from '@/components/ui/kit';
import { MaterialIcon } from './DepotIcon';
import { depotError } from '../DepotContext';

// Token class dùng chung với bộ chuẩn RETRACK (components/ui/kit); giữ tên cũ cho các trang Depot.
export const inputClass = `${ui.input} mt-1`;
export const buttonClass = 'inline-flex items-center justify-center gap-2 min-h-11 rounded-full px-5 py-2.5 bg-d-primary text-white text-sm font-semibold shadow-sm transition hover:bg-[#385500] active:scale-[.98] outline-none focus-visible:ring-2 focus-visible:ring-d-primary/30 focus-visible:ring-offset-1 disabled:pointer-events-none disabled:opacity-50';
export const secondaryButtonClass = 'inline-flex items-center justify-center gap-2 min-h-11 rounded-full border border-d-border-subtle bg-white px-5 py-2.5 text-sm font-semibold text-d-on-surface transition hover:bg-d-surface-container active:scale-[.98] outline-none focus-visible:ring-2 focus-visible:ring-d-primary/30 focus-visible:ring-offset-1 disabled:pointer-events-none disabled:opacity-50';
export const cellClass = 'px-5 py-3.5 border-b border-d-border-subtle';
export function Page({ title, description, action, icon, children }) {
  return <div className="mx-auto flex min-h-full w-full max-w-7xl flex-col gap-6 p-4 md:p-6">
    <PageHeader icon={icon} title={title} description={description}>{action}</PageHeader>
    {children}
  </div>;
}
export function Cards({ items }) {
  const columns = items.length >= 4 ? 'xl:grid-cols-4' : items.length === 3 ? 'lg:grid-cols-3' : '';
  return <div className={`grid grid-cols-1 sm:grid-cols-2 ${columns} gap-4`}>{items.map(({ label, value, icon = 'monitoring', description }) =>
    <div key={label} className="group relative overflow-hidden rounded-2xl border border-d-border-subtle bg-white p-5 shadow-sm transition hover:-translate-y-0.5 hover:shadow-md">
      <div className="absolute -right-5 -top-5 size-24 rounded-full bg-d-primary/5 transition group-hover:bg-d-primary/10" />
      <div className="relative flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="text-xs font-semibold text-d-on-surface-variant">{label}</p>
          <p className="mt-1.5 break-words text-2xl font-black tracking-tight text-d-on-surface">{value}</p>
        </div>
        <span className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-d-primary/10 text-d-primary"><MaterialIcon name={icon} className="text-xl" /></span>
      </div>
      {description && <p className="relative mt-3 text-sm leading-5 text-d-on-surface-variant">{description}</p>}
    </div>)}</div>;
}
export function QueryState({ query, children }) {
  if (query.isPending) return <LoadingState />;
  if (query.isError) return <ErrorState onRetry={() => query.refetch()}>{depotError(query.error)}</ErrorState>;
  return children;
}
export function GridTable({ headers, children, empty }) {
  return <div className="overflow-x-auto rounded-2xl border border-d-border-subtle bg-white shadow-sm"><table className="w-full text-left text-sm"><thead className="bg-d-surface-container-low text-d-on-surface-variant"><tr>
    {headers.map((h) => <th key={h} className={cellClass + ' whitespace-nowrap text-[11px] font-bold uppercase tracking-wider'}>{h}</th>)}</tr></thead><tbody className="text-d-on-surface">{children}</tbody></table>
    {empty && <EmptyState title="Chưa có dữ liệu phù hợp" description="Thử đổi bộ lọc hoặc quay lại sau." />}</div>;
}
export function Pager({ page, setPage, total, size = 20 }) {
  const pages = Math.max(1, Math.ceil((total ?? 0) / size));
  useEffect(() => {
    if (total != null && page > pages) setPage(pages);
  }, [page, pages, setPage, total]);
  if (total == null) return null;
  const pagerBtn = 'flex size-10 items-center justify-center rounded-full border border-d-border-subtle bg-white text-d-on-surface-variant transition hover:bg-d-surface-container disabled:cursor-not-allowed disabled:opacity-40';
  return <div className="flex w-full flex-wrap items-center justify-between gap-3 text-sm text-d-on-surface-variant"><span>Trang {page}/{pages} · {total} mục</span><div className="flex items-center gap-1.5">
    <button type="button" aria-label="Trang trước" className={pagerBtn} disabled={page === 1} onClick={() => setPage(page - 1)}><ChevronLeft size={17} /></button>
    <span className="flex size-10 items-center justify-center rounded-full bg-d-primary font-bold text-white">{page}</span>
    <button type="button" aria-label="Trang sau" className={pagerBtn} disabled={page * size >= total} onClick={() => setPage(page + 1)}><ChevronRight size={17} /></button></div></div>;
}
export function Dialog({ wide = false, icon = ClipboardList, ...props }) {
  return <Modal {...props} icon={icon} size={wide ? "lg" : "md"} />;
}
export function MutationError({ mutation }) { return mutation.isError ? <p role="alert" className="my-3 flex items-start gap-2 rounded-xl bg-d-error-container/40 px-3 py-2 text-sm text-d-on-error-container">{depotError(mutation.error)}</p> : null; }
export function PeriodFilter({ period, setPeriod, showGroupBy = true }) {
  return <div className="flex flex-wrap gap-4 rounded-2xl border border-d-border-subtle bg-white p-4 text-sm font-semibold text-d-on-surface-variant shadow-sm">
    <label>Từ ngày<input type="date" className={inputClass} value={period.from ?? ''} onChange={(e) => setPeriod({ ...period, from: e.target.value || undefined })} /></label>
    <label>Đến ngày<input type="date" className={inputClass} value={period.to ?? ''} onChange={(e) => setPeriod({ ...period, to: e.target.value || undefined })} /></label>
    {showGroupBy && <label>Nhóm theo<select className={inputClass} value={period.groupBy ?? 'day'} onChange={(e) => setPeriod({ ...period, groupBy: e.target.value })}><option value="day">Ngày</option><option value="week">Tuần</option><option value="month">Tháng</option></select></label>}
  </div>;
}
