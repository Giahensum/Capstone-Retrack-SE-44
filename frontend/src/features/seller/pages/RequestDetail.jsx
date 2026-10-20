import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { sellerApi } from '../api';
import {
  formatCurrency,
  formatDate,
  PICKUP_STATUS_LABEL,
  PICKUP_STATUS_STEPS,
  PICKUP_STATUS_STYLE,
  MATERIAL_TYPE_LABEL,
} from '@/lib/utils';
import { MaterialIcon } from '@/features/depot/components/DepotIcon';

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
    onSuccess: () => {
      invalidate();
      toast.success('Đã xác nhận kết quả cân!');
    },
  });

  const rejectMutation = useMutation({
    mutationFn: () => sellerApi.rejectWeigh(id),
    onSuccess: () => {
      invalidate();
      toast.success('Đã từ chối kết quả. Nhân viên sẽ tiến hành cân lại.');
    },
  });

  const cancelMutation = useMutation({
    mutationFn: () => sellerApi.cancelRequest(id),
    onSuccess: () => {
      invalidate();
      toast.success('Đã hủy đơn.');
      navigate('/seller/requests');
    },
    onError: (err) => toast.error(err?.response?.data?.message || 'Không thể hủy đơn.'),
  });

  const confirmPaymentMutation = useMutation({
    mutationFn: () => sellerApi.confirmPayment(id),
    onSuccess: () => {
      invalidate();
      toast.success('Xác nhận nhận tiền thành công! 🎉');
    },
    onError: (err) => toast.error(err?.response?.data?.message || 'Có lỗi xảy ra.'),
  });

  const reviewMutation = useMutation({
    mutationFn: () => sellerApi.reviewDepot(id, review),
    onSuccess: () => {
      invalidate();
      toast.success('Đánh giá kho vựa thành công!');
      setReviewOpen(false);
    },
  });

  if (isLoading) {
    return (
      <div className="flex justify-center items-center h-64">
        <div className="w-8 h-8 border-3 border-d-primary border-t-transparent rounded-full animate-spin" />
      </div>
    );
  }

  if (!req) {
    return (
      <div className="p-12 text-center text-d-on-surface-variant">
        Không tìm thấy thông tin đơn thu gom
      </div>
    );
  }

  const currentStep = PICKUP_STATUS_STEPS.indexOf(req.status);
  const requestImages = req.requestImageUrl
    ? req.requestImageUrl.split(',').filter(Boolean)
    : [];

  return (
    <div className="flex flex-col p-4 md:p-6 gap-6 w-full max-w-4xl mx-auto">
      {/* Header & Back */}
      <div className="flex items-center justify-between">
        <button
          type="button"
          onClick={() => navigate('/seller/requests')}
          className="inline-flex items-center gap-1.5 text-sm font-semibold text-d-on-surface-variant hover:text-d-primary transition-colors"
        >
          <MaterialIcon name="arrow_forward" className="rotate-180 text-base" />
          <span>Về danh sách đơn</span>
        </button>

        <span
          className={`text-xs font-bold px-3 py-1.5 rounded-full border ${
            PICKUP_STATUS_STYLE[req.status] ??
            'bg-d-surface-container text-d-on-surface-variant border-d-border-subtle'
          }`}
        >
          {PICKUP_STATUS_LABEL[req.status] ?? req.status}
        </span>
      </div>

      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2">
        <h1 className="font-d-headline-lg text-d-headline-lg text-d-on-surface">
          Chi tiết đơn thu gom #{req.id?.slice(0, 8)}
        </h1>
        {req.depotName && (
          <span className="text-xs text-d-on-surface-variant bg-d-surface-container px-3 py-1 rounded-full font-medium self-start sm:self-auto">
            🏢 {req.depotName}
          </span>
        )}
      </div>

      {/* Progress Bar / Stepper */}
      {req.status !== 'CANCELLED' && (
        <div className="bg-d-surface-container-lowest border border-d-border-subtle rounded-2xl p-5 shadow-sm overflow-x-auto">
          <div className="min-w-[720px]">
            <div className="flex items-center justify-between mb-3">
              {PICKUP_STATUS_STEPS.map((s, i) => (
                <div key={s} className="flex items-center flex-1 last:flex-initial">
                  <div
                    className={`w-8 h-8 rounded-full flex items-center justify-center text-xs font-bold shrink-0 transition-all ${
                      i <= currentStep
                        ? 'bg-d-primary text-d-on-primary'
                        : 'bg-d-surface-container text-d-on-surface-variant'
                    }`}
                  >
                    {i <= currentStep ? (
                      <MaterialIcon name="check_circle" className="text-sm" />
                    ) : (
                      i + 1
                    )}
                  </div>
                  {i < PICKUP_STATUS_STEPS.length - 1 && (
                    <div
                      className={`flex-1 h-0.5 mx-2 rounded-full ${
                        i < currentStep ? 'bg-d-primary' : 'bg-d-surface-container'
                      }`}
                    />
                  )}
                </div>
              ))}
            </div>
            <div className="flex justify-between">
              {PICKUP_STATUS_STEPS.map((s) => (
                <span
                  key={s}
                  className="text-[10px] text-d-on-surface-variant font-medium text-center leading-tight px-1"
                  style={{ width: `${100 / PICKUP_STATUS_STEPS.length}%` }}
                >
                  {PICKUP_STATUS_LABEL[s] ?? s}
                </span>
              ))}
            </div>
          </div>
        </div>
      )}

      {/* Thông tin đơn hàng */}
      <div className="bg-d-surface-container-lowest border border-d-border-subtle rounded-2xl shadow-sm divide-y divide-d-border-subtle">
        <InfoRow
          icon={<MaterialIcon name="inventory_2" className="text-d-primary text-base" />}
          label="Mô tả phế liệu"
          value={req.description || '(Không có mô tả chi tiết)'}
        />
        <InfoRow
          icon={<MaterialIcon name="local_shipping" className="text-d-primary text-base" />}
          label="Địa chỉ thu gom"
          value={req.address}
        />
        <InfoRow
          icon={<MaterialIcon name="calendar_today" className="text-d-primary text-base" />}
          label="Thời gian thu gom"
          value={req.preferredDatetime ? formatDate(req.preferredDatetime) : 'Linh hoạt'}
        />
        {req.depotName && (
          <InfoRow
            icon={<MaterialIcon name="domain" className="text-d-primary text-base" />}
            label="Kho vựa tiếp nhận"
            value={req.depotName}
          />
        )}
        <InfoRow
          icon={<MaterialIcon name="schedule" className="text-d-primary text-base" />}
          label="Ngày tạo đơn"
          value={formatDate(req.createdAt)}
        />
      </div>

      {/* Ảnh phế liệu đính kèm */}
      {requestImages.length > 0 && (
        <div className="bg-d-surface-container-lowest border border-d-border-subtle rounded-2xl shadow-sm overflow-hidden">
          <button
            type="button"
            onClick={() => setShowImages(!showImages)}
            className="w-full flex items-center justify-between px-6 py-4 hover:bg-d-surface-container-low transition-colors"
          >
            <div className="flex items-center gap-2">
              <MaterialIcon name="inventory_2" className="text-d-primary text-lg" />
              <span className="text-sm font-bold text-d-on-surface">
                Hình ảnh phế liệu ({requestImages.length} ảnh)
              </span>
            </div>
            <span className="text-xs text-d-primary font-bold">
              {showImages ? 'Ẩn ảnh' : 'Xem ảnh'}
            </span>
          </button>
          {showImages && (
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 p-5 pt-0">
              {requestImages.map((url, i) => (
                <img
                  key={i}
                  src={url.trim()}
                  alt={`Ảnh ${i + 1}`}
                  className="w-full aspect-square object-cover rounded-xl border border-d-border-subtle shadow-xs"
                />
              ))}
            </div>
          )}
        </div>
      )}

      {/* Kết quả cân phế liệu */}
      {req.items && req.items.length > 0 && (
        <div className="bg-d-surface-container-lowest border border-d-border-subtle rounded-2xl shadow-sm overflow-hidden">
          <div className="px-6 py-4 border-b border-d-border-subtle bg-d-surface-container-low">
            <h3 className="text-sm font-bold text-d-on-surface flex items-center gap-2">
              <MaterialIcon name="scale" className="text-d-primary text-lg" />
              <span>Kết quả phân loại và khối lượng cân</span>
            </h3>
          </div>
          <div className="divide-y divide-d-border-subtle">
            {req.items.map((item, idx) => (
              <div key={idx} className="flex items-center justify-between px-6 py-3.5 text-sm">
                <div>
                  <p className="font-semibold text-d-on-surface">
                    {MATERIAL_TYPE_LABEL[item.materialType] ?? item.materialType}
                  </p>
                  <p className="text-xs text-d-on-surface-variant mt-0.5">
                    {item.weightKg} kg × {formatCurrency(item.pricePerKg)}/kg
                  </p>
                </div>
                <span className="font-bold text-d-primary">
                  {formatCurrency(item.subTotal)}
                </span>
              </div>
            ))}
          </div>
          <div className="px-6 py-4 bg-d-surface-container-low border-t border-d-border-subtle flex justify-between items-center">
            <span className="text-sm font-bold text-d-on-surface">Tổng tiền thanh toán</span>
            <span className="text-xl font-bold text-d-primary">
              {formatCurrency(req.grossAmount)}
            </span>
          </div>
        </div>
      )}

      {/* ═══ ACTION: Xác nhận kết quả cân (WEIGHED) ═══ */}
      {req.status === 'WEIGHED' && (
        <div className="bg-d-surface-container-lowest border-2 border-d-primary/40 rounded-2xl p-6 shadow-sm space-y-4">
          <div>
            <h3 className="text-base font-bold text-d-on-surface flex items-center gap-2">
              <MaterialIcon name="scale" className="text-d-primary text-xl" />
              <span>Xác nhận kết quả cân phế liệu</span>
            </h3>
            <p className="text-xs text-d-on-surface-variant mt-1">
              Nhân viên thu gom đã cân xong tại chỗ. Vui lòng đối chiếu số liệu ở trên và chọn xác nhận.
            </p>
          </div>
          <div className="flex gap-3">
            <button
              type="button"
              onClick={() => confirmMutation.mutate()}
              disabled={confirmMutation.isPending}
              className="flex-1 rounded-full bg-d-primary hover:bg-[#385500] text-d-on-primary font-semibold py-3 flex items-center justify-center gap-2 transition-all active:scale-95 text-sm shadow-sm disabled:opacity-50"
            >
              <MaterialIcon name="check_circle" className="text-base" />
              <span>Đồng ý kết quả</span>
            </button>
            <button
              type="button"
              onClick={() => rejectMutation.mutate()}
              disabled={rejectMutation.isPending}
              className="flex-1 rounded-full border border-d-error text-d-error hover:bg-d-error-container/20 font-semibold py-3 flex items-center justify-center gap-2 transition-all active:scale-95 text-sm disabled:opacity-50"
            >
              <MaterialIcon name="cancel" className="text-base" />
              <span>Yêu cầu cân lại</span>
            </button>
          </div>
        </div>
      )}

      {/* Thông báo trạng thái bàn giao & thanh toán */}
      {req.status === 'SELLER_CONFIRMED' && (
        <div className="rounded-2xl border border-d-primary/20 bg-d-surface-container p-5 text-sm text-d-on-surface">
          <p className="font-bold flex items-center gap-2">
            <MaterialIcon name="check_circle" className="text-d-primary text-base" />
            <span>Đã xác nhận cân · Đang chờ bàn giao kho</span>
          </p>
          <p className="text-xs text-d-on-surface-variant mt-1">
            Nhân viên thu gom đang vận chuyển phế liệu về kho vựa để tiến hành nhập kho và duyệt thanh toán.
          </p>
        </div>
      )}

      {req.status === 'AWAITING_PAYMENT' && (
        <div className="rounded-2xl border border-d-primary/20 bg-d-surface-container p-5 text-sm text-d-on-surface">
          <p className="font-bold flex items-center gap-2">
            <MaterialIcon name="payments" className="text-d-primary text-base" />
            <span>Chờ chủ kho vựa chuyển tiền</span>
          </p>
          <p className="text-xs text-d-on-surface-variant mt-1">
            Hàng đã về kho. Chủ kho đang đối soát và thực hiện lệnh chuyển khoản tới tài khoản ngân hàng của bạn.
          </p>
        </div>
      )}

      {/* ═══ ACTION: Xác nhận nhận tiền (PAYMENT_SENT) ═══ */}
      {req.status === 'PAYMENT_SENT' && (
        <div className="bg-d-surface-container-lowest border-2 border-d-secondary/40 rounded-2xl p-6 shadow-sm space-y-4">
          <div>
            <h3 className="text-base font-bold text-d-on-surface flex items-center gap-2">
              <MaterialIcon name="payments" className="text-d-secondary text-xl" />
              <span>Chủ kho đã chuyển tiền thanh toán</span>
            </h3>
            <p className="text-xs text-d-on-surface-variant mt-1">
              Vui lòng kiểm tra tài khoản ngân hàng của bạn. Nếu đã nhận đủ số tiền{' '}
              <strong className="text-d-primary">{formatCurrency(req.netAmount ?? req.grossAmount)}</strong>, hãy bấm xác nhận.
            </p>
          </div>

          {/^https?:\/\/\S+$/i.test(req.paymentProofUrl ?? '') && (
            <a
              href={req.paymentProofUrl}
              target="_blank"
              rel="noreferrer"
              className="inline-flex items-center gap-2 rounded-xl border border-d-border-subtle bg-d-surface-container px-4 py-2.5 text-xs font-semibold text-d-primary hover:bg-d-surface-container-high transition-colors"
            >
              <MaterialIcon name="visibility" className="text-base" />
              <span>Xem chứng từ ủy nhiệm chi</span>
            </a>
          )}

          <button
            type="button"
            onClick={() => confirmPaymentMutation.mutate()}
            disabled={confirmPaymentMutation.isPending}
            className="w-full rounded-full bg-d-secondary hover:bg-[#005a3c] text-d-on-secondary font-bold py-3.5 flex items-center justify-center gap-2 transition-all active:scale-95 shadow-sm text-sm disabled:opacity-50"
          >
            <MaterialIcon name="check_circle" className="text-base" />
            <span>Xác nhận đã nhận tiền thành công</span>
          </button>
        </div>
      )}

      {/* ═══ ACTION: Hủy đơn thu gom (PENDING) ═══ */}
      {req.status === 'PENDING' && (
        <button
          type="button"
          onClick={() => {
            if (confirm('Bạn có chắc chắn muốn hủy đơn thu gom này không?')) {
              cancelMutation.mutate();
            }
          }}
          disabled={cancelMutation.isPending}
          className="w-full rounded-full border border-d-error text-d-error hover:bg-d-error-container/20 font-semibold py-3 flex items-center justify-center gap-2 transition-colors text-sm disabled:opacity-50"
        >
          <MaterialIcon name="cancel" className="text-base" />
          <span>Hủy yêu cầu thu gom</span>
        </button>
      )}

      {/* ═══ ACTION: Đánh giá dịch vụ (DONE) ═══ */}
      {req.status === 'DONE' && (
        <div className="space-y-3">
          {req.reviewRating ? (
            <div className="bg-d-surface-container-lowest border border-d-border-subtle rounded-2xl p-6 shadow-sm space-y-3">
              <h3 className="text-sm font-bold text-d-on-surface">
                ⭐ Đánh giá của bạn về kho vựa
              </h3>
              <div className="flex gap-1 justify-center">
                {[1, 2, 3, 4, 5].map((n) => (
                  <span
                    key={n}
                    className={`text-2xl ${
                      n <= req.reviewRating ? 'text-[#eab308]' : 'text-d-outline-variant'
                    }`}
                  >
                    ★
                  </span>
                ))}
              </div>
              {req.reviewComment && (
                <p className="text-sm text-d-on-surface-variant bg-d-surface-container rounded-xl px-4 py-3 italic text-center">
                  “{req.reviewComment}”
                </p>
              )}
              <p className="text-[11px] text-d-on-surface-variant text-center">
                Đã đánh giá vào {req.reviewCreatedAt ? formatDate(req.reviewCreatedAt) : ''}
              </p>
            </div>
          ) : !reviewOpen ? (
            <button
              type="button"
              onClick={() => setReviewOpen(true)}
              className="w-full rounded-full bg-d-surface-container-lowest border border-d-border-subtle hover:border-d-primary text-d-on-surface font-semibold py-3 flex items-center justify-center gap-2 transition-all text-sm shadow-sm"
            >
              <MaterialIcon name="star" className="text-[#eab308] text-base" />
              <span>Đánh giá dịch vụ kho vựa</span>
            </button>
          ) : (
            <div className="bg-d-surface-container-lowest border border-d-border-subtle rounded-2xl p-6 shadow-sm space-y-4">
              <h3 className="text-sm font-bold text-d-on-surface">
                ⭐ Đánh giá trải nghiệm thu gom
              </h3>
              <div className="flex gap-2 justify-center">
                {[1, 2, 3, 4, 5].map((n) => (
                  <button
                    key={n}
                    type="button"
                    onClick={() => setReview({ ...review, rating: n })}
                    className="text-3xl transition-transform hover:scale-125"
                  >
                    <span className={n <= review.rating ? 'text-[#eab308]' : 'text-d-outline-variant'}>
                      ★
                    </span>
                  </button>
                ))}
              </div>
              <textarea
                value={review.comment}
                onChange={(e) => setReview({ ...review, comment: e.target.value })}
                placeholder="Nhận xét của bạn về thái độ phục vụ, tính chính xác khi cân (tuỳ chọn)..."
                rows={3}
                className="w-full rounded-xl border border-d-border-subtle bg-d-surface-container-lowest p-3 text-sm text-d-on-surface focus:border-d-primary focus:ring-1 focus:ring-d-primary outline-none transition-all resize-none"
              />
              <div className="flex gap-3">
                <button
                  type="button"
                  onClick={() => reviewMutation.mutate()}
                  disabled={reviewMutation.isPending}
                  className="flex-1 rounded-full bg-d-primary hover:bg-[#385500] text-d-on-primary font-bold py-3 transition-all active:scale-95 disabled:opacity-50 text-sm"
                >
                  Gửi đánh giá
                </button>
                <button
                  type="button"
                  onClick={() => setReviewOpen(false)}
                  className="px-6 py-3 rounded-full bg-d-surface-container hover:bg-d-surface-container-high text-d-on-surface font-semibold transition-all text-sm"
                >
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
    <div className="flex items-center justify-between px-6 py-4">
      <div className="flex items-center gap-2.5">
        {icon}
        <span className="text-xs text-d-on-surface-variant font-medium">{label}</span>
      </div>
      <span className="text-sm text-d-on-surface font-medium text-right max-w-[60%] truncate">
        {value}
      </span>
    </div>
  );
}
