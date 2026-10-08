import { useQuery } from '@tanstack/react-query';
import { sellerApi } from '../api';
import { formatCurrency } from '@/lib/utils';
import { TrendingUp, DollarSign, Package, CheckCircle, Clock, ArrowUpRight, ArrowDownRight } from 'lucide-react';

export default function SellerIncome() {
  const { data: stats, isLoading } = useQuery({
    queryKey: ['seller-stats'],
    queryFn: sellerApi.getStats,
  });

  const { data: requests = [] } = useQuery({
    queryKey: ['seller-requests'],
    queryFn: () => sellerApi.getRequests(),
  });

  const doneRequests = requests.filter(r => r.status === 'DONE');
  const totalIncome = doneRequests.reduce((sum, r) => sum + (r.netAmount || r.grossAmount || 0), 0);

  // Group by month
  const monthlyMap = {};
  doneRequests.forEach(r => {
    const d = new Date(r.createdAt);
    const key = `${d.getMonth() + 1}/${d.getFullYear()}`;
    if (!monthlyMap[key]) monthlyMap[key] = { month: key, count: 0, income: 0 };
    monthlyMap[key].count++;
    monthlyMap[key].income += r.netAmount || r.grossAmount || 0;
  });
  const monthlyData = Object.values(monthlyMap).reverse();

  return (
    <div className="p-4 lg:p-8 max-w-4xl mx-auto space-y-6">
      <h1 className="text-xl font-black text-gray-900">Thống kê thu nhập</h1>

      {/* Summary Cards */}
      <div className="grid grid-cols-2 gap-3">
        <div className="bg-gradient-to-br from-emerald-500/20 to-emerald-600/10 border border-emerald-500/30 rounded-2xl p-5">
          <div className="w-10 h-10 bg-green-200/20 rounded-xl flex items-center justify-center mb-3">
            <DollarSign size={20} className="text-green-700" />
          </div>
          <p className="text-xs text-gray-600 font-medium uppercase tracking-wide">Tổng thu nhập</p>
          <p className="text-xl lg:text-2xl font-black text-green-700 mt-1">{formatCurrency(totalIncome)}</p>
        </div>
        <div className="bg-gradient-to-br from-blue-500/20 to-blue-600/10 border border-blue-500/30 rounded-2xl p-5">
          <div className="w-10 h-10 bg-blue-500/20 rounded-xl flex items-center justify-center mb-3">
            <TrendingUp size={20} className="text-blue-400" />
          </div>
          <p className="text-xs text-gray-600 font-medium uppercase tracking-wide">Thu nhập tháng này</p>
          <p className="text-xl lg:text-2xl font-black text-blue-400 mt-1">{formatCurrency(stats?.monthlyIncome ?? 0)}</p>
        </div>
      </div>

      <div className="grid grid-cols-2 gap-3">
        <div className="bg-gray-100/50 border border-gray-300/50 rounded-2xl p-5">
          <div className="flex items-center gap-2 mb-1">
            <CheckCircle size={16} className="text-green-700" />
            <span className="text-xs text-gray-600 font-medium">Đơn hoàn thành</span>
          </div>
          <p className="text-2xl font-bold text-gray-900">{doneRequests.length}</p>
        </div>
        <div className="bg-gray-100/50 border border-gray-300/50 rounded-2xl p-5">
          <div className="flex items-center gap-2 mb-1">
            <Package size={16} className="text-blue-400" />
            <span className="text-xs text-gray-600 font-medium">Trung bình/đơn</span>
          </div>
          <p className="text-2xl font-bold text-gray-900">{doneRequests.length > 0 ? formatCurrency(totalIncome / doneRequests.length) : '–'}</p>
        </div>
      </div>

      {/* Monthly Breakdown */}
      <div className="bg-gray-100/50 border border-gray-300/50 rounded-2xl overflow-hidden">
        <div className="px-5 py-4 border-b border-gray-300/50">
          <h2 className="text-sm font-bold text-gray-800">Thu nhập theo tháng</h2>
        </div>
        {monthlyData.length === 0 ? (
          <div className="p-10 text-center text-gray-500 text-sm">Chưa có dữ liệu</div>
        ) : (
          <div className="divide-y divide-slate-700/30">
            {monthlyData.map(m => (
              <div key={m.month} className="flex items-center justify-between px-5 py-4">
                <div>
                  <p className="text-sm font-semibold text-gray-800">Tháng {m.month}</p>
                  <p className="text-xs text-gray-500">{m.count} đơn hoàn thành</p>
                </div>
                <span className="text-sm font-bold text-green-700">{formatCurrency(m.income)}</span>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Recent completed */}
      <div className="bg-gray-100/50 border border-gray-300/50 rounded-2xl overflow-hidden">
        <div className="px-5 py-4 border-b border-gray-300/50">
          <h2 className="text-sm font-bold text-gray-800">Giao dịch gần đây</h2>
        </div>
        {doneRequests.length === 0 ? (
          <div className="p-10 text-center text-gray-500 text-sm">Chưa có giao dịch hoàn thành</div>
        ) : (
          <div className="divide-y divide-slate-700/30">
            {doneRequests.slice(0, 10).map(r => (
              <div key={r.id} className="flex items-center justify-between px-5 py-3.5">
                <div className="min-w-0 flex-1">
                  <p className="text-sm font-medium text-gray-800 truncate">{r.description || r.address}</p>
                  <p className="text-xs text-gray-500">{new Date(r.createdAt).toLocaleDateString('vi-VN')}</p>
                </div>
                <div className="flex items-center gap-1 text-green-700">
                  <ArrowUpRight size={14} />
                  <span className="text-sm font-bold">{formatCurrency(r.netAmount || r.grossAmount)}</span>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

