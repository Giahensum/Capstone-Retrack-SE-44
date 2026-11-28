import { useState } from 'react';
import { useIsMutating } from '@tanstack/react-query';
import {
  Layers, ClipboardCheck, Search, Gavel, MapPin, Phone, Lock, Timer, PackageCheck, FileText, Handshake, Info,
  Images, ShieldCheck, AlertTriangle, Building2, Crown, Plus, WifiOff, History,
} from 'lucide-react';
import { Form, Input } from '@/components/forms/ValidatedForm';
import { QueryState, MutationError, Pager, inputClass } from '@/features/depot/components/DepotUI';
import { money } from '@/features/depot/depotApi';
import { Modal, Section, Tile, Notice, Steps, Badge, PHASE_TONE, useCountdown, formatRemaining } from '@/components/liquidation/LiquidationUI';
import { liquidationLabels, localTime } from '../liquidationApi';
import { useV2Query, useV2Mutation, useV2Scope, unitLabel, quantity, newOperationId } from './liquidationV2Api';
import LiquidationV2Agreement from './LiquidationV2Agreement';

const primaryBtn = 'inline-flex items-center justify-center gap-2 min-h-11 rounded-full bg-d-primary px-5 py-2.5 text-sm font-semibold text-white shadow-sm transition hover:bg-[#385500] active:scale-[.98] disabled:pointer-events-none disabled:opacity-50';

