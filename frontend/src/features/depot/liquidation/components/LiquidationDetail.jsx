import { useState } from 'react';
import { useIsMutating } from '@tanstack/react-query';
import {
  Gavel, Search, ClipboardCheck, Timer, PackageCheck, MapPin, Phone, FileText, BadgePercent, Handshake,
  History, Lock, Trophy, UserCheck, AlertTriangle, Building2, Info, Images, Layers, ShieldCheck,
} from 'lucide-react';
import { QueryState, MutationError, Pager, inputClass } from '@/features/depot/components/DepotUI';
import { money } from '@/features/depot/depotApi';
import {
  Modal, Section, Tile, Notice, Steps, Badge, PriceHero, PriceBar, RankIcon, PHASE_TONE,
} from '@/components/liquidation/LiquidationUI';
import { useLiquidationQuery, useLiquidationMutation, useLiquidationScope, liquidationLabels, localTime } from '../liquidationApi';
import LiquidationAgreement from './LiquidationAgreement';

export default function LiquidationDetail({ id, onClose }) {
  const scope = useLiquidationScope();
  const query = useLiquidationQuery(`/${id}`, {}, true, true);
  const [consent, setConsent] = useState(false);
  const [agreement, setAgreement] = useState(false);
  const register = useLiquidationMutation('post', `/${id}/participation`, () => setConsent(false));
  const survey = useLiquidationMutation('patch', `/${id}/survey-confirmation`, () => setConsent(false));
  const d = query.data, lot = d?.lot;
  const busy = useIsMutating({ mutationKey: ['liquidation', scope.userId, scope.depotId] }) > 0;
  const now = new Date(lot?.serverTime).getTime();
  const mine = d?.participationDepotId === scope.depotId;

  const steps = [
    { key: 'register', label: 'Đăng ký khảo sát', icon: ClipboardCheck, state: d?.participationDepotId ? 'done' : 'current' },
    { key: 'survey', label: 'Xác nhận hiện trạng', icon: Search, state: d?.surveyDeclared ? 'done' : d?.participationDepotId ? 'current' : 'todo' },
    { key: 'bid', label: 'Trả giá', icon: Gavel, state: d?.leading ? 'done' : d?.surveyDeclared ? 'current' : 'todo' },
  ];

  return <>
    <Modal
      icon={Gavel} tone={PHASE_TONE[lot?.phase] ?? 'primary'} title={lot?.title ?? 'Chi tiết lô thanh lý'} onClose={onClose} busy={busy}
      badge={lot && <Badge tone={PHASE_TONE[lot.phase] ?? 'slate'}>{liquidationLabels[lot.phase]}</Badge>}
      subtitle={lot && <span className="inline-flex items-center gap-1"><MapPin size={13} aria-hidden="true" />{lot.publicArea}</span>}
      footer={d?.canBid ? <BidBar id={id} lot={lot} refreshing={query.isFetching || query.isError} /> : null}
    >
      <QueryState query={query}>{d && <>
        <PriceHero lot={lot} fetchedAt={query.dataUpdatedAt} leading={d.leading} perspective="depot" />
        {d.cancelReason && <Notice tone="red" icon={AlertTriangle}>Lý do hủy: {d.cancelReason}</Notice>}

        {lot.status === 'PUBLISHED' && <Section icon={UserCheck} title="Các bước tham gia">
          <Steps steps={steps} />
          <div className="mt-4 space-y-3 border-t border-d-border-subtle pt-4">
            {!d.participationDepotId ? <>
              <label className="flex cursor-pointer gap-3 rounded-xl bg-d-surface-container-low p-3 text-sm"><input type="checkbox" className="mt-0.5 size-4 accent-[#446900]" checked={consent} disabled={busy} onChange={e => setConsent(e.target.checked)} /><span>Tôi chấp nhận điều kiện đã công bố và đăng ký cho kho đang chọn.</span></label>
              <button type="button" className="inline-flex items-center gap-2 rounded-full bg-d-primary px-6 py-2.5 text-sm font-bold text-white shadow-sm transition active:scale-95 disabled:opacity-50" disabled={!consent || busy || now >= new Date(lot.surveyEnd).getTime()} onClick={() => register.mutate({ accepted: consent })}><ClipboardCheck size={16} />Đăng ký khảo sát</button>
            </> : !mine ? <Notice tone="amber" icon={Building2}>Bạn đã đăng ký bằng một kho khác. Chuyển về đúng kho đó để trả giá; nhiều kho cùng chủ không tham gia như các đối thủ riêng.</Notice>
              : !d.surveyDeclared ? <>
                <label className="flex cursor-pointer gap-3 rounded-xl bg-d-surface-container-low p-3 text-sm"><input type="checkbox" className="mt-0.5 size-4 accent-[#446900]" checked={consent} disabled={busy} onChange={e => setConsent(e.target.checked)} /><span>Tôi đã tự khảo sát và hiểu hiện trạng, phạm vi cùng điều kiện mua cả lô.</span></label>
                <button type="button" className="inline-flex items-center gap-2 rounded-full bg-d-primary px-6 py-2.5 text-sm font-bold text-white shadow-sm transition active:scale-95 disabled:opacity-50" disabled={!consent || busy || now < new Date(lot.surveyStart).getTime() || now >= new Date(lot.biddingEnd).getTime()} onClick={() => survey.mutate({ accepted: consent })}><Search size={16} />Xác nhận đã khảo sát / hiểu hiện trạng</button>
              </> : <Notice tone="primary" icon={ShieldCheck}>Kho đã đăng ký và tự xác nhận hiện trạng. {d.canBid ? 'Nhập giá ở thanh bên dưới để trả giá.' : 'Bạn sẽ trả giá được khi phiên mở.'}</Notice>}
            <MutationError mutation={register} /><MutationError mutation={survey} />
          </div>
        </Section>}

        {mine && <PriceLadder id={id} lot={lot} />}
        {d.participationDepotId && <MyBids id={id} />}

        {d.agreementId && <Section icon={Handshake} tone="teal" title="Bạn đã trúng lô này" className="border-teal-200 bg-teal-50/40">
          <p className="mb-3 text-sm text-d-on-surface-variant">Ký hợp đồng đặt cọc, chuyển tiền và nhận hàng theo từng bước trong thỏa thuận.</p>
          <button type="button" className="inline-flex items-center gap-2 rounded-full bg-teal-600 px-6 py-2.5 text-sm font-bold text-white shadow-sm transition active:scale-95" onClick={() => setAgreement(true)}><Handshake size={16} />Mở thỏa thuận & giao nhận</button>
        </Section>}

        <div className="grid gap-3 sm:grid-cols-2">
          <Tile icon={Search} tone="blue" label="Khảo sát" value={`${localTime(lot.surveyStart)} → ${localTime(lot.surveyEnd)}`} hint="Đăng ký trước giờ kết thúc khảo sát" />
          <Tile icon={Gavel} tone="primary" label="Mở trả giá" value={localTime(lot.biddingStart)} hint={`Bước tăng tối thiểu ${money(lot.bidIncrement)}`} />
          <Tile icon={Timer} tone="amber" label="Kết thúc hiện tại" value={localTime(lot.biddingEnd)} hint={`Gia hạn tối đa tới ${localTime(lot.hardEnd)}`} />
          <Tile icon={PackageCheck} tone="teal" label="Hạn lấy hết hàng" value={localTime(lot.collectionDeadline)} />
          <Tile icon={BadgePercent} tone="lime" label="Tiền đặt cọc khi trúng" value={lot.depositPercent > 0 ? `${lot.depositPercent}% giá trúng` : 'Không đặt cọc'} hint="Chuyển ngoài hệ thống" />
          <Tile icon={d.address ? MapPin : Lock} tone={d.address ? 'blue' : 'slate'} label="Địa chỉ khảo sát" value={d.address ?? 'Đăng ký khảo sát để xem'} hint={d.contactPhone ? undefined : undefined} />
          <Tile icon={d.contactPhone ? Phone : Lock} tone={d.contactPhone ? 'blue' : 'slate'} label="Liên hệ bên bán" value={d.contactPhone ?? 'Chỉ hiện cho kho đã đăng ký'} className="sm:col-span-2" />
        </div>

        <Section icon={FileText} title="Hiện trạng và phạm vi mua">
          <p className="whitespace-pre-wrap break-words text-sm leading-relaxed">{d.description}</p>
          <div className="mt-3 rounded-xl bg-d-surface-container-low p-3"><p className="mb-1 flex items-center gap-1.5 text-xs font-bold text-d-on-surface-variant"><Layers size={13} />Phạm vi</p><p className="whitespace-pre-wrap break-words text-sm">{d.scopeDetails}</p></div>
          <div className="mt-3 rounded-xl bg-d-surface-container-low p-3"><p className="mb-1 flex items-center gap-1.5 text-xs font-bold text-d-on-surface-variant"><Handshake size={13} />Điều kiện thực hiện</p><p className="whitespace-pre-wrap break-words text-sm">{d.collectionTerms}</p></div>
        </Section>

        {lot.imageUrls.length > 0 && <Section icon={Images} title={`Ảnh hiện trạng (${lot.imageUrls.length})`}>
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">{lot.imageUrls.map((url, i) => <a key={url} href={url} target="_blank" rel="noopener noreferrer"><img src={url} alt={`Hiện trạng lô ảnh ${i + 1}`} className="h-32 w-full rounded-xl object-cover transition hover:opacity-90" /></a>)}</div>
        </Section>}

        <Notice tone="blue" icon={Info}>Giờ Việt Nam theo dữ liệu server. Mỗi lượt hợp lệ trong 2 phút cuối có thể gia hạn thêm, tổng không quá 10 phút. Giá trọn lô không thay đổi theo cân sau mua. Khảo sát do chủ kho thực hiện và tự xác nhận, chưa phải kiểm định của RETRACK.</Notice>
      </>}</QueryState>
    </Modal>
    {agreement && <LiquidationAgreement id={d.agreementId} onClose={() => setAgreement(false)} />}
  </>;
}

