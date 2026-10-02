import { useEffect, useRef } from 'react';
import { MaterialIcon } from '@/components/ui/MaterialIcon';
import { depotError } from '../DepotContext';

export const inputClass = 'block w-full rounded-xl border border-d-border-subtle bg-d-surface-container-lowest p-3 mt-1 text-d-on-surface';
export const buttonClass = 'rounded-full px-5 py-2.5 bg-d-primary-container text-d-on-primary-container font-semibold disabled:opacity-40';
export const cellClass = 'px-6 py-4 border-b border-d-border-subtle';
export function Page({ title, description, action, children }) {
  return <div className="p-4 md:p-6 flex flex-col gap-6 bg-d-surface min-h-full"><header className="flex flex-wrap justify-between items-end gap-4"><div>
    <h1 className="font-d-headline-lg text-d-headline-lg text-d-on-surface">{title}</h1><p className="text-d-on-surface-variant mt-2">{description}</p>
  </div>{action}</header>{children}</div>;
}
export function Cards({ items }) {
  return <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-6">{items.map(({ label, value, icon = 'monitoring' }) =>
    <div key={label} className="bg-d-surface-container-lowest border border-d-border-subtle rounded-[20px] p-6 shadow-sm">
      <MaterialIcon name={icon} className="text-d-primary bg-d-surface-accent rounded-xl p-2 mb-4" />
      <p className="text-d-on-surface-variant text-sm">{label}</p><p className="text-2xl font-bold text-d-on-surface mt-2 break-words">{value}</p>
    </div>)}</div>;
}
export function QueryState({ query, children }) {
  if (query.isPending) return <p role="status" className="p-8">Đang tải dữ liệu…</p>;
  if (query.isError) return <div role="alert" className="p-6 text-d-error">{depotError(query.error)} <button className={buttonClass} onClick={() => query.refetch()}>Thử lại</button></div>;
  return children;
}
export function GridTable({ headers, children, empty }) {
  return <div className="bg-white rounded-[20px] border border-d-border-subtle overflow-x-auto shadow-sm"><table className="w-full text-left"><thead className="bg-d-surface-container-low text-d-on-surface-variant"><tr>
    {headers.map((h) => <th key={h} className={cellClass + ' whitespace-nowrap font-medium'}>{h}</th>)}</tr></thead><tbody>{children}</tbody></table>
    {empty && <p role="status" className="p-8 text-center">Chưa có dữ liệu phù hợp.</p>}</div>;
}
export function Pager({ page, setPage, total = 0, size = 20 }) {
  const pagerBtn = "px-3 py-1.5 rounded-lg border border-d-border-subtle bg-d-surface hover:bg-d-surface-container-low disabled:opacity-40 disabled:hover:bg-d-surface transition-colors font-medium";
  return <div className="flex justify-between items-center gap-4 text-sm text-d-on-surface-variant"><span>Trang {page} · {total} mục</span><div className="flex gap-2">
    <button type="button" className={pagerBtn} disabled={page === 1} onClick={() => setPage(page - 1)}>Trước</button>
    <button type="button" className={pagerBtn} disabled={page * size >= total} onClick={() => setPage(page + 1)}>Sau</button></div></div>;
}
export function Dialog({ title, onClose, busy, children }) {
  const ref = useRef(null);
  useEffect(() => { ref.current.showModal(); }, []);
  return <dialog ref={ref} aria-label={title} onCancel={(e) => { if (busy) e.preventDefault(); else onClose(); }}
    className="m-auto w-[calc(100%-2rem)] max-w-xl max-h-[90vh] rounded-2xl p-6 border border-d-border-subtle bg-d-surface backdrop:bg-black/40">
    <div className="flex justify-between gap-4 mb-6"><h2 className="text-xl font-bold">{title}</h2><button disabled={busy} aria-label="Đóng" onClick={onClose}>✕</button></div>{children}</dialog>;
}
export function MutationError({ mutation }) { return mutation.isError ? <p role="alert" className="text-d-error my-3">{depotError(mutation.error)}</p> : null; }
export function PeriodFilter({ period, setPeriod }) {
  return <div className="flex flex-wrap gap-4 bg-white rounded-2xl p-4 border border-d-border-subtle">
    <label>Từ ngày<input type="date" className={inputClass} value={period.from ?? ''} onChange={(e) => setPeriod({ ...period, from: e.target.value || undefined })} /></label>
    <label>Đến ngày<input type="date" className={inputClass} value={period.to ?? ''} onChange={(e) => setPeriod({ ...period, to: e.target.value || undefined })} /></label>
    <label>Nhóm theo<select className={inputClass} value={period.groupBy ?? 'day'} onChange={(e) => setPeriod({ ...period, groupBy: e.target.value })}><option value="day">Ngày</option><option value="week">Tuần</option><option value="month">Tháng</option></select></label>
  </div>;
}
