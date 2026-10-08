import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { sellerApi } from '../api';
import { formatCurrency, formatDate, PICKUP_STATUS_LABEL } from '@/lib/utils';
import { Package, Search, Filter, ChevronRight } from 'lucide-react';

const STATUS_TABS = [
  { key: '', label: 'Tất cả' },
  { key: 'PENDING', label: 'Chờ xử lý' },
  { key: 'SCHEDULED', label: 'Đã nhận' },
  { key: 'WEIGHED', label: 'Chờ xác nhận' },
  { key: 'PAYMENT_SENT', label: 'Đã chuyển khoản' },
  { key: 'DONE', label: 'Hoàn thành' },
];

const STATUS_STYLE = {
  PENDING: 'bg-yellow-500/15 text-yellow-400 border-yellow-500/30',
  SCHEDULED: 'bg-blue-500/15 text-blue-400 border-blue-500/30',
  IN_PROGRESS: 'bg-indigo-500/15 text-indigo-400 border-indigo-500/30',
  WEIGHED: 'bg-purple-500/15 text-purple-400 border-purple-500/30',
  SELLER_CONFIRMED: 'bg-cyan-500/15 text-cyan-400 border-cyan-500/30',
  AWAITING_PAYMENT: 'bg-orange-500/15 text-orange-400 border-orange-500/30',
  PAYMENT_SENT: 'bg-teal-500/15 text-teal-400 border-teal-500/30',
  DONE: 'bg-green-200/15 text-green-700 border-emerald-500/30',
};

export default function RequestList() {
  const [statusFilter, setStatusFilter] = useState('');
  const [search, setSearch] = useState('');

  const { data: requests = [], isLoading } = useQuery({
    queryKey: ['seller-requests', statusFilter],
    queryFn: () => sellerApi.getRequests(statusFilter || undefined),
  });

  const filtered = requests.filter(r =>
    !search || r.address?.toLowerCase().includes(search.toLowerCase()) || r.description?.toLowerCase().includes(search.toLowerCase())
  );

  return (
    <div className="p-4 lg:p-8 max-w-4xl mx-auto space-y-5">
      <h1 className="text-xl font-black text-gray-900">Đơn thu gom của tôi</h1>

      {/* Filter tabs */}
      <div className="flex gap-2 overflow-x-auto pb-1 scrollbar-hide">
        {STATUS_TABS.map(tab => (
          <button
            key={tab.key}
            onClick={() => setStatusFilter(tab.key)}
            className={`px-4 py-2 rounded-full text-xs font-bold whitespace-nowrap transition-all ${
              statusFilter === tab.key
                ? 'bg-green-200 text-gray-900 shadow-lg shadow-green-200/50'
                : 'bg-gray-100 text-gray-600 hover:text-gray-800'
            }`}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {/* Search */}
      <div className="relative">
        <Search size={16} className="absolute left-4 top-1/2 -translate-y-1/2 text-gray-500" />
        <input
          value={search}
          onChange={e => setSearch(e.target.value)}
          placeholder="Tìm theo địa chỉ, mô tả..."
          className="w-full bg-white border border-gray-300 rounded-xl pl-11 pr-4 py-3 text-sm text-gray-800 placeholder-slate-600 focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500 outline-none transition-all"
        />
      </div>

      {/* List */}
      {isLoading ? (
        <div className="flex justify-center py-20">
          <div className="w-8 h-8 border-3 border-emerald-500 border-t-transparent rounded-full animate-spin" />
        </div>
      ) : filtered.length === 0 ? (
        <div className="text-center py-16">
          <Package size={48} className="mx-auto mb-4 text-slate-700" />
          <p className="text-gray-500 text-sm">Không tìm thấy đơn nào</p>
        </div>
      ) : (
        <div className="space-y-3">
          {filtered.map(req => (
            <Link
              key={req.id}
              to={`/seller/requests/${req.id}`}
              className="block bg-gray-100/50 border border-gray-300/50 rounded-2xl p-4 hover:border-emerald-500/30 hover:bg-gray-100/70 transition-all group"
            >
              <div className="flex items-start justify-between gap-3">
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 mb-1.5">
                    <span className={`text-[10px] font-bold px-2.5 py-0.5 rounded-full border ${STATUS_STYLE[req.status] ?? 'bg-slate-500/15 text-gray-600 border-slate-500/30'}`}>
                      {PICKUP_STATUS_LABEL[req.status] ?? req.status}
                    </span>
                    {req.depotName && (
                      <span className="text-[10px] text-gray-500 font-medium">• {req.depotName}</span>
                    )}
                  </div>
                  <p className="text-sm font-medium text-gray-800 truncate">{req.description || 'Không có mô tả'}</p>
                  <p className="text-xs text-gray-500 mt-1 truncate">{req.address}</p>
                  <div className="flex items-center gap-4 mt-2">
                    <span className="text-xs text-gray-500">{formatDate(req.createdAt)}</span>
                    {req.grossAmount > 0 && (
                      <span className="text-xs font-bold text-green-700">{formatCurrency(req.grossAmount)}</span>
                    )}
                  </div>
                </div>
                <ChevronRight size={18} className="text-slate-600 group-hover:text-green-700 shrink-0 mt-4 transition-colors" />
              </div>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}