// Thanh trả giá cố định ở đáy popup: luôn thấy, không phải cuộn.
function BidBar({ id, lot, refreshing }) {
  const [form, setForm] = useState({ operationId: crypto.randomUUID(), amount: '', consent: false });
  const save = useLiquidationMutation('post', `/${id}/bids`, () => setForm({ operationId: crypto.randomUUID(), amount: '', consent: false }));
  const minimum = lot.highestPrice == null ? lot.startingPrice : lot.highestPrice + lot.bidIncrement;
  return <form className="space-y-2.5" onSubmit={e => { e.preventDefault(); save.mutate({ operationId: form.operationId, amount: Number(form.amount) }); }}>
    <div className="flex flex-col gap-2.5 sm:flex-row sm:items-end">
      <label className="block flex-1 text-xs font-semibold text-d-on-surface-variant">Tổng tiền trả cho cả lô (đ) — tối thiểu {money(minimum)}
        <input required type="number" inputMode="numeric" min={minimum} max="1000000000000" step="1" disabled={save.isPending} className={`${inputClass} mt-1 text-base font-bold`} value={form.amount} onChange={e => setForm({ ...form, amount: e.target.value })} placeholder={String(minimum)} />
      </label>
      <button className="inline-flex shrink-0 items-center justify-center gap-2 rounded-full bg-d-primary px-7 py-3 text-sm font-bold text-white shadow-md transition active:scale-95 disabled:opacity-50" disabled={save.isPending || !form.consent || refreshing}><Gavel size={16} />{save.isPending ? 'Đang ghi nhận…' : 'Trả giá cả lô'}</button>
    </div>
    <label className="flex cursor-pointer gap-2.5 text-xs text-d-on-surface-variant"><input required type="checkbox" className="mt-0.5 size-4 accent-[#446900]" disabled={save.isPending} checked={form.consent} onChange={e => setForm({ ...form, consent: e.target.checked })} /><span>Tôi xác nhận mua cả lô với mức tiền này nếu trúng; không tự rút hoặc sửa lượt giá đã ghi.{lot.depositPercent > 0 && ` Nếu trúng: ký hợp đồng và cọc ${lot.depositPercent}% giá trúng (tối thiểu ≈ ${money(Math.round(minimum * lot.depositPercent / 100))}) ngoài hệ thống.`}</span></label>
    <MutationError mutation={save} />
  </form>;
}

