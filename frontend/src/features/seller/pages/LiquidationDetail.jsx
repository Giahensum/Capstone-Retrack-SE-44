import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient, keepPreviousData } from '@tanstack/react-query';
import { Link, useParams } from 'react-router-dom';
import toast from 'react-hot-toast';
import { sellerApi } from '../api';
import { formatCurrency, formatDate } from '@/lib/utils';
import DepositContract from '@/components/liquidation/DepositContract';
import {
  AGREEMENT_STAGE_LABEL, LIQUIDATION_PHASE_LABEL, LIQUIDATION_PHASE_STYLE,
} from '../liquidationLabels';

const card = 'bg-d-surface-container-lowest border border-d-border-subtle rounded-2xl p-5';

// Hành động Seller tiếp theo theo giai đoạn thỏa thuận (server vẫn kiểm lại trạng thái).
const STAGE_ACTIONS = {
  WAITING_SIGNATURES: { key: 'sign', button: 'Ký hợp đồng đặt cọc', ask: 'Xác nhận ký hợp đồng đặt cọc với chủ kho trúng giá?' },
  WAITING_DEPOSIT_CONFIRM: { key: 'deposit', button: 'Tôi đã nhận tiền cọc', ask: 'Xác nhận tài khoản của bạn đã nhận đủ tiền cọc?' },
  WAITING_SELLER_CONFIRM: { key: 'payment', button: 'Tôi đã nhận đủ tiền', ask: 'Xác nhận bạn đã nhận đủ tiền?' },
  WAITING_HANDOVER: { key: 'handover', button: 'Tôi đã bàn giao lô', ask: 'Xác nhận đã bàn giao lô cho chủ kho?' },
};

function Row({ label, value }) {
  return (
    <div className="flex justify-between gap-4 text-sm py-1.5">
      <span className="text-d-on-surface-variant">{label}</span>
      <span className="font-semibold text-d-on-surface text-right">{value}</span>
    </div>
  );
}

