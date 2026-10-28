import { useState } from 'react';
import { useQuery, keepPreviousData } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { sellerApi } from '../api';
import { formatCurrency, formatDate } from '@/lib/utils';
import { MaterialIcon } from '@/features/depot/components/DepotIcon';
import { LIQUIDATION_FILTERS, LIQUIDATION_PHASE_LABEL, LIQUIDATION_PHASE_STYLE } from '../liquidationLabels';

export default function LiquidationList() {
  const [status, setStatus] = useState('');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['seller-liquidations', status, search, page],
    queryFn: () => sellerApi.getLiquidations({ status: status || undefined, search: search || undefined, page, pageSize: 10 }),
    placeholderData: keepPreviousData,
    refetchInterval: 30000,
  });
  const items = data?.items ?? [];
  const totalPages = Math.max(1, Math.ceil((data?.totalCount ?? 0) / 10));

  return (
    <div className="flex flex-col p-4 md:p-6 gap-6 w-full max-w-7xl mx-auto">
      <div className="flex flex-col md:flex-row md:items-end justify-between gap-4">
        <div>
          <h1 className="text-d-headline-xl-mobile md:text-d-headline-xl font-d-headline-xl text-d-on-surface">Lô thanh lý của tôi</h1>
          <p className="text-d-body-md text-d-on-surface-variant mt-2">
            Đăng lô để các chủ kho vựa khảo sát và trả giá trọn lô. Tiền và giao nhận do hai bên thực hiện ngoài hệ thống.
          </p>
        </div>
        <Link
          to="/seller/liquidations/new"
          className="inline-flex items-center justify-center gap-2 rounded-full bg-d-primary hover:bg-[#385500] text-d-on-primary font-semibold px-6 py-3 transition-all shadow-sm active:scale-95 text-sm shrink-0"
        >
          <MaterialIcon name="add" className="text-lg" />
          <span>Đăng lô mới</span>
        </Link>
      </div>

      <div className="flex gap-2 overflow-x-auto pb-1 scrollbar-hide">
        {LIQUIDATION_FILTERS.map((tab) => (
          <button
            key={tab.key}
            type="button"
            onClick={() => { setStatus(tab.key); setPage(1); }}
            className={`px-4 py-2 rounded-full text-xs font-semibold whitespace-nowrap transition-all ${
              status === tab.key
                ? 'bg-d-primary text-d-on-primary shadow-xs'
                : 'bg-d-surface-container-lowest border border-d-border-subtle text-d-on-surface-variant hover:bg-d-surface-container-low'
            }`}
          >
            {tab.label}
          </button>
        ))}
      </div>

      <div className="relative">
        <MaterialIcon name="search" className="absolute left-4 top-1/2 -translate-y-1/2 text-d-on-surface-variant text-lg" />
        <label htmlFor="liquidation-search" className="sr-only">Tìm lô thanh lý</label>
        <input
          id="liquidation-search"
          value={search}
          maxLength={255}
          onChange={(e) => { setSearch(e.target.value); setPage(1); }}
          placeholder="Tìm theo tiêu đề hoặc khu vực..."
          className="w-full bg-d-surface-container-lowest border border-d-border-subtle rounded-xl pl-11 pr-4 py-3 text-sm text-d-on-surface focus:border-d-primary focus:ring-1 focus:ring-d-primary outline-none transition-all"
        />
      </div>

      {isLoading ? (
        <div className="flex justify-center py-20">
          <div className="w-8 h-8 border-3 border-d-primary border-t-transparent rounded-full animate-spin" />
        </div>
      ) : isError ? (
        <div className="bg-d-surface-container-lowest border border-red-200 rounded-2xl p-8 text-center">
          <p className="text-sm font-semibold text-red-700">Không tải được danh sách lô thanh lý.</p>
          <button type="button" onClick={() => refetch()} className="mt-3 text-sm font-semibold text-d-primary underline">Thử lại</button>
        </div>
      ) : items.length === 0 ? (
        <div className="bg-d-surface-container-lowest border border-d-border-subtle rounded-2xl p-12 text-center shadow-sm">
          <div className="w-14 h-14 rounded-full bg-d-surface-container flex items-center justify-center mx-auto mb-3 text-d-on-surface-variant">
            <MaterialIcon name="gavel" className="text-2xl" />
          </div>
          <p className="text-sm font-semibold text-d-on-surface">Chưa có lô thanh lý nào</p>
          <p className="text-xs text-d-on-surface-variant mt-1">Đăng lô đầu tiên hoặc đổi bộ lọc.</p>
        </div>
      ) : (
        <div className="space-y-3">
          {items.map((lot) => (
            <Link
              key={lot.id}
              to={`/seller/liquidations/${lot.id}`}
              className="block bg-d-surface-container-lowest border border-d-border-subtle rounded-2xl p-5 hover:border-d-primary hover:shadow-sm transition-all group"
            >
              <div className="flex items-start justify-between gap-4">
                <div className="flex-1 min-w-0">
                  <span className={`inline-block text-[11px] font-bold px-3 py-1 rounded-full border mb-2 ${LIQUIDATION_PHASE_STYLE[lot.phase] ?? ''}`}>
                    {LIQUIDATION_PHASE_LABEL[lot.phase] ?? lot.phase}
                  </span>
                  <p className="text-sm font-bold text-d-on-surface truncate group-hover:text-d-primary transition-colors">{lot.title}</p>
                  <p className="text-xs text-d-on-surface-variant mt-1 truncate">📍 {lot.publicArea}</p>
                  <div className="flex flex-wrap items-center gap-x-4 gap-y-1 mt-3 text-xs text-d-on-surface-variant">
                    <span>Khởi điểm {formatCurrency(lot.startingPrice)}</span>
                    <span className="font-bold text-d-primary">
                      {lot.highestPrice ? `Cao nhất ${formatCurrency(lot.highestPrice)}` : 'Chưa có lượt trả giá'}
                    </span>
                    <span>{lot.bidCount} lượt • {lot.participantCount} kho đăng ký</span>
                    <span>🕒 Đóng {formatDate(lot.biddingEnd)}</span>
                  </div>
                </div>
                <MaterialIcon name="chevron_right" className="text-d-on-surface-variant group-hover:text-d-primary text-xl transition-colors self-center" />
              </div>
            </Link>
          ))}
        </div>
      )}

      {totalPages > 1 && (
        <div className="flex items-center justify-center gap-3 text-sm">
          <button type="button" disabled={page <= 1} onClick={() => setPage((p) => p - 1)} className="px-4 py-2 rounded-full border border-d-border-subtle disabled:opacity-40">Trước</button>
          <span className="text-d-on-surface-variant">Trang {page}/{totalPages}</span>
          <button type="button" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)} className="px-4 py-2 rounded-full border border-d-border-subtle disabled:opacity-40">Sau</button>
        </div>
      )}
    </div>
  );
}
