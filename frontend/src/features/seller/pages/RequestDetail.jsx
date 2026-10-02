import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { sellerApi } from '../api';
import { formatCurrency, formatDate, PICKUP_STATUS_LABEL, MATERIAL_TYPE_LABEL } from '@/lib/utils';
import { ArrowLeft, MapPin, Calendar, Clock, CheckCircle, XCircle, Star, Package, Ban, Banknote, Image as ImageIcon } from 'lucide-react';

const STATUS_STEPS = ['PENDING', 'SCHEDULED', 'WEIGHED', 'SELLER_CONFIRMED', 'DONE'];

export default function RequestDetail() {
  const { id } = useParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [reviewOpen, setReviewOpen] = useState(false);
  const [review, setReview] = useState({ rating: 5, comment: '' });
  const [showImages, setShowImages] = useState(false);

  const { data: req, isLoading } = useQuery({
    queryKey: ['seller-request', id],
    queryFn: () => sellerApi.getRequestById(id),
  });

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['seller-request', id] });
    queryClient.invalidateQueries({ queryKey: ['seller-requests'] });
    queryClient.invalidateQueries({ queryKey: ['seller-stats'] });
  };

  const confirmMutation = useMutation({
    mutationFn: () => sellerApi.confirmWeigh(id),
    onSuccess: () => { invalidate(); toast.success('Đã xác nhận giá!'); },
  });

  const rejectMutation = useMutation({
    mutationFn: () => sellerApi.rejectWeigh(id),
    onSuccess: () => { invalidate(); toast.success('Đã từ chối. Nhân viên sẽ cân lại.'); },
  });

  const cancelMutation = useMutation({
    mutationFn: () => sellerApi.cancelRequest(id),
    onSuccess: () => { invalidate(); toast.success('Đã hủy đơn.'); navigate('/seller/requests'); },
    onError: (err) => toast.error(err?.response?.data?.message || 'Không thể hủy đơn.'),
  });

  const confirmPaymentMutation = useMutation({
    mutationFn: () => sellerApi.confirmPayment(id),
    onSuccess: () => { invalidate(); toast.success('Xác nhận nhận tiền thành công! 🎉'); },
    onError: (err) => toast.error(err?.response?.data?.message || 'Có lỗi.'),
  });

  const reviewMutation = useMutation({
    mutationFn: () => sellerApi.reviewDepot(id, review),
    onSuccess: () => { toast.success('Đánh giá thành công!'); setReviewOpen(false); },
  });

  if (isLoading) return (
    <div className="flex justify-center items-center h-64">
      <div className="w-8 h-8 border-3 border-emerald-500 border-t-transparent rounded-full animate-spin" />
    </div>
  );

  if (!req) return <div className="p-8 text-center text-slate-500">Không tìm thấy đơn</div>;

  const currentStep = STATUS_STEPS.indexOf(req.status);
  const requestImages = req.requestImageUrl ? req.requestImageUrl.split(',').filter(Boolean) : [];

  return (
    <div className="p-4 lg:p-8 max-w-2xl mx-auto space-y-5">
      {/* Header */}
      <button onClick={() => navigate('/seller/requests')} className="flex items-center gap-2 text-slate-400 hover:text-slate-200 text-sm font-medium transition-colors">
        <ArrowLeft size={16} /> Đơn của tôi
      </button>

      <div className="flex items-center justify-between">
        <h1 className="text-xl font-black text-white">Chi tiết đơn</h1>
        <span className={`text-[10px] font-bold px-3 py-1.5 rounded-full border ${getStatusStyle(req.status)}`}>
          {PICKUP_STATUS_LABEL[req.status] ?? req.status}
        </span>
      </div>

      {/* Progress bar */}
      {req.status !== 'CANCELLED' && (
        <div className="bg-slate-800/50 border border-slate-700/50 rounded-2xl p-5">
          <div className="flex items-center justify-between mb-3">
            {STATUS_STEPS.map((s, i) => (
              <div key={s} className="flex items-center flex-1 last:flex-initial">
                <div className={`w-7 h-7 rounded-full flex items-center justify-center text-[10px] font-bold shrink-0 transition-all ${
                  i <= currentStep ? 'bg-emerald-500 text-white' : 'bg-slate-700 text-slate-500'
                }`}>
                  {i <= currentStep ? <CheckCircle size={14} /> : i + 1}
                </div>
                {i < STATUS_STEPS.length - 1 && (
                  <div className={`flex-1 h-0.5 mx-1 rounded-full ${i < currentStep ? 'bg-emerald-500' : 'bg-slate-700'}`} />
                )}
              </div>
            ))}
          </div>
          <div className="flex justify-between">
            {STATUS_STEPS.map(s => (
              <span key={s} className="text-[9px] text-slate-500 font-medium text-center" style={{ width: '18%' }}>
                {PICKUP_STATUS_LABEL[s] ?? s}
              </span>
            ))}
          </div>
        </div>
      )}

      {/* Images */}
      {requestImages.length > 0 && (
        <div className="bg-slate-800/50 border border-slate-700/50 rounded-2xl overflow-hidden">
          <button onClick={() => setShowImages(!showImages)} className="w-full flex items-center justify-between px-5 py-3.5 hover:bg-slate-800/50 transition-colors">
            <div className="flex items-center gap-2">
              <ImageIcon size={14} className="text-purple-400" />
              <span className="text-sm font-semibold text-slate-200">Hình ảnh phế liệu ({requestImages.length})</span>
            </div>
            <span className="text-xs text-emerald-400">{showImages ? 'Ẩn' : 'Xem'}</span>
          </button>
          {showImages && (
            <div className="grid grid-cols-3 gap-2 p-4 pt-0">
              {requestImages.map((url, i) => (
                <img key={i} src={url.trim()} alt={`Ảnh ${i + 1}`} className="w-full aspect-square object-cover rounded-xl border border-slate-700" />
              ))}
            </div>
          )}
        </div>
      )}

      {/* Info */}
      <div className="bg-slate-800/50 border border-slate-700/50 rounded-2xl divide-y divide-slate-700/50">
        <InfoRow icon={<Package size={14} className="text-blue-400" />} label="Mô tả" value={req.description || '(không có)'} />
        <InfoRow icon={<MapPin size={14} className="text-emerald-400" />} label="Địa chỉ" value={req.address} />
        <InfoRow icon={<Calendar size={14} className="text-purple-400" />} label="Ngày giờ" value={req.preferredDatetime ? formatDate(req.preferredDatetime) : 'Linh hoạt'} />
        {req.depotName && <InfoRow icon={<Clock size={14} className="text-orange-400" />} label="Kho vựa" value={req.depotName} />}
        <InfoRow icon={<Clock size={14} className="text-slate-400" />} label="Ngày tạo" value={formatDate(req.createdAt)} />
      </div>

      {/* Weigh Items */}
      {req.items && req.items.length > 0 && (
        <div className="bg-slate-800/50 border border-slate-700/50 rounded-2xl overflow-hidden">
          <div className="px-5 py-4 border-b border-slate-700/50">
            <h3 className="text-sm font-bold text-slate-200">⚖️ Kết quả phân loại & cân</h3>
          </div>
          <div className="divide-y divide-slate-700/30">
            {req.items.map((item, idx) => (
              <div key={idx} className="flex items-center justify-between px-5 py-3">
                <div>
                  <p className="text-sm font-medium text-slate-200">{MATERIAL_TYPE_LABEL[item.materialType] ?? item.materialType}</p>
                  <p className="text-xs text-slate-500">{item.weightKg} kg × {formatCurrency(item.pricePerKg)}/kg</p>
                </div>
                <span className="text-sm font-bold text-emerald-400">{formatCurrency(item.subTotal)}</span>
              </div>
            ))}
          </div>
          <div className="px-5 py-4 bg-emerald-500/5 border-t border-emerald-500/20">
            <div className="flex justify-between items-center">
              <span className="text-sm font-bold text-slate-300">Tổng cộng</span>
              <span className="text-lg font-black text-emerald-400">{formatCurrency(req.grossAmount)}</span>
            </div>
          </div>
        </div>
      )}

      {/* ═══ ACTION: Confirm / Reject weigh (UC-1.8) ═══ */}
      {req.status === 'WEIGHED' && (
        <div className="bg-gradient-to-br from-purple-500/10 to-purple-600/5 border border-purple-500/30 rounded-2xl p-5 space-y-4">
          <div>
            <h3 className="text-base font-bold text-white">⚖️ Xác nhận kết quả cân</h3>
            <p className="text-xs text-slate-400 mt-1">Nhân viên đã cân xong. Bạn có đồng ý với giá trên không?</p>
          </div>
          <div className="flex gap-3">
            <button
              onClick={() => confirmMutation.mutate()}
              disabled={confirmMutation.isPending}
              className="flex-1 bg-emerald-500 hover:bg-emerald-600 text-white font-bold py-3 rounded-xl flex items-center justify-center gap-2 transition-all active:scale-95 disabled:opacity-60"
            >
              <CheckCircle size={16} /> Đồng ý
            </button>
            <button
              onClick={() => rejectMutation.mutate()}
              disabled={rejectMutation.isPending}
              className="flex-1 bg-slate-700 hover:bg-slate-600 text-white font-bold py-3 rounded-xl flex items-center justify-center gap-2 transition-all active:scale-95 disabled:opacity-60"
            >
              <XCircle size={16} /> Từ chối
            </button>
          </div>
        </div>
      )}

      {/* ═══ ACTION: Confirm Payment Received (UC-1.9) ═══ */}
      {req.status === 'PAYMENT_SENT' && (
        <div className="bg-gradient-to-br from-teal-500/10 to-teal-600/5 border border-teal-500/30 rounded-2xl p-5 space-y-4">
          <div>
            <h3 className="text-base font-bold text-white">💰 Chủ kho đã chuyển tiền</h3>
            <p className="text-xs text-slate-400 mt-1">
              Vui lòng kiểm tra tài khoản ngân hàng. Nếu đã nhận được <span className="text-emerald-400 font-bold">{formatCurrency(req.netAmount)}</span>, hãy bấm xác nhận.
            </p>
          </div>
          <button
            onClick={() => confirmPaymentMutation.mutate()}
            disabled={confirmPaymentMutation.isPending}
            className="w-full bg-emerald-500 hover:bg-emerald-600 text-white font-bold py-3.5 rounded-xl flex items-center justify-center gap-2 transition-all active:scale-95 disabled:opacity-60 shadow-lg shadow-emerald-500/20"
          >
            <Banknote size={18} /> Xác nhận đã nhận tiền
          </button>
        </div>
      )}

      {/* ═══ ACTION: Cancel (UC-1.5) ═══ */}
      {req.status === 'PENDING' && (
        <button
          onClick={() => { if (confirm('Bạn có chắc muốn hủy đơn này?')) cancelMutation.mutate(); }}
          disabled={cancelMutation.isPending}
          className="w-full bg-red-500/10 border border-red-500/30 text-red-400 font-bold py-3 rounded-xl flex items-center justify-center gap-2 hover:bg-red-500/20 transition-all disabled:opacity-60"
        >
          <Ban size={16} /> Hủy đơn thu gom
        </button>
      )}

      {/* ═══ ACTION: Review depot (UC-1.11) ═══ */}
      {req.status === 'DONE' && (
        <div className="space-y-3">
          {!reviewOpen ? (
            <button onClick={() => setReviewOpen(true)} className="w-full bg-yellow-500/10 border border-yellow-500/30 text-yellow-400 font-bold py-3 rounded-xl flex items-center justify-center gap-2 hover:bg-yellow-500/20 transition-all">
              <Star size={16} /> Đánh giá kho vựa
            </button>
          ) : (
            <div className="bg-slate-800/50 border border-slate-700/50 rounded-2xl p-5 space-y-4">
              <h3 className="text-sm font-bold text-white">⭐ Đánh giá dịch vụ</h3>
              <div className="flex gap-2 justify-center">
                {[1, 2, 3, 4, 5].map(n => (
                  <button key={n} onClick={() => setReview({ ...review, rating: n })} className="transition-transform hover:scale-110">
                    <Star size={32} className={n <= review.rating ? 'text-yellow-400 fill-yellow-400' : 'text-slate-600'} />
                  </button>
                ))}
              </div>
              <textarea
                value={review.comment}
                onChange={e => setReview({ ...review, comment: e.target.value })}
                placeholder="Nhận xét của bạn (tùy chọn)..."
                rows={3}
                className="w-full bg-slate-900 border border-slate-700 rounded-xl px-4 py-3 text-sm text-slate-200 placeholder-slate-600 focus:border-emerald-500 outline-none resize-none"
              />
              <div className="flex gap-3">
                <button onClick={() => reviewMutation.mutate()} disabled={reviewMutation.isPending} className="flex-1 bg-emerald-500 hover:bg-emerald-600 text-white font-bold py-3 rounded-xl transition-all active:scale-95 disabled:opacity-60">
                  Gửi đánh giá
                </button>
                <button onClick={() => setReviewOpen(false)} className="px-5 py-3 bg-slate-700 text-white font-bold rounded-xl hover:bg-slate-600 transition-all">
                  Hủy
                </button>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function InfoRow({ icon, label, value }) {
  return (
    <div className="flex items-center justify-between px-5 py-3.5">
      <div className="flex items-center gap-2">
        {icon}
        <span className="text-xs text-slate-500 font-medium">{label}</span>
      </div>
      <span className="text-sm text-slate-200 font-medium text-right max-w-[60%] truncate">{value}</span>
    </div>
  );
}

function getStatusStyle(status) {
  const map = {
    PENDING: 'bg-yellow-500/15 text-yellow-400 border-yellow-500/30',
    SCHEDULED: 'bg-blue-500/15 text-blue-400 border-blue-500/30',
    WEIGHED: 'bg-purple-500/15 text-purple-400 border-purple-500/30',
    SELLER_CONFIRMED: 'bg-cyan-500/15 text-cyan-400 border-cyan-500/30',
    AWAITING_PAYMENT: 'bg-orange-500/15 text-orange-400 border-orange-500/30',
    PAYMENT_SENT: 'bg-teal-500/15 text-teal-400 border-teal-500/30',
    DONE: 'bg-emerald-500/15 text-emerald-400 border-emerald-500/30',
    CANCELLED: 'bg-red-500/15 text-red-400 border-red-500/30',
  };
  return map[status] ?? 'bg-slate-500/15 text-slate-400 border-slate-500/30';
}