/** Một popup duy nhất cho phiên nhiều hạng mục: đăng ký hạng mục, xác nhận khảo sát và trả giá từng dòng. */
export default function LiquidationV2Detail({ id, onClose, startWithAgreement = false }) {
  const scope = useV2Scope();
  const [openAgreement, setOpenAgreement] = useState(startWithAgreement);
  const probe = useV2Query(`/${id}`);
  const bidding = probe.data?.campaign.phase === 'BIDDING' || probe.data?.campaign.phase === 'CLOSING';
  // Cập nhật nhẹ 5 giây khi đang trả giá; server vẫn là nơi quyết định người dẫn.
  const query = useV2Query(`/${id}`, {}, { poll: bidding });
  const d = query.data ?? probe.data, c = d?.campaign;
  const busy = useIsMutating({ mutationKey: ['liquidation-v2', scope.userId, scope.depotId] }) > 0;

  if (openAgreement && d?.agreementId) return <LiquidationV2Agreement id={d.agreementId} onClose={() => setOpenAgreement(false)} />;

  return <Modal icon={Layers} tone={PHASE_TONE[c?.phase] ?? 'primary'} size="xl" title={c?.title ?? 'Phiên thanh lý nhiều hạng mục'} onClose={onClose} busy={busy}
    badge={c && <Badge tone={PHASE_TONE[c.phase] ?? 'slate'}>{liquidationLabels[c.phase] ?? c.phase}</Badge>}
    subtitle={c && <span className="inline-flex items-center gap-1"><MapPin size={13} aria-hidden="true" />{c.publicArea} · {c.itemCount} hạng mục</span>}>
    <QueryState query={query.data ? query : probe}>{d && <>
      {query.isError && query.data && <Notice tone="amber" icon={WifiOff}>Mất kết nối tạm thời, đang hiển thị dữ liệu lúc {localTime(query.dataUpdatedAt)}. Giá có thể đã thay đổi.</Notice>}
      <Clock campaign={c} fetchedAt={query.dataUpdatedAt || probe.dataUpdatedAt} />
      {d.cancelReason && <Notice tone="red" icon={AlertTriangle}>Lý do hủy: {d.cancelReason}</Notice>}
      {d.agreementId && <Section icon={Handshake} tone="teal" title={`Kho thắng ${c.wonItemCount} hạng mục`} className="border-teal-200 bg-teal-50/40">
        <p className="mb-3 text-sm text-d-on-surface-variant">{c.pricingBasis === 'CONFIRMED_HANDOVER_QUANTITY' ? 'Chốt cân / kiểm đếm với Seller rồi thanh toán theo lượng đã xác nhận × đơn giá trúng.' : 'Phiên cũ giữ tiền mua theo số lượng Seller đã đăng.'} Hai bên tự thanh toán và bàn giao.</p>
        <button type="button" className={primaryBtn} onClick={() => setOpenAgreement(true)}><Handshake size={16} />Mở thỏa thuận & nhận hàng</button>
      </Section>}
      {c.status === 'PUBLISHED' && <Participation id={id} d={d} busy={busy} />}
      <ItemsTable id={id} d={d} fetching={query.isFetching} />
      <div className="grid gap-3 sm:grid-cols-2">
        <Tile icon={Search} tone="blue" label="Khảo sát" value={`${localTime(c.surveyStart)} → ${localTime(c.surveyEnd)}`} hint="Đăng ký hạng mục trước giờ kết thúc khảo sát" />
        <Tile icon={Gavel} tone="primary" label="Mở trả giá" value={localTime(c.biddingStart)} hint="Mọi hạng mục dùng chung một giờ đóng" />
        <Tile icon={Timer} tone="amber" label="Đóng hiện tại" value={localTime(c.biddingEnd)} hint={`Còn ${c.extensionsLeft}/5 lượt gia hạn 2 phút, tối đa tới ${localTime(c.hardEnd)}`} />
        <Tile icon={PackageCheck} tone="teal" label="Hạn thu gom" value={c.phase === 'AWARDED' || c.phase === 'NO_SALE' ? localTime(c.collectionDeadline) : `${Math.round(c.collectionWindowHours / 24 * 10) / 10} ngày sau khi đóng`} />
        <Tile icon={d.address ? MapPin : Lock} tone={d.address ? 'blue' : 'slate'} label="Địa chỉ khảo sát" value={d.address ?? 'Đăng ký khảo sát để xem'} />
        <Tile icon={d.contactPhone ? Phone : Lock} tone={d.contactPhone ? 'blue' : 'slate'} label="Liên hệ bên bán" value={d.contactPhone ?? 'Chỉ hiện cho kho đã đăng ký'} />
      </div>
      <Section icon={FileText} title="Hiện trạng và điều kiện">
        <p className="whitespace-pre-wrap break-words text-sm leading-relaxed">{d.description}</p>
        <div className="mt-3 rounded-xl bg-d-surface-container-low p-3"><p className="mb-1 flex items-center gap-1.5 text-xs font-bold text-d-on-surface-variant"><Handshake size={13} />Điều kiện thu gom</p><p className="whitespace-pre-wrap break-words text-sm">{d.collectionTerms}</p></div>
      </Section>
      {c.imageUrls.length > 0 && <Section icon={Images} title={`Ảnh hiện trạng (${c.imageUrls.length})`}>
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">{c.imageUrls.map((url, i) => <a key={url} href={url} target="_blank" rel="noopener noreferrer"><img src={url} alt={`Hiện trạng ảnh ${i + 1}`} className="h-28 w-full rounded-xl object-cover" /></a>)}</div>
      </Section>}
      <Notice tone="blue" icon={Info}>Trả giá theo đơn giá (đ/kg, đ/cái, đ/bộ) cho từng hạng mục, mỗi hạng mục có kho thắng riêng và kho thắng nhận toàn bộ hạng mục. {c.pricingBasis === 'CONFIRMED_HANDOVER_QUANTITY' ? 'Tiền mua theo cân / kiểm đếm bàn giao được Seller xác nhận; hao hụt sau phân loại do kho chịu.' : 'Phiên cũ tính tiền theo lượng đã đăng.'} Giá hợp lệ trong 2 phút cuối cộng thêm 2 phút cho giờ đóng chung, tối đa 5 lần. RETRACK không thu hộ tiền hàng hay cọc.</Notice>
    </>}</QueryState>
  </Modal>;
}

function Clock({ campaign: c, fetchedAt }) {
  const bidding = c.phase === 'BIDDING';
  const remaining = useCountdown(bidding ? c.biddingEnd : c.phase === 'UPCOMING' || c.phase === 'SURVEY' ? c.biddingStart : null, c.serverTime, fetchedAt);
  if (remaining === null) return null;
  return <div aria-live="polite" className="flex flex-wrap items-center justify-between gap-3 rounded-2xl bg-gradient-to-br from-[#446900] to-[#2d4700] p-4 text-white">
    <p className="flex items-center gap-2 text-sm font-semibold text-[#a3e635]"><Timer size={16} aria-hidden="true" />{bidding ? 'Phiên đóng chung sau' : 'Mở trả giá sau'}</p>
    <p className="text-2xl font-black tabular-nums">{formatRemaining(remaining)}</p>
    {c.extensionCount > 0 && <p className="w-full text-xs text-white/80">Đã gia hạn {c.extensionCount} lần (còn {c.extensionsLeft}). Giờ đóng mới: {localTime(c.biddingEnd)}.</p>}
  </div>;
}

