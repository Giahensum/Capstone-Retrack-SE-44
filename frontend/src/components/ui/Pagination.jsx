import { ChevronLeft, ChevronRight } from 'lucide-react';

export default function Pagination({ page, pageSize, totalCount, onPageChange }) {
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const btn = 'flex size-9 items-center justify-center rounded-full border border-d-border-subtle bg-white text-d-on-surface-variant transition hover:bg-d-surface-container disabled:cursor-not-allowed disabled:opacity-40';
  return (
    <div className="flex items-center justify-between gap-3 border-t border-d-border-subtle bg-white px-5 py-3.5 text-sm text-d-on-surface-variant">
      <div>{totalCount === 0 ? 'Không có dữ liệu' : `Trang ${page}/${totalPages} — ${totalCount} kết quả`}</div>
      <div className="flex items-center gap-1.5">
        <button type="button" aria-label="Trang trước" onClick={() => onPageChange(page - 1)} disabled={page <= 1} className={btn}><ChevronLeft size={17} /></button>
        <span className="flex size-9 items-center justify-center rounded-full bg-d-primary font-bold text-white">{page}</span>
        <button type="button" aria-label="Trang sau" onClick={() => onPageChange(page + 1)} disabled={page >= totalPages} className={btn}><ChevronRight size={17} /></button>
      </div>
    </div>
  );
}
