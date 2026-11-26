import { useState } from 'react';
import { useQuery, keepPreviousData } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { sellerApi } from '../api';
import { formatCurrency, formatDate } from '@/lib/utils';
import { MaterialIcon } from '@/features/depot/components/DepotIcon';
import { LIQUIDATION_FILTERS, LIQUIDATION_PHASE_LABEL } from '../liquidationLabels';
import { MapPin, Coins, TrendingUp, Users, Timer } from 'lucide-react';
import { Badge, PHASE_TONE } from '@/components/liquidation/LiquidationUI';
import { PageHeader, buttonClass } from '@/components/ui/kit';

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
      <PageHeader title="Lô thanh lý của tôi" description="Đăng phiên nhiều hạng mục để các chủ kho khảo sát và trả giá theo đơn giá từng hạng mục. Tiền và giao nhận do hai bên tự thực hiện.">
        <Link to="/seller/liquidations/new" className={buttonClass()}><MaterialIcon name="add" className="text-lg" />Đăng phiên mới</Link>
      </PageHeader>

      <V2Campaigns />

      <h2 className="text-sm font-bold text-d-on-surface">Lô trọn gói đã đăng trước đây</h2>
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
                  <Badge tone={PHASE_TONE[lot.phase] ?? 'slate'} className="mb-2">
                    {LIQUIDATION_PHASE_LABEL[lot.phase] ?? lot.phase}
                  </Badge>
                  <p className="text-base font-bold text-d-on-surface truncate group-hover:text-d-primary transition-colors">{lot.title}</p>
                  <p className="mt-1 flex items-center gap-1.5 truncate text-xs text-d-on-surface-variant"><MapPin size={13} />{lot.publicArea}</p>
                  <div className="mt-3 flex flex-wrap items-center gap-2 text-xs">
                    <span className="inline-flex items-center gap-1.5 rounded-full bg-d-surface-container px-3 py-1 text-d-on-surface-variant"><Coins size={13} />Khởi điểm {formatCurrency(lot.startingPrice)}</span>
                    <span className="inline-flex items-center gap-1.5 rounded-full bg-d-primary/10 px-3 py-1 font-bold text-d-primary"><TrendingUp size={13} />{lot.highestPrice ? `Cao nhất ${formatCurrency(lot.highestPrice)}` : 'Chưa có lượt trả giá'}</span>
                    <span className="inline-flex items-center gap-1.5 rounded-full bg-blue-50 px-3 py-1 text-blue-700"><Users size={13} />{lot.bidCount} lượt • {lot.participantCount} kho</span>
                    <span className="inline-flex items-center gap-1.5 rounded-full bg-amber-50 px-3 py-1 text-amber-700"><Timer size={13} />Đóng {formatDate(lot.biddingEnd)}</span>
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

// Phiên nhiều hạng mục (V2): phiên mới đăng từ nay; lô trọn gói cũ vẫn hiển thị bên dưới để hoàn tất.
function V2Campaigns() {
  const [page, setPage] = useState(1);
  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['seller-liquidations-v2', 'list', page],
    queryFn: () => sellerApi.getLiquidationsV2({ page, pageSize: 6 }),
    placeholderData: keepPreviousData, refetchInterval: 30000,
  });
  const items = data?.items ?? [];
  const totalPages = Math.max(1, Math.ceil((data?.totalCount ?? 0) / 6));
  return (
    <section className="space-y-3">
      <h2 className="text-sm font-bold text-d-on-surface">Phiên nhiều hạng mục</h2>
      {isLoading ? <p role="status" className="text-sm text-d-on-surface-variant">Đang tải…</p>
        : isError ? <p role="alert" className="text-sm text-d-error">Không tải được phiên. <button className="underline" onClick={() => refetch()}>Thử lại</button></p>
        : !items.length ? <p className="rounded-2xl border border-d-border-subtle bg-white p-6 text-center text-sm text-d-on-surface-variant">Chưa có phiên nhiều hạng mục. Bấm “Đăng phiên mới” để bắt đầu.</p>
        : <div className="grid gap-3 md:grid-cols-2">{items.map(({ campaign: c, participantCount }) => (
          <Link key={c.id} to={`/seller/liquidations/v2/${c.id}`} className="group flex flex-col gap-2 rounded-2xl border border-d-border-subtle bg-white p-4 shadow-sm transition hover:border-d-primary/40">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <h3 className="font-bold text-d-on-surface group-hover:text-d-primary">{c.title}</h3>
              <Badge tone={PHASE_TONE[c.phase] ?? 'slate'}>{LIQUIDATION_PHASE_LABEL[c.phase] ?? c.phase}</Badge>
            </div>
            <p className="flex items-center gap-1.5 text-xs text-d-on-surface-variant"><MapPin size={13} />{c.publicArea}</p>
            <p className="text-sm text-d-on-surface-variant">{c.itemCount} hạng mục: {c.itemNames.join(' · ')}{c.itemCount > c.itemNames.length ? ' …' : ''}</p>
            <div className="flex flex-wrap gap-4 text-xs text-d-on-surface-variant">
              <span className="inline-flex items-center gap-1"><Users size={13} />{participantCount} kho</span>
              <span className="inline-flex items-center gap-1"><TrendingUp size={13} />{c.bidCount} lượt giá</span>
              <span className="inline-flex items-center gap-1"><Timer size={13} />Đóng {formatDate(c.biddingEnd)}</span>
              {c.wonItemCount > 0 && <span className="inline-flex items-center gap-1 font-semibold text-d-primary"><Coins size={13} />{c.wonItemCount}/{c.itemCount} hạng mục đã bán</span>}
            </div>
          </Link>))}</div>}
      {totalPages > 1 && <div className="flex justify-end gap-2 text-sm">
        <button disabled={page === 1} onClick={() => setPage(page - 1)} className="rounded-full border border-d-border-subtle px-4 py-1.5 disabled:opacity-40">Trước</button>
        <span className="py-1.5">{page}/{totalPages}</span>
        <button disabled={page >= totalPages} onClick={() => setPage(page + 1)} className="rounded-full border border-d-border-subtle px-4 py-1.5 disabled:opacity-40">Sau</button>
      </div>}
    </section>
  );
}