// Thang giá ẩn danh: thấy mọi mức giá đang có để biết giá đang ở đâu, không thấy kho nào đặt.
function PriceLadder({ id, lot }) {
  const [page, setPage] = useState(1);
  const query = useLiquidationQuery(`/${id}/ladder`, { page }, true, true);
  const top = query.data?.items[0]?.amount ?? lot.highestPrice ?? lot.startingPrice;
  return <Section icon={Trophy} tone="amber" title={`Thang giá hiện tại (${query.data?.totalCount ?? 0} lượt)`}><QueryState query={query}>
    <div className="space-y-2">{query.data?.items.map(x => <div key={`${x.rank}-${x.createdAt}`} className={`rounded-xl border p-3 ${x.leading ? 'border-d-primary/40 bg-d-surface-accent' : 'border-d-border-subtle bg-white'}`}>
      <div className="flex items-center gap-3">
        <RankIcon rank={x.rank} />
        <div className="min-w-0 flex-1"><p className="text-xs text-d-on-surface-variant">{localTime(x.createdAt)}{x.mine && <strong className="ml-2 text-d-primary">Của bạn</strong>}{x.leading && <strong className="ml-2 text-amber-600">Dẫn đầu</strong>}</p></div>
        <strong className="text-base">{money(x.amount)}</strong>
      </div>
      <div className="mt-2"><PriceBar amount={x.amount} top={top} leading={x.leading} /></div>
    </div>)}</div>
    {!query.data?.items.length && <p className="text-sm text-d-on-surface-variant">Chưa có lượt trả giá. Giá đang ở mức khởi điểm {money(lot.startingPrice)}.</p>}<Pager page={page} setPage={setPage} total={query.data?.totalCount} />
  </QueryState></Section>;
}

function MyBids({ id }) {
  const [page, setPage] = useState(1);
  const query = useLiquidationQuery(`/${id}/bids`, { page });
  return <Section icon={History} tone="blue" title="Lịch sử trả giá của bạn"><QueryState query={query}>
    <div className="space-y-2">{query.data?.items.map(x => <p key={x.id} className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-d-border-subtle bg-white p-3 text-sm"><span className="text-d-on-surface-variant">{localTime(x.createdAt)}</span><strong>{money(x.amount)}</strong></p>)}</div>
    {!query.data?.items.length && <p className="text-sm text-d-on-surface-variant">Bạn chưa có lượt trả giá nào.</p>}<Pager page={page} setPage={setPage} total={query.data?.totalCount} />
  </QueryState></Section>;
}