function Participation({ id, d, busy }) {
  const scope = useV2Scope();
  const c = d.campaign;
  const mine = d.participationDepotId === scope.depotId;
  const registered = new Set(d.items.filter((x) => x.registered).map((x) => x.id));
  const [selected, setSelected] = useState(() => new Set());
  const [consent, setConsent] = useState(false);
  const register = useV2Mutation('post', `/${id}/participation`, () => { setSelected(new Set()); setConsent(false); });
  const survey = useV2Mutation('patch', `/${id}/survey-confirmation`, () => setConsent(false));
  const now = new Date(c.serverTime).getTime();
  const choices = d.items.filter((x) => !registered.has(x.id));
  const steps = [
    { key: 'register', label: 'Chọn hạng mục', icon: ClipboardCheck, state: registered.size ? 'done' : 'current' },
    { key: 'survey', label: 'Xác nhận hiện trạng', icon: Search, state: d.surveyDeclared ? 'done' : registered.size ? 'current' : 'todo' },
    { key: 'bid', label: 'Trả giá từng dòng', icon: Gavel, state: d.surveyDeclared ? 'current' : 'todo' },
  ];
  const toggle = (itemId) => setSelected((prev) => {
    const next = new Set(prev);
    if (next.has(itemId)) next.delete(itemId); else next.add(itemId);
    return next;
  });
  if (d.participationDepotId && !mine) return <Notice tone="amber" icon={Building2}>Bạn đã tham gia phiên này bằng một kho khác. Chuyển về đúng kho đó để trả giá.</Notice>;
  return <Section icon={ClipboardCheck} title="Tham gia phiên">
    <Steps steps={steps} />
    <div className="mt-4 space-y-3 border-t border-d-border-subtle pt-4">
      {d.canRegister && choices.length > 0 && <fieldset className="space-y-2">
        <legend className="mb-1 text-sm font-semibold">{registered.size ? 'Đăng ký thêm hạng mục' : 'Chọn hạng mục muốn khảo sát và trả giá'}</legend>
        {choices.map((x) => <label key={x.id} className="flex cursor-pointer items-start gap-3 rounded-xl border border-d-border-subtle bg-white p-3 text-sm">
          <input type="checkbox" className="mt-0.5 size-4 accent-[#446900]" checked={selected.has(x.id)} disabled={busy} onChange={() => toggle(x.id)} />
          <span className="min-w-0"><strong>{x.name}</strong> · {quantity(x.quantity, x.unit)} · khởi điểm {money(x.startingUnitPrice)}/{unitLabel(x.unit)}</span>
        </label>)}
        <label className="flex cursor-pointer gap-3 rounded-xl bg-d-surface-container-low p-3 text-sm"><input type="checkbox" className="mt-0.5 size-4 accent-[#446900]" checked={consent} disabled={busy} onChange={(e) => setConsent(e.target.checked)} /><span>Tôi chấp nhận điều kiện đã công bố và đăng ký các hạng mục đã chọn cho kho đang chọn.</span></label>
        <button type="button" className={primaryBtn} disabled={!consent || !selected.size || busy} onClick={() => register.mutate({ accepted: true, itemIds: [...selected] })}><ClipboardCheck size={16} />Đăng ký {selected.size || ''} hạng mục</button>
      </fieldset>}
      {registered.size > 0 && !d.surveyDeclared && <>
        <label className="flex cursor-pointer gap-3 rounded-xl bg-d-surface-container-low p-3 text-sm"><input type="checkbox" className="mt-0.5 size-4 accent-[#446900]" checked={consent} disabled={busy} onChange={(e) => setConsent(e.target.checked)} /><span>Tôi đã tự khảo sát, hiểu hiện trạng và chịu hao hụt, tạp chất, chi phí phân loại sau mua của các hạng mục đã đăng ký.</span></label>
        <button type="button" className={primaryBtn} disabled={!consent || busy || now < new Date(c.surveyStart).getTime() || now >= new Date(c.biddingEnd).getTime()} onClick={() => survey.mutate({ accepted: true })}><Search size={16} />Xác nhận đã khảo sát</button>
      </>}
      {d.surveyDeclared && <Notice tone="primary" icon={ShieldCheck}>Kho đã xác nhận hiện trạng {registered.size} hạng mục. {d.canBid ? 'Nhập đơn giá ở từng dòng bên dưới.' : 'Bạn trả giá được khi phiên mở.'}</Notice>}
      {!d.canRegister && !registered.size && <Notice tone="slate" icon={Lock}>Đã hết thời gian đăng ký khảo sát cho phiên này.</Notice>}
      <MutationError mutation={register} /><MutationError mutation={survey} />
    </div>
  </Section>;
}