// Đếm ngược theo giờ server (lệch đồng hồ máy được bù bằng serverTime của lần tải gần nhất).
function useCountdown(target, serverTime, fetchedAt) {
  const [tick, setTick] = useState(0);
  useEffect(() => {
    const timer = setInterval(() => setTick((t) => t + 1), 1000);
    return () => clearInterval(timer);
  }, []);
  if (!target || !serverTime) return null;
  const offset = new Date(serverTime).getTime() - fetchedAt;
  void tick;
  return Math.max(0, new Date(target).getTime() - (Date.now() + offset));
}
function formatRemaining(ms) {
  const total = Math.floor(ms / 1000);
  const d = Math.floor(total / 86400), h = Math.floor((total % 86400) / 3600), m = Math.floor((total % 3600) / 60), s = total % 60;
  return `${d > 0 ? `${d} ngày ` : ''}${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
}

function PricePanel({ lot, fetchedAt }) {
  const current = lot.highestPrice ?? lot.startingPrice;
  const nextMin = lot.highestPrice ? lot.highestPrice + lot.bidIncrement : lot.startingPrice;
  const raise = lot.highestPrice ? ((lot.highestPrice - lot.startingPrice) / lot.startingPrice) * 100 : 0;
  const bidding = lot.phase === 'BIDDING' || lot.phase === 'CLOSING';
  const remaining = useCountdown(bidding ? lot.biddingEnd : lot.biddingStart, lot.serverTime, fetchedAt);
  const deposit = Math.round((current * lot.depositPercent) / 100);
  return (
    <section className={`${card} border-d-primary`} aria-label="Giá hiện tại" aria-live="polite">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-xs font-semibold text-d-on-surface-variant">
            {lot.highestPrice ? `Giá cao nhất hiện tại • ${lot.bidCount} lượt trả giá` : 'Giá khởi điểm • chưa có lượt trả giá'}
          </p>
          <p className="text-3xl md:text-4xl font-bold text-d-primary mt-1">{formatCurrency(current)}</p>
          <p className="text-xs text-d-on-surface-variant mt-1">
            {lot.highestPrice ? `Cao hơn giá khởi điểm ${raise.toFixed(1)}%` : 'Giá trọn lô (VND)'}
            {lot.depositPercent > 0 && ` • Cọc ${lot.depositPercent}% ≈ ${formatCurrency(deposit)}`}
          </p>
        </div>
        <div className="text-right">
          <p className="text-xs font-semibold text-d-on-surface-variant">Mức tối thiểu cho lượt tiếp theo</p>
          <p className="text-lg font-bold text-d-on-surface">{formatCurrency(nextMin)}</p>
          {remaining !== null && ['UPCOMING', 'SURVEY', 'BIDDING', 'CLOSING'].includes(lot.phase) && (
            <p className="text-sm mt-2 font-semibold text-d-on-surface">
              {bidding ? 'Còn lại: ' : 'Mở trả giá sau: '}
              <span className="tabular-nums">{formatRemaining(remaining)}</span>
            </p>
          )}
        </div>
      </div>
    </section>
  );
}

export default function LiquidationDetail() {
  const { id } = useParams();
  const queryClient = useQueryClient();
  const [bidPage, setBidPage] = useState(1);
  const [cancelOpen, setCancelOpen] = useState(false);
  const [reason, setReason] = useState('');
  const [confirming, setConfirming] = useState(null);

  const { data, isLoading, isError, refetch, dataUpdatedAt } = useQuery({
    queryKey: ['seller-liquidation', id],
    queryFn: () => sellerApi.getLiquidation(id),
    // Đang trong phiên thì tải lại thường xuyên để thấy giá mới; client không tự kết luận ai thắng.
    refetchInterval: (query) => {
      const phase = query.state.data?.lot?.phase;
      return phase === 'BIDDING' || phase === 'CLOSING' ? 5000 : 30000;
    },
  });
  const { data: bids } = useQuery({
    queryKey: ['seller-liquidation-bids', id, bidPage],
    queryFn: () => sellerApi.getLiquidationBids(id, bidPage),
    placeholderData: keepPreviousData,
    refetchInterval: 5000,
    enabled: !!data,
  });

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ['seller-liquidation', id] });
    queryClient.invalidateQueries({ queryKey: ['seller-liquidation-bids', id] });
    queryClient.invalidateQueries({ queryKey: ['seller-liquidations'] });
  };
  const onError = (err) => toast.error(err?.response?.data?.message || 'Thao tác thất bại. Vui lòng thử lại.');

  const cancelMutation = useMutation({
    mutationFn: () => sellerApi.cancelLiquidation(id, reason.trim()),
    onSuccess: () => { toast.success('Đã hủy lô thanh lý.'); setCancelOpen(false); setReason(''); refresh(); },
    onError: (err) => { onError(err); refresh(); },
  });
  const stepMutation = useMutation({
    mutationFn: (kind) => {
      const agreementId = data.agreement.id;
      if (kind === 'sign') return sellerApi.signLiquidation(agreementId);
      if (kind === 'deposit') return sellerApi.confirmLiquidationDeposit(agreementId);
      if (kind === 'payment') return sellerApi.confirmLiquidationPayment(agreementId);
      return sellerApi.confirmLiquidationHandover(agreementId);
    },
    onSuccess: () => { toast.success('Đã ghi nhận.'); setConfirming(null); refresh(); },
    onError: (err) => { onError(err); setConfirming(null); refresh(); },
  });

  if (isLoading) {
    return <div className="flex justify-center py-20"><div className="w-8 h-8 border-3 border-d-primary border-t-transparent rounded-full animate-spin" /></div>;
  }
  if (isError || !data) {
    return (
      <div className="p-6 max-w-xl mx-auto text-center">
        <p className="text-sm font-semibold text-red-700">Không tải được lô thanh lý (có thể lô không tồn tại).</p>
        <div className="mt-3 flex justify-center gap-4 text-sm font-semibold text-d-primary">
          <button type="button" onClick={() => refetch()} className="underline">Thử lại</button>
          <Link to="/seller/liquidations" className="underline">Về danh sách</Link>
        </div>
      </div>
    );
  }

  const { lot, agreement } = data;
  const bidTotalPages = Math.max(1, Math.ceil((bids?.totalCount ?? 0) / 20));
  const topBid = bids?.items?.[0]?.amount ?? lot.highestPrice ?? lot.startingPrice;
  const reasonValid = reason.trim().length >= 5;
  const action = agreement ? STAGE_ACTIONS[agreement.stage] : null;
  const showContract = agreement && agreement.depositAmount > 0;

  return (
    <div className="flex flex-col p-4 md:p-6 gap-6 w-full max-w-5xl mx-auto">
      <div>
        <Link to="/seller/liquidations" className="text-xs font-semibold text-d-primary">← Lô thanh lý của tôi</Link>
        <div className="flex flex-wrap items-center gap-3 mt-2">
          <h1 className="text-d-headline-xl-mobile md:text-d-headline-xl font-d-headline-xl text-d-on-surface">{lot.title}</h1>
          <span className={`text-[11px] font-bold px-3 py-1 rounded-full border ${LIQUIDATION_PHASE_STYLE[lot.phase] ?? ''}`}>
            {LIQUIDATION_PHASE_LABEL[lot.phase] ?? lot.phase}
          </span>
        </div>
        <p className="text-sm text-d-on-surface-variant mt-1">📍 {lot.publicArea}</p>
      </div>

      {lot.status === 'CANCELLED' && (
        <div role="status" className="rounded-2xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">
          Lô đã hủy. Lý do: {data.cancelReason}
        </div>
      )}
      {lot.status === 'NO_SALE' && (
        <div role="status" className="rounded-2xl border border-d-border-subtle bg-d-surface-container p-4 text-sm text-d-on-surface-variant">
          Phiên đã kết thúc và không có lượt trả giá hợp lệ. Bạn có thể đăng lô mới với giá hoặc lịch khác.
        </div>
      )}

      <PricePanel lot={lot} fetchedAt={dataUpdatedAt} />

      <section className={card} aria-label="Bảng giá đấu">
        <div className="flex items-baseline justify-between gap-3 mb-2">
          <h2 className="text-sm font-bold text-d-on-surface">Bảng giá đấu (cao → thấp)</h2>
          <span className="text-xs text-d-on-surface-variant">Khởi điểm {formatCurrency(lot.startingPrice)} • bước giá {formatCurrency(lot.bidIncrement)}</span>
        </div>
        {!bids || bids.items.length === 0 ? (
          <p className="text-sm text-d-on-surface-variant">Chưa có lượt trả giá. Giá hiện đang ở mức khởi điểm.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-xs text-d-on-surface-variant">
                  <th className="py-2 pr-3">#</th><th className="py-2 pr-4">Kho</th><th className="py-2 pr-4">Giá</th><th className="py-2 pr-4 w-1/3">Mức</th><th className="py-2">Thời điểm</th>
                </tr>
              </thead>
              <tbody>
                {bids.items.map((b, i) => (
                  <tr key={b.id} className={`border-t border-d-border-subtle ${b.leading ? 'bg-emerald-50/60' : ''}`}>
                    <td className="py-2 pr-3 text-d-on-surface-variant">{(bidPage - 1) * 20 + i + 1}</td>
                    <td className="py-2 pr-4">{b.depotName} {b.leading && <span className="ml-1 text-[11px] font-bold text-d-primary">Dẫn đầu</span>}</td>
                    <td className="py-2 pr-4 font-semibold">{formatCurrency(b.amount)}</td>
                    <td className="py-2 pr-4">
                      <div className="h-2 rounded-full bg-d-surface-container overflow-hidden" role="img" aria-label={`${Math.round((b.amount / topBid) * 100)}% so với giá cao nhất`}>
                        <div className="h-full bg-d-primary" style={{ width: `${Math.min(100, (b.amount / topBid) * 100)}%` }} />
                      </div>
                    </td>
                    <td className="py-2 text-d-on-surface-variant">{formatDate(b.createdAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {bidTotalPages > 1 && (
          <div className="flex items-center justify-center gap-3 text-sm mt-3">
            <button type="button" disabled={bidPage <= 1} onClick={() => setBidPage((p) => p - 1)} className="px-3 py-1.5 rounded-full border border-d-border-subtle disabled:opacity-40">Trước</button>
            <span className="text-d-on-surface-variant">Trang {bidPage}/{bidTotalPages}</span>
            <button type="button" disabled={bidPage >= bidTotalPages} onClick={() => setBidPage((p) => p + 1)} className="px-3 py-1.5 rounded-full border border-d-border-subtle disabled:opacity-40">Sau</button>
          </div>
        )}
      </section>

      {agreement && (
        <section className={`${card} border-d-primary`} aria-label="Thỏa thuận">
          <h2 className="text-sm font-bold text-d-on-surface mb-2">Thỏa thuận với chủ kho trúng giá</h2>
          <Row label="Kho trúng" value={agreement.depotName} />
          <Row label="Giá trúng (trọn lô, cố định)" value={formatCurrency(agreement.amount)} />
          {showContract && (
            <>
              <Row label={`Tiền cọc (${agreement.depositPercent}%)`} value={formatCurrency(agreement.depositAmount)} />
              <Row label="Phần còn lại" value={formatCurrency(agreement.remainingAmount)} />
              <Row label="Hai bên đã ký" value={`Bạn: ${agreement.sellerSignedAt ? formatDate(agreement.sellerSignedAt) : 'chưa'} • Chủ kho: ${agreement.buyerSignedAt ? formatDate(agreement.buyerSignedAt) : 'chưa'}`} />
              <Row label="Tham chiếu chuyển cọc" value={agreement.depositReference ?? '—'} />
              <Row label="Chủ kho báo chuyển cọc" value={agreement.depositReportedAt ? formatDate(agreement.depositReportedAt) : '—'} />
              <Row label="Bạn xác nhận nhận cọc" value={agreement.depositConfirmedAt ? formatDate(agreement.depositConfirmedAt) : '—'} />
            </>
          )}
          <Row label="Tham chiếu chuyển tiền" value={agreement.paymentReference ?? '—'} />
          <Row label="Chủ kho báo chuyển tiền" value={agreement.paymentReportedAt ? formatDate(agreement.paymentReportedAt) : '—'} />
          <Row label="Bạn xác nhận nhận tiền" value={agreement.paymentConfirmedAt ? formatDate(agreement.paymentConfirmedAt) : '—'} />
          <Row label="Bạn xác nhận bàn giao" value={agreement.sellerHandedOverAt ? formatDate(agreement.sellerHandedOverAt) : '—'} />
          <Row label="Chủ kho xác nhận thu gom" value={agreement.ownerCollectedAt ? formatDate(agreement.ownerCollectedAt) : '—'} />
          <p className="mt-3 text-sm font-semibold text-d-primary">{AGREEMENT_STAGE_LABEL[agreement.stage] ?? agreement.stage}</p>
          <p className="text-xs text-d-on-surface-variant mt-1">Chỉ xác nhận khi tài khoản ngân hàng của bạn đã nhận đủ tiền. Nền tảng không giữ hộ tiền.</p>

          {showContract && (
            <div className="mt-4">
              <DepositContract
                title={agreement.title} depotName={agreement.depotName} amount={agreement.amount}
                depositPercent={agreement.depositPercent} depositAmount={agreement.depositAmount} remainingAmount={agreement.remainingAmount}
                collectionDeadline={agreement.collectionDeadline} sellerSignedAt={agreement.sellerSignedAt} buyerSignedAt={agreement.buyerSignedAt}
              />
            </div>
          )}

          {action && !(action.key === 'sign' && agreement.sellerSignedAt) && (
            <div className="mt-4">
              {confirming ? (
                <div className="flex flex-wrap items-center gap-3">
                  <span className="text-sm text-d-on-surface">{STAGE_ACTIONS[agreement.stage].ask}</span>
                  <button type="button" disabled={stepMutation.isPending} onClick={() => stepMutation.mutate(confirming)}
                    className="px-5 py-2 rounded-full bg-d-primary text-d-on-primary text-sm font-semibold disabled:opacity-50">
                    {stepMutation.isPending ? 'Đang lưu...' : 'Đồng ý'}
                  </button>
                  <button type="button" disabled={stepMutation.isPending} onClick={() => setConfirming(null)}
                    className="px-5 py-2 rounded-full border border-d-border-subtle text-sm font-semibold">Không</button>
                </div>
              ) : (
                <button type="button" onClick={() => setConfirming(action.key)}
                  className="px-5 py-2.5 rounded-full bg-d-primary text-d-on-primary text-sm font-semibold active:scale-95 transition-all">
                  {action.button}
                </button>
              )}
            </div>
          )}
          {agreement.stage === 'WAITING_SIGNATURES' && agreement.sellerSignedAt && (
            <p className="mt-3 text-xs text-d-on-surface-variant">Bạn đã ký. Đang chờ chủ kho ký hợp đồng.</p>
          )}
        </section>
      )}

      <div className="grid gap-6 md:grid-cols-2">
        <section className={card} aria-label="Giá và lịch">
          <h2 className="text-sm font-bold text-d-on-surface mb-2">Giá & lịch phiên</h2>
          <Row label="Giá khởi điểm" value={formatCurrency(lot.startingPrice)} />
          <Row label="Bước giá" value={formatCurrency(lot.bidIncrement)} />
          <Row label="Tiền đặt cọc" value={`${lot.depositPercent}% giá trúng`} />
          <Row label="Kho đăng ký khảo sát" value={lot.participantCount} />
          <Row label="Khảo sát" value={`${formatDate(lot.surveyStart)} → ${formatDate(lot.surveyEnd)}`} />
          <Row label="Mở trả giá" value={formatDate(lot.biddingStart)} />
          <Row label="Đóng trả giá" value={formatDate(lot.biddingEnd)} />
          <Row label="Đóng tối đa (nếu gia hạn)" value={formatDate(lot.hardEnd)} />
          <Row label="Hạn thu gom" value={formatDate(lot.collectionDeadline)} />
        </section>

        <section className={card} aria-label="Nội dung lô">
          <h2 className="text-sm font-bold text-d-on-surface mb-2">Nội dung lô</h2>
          <p className="text-sm text-d-on-surface whitespace-pre-line">{data.description}</p>
          <p className="text-xs font-semibold text-d-on-surface-variant mt-3">Phạm vi tài sản</p>
          <p className="text-sm text-d-on-surface whitespace-pre-line">{data.scopeDetails}</p>
          <p className="text-xs font-semibold text-d-on-surface-variant mt-3">Điều kiện thu gom & thanh toán</p>
          <p className="text-sm text-d-on-surface whitespace-pre-line">{data.collectionTerms}</p>
          <p className="text-xs font-semibold text-d-on-surface-variant mt-3">Địa chỉ khảo sát • Liên hệ</p>
          <p className="text-sm text-d-on-surface">{data.address} • {data.contactPhone}</p>
          {lot.imageUrls?.length > 0 && (
            <div className="flex flex-wrap gap-2 mt-3">
              {lot.imageUrls.map((url, i) => (
                <img key={url} src={url} alt={`Ảnh lô ${i + 1}`} className="w-20 h-20 object-cover rounded-lg border border-d-border-subtle" />
              ))}
            </div>
          )}
        </section>
      </div>

      {lot.status === 'PUBLISHED' && (
        <section className={card} aria-label="Hủy lô">
          {data.canCancel ? (
            cancelOpen ? (
              <div className="grid gap-3">
                <label htmlFor="cancel-reason" className="text-xs font-semibold text-d-on-surface-variant">Lý do hủy (hiển thị cho các kho đã đăng ký)</label>
                <textarea id="cancel-reason" rows={2} maxLength={1000} value={reason} onChange={(e) => setReason(e.target.value)}
                  className="w-full bg-d-surface-container-lowest border border-d-border-subtle rounded-xl px-4 py-3 text-sm outline-none focus:border-d-primary" />
                <div className="flex gap-3">
                  <button type="button" disabled={!reasonValid || cancelMutation.isPending} onClick={() => cancelMutation.mutate()}
                    className="px-5 py-2 rounded-full bg-red-600 text-white text-sm font-semibold disabled:opacity-50">
                    {cancelMutation.isPending ? 'Đang hủy...' : 'Xác nhận hủy lô'}
                  </button>
                  <button type="button" disabled={cancelMutation.isPending} onClick={() => setCancelOpen(false)}
                    className="px-5 py-2 rounded-full border border-d-border-subtle text-sm font-semibold">Giữ lô</button>
                </div>
              </div>
            ) : (
              <button type="button" onClick={() => setCancelOpen(true)} className="text-sm font-semibold text-red-600 underline">Hủy lô thanh lý</button>
            )
          ) : (
            <p className="text-xs text-d-on-surface-variant">
              {lot.bidCount > 0
                ? 'Phiên đã có lượt trả giá nên không thể tự hủy để bảo vệ người đã trả giá. Liên hệ Admin nếu có sự cố.'
                : 'Phiên đã hết hạn nhận giá nên không thể hủy.'}
            </p>
          )}
        </section>
      )}
    </div>
  );
}
