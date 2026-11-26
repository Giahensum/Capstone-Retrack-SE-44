import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { ArrowLeft, MapPin, Layers, Users, Timer, Handshake, History, Flag, AlertTriangle, Info, CheckCircle2, PackageCheck } from 'lucide-react';
import { sellerApi } from '../api';
import { formatCurrency } from '@/lib/utils';
import { Badge, Notice, Section, Tile, PHASE_TONE, useCountdown, formatRemaining } from '@/components/liquidation/LiquidationUI';
import { LIQUIDATION_PHASE_LABEL } from '../liquidationLabels';

const UNIT = { kg: 'kg', cai: 'cái', bo: 'bộ' };
const qty = (v, u) => `${Number(v).toLocaleString('vi-VN', { maximumFractionDigits: 3 })} ${UNIT[u] ?? u}`;
const local = (v) => (v ? new Date(v).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' }) : 'Chưa có');
const STAGE = { WAITING_BUYER_PAYMENT: 'Chờ kho thanh toán', WAITING_SELLER_CONFIRM: 'Kho báo đã thanh toán — chờ bạn xác nhận', WAITING_HANDOVER: 'Chờ bạn bàn giao',
  WAITING_OWNER_RECEIVE: 'Chờ kho xác nhận nhận hàng', COMPLETED: 'Hoàn tất', DISPUTED: 'Chờ Admin xử lý', CANCELLED: 'Admin đã hủy' };
const ITEM = { OPEN: ['Đang mở', 'blue'], WON: ['Đã có kho thắng', 'primary'], UNSOLD: ['Không bán được', 'slate'] };
const btn = 'inline-flex min-h-11 items-center justify-center gap-2 rounded-full px-5 py-2.5 text-sm font-semibold transition active:scale-[.98] disabled:opacity-50';

export default function SellerLiquidationV2Detail() {
  const { id } = useParams();
  const client = useQueryClient();
  const query = useQuery({ queryKey: ['seller-liquidations-v2', id], queryFn: () => sellerApi.getLiquidationV2(id),
    refetchInterval: (q) => (q.state.data?.campaign.phase === 'BIDDING' ? 5000 : 30000) });
  const [cancelReason, setCancelReason] = useState('');
  const cancel = useMutation({ mutationFn: () => sellerApi.cancelLiquidationV2(id, cancelReason.trim()),
    onSuccess: () => { toast.success('Đã hủy phiên.'); client.invalidateQueries({ queryKey: ['seller-liquidations-v2'] }); },
    onError: (e) => toast.error(e?.response?.data?.message || 'Không hủy được phiên.') });
  const d = query.data, c = d?.campaign;
  const remaining = useCountdown(c?.phase === 'BIDDING' ? c.biddingEnd : null, c?.serverTime, query.dataUpdatedAt);

  if (query.isLoading) return <p role="status" className="p-8">Đang tải phiên…</p>;
  if (query.isError) return <div role="alert" className="p-8 text-d-error">{query.error?.response?.data?.message || 'Không tải được phiên.'} <button className="underline" onClick={() => query.refetch()}>Thử lại</button></div>;

  return <div className="mx-auto flex w-full max-w-6xl flex-col gap-5 p-4 md:p-6">
    <div>
      <Link to="/seller/liquidations" className="inline-flex items-center gap-1.5 text-xs font-semibold text-d-primary hover:underline"><ArrowLeft size={14} />Lô thanh lý của tôi</Link>
      <div className="mt-2 flex flex-wrap items-center gap-3"><h1 className="text-2xl font-black text-d-on-surface">{c.title}</h1>
        <Badge tone={PHASE_TONE[c.phase] ?? 'slate'}>{LIQUIDATION_PHASE_LABEL[c.phase] ?? c.phase}</Badge></div>
      <p className="mt-1 flex items-center gap-1.5 text-sm text-d-on-surface-variant"><MapPin size={14} />{c.publicArea} · {c.itemCount} hạng mục</p>
    </div>
    {remaining !== null && <div aria-live="polite" className="flex flex-wrap items-center justify-between gap-3 rounded-2xl bg-gradient-to-br from-[#446900] to-[#2d4700] p-4 text-white">
      <p className="flex items-center gap-2 text-sm font-semibold text-[#a3e635]"><Timer size={16} />Phiên đóng chung sau</p>
      <p className="text-2xl font-black tabular-nums">{formatRemaining(remaining)}</p>
      {c.extensionCount > 0 && <p className="w-full text-xs text-white/80">Đã gia hạn {c.extensionCount}/5 lần vì có giá phút chót.</p>}
    </div>}
    {d.cancelReason && <Notice tone="red" icon={AlertTriangle}>Đã hủy: {d.cancelReason}</Notice>}
    <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
      <Tile icon={Users} tone="blue" label="Kho tham gia" value={d.participantCount} />
      <Tile icon={Layers} tone="primary" label="Lượt trả giá" value={c.bidCount} />
      <Tile icon={Timer} tone="amber" label="Đóng trả giá" value={local(c.biddingEnd)} />
      <Tile icon={PackageCheck} tone="teal" label="Hạn thu gom" value={c.phase === 'AWARDED' || c.phase === 'NO_SALE' ? local(c.collectionDeadline) : `${Math.round(c.collectionWindowHours / 24 * 10) / 10} ngày sau khi đóng`} />
    </div>
    <Section icon={Layers} title="Hạng mục">
      <div className="space-y-3">{d.items.map((x) => <ItemCard key={x.id} lotId={id} item={x} />)}</div>
    </Section>
    {d.agreements.length > 0 && <Section icon={Handshake} tone="teal" title={`Thỏa thuận với kho thắng (${d.agreements.length})`}>
      <div className="space-y-3">{d.agreements.map((a) => <AgreementCard key={a.id} a={a} />)}</div>
    </Section>}
    {d.canCancel && <Section icon={AlertTriangle} tone="red" title="Hủy phiên (chỉ khi chưa có lượt trả giá)">
      <textarea rows={2} maxLength={1000} className="w-full rounded-xl border border-d-border-subtle p-3 text-sm" placeholder="Lý do hủy (5–1000 ký tự)" value={cancelReason} onChange={(e) => setCancelReason(e.target.value)} />
      <button type="button" className={`${btn} mt-2 bg-d-error text-white`} disabled={cancel.isPending || cancelReason.trim().length < 5} onClick={() => cancel.mutate()}>Hủy phiên</button>
    </Section>}
    <Notice tone="blue" icon={Info}>Mỗi hạng mục có kho thắng riêng. Tiền mua = đơn giá trúng × số lượng bạn đã đăng, không tính lại theo cân sau mua. Hai bên tự thanh toán và bàn giao; bạn chỉ xác nhận khi đã nhận đủ tiền thật.</Notice>
  </div>;
}

function ItemCard({ lotId, item: x }) {
  const [open, setOpen] = useState(false);
  const [page, setPage] = useState(1);
  const bids = useQuery({ queryKey: ['seller-liquidations-v2', lotId, 'bids', x.id, page], enabled: open, queryFn: () => sellerApi.getLiquidationV2ItemBids(lotId, x.id, page) });
  const [label, tone] = ITEM[x.status] ?? [x.status, 'slate'];
  const unit = UNIT[x.unit] ?? x.unit;
  return <article className="rounded-2xl border border-d-border-subtle bg-white p-4">
    <div className="flex flex-wrap items-start justify-between gap-2">
      <div className="min-w-0"><h3 className="font-bold">{x.position}. {x.name}</h3><p className="text-sm text-d-on-surface-variant">{qty(x.quantity, x.unit)} · {x.description}</p></div>
      <Badge tone={tone}>{label}</Badge>
    </div>
    <dl className="mt-3 grid grid-cols-2 gap-2 text-sm sm:grid-cols-4">
      <div><dt className="text-xs text-d-on-surface-variant">Khởi điểm</dt><dd className="font-semibold">{formatCurrency(x.startingUnitPrice)}/{unit}</dd></div>
      <div><dt className="text-xs text-d-on-surface-variant">Cao nhất · {x.bidCount} lượt</dt><dd className="font-semibold">{x.highestUnitPrice == null ? 'Chưa có giá' : `${formatCurrency(x.highestUnitPrice)}/${unit}`}</dd></div>
      <div><dt className="text-xs text-d-on-surface-variant">Kho đăng ký</dt><dd className="font-semibold">{x.participantCount}</dd></div>
      <div><dt className="text-xs text-d-on-surface-variant">{x.winnerDepotName ? 'Kho thắng' : 'Thành tiền dự kiến'}</dt>
        <dd className="font-semibold">{x.winnerDepotName ? `${x.winnerDepotName} · ${formatCurrency(x.amount)}` : x.highestUnitPrice ? formatCurrency(Math.round(x.highestUnitPrice * x.quantity)) : '—'}</dd></div>
    </dl>
    {x.bidCount > 0 && <button type="button" className="mt-2 inline-flex items-center gap-1.5 text-xs font-semibold text-d-primary" onClick={() => setOpen((v) => !v)}><History size={13} />{open ? 'Ẩn' : 'Xem'} lịch sử giá</button>}
    {open && <div className="mt-2 rounded-xl bg-d-surface-container-low p-3 text-sm">
      {bids.isLoading ? <p>Đang tải…</p> : <ul className="space-y-1">{bids.data?.items.map((b) => <li key={`${b.rank}-${b.createdAt}`} className="flex flex-wrap justify-between gap-2">
        <span>#{b.rank} · {b.depotName}{b.leading && <strong className="ml-1 text-d-primary">(cao nhất)</strong>}</span>
        <span className="text-d-on-surface-variant">{local(b.createdAt)}</span><strong>{formatCurrency(b.unitPrice)}/{unit}</strong></li>)}</ul>}
      {(bids.data?.totalCount ?? 0) > 20 && <div className="mt-2 flex gap-2"><button disabled={page === 1} onClick={() => setPage(page - 1)} className="underline disabled:opacity-40">Trước</button><button disabled={page * 20 >= bids.data.totalCount} onClick={() => setPage(page + 1)} className="underline disabled:opacity-40">Sau</button></div>}
    </div>}
  </article>;
}

function AgreementCard({ a }) {
  const client = useQueryClient();
  const [reason, setReason] = useState('');
  const [abandon, setAbandon] = useState(false);
  const action = useMutation({ mutationFn: ({ kind, text }) => sellerApi.liquidationV2AgreementAction(a.id, kind, text),
    onSuccess: () => { toast.success('Đã ghi nhận.'); setAbandon(false); setReason(''); client.invalidateQueries({ queryKey: ['seller-liquidations-v2'] }); },
    onError: (e) => toast.error(e?.response?.data?.message || 'Không thực hiện được.') });
  const busy = action.isPending;
  return <article className="rounded-2xl border border-d-border-subtle bg-white p-4">
    <div className="flex flex-wrap items-center justify-between gap-2">
      <div><h3 className="font-bold">{a.depotName}</h3><p className="text-xs text-d-on-surface-variant">Người mua: {a.buyerName ?? '—'}{a.buyerPhone ? ` · ${a.buyerPhone}` : ''}</p></div>
      <Badge tone={a.stage === 'COMPLETED' ? 'primary' : a.stage === 'CANCELLED' ? 'red' : a.stage === 'DISPUTED' ? 'amber' : 'blue'}>{STAGE[a.stage] ?? a.stage}</Badge>
    </div>
    <ul className="mt-3 divide-y divide-d-border-subtle text-sm">{a.items.map((x) => <li key={x.id} className="flex flex-wrap justify-between gap-2 py-1.5">
      <span>{x.name} · {qty(x.quantity, x.unit)} × {formatCurrency(x.unitPrice)}</span><strong>{formatCurrency(x.amount)}</strong></li>)}</ul>
    <p className="mt-2 text-right text-sm">Tổng phải nhận: <strong className="text-lg text-d-primary">{formatCurrency(a.totalAmount)}</strong></p>
    {a.paymentReference && <p className="mt-1 text-xs text-d-on-surface-variant">Kho báo thanh toán lúc {local(a.paymentReportedAt)}: {a.paymentReference}</p>}
    {a.abandonReportedAt && <Notice tone="amber" icon={AlertTriangle}>{a.abandonReporterRole === 'SELLER' ? 'Bạn' : 'Kho'} báo bỏ mua: {a.abandonReason}</Notice>}
    {a.resolution && <Notice tone={a.resolution === 'CANCELLED' ? 'red' : 'primary'} icon={Info}>Admin: {a.resolution === 'CANCELLED' ? 'đã hủy thỏa thuận' : 'tiếp tục thực hiện'} — {a.resolutionNote}</Notice>}
    <div className="mt-3 flex flex-wrap justify-end gap-2">
      {a.nextAction === 'CONFIRM_PAYMENT' && <button className={`${btn} bg-d-primary text-white`} disabled={busy} onClick={() => action.mutate({ kind: 'confirm-payment' })}><CheckCircle2 size={16} />Đã nhận đủ {formatCurrency(a.totalAmount)}</button>}
      {a.nextAction === 'CONFIRM_HANDOVER' && <button className={`${btn} bg-d-primary text-white`} disabled={busy} onClick={() => action.mutate({ kind: 'confirm-handover' })}><PackageCheck size={16} />Đã bàn giao đủ hạng mục</button>}
      {!a.abandonReportedAt && !a.sellerHandedOverAt && !abandon && <button className={`${btn} text-d-error`} onClick={() => setAbandon(true)}><Flag size={15} />Báo bên mua bỏ mua</button>}
    </div>
    {abandon && <div className="mt-3 space-y-2 rounded-xl bg-d-error-container/30 p-3">
      <p className="text-xs">Thỏa thuận tạm dừng chờ Admin. Hệ thống không tự phạt hay chuyển hạng mục cho người trả giá khác.</p>
      <textarea rows={2} maxLength={1000} className="w-full rounded-xl border border-d-border-subtle p-3 text-sm" placeholder="Lý do (5–1000 ký tự)" value={reason} onChange={(e) => setReason(e.target.value)} />
      <div className="flex justify-end gap-2"><button className={`${btn} border border-d-border-subtle bg-white`} onClick={() => setAbandon(false)}>Hủy</button>
        <button className={`${btn} bg-d-error text-white`} disabled={busy || reason.trim().length < 5} onClick={() => action.mutate({ kind: 'abandon', text: reason.trim() })}>Gửi Admin xử lý</button></div>
    </div>}
  </article>;
}