function ItemsTable({ id, d, fetching }) {
  return <Section icon={Gavel} title="Hạng mục và giá" action={fetching ? <span className="text-xs text-d-on-surface-variant">Đang cập nhật…</span> : null}>
    <div className="space-y-3">
      {d.items.map((item) => <ItemRow key={item.id} id={id} item={item} canBid={d.canBid && item.registered && item.status === 'OPEN'} />)}
    </div>
  </Section>;
}

function itemState(item, canBid) {
  if (item.status === 'WON') return item.wonByMe ? ['Kho bạn thắng', 'primary'] : ['Đã kết thúc', 'slate'];
  if (item.status === 'UNSOLD') return ['Không bán được', 'slate'];
  if (!item.registered) return ['Chưa tham gia', 'slate'];
  if (item.leading) return ['Đang dẫn', 'primary'];
  if (item.myUnitPrice != null) return ['Bị vượt', 'amber'];
  return [canBid ? 'Chưa trả giá' : 'Đã đăng ký', 'blue'];
}

/** Một dòng: Tên · Lượng · Cao nhất · Giá của kho · Ô giá mới · +1 bước · Trả giá. Mỗi dòng gửi và khóa độc lập. */
function ItemRow({ id, item, canBid }) {
  const [price, setPrice] = useState('');
  const [operationId, setOperationId] = useState(newOperationId);
  const [history, setHistory] = useState(false);
  const save = useV2Mutation('post', (b) => `/${id}/items/${b.itemId}/bids`, () => { setPrice(''); setOperationId(newOperationId()); }, `bid-${item.id}`);
  const unit = unitLabel(item.unit);
  const value = Number(price);
  const problem = price === '' ? null
    : !Number.isSafeInteger(value) || value <= 0 ? 'Đơn giá phải là số nguyên dương.'
    : value < item.nextMinUnitPrice ? `Thấp hơn mức tối thiểu ${money(item.nextMinUnitPrice)}/${unit}.`
    : (value - item.startingUnitPrice) % item.bidIncrement !== 0 ? `Phải bằng khởi điểm cộng bội số bước ${money(item.bidIncrement)}.` : null;
  const [stateLabel, tone] = itemState(item, canBid);
  const submit = (e) => {
    e.preventDefault();
    if (problem || price === '' || save.isPending) return;
    save.mutate({ itemId: item.id, payload: { operationId, unitPrice: value, expectedBidCount: item.bidCount } });
  };
  return <article className={`rounded-2xl border p-4 ${item.leading && item.status === 'OPEN' ? 'border-d-primary/40 bg-d-surface-accent' : 'border-d-border-subtle bg-white'} ${item.registered || item.status !== 'OPEN' ? '' : 'opacity-80'}`}>
    <div className="flex flex-wrap items-start justify-between gap-2">
      <div className="min-w-0">
        <h3 className="font-bold text-d-on-surface">{item.position}. {item.name}</h3>
        <p className="text-sm text-d-on-surface-variant">{quantity(item.quantity, item.unit)} · {item.description}</p>
      </div>
      <Badge tone={tone} icon={item.leading && item.status === 'OPEN' ? Crown : undefined}>{stateLabel}</Badge>
    </div>
    <dl className="mt-3 grid grid-cols-2 gap-2 text-sm sm:grid-cols-4">
      <div><dt className="text-xs text-d-on-surface-variant">Khởi điểm</dt><dd className="font-semibold">{money(item.startingUnitPrice)}/{unit}</dd></div>
      <div><dt className="text-xs text-d-on-surface-variant">Cao nhất · {item.bidCount} lượt</dt><dd className="font-semibold">{item.highestUnitPrice == null ? 'Chưa có giá' : `${money(item.highestUnitPrice)}/${unit}`}</dd>
        {item.highestTotal != null && <dd className="text-xs text-d-on-surface-variant">≈ {money(item.highestTotal)} cả hạng mục</dd>}</div>
      <div><dt className="text-xs text-d-on-surface-variant">Giá của kho</dt><dd className="font-semibold">{item.myUnitPrice == null ? '—' : `${money(item.myUnitPrice)}/${unit}`}</dd>
        {item.myTotal != null && <dd className="text-xs text-d-on-surface-variant">≈ {money(item.myTotal)} cả hạng mục</dd>}</div>
      <div><dt className="text-xs text-d-on-surface-variant">Bước giá</dt><dd className="font-semibold">{money(item.bidIncrement)}/{unit}</dd></div>
    </dl>
    {canBid && <Form className="mt-3 border-t border-d-border-subtle pt-3" validate={() => (problem ? { [`price-${item.id}`]: problem } : {})} onSubmit={submit}>
      <div className="flex flex-col gap-2 sm:flex-row sm:items-end">
        <label className="flex-1 text-xs font-semibold text-d-on-surface-variant">Đơn giá mới (đ/{unit}) — tối thiểu {money(item.nextMinUnitPrice)}
          <Input name={`price-${item.id}`} data-field-label={`Đơn giá mới ${item.name}`} type="text" inputMode="numeric" autoComplete="off"
            disabled={save.isPending} className={`${inputClass} text-base font-bold`} value={price} placeholder={String(item.nextMinUnitPrice)}
            onChange={(e) => setPrice(e.target.value.replace(/\D/g, ''))} />
          {price !== '' && Number.isSafeInteger(value) && <span className="mt-1 block text-xs font-normal">Thành tiền ≈ {money(Math.round(value * item.quantity))} cho {quantity(item.quantity, item.unit)}</span>}
        </label>
        {/* +1 bước chỉ điền mức hợp lệ kế tiếp, không gửi giá thay chủ kho. */}
        <button type="button" disabled={save.isPending} onClick={() => setPrice(String(price === '' || value < item.nextMinUnitPrice ? item.nextMinUnitPrice : value + item.bidIncrement))}
          className="inline-flex min-h-11 items-center justify-center gap-1.5 rounded-full border border-d-border-subtle bg-white px-4 text-sm font-semibold hover:bg-d-surface-container disabled:opacity-50"><Plus size={15} />1 bước</button>
        <button className={primaryBtn} disabled={save.isPending || price === '' || !!problem}><Gavel size={16} />{save.isPending ? 'Đang gửi…' : 'Trả giá'}</button>
      </div>
      <MutationError mutation={save} />
    </Form>}
    {item.myUnitPrice != null && <button type="button" className="mt-2 inline-flex items-center gap-1.5 text-xs font-semibold text-d-primary" onClick={() => setHistory((v) => !v)}><History size={13} />{history ? 'Ẩn' : 'Xem'} lịch sử giá của kho</button>}
    {history && <MyBids id={id} item={item} />}
  </article>;
}

function MyBids({ id, item }) {
  const [page, setPage] = useState(1);
  const query = useV2Query(`/${id}/items/${item.id}/bids`, { page });
  return <div className="mt-2 rounded-xl bg-d-surface-container-low p-3"><QueryState query={query}>
    <ul className="space-y-1 text-sm">{query.data?.items.map((x) => <li key={x.id} className="flex justify-between gap-2"><span className="text-d-on-surface-variant">{localTime(x.createdAt)}</span><strong>{money(x.unitPrice)}/{unitLabel(item.unit)}</strong></li>)}</ul>
    <Pager page={page} setPage={setPage} total={query.data?.totalCount} />
  </QueryState></div>;
}
