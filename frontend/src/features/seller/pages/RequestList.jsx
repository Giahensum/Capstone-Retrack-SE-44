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
  DONE: 'bg-emerald-500/15 text-emerald-400 border-emerald-500/30',
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
      <h1 className="text-xl font-black text-white">Đơn thu gom của tôi</h1>

      {/* Filter tabs */}
      <div className="flex gap-2 overflow-x-auto pb-1 scrollbar-hide">
        {STATUS_TABS.map(tab => (
          <button
            key={tab.key}
            onClick={() => setStatusFilter(tab.key)}
            className={`px-4 py-2 rounded-full text-xs font-bold whitespace-nowrap transition-all ${
              statusFilter === tab.key
                ? 'bg-emerald-500 text-white shadow-lg shadow-emerald-500/30'
                : 'bg-slate-800 text-slate-400 hover:text-slate-200'
            }`}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {/* Search */}
      <div className="relative">
        <Search size={16} className="absolute left-4 top-1/2 -translate-y-1/2 text-slate-500" />
        <input
          value={search}
          onChange={e => setSearch(e.target.value)}
          placeholder="Tìm theo địa chỉ, mô tả..."
          className="w-full bg-slate-900 border border-slate-700 rounded-xl pl-11 pr-4 py-3 text-sm text-slate-200 placeholder-slate-600 focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500 outline-none transition-all"
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
          <p className="text-slate-500 text-sm">Không tìm thấy đơn nào</p>
        </div>
      ) : (
        <div className="space-y-3">
          {filtered.map(req => (
            <Link
              key={req.id}
              to={`/seller/requests/${req.id}`}
              className="block bg-slate-800/50 border border-slate-700/50 rounded-2xl p-4 hover:border-emerald-500/30 hover:bg-slate-800/70 transition-all group"
            >
              <div className="flex items-start justify-between gap-3">
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 mb-1.5">
                    <span className={`text-[10px] font-bold px-2.5 py-0.5 rounded-full border ${STATUS_STYLE[req.status] ?? 'bg-slate-500/15 text-slate-400 border-slate-500/30'}`}>
                      {PICKUP_STATUS_LABEL[req.status] ?? req.status}
                    </span>
                    {req.depotName && (
                      <span className="text-[10px] text-slate-500 font-medium">• {req.depotName}</span>
                    )}
                  </div>
                  <p className="text-sm font-medium text-slate-200 truncate">{req.description || 'Không có mô tả'}</p>
                  <p className="text-xs text-slate-500 mt-1 truncate">{req.address}</p>
                  <div className="flex items-center gap-4 mt-2">
                    <span className="text-xs text-slate-500">{formatDate(req.createdAt)}</span>
                    {req.grossAmount > 0 && (
                      <span className="text-xs font-bold text-emerald-400">{formatCurrency(req.grossAmount)}</span>
                    )}
                  </div>
                </div>
                <ChevronRight size={18} className="text-slate-600 group-hover:text-emerald-400 shrink-0 mt-4 transition-colors" />
              </div>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}
