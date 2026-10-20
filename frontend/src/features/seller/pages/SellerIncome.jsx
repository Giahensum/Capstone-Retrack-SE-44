import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { sellerApi } from '../api';
import { formatCurrency, formatDate } from '@/lib/utils';
import { MaterialIcon } from '@/features/depot/components/DepotIcon';

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
  const totalIncome = doneRequests.reduce(
    (sum, r) => sum + Number(r.netAmount ?? r.grossAmount ?? 0),
    0
  );

  // Group by month
  const monthlyMap = {};
  doneRequests.forEach((r) => {
    const d = new Date(r.updatedAt ?? r.createdAt);
    const key = new Intl.DateTimeFormat('vi-VN', {
      month: 'numeric',
      year: 'numeric',
      timeZone: 'Asia/Ho_Chi_Minh',
    }).format(d);
    if (!monthlyMap[key]) monthlyMap[key] = { month: key, count: 0, income: 0 };
    monthlyMap[key].count++;
    monthlyMap[key].income += Number(r.netAmount ?? r.grossAmount ?? 0);
  });
  const monthlyData = Object.values(monthlyMap).reverse();

  return (
    <div className="flex flex-col p-4 md:p-6 gap-6 w-full max-w-7xl mx-auto">
      {/* Page Title */}
      <div>
        <h1 className="font-d-headline-xl-mobile md:font-d-headline-xl text-d-headline-xl-mobile md:text-d-headline-xl text-d-on-surface">
          Thống kê thu nhập
        </h1>
        <p className="font-d-body-md text-d-body-md text-d-on-surface-variant mt-2">
          Tổng quan số tiền nhận được từ các đơn thu gom phế liệu đã hoàn thành.
        </p>
      </div>

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
            Đơn hoàn thành
          </p>
          <h3 className="font-d-headline-lg text-d-headline-lg text-d-on-surface">
            {doneRequests.length}
          </h3>
        </div>

        <div className="d-glass-panel bg-d-surface-container-lowest border border-d-border-subtle rounded-2xl p-6 shadow-sm">
          <div className="w-10 h-10 rounded-xl bg-d-surface-container flex items-center justify-center text-d-on-surface-variant mb-3">
            <MaterialIcon name="receipt" className="text-xl" />
          </div>
          <p className="font-d-body-sm text-d-body-sm text-d-on-surface-variant mb-1">
            Trung bình mỗi đơn
          </p>
          <h3 className="font-d-headline-lg text-d-headline-lg text-d-on-surface">
            {doneRequests.length > 0
              ? formatCurrency(totalIncome / doneRequests.length)
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
                    {m.count} đơn thu gom hoàn thành
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
              Danh sách các đơn thu gom đã nhận chuyển khoản thành công
            </p>
          </div>
        </div>

        {doneRequests.length === 0 ? (
          <div className="p-10 text-center text-d-on-surface-variant text-sm">
            Chưa có giao dịch hoàn tất
          </div>
        ) : (
          <div className="divide-y divide-d-border-subtle">
            {doneRequests.slice(0, 10).map((req) => (
              <Link
                key={req.id}
                to={`/seller/requests/${req.id}`}
                className="flex items-center justify-between px-6 py-4 hover:bg-d-surface-container-low transition-colors group"
              >
                <div className="min-w-0 flex-1 pr-4">
                  <p className="font-semibold text-sm text-d-on-surface group-hover:text-d-primary transition-colors truncate">
                    {req.description || req.address}
                  </p>
                  <div className="flex items-center gap-2 text-xs text-d-on-surface-variant mt-1">
                    <span>{formatDate(req.updatedAt ?? req.createdAt)}</span>
                    {req.depotName && <span>• 🏢 {req.depotName}</span>}
                  </div>
                </div>

                <div className="flex items-center gap-3 shrink-0">
                  <span className="font-bold text-sm text-d-primary">
                    +{formatCurrency(req.netAmount ?? req.grossAmount)}
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
