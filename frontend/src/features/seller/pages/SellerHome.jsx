import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { sellerApi } from '../api';
import { formatCurrency } from '@/lib/utils';
import { Package, Clock, CheckCircle, DollarSign, PlusCircle, ArrowRight, TrendingUp, Recycle } from 'lucide-react';

export default function SellerHome() {
  const { data: stats, isLoading } = useQuery({
    queryKey: ['seller-stats'],
    queryFn: sellerApi.getStats,
  });

  const { data: requests } = useQuery({
    queryKey: ['seller-requests'],
    queryFn: () => sellerApi.getRequests(),
  });

  const recentRequests = (requests ?? []).slice(0, 5);

  return (
    <div className="p-4 lg:p-8 space-y-6 max-w-6xl mx-auto">
      {/* Welcome Banner */}
      <div className="relative overflow-hidden rounded-2xl bg-gradient-to-br from-emerald-600 via-emerald-500 to-teal-500 p-6 lg:p-8">
        <div className="absolute top-0 right-0 w-64 h-64 bg-white/5 rounded-full -translate-y-1/2 translate-x-1/2" />
        <div className="absolute bottom-0 left-0 w-40 h-40 bg-emerald-400/10 rounded-full translate-y-1/2 -translate-x-1/2" />
        <div className="relative z-10">
          <div className="flex items-center gap-3 mb-3">
            <div className="w-12 h-12 bg-white/20 rounded-2xl flex items-center justify-center backdrop-blur-sm">
              <Recycle size={24} className="text-gray-900" />
            </div>
            <div>
              <h1 className="text-xl lg:text-2xl font-black text-gray-900">Xin chào! 👋</h1>
              <p className="text-emerald-100 text-sm">Biến phế liệu thành giá trị</p>
            </div>
          </div>
          <Link
            to="/seller/create"
            className="inline-flex items-center gap-2 mt-4 bg-white text-emerald-700 font-bold px-5 py-2.5 rounded-xl hover:bg-emerald-50 transition-all shadow-lg active:scale-95 text-sm"
          >
            <PlusCircle size={18} />
            Tạo đơn thu gom mới
            <ArrowRight size={16} />
          </Link>
        </div>
      </div>

      {/* Stats Grid */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 lg:gap-4">
        {[
          { label: 'Tổng đơn', value: stats?.total ?? 0, icon: Package, color: 'from-blue-500/20 to-blue-600/10 border-blue-500/30', iconColor: 'text-blue-400' },
          { label: 'Đang xử lý', value: stats?.inProgress ?? 0, icon: Clock, color: 'from-orange-500/20 to-orange-600/10 border-orange-500/30', iconColor: 'text-orange-400' },
          { label: 'Hoàn thành', value: stats?.done ?? 0, icon: CheckCircle, color: 'from-emerald-500/20 to-emerald-600/10 border-emerald-500/30', iconColor: 'text-green-700' },
          { label: 'Thu nhập tháng', value: formatCurrency(stats?.monthlyIncome ?? 0), icon: TrendingUp, color: 'from-purple-500/20 to-purple-600/10 border-purple-500/30', iconColor: 'text-purple-400' },
        ].map(({ label, value, icon: Icon, color, iconColor }) => (
          <div key={label} className={`bg-gradient-to-br border rounded-2xl p-4 lg:p-5 ${color}`}>
            <div className={`w-9 h-9 rounded-xl bg-white/10 flex items-center justify-center mb-3 ${iconColor}`}>
              <Icon size={18} />
            </div>
            <p className="text-xs text-gray-600 font-medium uppercase tracking-wide">{label}</p>
            <p className="text-lg lg:text-2xl font-bold text-slate-100 mt-0.5">{value}</p>
          </div>
        ))}
      </div>

      {/* Quick Actions */}
      <div className="grid grid-cols-2 gap-3">
        <Link to="/seller/create" className="bg-gray-100/70 border border-gray-300/50 rounded-2xl p-5 hover:bg-gray-100 transition-all group">
          <PlusCircle size={24} className="text-green-700 mb-3 group-hover:scale-110 transition-transform" />
          <h3 className="text-sm font-bold text-gray-800">Tạo đơn mới</h3>
          <p className="text-xs text-gray-500 mt-1">Bán phế liệu ngay</p>
        </Link>
        <Link to="/seller/requests" className="bg-gray-100/70 border border-gray-300/50 rounded-2xl p-5 hover:bg-gray-100 transition-all group">
          <ClipboardList size={24} className="text-blue-400 mb-3 group-hover:scale-110 transition-transform" />
          <h3 className="text-sm font-bold text-gray-800">Đơn của tôi</h3>
          <p className="text-xs text-gray-500 mt-1">Theo dõi trạng thái</p>
        </Link>
      </div>

      {/* Recent Requests */}
      <div className="bg-gray-100/50 border border-gray-300/50 rounded-2xl overflow-hidden">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-300/50">
          <h2 className="text-base font-bold text-slate-100">Đơn gần đây</h2>
          <Link to="/seller/requests" className="text-xs text-green-700 font-semibold hover:underline flex items-center gap-1">
            Xem tất cả <ArrowRight size={12} />
          </Link>
        </div>
        {recentRequests.length === 0 ? (
          <div className="p-10 text-center">
            <Package size={40} className="mx-auto mb-3 text-slate-600" />
            <p className="text-sm text-gray-500">Chưa có đơn nào. Hãy tạo đơn thu gom đầu tiên!</p>
          </div>
        ) : (
          <div className="divide-y divide-slate-700/50">
            {recentRequests.map(req => (
              <Link key={req.id} to={`/seller/requests/${req.id}`} className="flex items-center justify-between px-5 py-3 hover:bg-gray-100/50 transition-colors">
                <div className="min-w-0 flex-1">
                  <p className="text-sm font-medium text-gray-800 truncate">{req.description || req.address}</p>
                  <p className="text-xs text-gray-500 mt-0.5">{new Date(req.createdAt).toLocaleDateString('vi-VN')}</p>
                </div>
                <StatusBadge status={req.status} />
              </Link>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

function StatusBadge({ status }) {
  const map = {
    PENDING: { label: 'Chờ xử lý', cls: 'bg-yellow-500/15 text-yellow-400 border-yellow-500/30' },
    SCHEDULED: { label: 'Đã nhận đơn', cls: 'bg-blue-500/15 text-blue-400 border-blue-500/30' },
    IN_PROGRESS: { label: 'Đang thu gom', cls: 'bg-indigo-500/15 text-indigo-400 border-indigo-500/30' },
    WEIGHED: { label: 'Chờ xác nhận', cls: 'bg-purple-500/15 text-purple-400 border-purple-500/30' },
    SELLER_CONFIRMED: { label: 'Đã xác nhận', cls: 'bg-cyan-500/15 text-cyan-400 border-cyan-500/30' },
    AWAITING_PAYMENT: { label: 'Chờ thanh toán', cls: 'bg-orange-500/15 text-orange-400 border-orange-500/30' },
    PAYMENT_SENT: { label: 'Đã thanh toán', cls: 'bg-teal-500/15 text-teal-400 border-teal-500/30' },
    DONE: { label: 'Hoàn thành', cls: 'bg-green-200/15 text-green-700 border-emerald-500/30' },
  };
  const { label, cls } = map[status] ?? { label: status, cls: 'bg-slate-500/15 text-gray-600 border-slate-500/30' };
  return <span className={`text-[10px] font-bold px-2.5 py-1 rounded-full border ${cls}`}>{label}</span>;
}

function ClipboardList(props) {
  return (
    <svg xmlns="http://www.w3.org/2000/svg" width={props.size ?? 24} height={props.size ?? 24} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className={props.className}>
      <rect width="8" height="4" x="8" y="2" rx="1" ry="1"/><path d="M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2"/><path d="M12 11h4"/><path d="M12 16h4"/><path d="M8 11h.01"/><path d="M8 16h.01"/>
    </svg>
  );
}

