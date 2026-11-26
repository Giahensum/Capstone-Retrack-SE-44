import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { sellerApi } from '../api';
import { formatCurrency, formatDate } from '@/lib/utils';
import { MaterialIcon } from '@/features/depot/components/DepotIcon';
import { PageHeader } from '@/components/ui/kit';

export default function SellerIncome() {
  const { data: stats } = useQuery({
    queryKey: ['seller-stats'],
    queryFn: sellerApi.getStats,
  });

  const { data: requests = [] } = useQuery({
    queryKey: ['seller-requests'],
    queryFn: () => sellerApi.getRequests(),
  });

  const doneRequests = requests.filter((r) => r.status === 'DONE');
  const pickupIncome = doneRequests.reduce(
    (sum, r) => sum + Number(r.netAmount ?? 0),
    0
  );
  // Lô thanh lý đã được Seller xác nhận nhận đủ tiền cũng là thu nhập.
  const liquidationSales = stats?.liquidationSales ?? [];
  const liquidationIncome = Number(stats?.liquidationIncome ?? 0);
  const totalIncome = pickupIncome + liquidationIncome;

  // Gộp hai nguồn thành một danh sách giao dịch, mới nhất trước.
  const transactions = [
    ...doneRequests.map((r) => ({
      key: `pickup-${r.id}`,
      kind: 'pickup',
      to: `/seller/requests/${r.id}`,
      title: r.description || r.address,
      date: r.updatedAt ?? r.createdAt,
      depotName: r.depotName,
      amount: Number(r.netAmount ?? 0),
    })),
    ...liquidationSales.map((x) => ({
      key: `liquidation-${x.lotId}`,
      kind: 'liquidation',
      to: x.formatVersion === 2 ? `/seller/liquidations/v2/${x.lotId}` : `/seller/liquidations/${x.lotId}`,
      title: x.title,
      date: x.confirmedAt,
      depotName: x.depotName,
      amount: Number(x.amount ?? 0),
    })),
  ].sort((a, b) => new Date(b.date) - new Date(a.date));

  // Group by month
  const monthlyMap = {};
  transactions.forEach((t) => {
    const d = new Date(t.date);
    const key = new Intl.DateTimeFormat('vi-VN', {
      month: 'numeric',
      year: 'numeric',
      timeZone: 'Asia/Ho_Chi_Minh',
    }).format(d);
    if (!monthlyMap[key]) monthlyMap[key] = { month: key, pickups: 0, lots: 0, income: 0 };
    if (t.kind === 'liquidation') monthlyMap[key].lots++;
    else monthlyMap[key].pickups++;
    monthlyMap[key].income += t.amount;
  });
  const monthlyData = Object.values(monthlyMap);

  return (
    <div className="flex flex-col p-4 md:p-6 gap-6 w-full max-w-7xl mx-auto">
      <PageHeader title="Thống kê thu nhập" description="Tổng quan số tiền nhận được từ các đơn thu gom và lô thanh lý đã hoàn thành." />

      {/* KPI Cards Grid */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div className="d-glass-panel bg-d-surface-container-lowest border border-d-border-subtle rounded-2xl p-6 shadow-sm">
          <div className="w-10 h-10 rounded-xl bg-d-surface-container flex items-center justify-center text-d-primary mb-3">
            <MaterialIcon name="payments" className="text-xl" />
          </div>
          <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant mb-1">
            Tổng thu nhập tích lũy
          </p>
          <h3 className="font-d-headline-lg text-d-headline-lg text-d-primary">
            {formatCurrency(totalIncome)}
          </h3>
          <p className="mt-1 text-xs text-d-on-surface-variant">
            Thu gom {formatCurrency(pickupIncome)} · Thanh lý {formatCurrency(liquidationIncome)}
          </p>
        </div>

        <div className="d-glass-panel bg-d-surface-container-lowest border border-d-border-subtle rounded-2xl p-6 shadow-sm">
          <div className="w-10 h-10 rounded-xl bg-d-surface-container flex items-center justify-center text-d-secondary mb-3">
            <MaterialIcon name="monitoring" className="text-xl" />
          </div>
          <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant mb-1">
            Thu nhập tháng này
          </p>
          <h3 className="font-d-headline-lg text-d-headline-lg text-d-secondary">
            {formatCurrency(stats?.monthlyIncome ?? 0)}
          </h3>
        </div>

        <div className="d-glass-panel bg-d-surface-container-lowest border border-d-border-subtle rounded-2xl p-6 shadow-sm">
          <div className="w-10 h-10 rounded-xl bg-d-surface-container flex items-center justify-center text-d-primary mb-3">
            <MaterialIcon name="check_circle" className="text-xl" />
          </div>
          <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant mb-1">
            Giao dịch hoàn thành
          </p>
          <h3 className="font-d-headline-lg text-d-headline-lg text-d-on-surface">
            {transactions.length}
          </h3>
          <p className="mt-1 text-xs text-d-on-surface-variant">
            {doneRequests.length} đơn thu gom · {Number(stats?.liquidationSold ?? liquidationSales.length)} lô thanh lý
          </p>
        </div>

        <div className="d-glass-panel bg-d-surface-container-lowest border border-d-border-subtle rounded-2xl p-6 shadow-sm">
          <div className="w-10 h-10 rounded-xl bg-d-surface-container flex items-center justify-center text-d-on-surface-variant mb-3">
            <MaterialIcon name="receipt" className="text-xl" />
          </div>
          <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant mb-1">
            Trung bình mỗi giao dịch
          </p>
          <h3 className="font-d-headline-lg text-d-headline-lg text-d-on-surface">
            {transactions.length > 0
              ? formatCurrency(totalIncome / transactions.length)
              : '–'}
          </h3>
        </div>
      </div>

      {/* Monthly Breakdown */}
      <div className="bg-d-surface-container-lowest border border-d-border-subtle rounded-2xl shadow-sm overflow-hidden">
        <div className="px-6 py-4 border-b border-d-border-subtle bg-d-surface-container-low flex justify-between items-center">
          <div>
            <h2 className="font-bold text-d-on-surface text-base">
              Thu nhập theo từng tháng
            </h2>
            <p className="text-xs text-d-on-surface-variant mt-0.5">
              Chi tiết doanh số bán phế liệu phân bổ theo các kỳ
            </p>
          </div>
        </div>

        {monthlyData.length === 0 ? (
          <div className="p-10 text-center text-d-on-surface-variant text-sm">
            Chưa có phát sinh thu nhập nào
          </div>
        ) : (
          <div className="divide-y divide-d-border-subtle">
            {monthlyData.map((m) => (
              <div
                key={m.month}
                className="flex items-center justify-between px-6 py-4 hover:bg-d-surface-container-low transition-colors"
              >
                <div>
                  <p className="font-semibold text-sm text-d-on-surface">
                    Tháng {m.month}
                  </p>
                  <p className="text-xs text-d-on-surface-variant mt-0.5">
                    {[m.pickups > 0 && `${m.pickups} đơn thu gom`, m.lots > 0 && `${m.lots} lô thanh lý`].filter(Boolean).join(' · ')} hoàn thành
                  </p>
                </div>
                <span className="font-bold text-base text-d-primary">
                  {formatCurrency(m.income)}
                </span>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Recent Completed Pickups */}
      <div className="bg-d-surface-container-lowest border border-d-border-subtle rounded-2xl shadow-sm overflow-hidden">
        <div className="px-6 py-4 border-b border-d-border-subtle bg-d-surface-container-low flex justify-between items-center">
          <div>
            <h2 className="font-bold text-d-on-surface text-base">
              Giao dịch hoàn tất gần đây
            </h2>
            <p className="text-xs text-d-on-surface-variant mt-0.5">
              Các đơn thu gom và lô thanh lý đã nhận tiền thành công
            </p>
          </div>
        </div>

        {transactions.length === 0 ? (
          <div className="p-10 text-center text-d-on-surface-variant text-sm">
            Chưa có giao dịch hoàn tất
          </div>
        ) : (
          <div className="divide-y divide-d-border-subtle">
            {transactions.slice(0, 10).map((req) => (
              <Link
                key={req.key}
                to={req.to}
                className="flex items-center justify-between px-6 py-4 hover:bg-d-surface-container-low transition-colors group"
              >
                <div className="min-w-0 flex-1 pr-4">
                  <p className="font-semibold text-sm text-d-on-surface group-hover:text-d-primary transition-colors truncate">
                    {req.kind === 'liquidation' && (
                      <span className="mr-2 rounded-full bg-d-primary-container px-2 py-0.5 text-[10px] font-bold uppercase text-d-on-primary-container">Thanh lý</span>
                    )}
                    {req.title}
                  </p>
                  <div className="flex items-center gap-2 text-xs text-d-on-surface-variant mt-1">
                    <span>{formatDate(req.date)}</span>
                    {req.depotName && <span>• 🏢 {req.depotName}</span>}
                  </div>
                </div>

                <div className="flex items-center gap-3 shrink-0">
                  <span className="font-bold text-sm text-d-primary">
                    +{formatCurrency(req.amount)}
                  </span>
                  <MaterialIcon
                    name="chevron_right"
                    className="text-d-on-surface-variant group-hover:text-d-primary text-lg transition-colors"
                  />
                </div>
              </Link>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
