import { MaterialIcon } from './MaterialIcon';

export default function Pagination({ page, pageSize, totalCount, onPageChange }) {
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  return (
    <div className="p-4 border-t border-d-border-subtle bg-white flex items-center justify-between text-d-on-surface-variant font-d-body-sm text-d-body-sm">
      <div>{totalCount === 0 ? 'Không có dữ liệu' : `Trang ${page}/${totalPages} — ${totalCount} kết quả`}</div>
      <div className="flex gap-1">
        <button
          onClick={() => onPageChange(page - 1)}
          disabled={page <= 1}
          className="w-8 h-8 flex items-center justify-center rounded-md hover:bg-d-surface-container transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
        >
          <MaterialIcon name="chevron_left" className="text-[18px]" />
        </button>
        <span className="w-8 h-8 flex items-center justify-center rounded-md bg-d-secondary text-white font-medium">{page}</span>
        <button
          onClick={() => onPageChange(page + 1)}
          disabled={page >= totalPages}
          className="w-8 h-8 flex items-center justify-center rounded-md hover:bg-d-surface-container transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
        >
          <MaterialIcon name="chevron_right" className="text-[18px]" />
        </button>
      </div>
    </div>
  );
}
