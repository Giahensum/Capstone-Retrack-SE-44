import { useState } from 'react';
import { useMutation, useQuery, useQueryClient, keepPreviousData } from '@tanstack/react-query';
import { Link, useParams } from 'react-router-dom';
import toast from 'react-hot-toast';
import {
  ArrowLeft, MapPin, Phone, Search, Gavel, Timer, PackageCheck, BadgePercent, Building2, FileText, Layers, Handshake,
  Images, Trophy, FileSignature, Wallet, Banknote, Truck, CheckCircle2, XCircle, AlertTriangle, Coins, Hourglass, Ban,
  CalendarClock, Users, HandCoins,
} from 'lucide-react';
import { sellerApi } from '../api';
import { formatCurrency, formatDate } from '@/lib/utils';
import DepositContract from '@/components/liquidation/DepositContract';
import {
  Modal, ConfirmModal, Section, Tile, Notice, Steps, Badge, PriceHero, PriceBar, RankIcon, PHASE_TONE,
} from '@/components/liquidation/LiquidationUI';
import { AGREEMENT_STAGE_LABEL, LIQUIDATION_PHASE_LABEL } from '../liquidationLabels';

// Hành động Seller tiếp theo theo giai đoạn thỏa thuận (server vẫn kiểm lại trạng thái).
const STAGE_ACTIONS = {
  WAITING_SIGNATURES: { key: 'sign', icon: FileSignature, button: 'Ký hợp đồng đặt cọc', title: 'Ký hợp đồng đặt cọc?', ask: 'Bạn đồng ý các điều khoản của hợp đồng đặt cọc với chủ kho trúng giá. Việc ký được ghi lại thời điểm và không thể hoàn tác.' },
  WAITING_DEPOSIT_CONFIRM: { key: 'deposit', icon: Wallet, button: 'Tôi đã nhận tiền cọc', title: 'Xác nhận đã nhận tiền cọc?', ask: 'Chỉ xác nhận khi tài khoản ngân hàng của bạn đã nhận đủ tiền cọc. Nền tảng không giữ hộ tiền.' },
  WAITING_SELLER_CONFIRM: { key: 'payment', icon: Banknote, button: 'Tôi đã nhận đủ tiền', title: 'Xác nhận đã nhận đủ tiền?', ask: 'Chỉ xác nhận khi tài khoản ngân hàng của bạn đã nhận đủ phần tiền còn lại. Nền tảng không giữ hộ tiền.' },
  WAITING_HANDOVER: { key: 'handover', icon: Truck, button: 'Tôi đã bàn giao lô', title: 'Xác nhận đã bàn giao lô?', ask: 'Xác nhận bạn đã bàn giao toàn bộ lô hàng cho chủ kho.' },
};

function Row({ label, value }) {
  return (
    <div className="flex items-baseline justify-between gap-4 border-b border-d-border-subtle/60 py-2 text-sm last:border-0">
      <span className="text-d-on-surface-variant">{label}</span>
      <span className="break-words text-right font-semibold text-d-on-surface">{value}</span>
    </div>
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
    return <div className="flex justify-center py-20"><div className="size-8 animate-spin rounded-full border-4 border-d-primary border-t-transparent" /></div>;
  }
  if (isError || !data) {
    return (
      <div className="mx-auto max-w-xl p-6 text-center">
        <XCircle className="mx-auto mb-2 text-red-500" size={36} />
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
  const hasDeposit = agreement?.depositAmount > 0;
  const action = agreement ? STAGE_ACTIONS[agreement.stage] : null;
  const canAct = action && !(action.key === 'sign' && agreement.sellerSignedAt);

  let steps = [];
  if (agreement) {
    const bothSigned = !!(agreement.sellerSignedAt && agreement.buyerSignedAt);
    const flags = hasDeposit
      ? [bothSigned, !!agreement.depositConfirmedAt, !!agreement.paymentConfirmedAt, !!agreement.sellerHandedOverAt, !!agreement.ownerCollectedAt]
      : [!!agreement.paymentConfirmedAt, !!agreement.sellerHandedOverAt, !!agreement.ownerCollectedAt];
    const defs = hasDeposit
      ? [['sign', 'Ký hợp đồng', FileSignature], ['deposit', 'Nhận cọc', Wallet], ['pay', 'Nhận tiền', Banknote], ['hand', 'Bàn giao', Truck], ['done', 'Hoàn tất', CheckCircle2]]
      : [['pay', 'Nhận tiền', Banknote], ['hand', 'Bàn giao', Truck], ['done', 'Hoàn tất', CheckCircle2]];
    const firstOpen = flags.findIndex((f) => !f);
    steps = defs.map(([key, label, icon], i) => ({ key, label, icon, state: flags[i] ? 'done' : i === firstOpen ? 'current' : 'todo' }));
  }

  return (
    <div className="mx-auto flex w-full max-w-5xl flex-col gap-5 p-4 md:p-6">
      <div>
        <Link to="/seller/liquidations" className="inline-flex items-center gap-1.5 text-xs font-semibold text-d-primary hover:underline"><ArrowLeft size={14} />Lô thanh lý của tôi</Link>
        <div className="mt-2 flex flex-wrap items-center gap-3">
          <h1 className="text-2xl font-black text-d-on-surface">{lot.title}</h1>
          <Badge tone={PHASE_TONE[lot.phase] ?? 'slate'}>{LIQUIDATION_PHASE_LABEL[lot.phase] ?? lot.phase}</Badge>
        </div>
        <p className="mt-1 flex items-center gap-1.5 text-sm text-d-on-surface-variant"><MapPin size={14} />{lot.publicArea}</p>
      </div>

      {lot.status === 'CANCELLED' && <Notice tone="red" icon={Ban}>Lô đã hủy. Lý do: {data.cancelReason}</Notice>}
      {lot.status === 'NO_SALE' && <Notice tone="slate" icon={Hourglass}>Phiên đã kết thúc và không có lượt trả giá hợp lệ. Bạn có thể đăng lô mới với giá hoặc lịch khác.</Notice>}

      <PriceHero lot={lot} fetchedAt={dataUpdatedAt} />

      {agreement && (
        <Section icon={Handshake} tone="teal" title="Thỏa thuận với chủ kho trúng giá" className="border-teal-200">
          <div className="grid gap-3 sm:grid-cols-3">
            <Tile icon={Building2} tone="teal" label="Kho trúng" value={agreement.depotName} />
            <Tile icon={Coins} tone="primary" label="Giá trúng (cố định)" value={formatCurrency(agreement.amount)} />
            {hasDeposit && <Tile icon={BadgePercent} tone="amber" label={`Cọc ${agreement.depositPercent}% • Còn lại`} value={`${formatCurrency(agreement.depositAmount)} • ${formatCurrency(agreement.remainingAmount)}`} />}
          </div>

          <div className="mt-5"><Steps steps={steps} /></div>
          <p className="mt-3 flex items-center gap-2 rounded-xl bg-d-primary/10 px-3 py-2 text-sm font-bold text-d-primary">
            <Hourglass size={15} aria-hidden="true" />{AGREEMENT_STAGE_LABEL[agreement.stage] ?? agreement.stage}
          </p>

          {canAct && (
            <div className="mt-4">
              <button type="button" onClick={() => setConfirming(action.key)} className="inline-flex items-center gap-2 rounded-full bg-d-primary px-6 py-3 text-sm font-bold text-white shadow-md transition active:scale-95">
                <action.icon size={17} />{action.button}
              </button>
            </div>
          )}
          {agreement.stage === 'WAITING_SIGNATURES' && agreement.sellerSignedAt && <p className="mt-3 text-xs text-d-on-surface-variant">Bạn đã ký. Đang chờ chủ kho ký hợp đồng.</p>}

          {hasDeposit && (
            <div className="mt-5">
              <DepositContract
                title={agreement.title} depotName={agreement.depotName} amount={agreement.amount}
                depositPercent={agreement.depositPercent} depositAmount={agreement.depositAmount} remainingAmount={agreement.remainingAmount}
                collectionDeadline={agreement.collectionDeadline} sellerSignedAt={agreement.sellerSignedAt} buyerSignedAt={agreement.buyerSignedAt}
              />
            </div>
          )}

          <div className="mt-5 grid gap-x-8 sm:grid-cols-2">
            {hasDeposit && <>
              <Row label="Tham chiếu chuyển cọc" value={agreement.depositReference ?? '—'} />
              <Row label="Chủ kho báo chuyển cọc" value={agreement.depositReportedAt ? formatDate(agreement.depositReportedAt) : '—'} />
              <Row label="Bạn xác nhận nhận cọc" value={agreement.depositConfirmedAt ? formatDate(agreement.depositConfirmedAt) : '—'} />
            </>}
            <Row label="Tham chiếu chuyển tiền" value={agreement.paymentReference ?? '—'} />
            <Row label="Chủ kho báo chuyển tiền" value={agreement.paymentReportedAt ? formatDate(agreement.paymentReportedAt) : '—'} />
            <Row label="Bạn xác nhận nhận tiền" value={agreement.paymentConfirmedAt ? formatDate(agreement.paymentConfirmedAt) : '—'} />
            <Row label="Bạn xác nhận bàn giao" value={agreement.sellerHandedOverAt ? formatDate(agreement.sellerHandedOverAt) : '—'} />
            <Row label="Chủ kho xác nhận thu gom" value={agreement.ownerCollectedAt ? formatDate(agreement.ownerCollectedAt) : '—'} />
          </div>
        </Section>
      )}

      <Section icon={Trophy} tone="amber" title="Bảng giá đấu (cao → thấp)" action={<span className="text-xs text-d-on-surface-variant">Khởi điểm {formatCurrency(lot.startingPrice)} • bước {formatCurrency(lot.bidIncrement)}</span>}>
        {!bids || bids.items.length === 0 ? (
          <p className="text-sm text-d-on-surface-variant">Chưa có lượt trả giá. Giá hiện đang ở mức khởi điểm.</p>
        ) : (
          <div className="space-y-2">
            {bids.items.map((b, i) => (
              <div key={b.id} className={`rounded-xl border p-3 ${b.leading ? 'border-d-primary/40 bg-d-surface-accent' : 'border-d-border-subtle bg-white'}`}>
                <div className="flex items-center gap-3">
                  <RankIcon rank={(bidPage - 1) * 20 + i + 1} />
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm font-semibold">{b.depotName}{b.leading && <span className="ml-2 text-[11px] font-black text-amber-600">DẪN ĐẦU</span>}</p>
                    <p className="text-xs text-d-on-surface-variant">{formatDate(b.createdAt)}</p>
                  </div>
                  <strong className="text-base">{formatCurrency(b.amount)}</strong>
                </div>
                <div className="mt-2"><PriceBar amount={b.amount} top={topBid} leading={b.leading} /></div>
              </div>
            ))}
          </div>
        )}
        {bidTotalPages > 1 && (
          <div className="mt-3 flex items-center justify-center gap-3 text-sm">
            <button type="button" disabled={bidPage <= 1} onClick={() => setBidPage((p) => p - 1)} className="rounded-full border border-d-border-subtle px-4 py-1.5 disabled:opacity-40">Trước</button>
            <span className="text-d-on-surface-variant">Trang {bidPage}/{bidTotalPages}</span>
            <button type="button" disabled={bidPage >= bidTotalPages} onClick={() => setBidPage((p) => p + 1)} className="rounded-full border border-d-border-subtle px-4 py-1.5 disabled:opacity-40">Sau</button>
          </div>
        )}
      </Section>

      <div className="grid gap-3 sm:grid-cols-2">
        <Tile icon={Search} tone="blue" label="Khảo sát" value={`${formatDate(lot.surveyStart)} → ${formatDate(lot.surveyEnd)}`} />
        <Tile icon={Users} tone="teal" label="Kho đăng ký khảo sát" value={lot.participantCount} />
        <Tile icon={Gavel} tone="primary" label="Mở trả giá" value={formatDate(lot.biddingStart)} />
        <Tile icon={Timer} tone="amber" label="Đóng trả giá" value={formatDate(lot.biddingEnd)} hint={`Gia hạn tối đa tới ${formatDate(lot.hardEnd)}`} />
        <Tile icon={PackageCheck} tone="teal" label="Hạn thu gom" value={formatDate(lot.collectionDeadline)} />
        <Tile icon={HandCoins} tone="lime" label="Tiền đặt cọc" value={`${lot.depositPercent}% giá trúng`} />
      </div>

      <Section icon={FileText} title="Nội dung lô">
        <p className="whitespace-pre-line text-sm leading-relaxed">{data.description}</p>
        <div className="mt-3 rounded-xl bg-d-surface-container-low p-3"><p className="mb-1 flex items-center gap-1.5 text-xs font-bold text-d-on-surface-variant"><Layers size={13} />Phạm vi tài sản</p><p className="whitespace-pre-line text-sm">{data.scopeDetails}</p></div>
        <div className="mt-3 rounded-xl bg-d-surface-container-low p-3"><p className="mb-1 flex items-center gap-1.5 text-xs font-bold text-d-on-surface-variant"><Handshake size={13} />Điều kiện thu gom & thanh toán</p><p className="whitespace-pre-line text-sm">{data.collectionTerms}</p></div>
        <div className="mt-3 grid gap-3 sm:grid-cols-2">
          <Tile icon={MapPin} tone="blue" label="Địa chỉ khảo sát" value={data.address} />
          <Tile icon={Phone} tone="blue" label="Liên hệ" value={data.contactPhone} />
        </div>
        {lot.imageUrls?.length > 0 && (
          <div className="mt-4">
            <p className="mb-2 flex items-center gap-1.5 text-xs font-bold text-d-on-surface-variant"><Images size={13} />Ảnh ({lot.imageUrls.length})</p>
            <div className="flex flex-wrap gap-2">
              {lot.imageUrls.map((url, i) => <img key={url} src={url} alt={`Ảnh lô ${i + 1}`} className="size-24 rounded-xl border border-d-border-subtle object-cover" />)}
            </div>
          </div>
        )}
      </Section>

      {lot.status === 'PUBLISHED' && (
        <Section icon={CalendarClock} tone="red" title="Hủy lô">
          {data.canCancel ? (
            <button type="button" onClick={() => setCancelOpen(true)} className="inline-flex items-center gap-2 rounded-full border border-red-200 bg-red-50 px-5 py-2.5 text-sm font-bold text-red-600 transition hover:bg-red-100"><Ban size={16} />Hủy lô thanh lý</button>
          ) : (
            <p className="text-xs text-d-on-surface-variant">
              {lot.bidCount > 0
                ? 'Phiên đã có lượt trả giá nên không thể tự hủy để bảo vệ người đã trả giá. Liên hệ Admin nếu có sự cố.'
                : 'Phiên đã hết hạn nhận giá nên không thể hủy.'}
            </p>
          )}
        </Section>
      )}

      {confirming && canAct && (
        <ConfirmModal
          icon={action.icon} title={action.title} description={action.ask} confirmLabel={action.button}
          busy={stepMutation.isPending} onCancel={() => setConfirming(null)} onConfirm={() => stepMutation.mutate(confirming)}
        />
      )}

      {cancelOpen && (
        <Modal
          size="md" icon={AlertTriangle} tone="red" title="Hủy lô thanh lý" subtitle="Lý do hủy sẽ hiển thị cho các kho đã đăng ký" onClose={() => setCancelOpen(false)} busy={cancelMutation.isPending}
          footer={(
            <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
              <button type="button" disabled={cancelMutation.isPending} onClick={() => setCancelOpen(false)} className="rounded-full border border-d-border-subtle bg-white px-5 py-2.5 text-sm font-semibold text-d-on-surface-variant">Giữ lô</button>
              <button type="button" disabled={reason.trim().length < 5 || cancelMutation.isPending} onClick={() => cancelMutation.mutate()} className="rounded-full bg-red-600 px-6 py-2.5 text-sm font-bold text-white shadow-sm disabled:opacity-50">
                {cancelMutation.isPending ? 'Đang hủy…' : 'Xác nhận hủy lô'}
              </button>
            </div>
          )}
        >
          <label htmlFor="cancel-reason" className="block text-xs font-semibold text-d-on-surface-variant">Lý do hủy (tối thiểu 5 ký tự)</label>
          <textarea id="cancel-reason" rows={4} maxLength={1000} value={reason} onChange={(e) => setReason(e.target.value)} placeholder="VD: Thay đổi kế hoạch thanh lý…"
            className="w-full rounded-xl border border-d-border-subtle bg-white px-4 py-3 text-sm outline-none focus:border-d-primary focus:ring-1 focus:ring-d-primary" />
        </Modal>
      )}
    </div>
  );
}
