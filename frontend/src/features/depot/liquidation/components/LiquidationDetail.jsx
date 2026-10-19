import { useState } from 'react';
import { useIsMutating } from '@tanstack/react-query';
import { Dialog, QueryState, MutationError, Pager, inputClass, buttonClass } from '@/features/depot/components/DepotUI';
import { money } from '@/features/depot/depotApi';
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
  return <Dialog title={lot?.title ?? 'Chi tiết lô thanh lý'} onClose={onClose} busy={busy} wide>
    <QueryState query={query}>{d && <div className="space-y-5">
      <div className="rounded-xl bg-d-surface-accent p-4 space-y-2"><p className="font-semibold text-d-primary">{liquidationLabels[lot.phase]}</p><p>{lot.highestPrice == null ? 'Giá khởi điểm — chưa có lượt trả giá' : `Giá cao nhất · ${lot.bidCount} lượt trả giá`}</p><p className="text-2xl font-bold">{money(lot.highestPrice ?? lot.startingPrice)} / cả lô</p>{d.leading && <p className="font-semibold">Bạn đang giữ mức giá cao nhất.</p>}</div>
      <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">{lot.imageUrls.map((url, i) => <a key={url} href={url} target="_blank" rel="noopener noreferrer"><img src={url} alt={`Hiện trạng lô ảnh ${i + 1}`} className="rounded-xl h-32 w-full object-cover" /></a>)}</div>
      <section className="space-y-3"><h3 className="font-semibold">Hiện trạng và phạm vi mua</h3><p className="whitespace-pre-wrap break-words">{d.description}</p><p className="whitespace-pre-wrap break-words rounded-xl border border-d-border-subtle bg-white p-3">{d.scopeDetails}</p><h3 className="font-semibold">Điều kiện thực hiện</h3><p className="whitespace-pre-wrap break-words">{d.collectionTerms}</p></section>
      <dl className="grid sm:grid-cols-2 gap-4 rounded-xl bg-white border border-d-border-subtle p-4">{[
        ['Khu vực', lot.publicArea], ['Khảo sát từ', localTime(lot.surveyStart)], ['Khảo sát đến', localTime(lot.surveyEnd)],
        ['Mở trả giá', localTime(lot.biddingStart)], ['Kết thúc hiện tại', localTime(lot.biddingEnd)], ['Gia hạn tối đa tới', localTime(lot.hardEnd)],
        ['Hạn lấy hết hàng', localTime(lot.collectionDeadline)], ['Bước tăng tối thiểu', money(lot.bidIncrement)],
        ['Địa chỉ khảo sát', d.address ?? 'Đăng ký khảo sát để xem'], ['Liên hệ bên bán', d.contactPhone ?? 'Chỉ hiện cho kho đã đăng ký']
      ].map(([label, value]) => <div key={label}><dt className="text-sm text-d-on-surface-variant">{label}</dt><dd className="mt-1 font-medium break-words">{value}</dd></div>)}</dl>
      <p className="text-sm text-d-on-surface-variant">Giờ Việt Nam theo dữ liệu server. Mỗi lượt hợp lệ trong 2 phút cuối có thể gia hạn thêm, tổng không quá 10 phút. Giá trọn lô không thay đổi theo cân sau mua. Khảo sát do chủ kho thực hiện và tự xác nhận, chưa phải kiểm định của RETRACK.</p>
      {d.cancelReason && <p className="text-d-error">Lý do hủy: {d.cancelReason}</p>}
      {lot.status === 'PUBLISHED' && <section className="rounded-xl border border-d-border-subtle p-4 space-y-3">
        {!d.participationDepotId ? <><h3 className="font-semibold">Đăng ký khảo sát</h3><label className="flex gap-3"><input type="checkbox" checked={consent} disabled={busy} onChange={e => setConsent(e.target.checked)} /><span>Tôi chấp nhận điều kiện đã công bố và đăng ký cho kho đang chọn.</span></label><button type="button" className={buttonClass} disabled={!consent || busy || now >= new Date(lot.surveyEnd).getTime()} onClick={() => register.mutate({ accepted: consent })}>Đăng ký khảo sát</button></>
          : d.participationDepotId !== scope.depotId ? <p>Bạn đã đăng ký bằng một kho khác. Chuyển về đúng kho đó để trả giá; nhiều kho cùng chủ không tham gia như các đối thủ riêng.</p>
          : !d.surveyDeclared ? <><h3 className="font-semibold">Xác nhận hiện trạng trước khi trả giá</h3><label className="flex gap-3"><input type="checkbox" checked={consent} disabled={busy} onChange={e => setConsent(e.target.checked)} /><span>Tôi đã tự khảo sát và hiểu hiện trạng, phạm vi cùng điều kiện mua cả lô.</span></label><button type="button" className={buttonClass} disabled={!consent || busy || now < new Date(lot.surveyStart).getTime() || now >= new Date(lot.biddingEnd).getTime()} onClick={() => survey.mutate({ accepted: consent })}>Xác nhận đã khảo sát / hiểu hiện trạng</button></>
          : <p className="text-d-primary font-medium">Kho đã đăng ký và tự xác nhận hiện trạng.</p>}
        <MutationError mutation={register} /><MutationError mutation={survey} />
      </section>}
      {d.canBid && <BidForm id={id} lot={lot} refreshing={query.isFetching || query.isError} />}
      {d.participationDepotId && <MyBids id={id} />}
      {d.agreementId && <button type="button" className={buttonClass} onClick={() => setAgreement(true)}>Xem thỏa thuận lô trúng và giao nhận</button>}
      {agreement && <LiquidationAgreement id={d.agreementId} onClose={() => setAgreement(false)} />}
    </div>}</QueryState>
  </Dialog>;
}
function BidForm({ id, lot, refreshing }) {
  const [form, setForm] = useState({ operationId: crypto.randomUUID(), amount: '', consent: false });
  const save = useLiquidationMutation('post', `/${id}/bids`, () => setForm({ operationId: crypto.randomUUID(), amount: '', consent: false }));
  const minimum = lot.highestPrice == null ? lot.startingPrice : lot.highestPrice + lot.bidIncrement;
  return <form className="space-y-3 rounded-2xl bg-d-surface-accent p-4" onSubmit={e => { e.preventDefault(); save.mutate({ operationId: form.operationId, amount: Number(form.amount) }); }}>
    <h3 className="font-semibold">Trả giá mua cả lô</h3><p className="text-sm">Mức tối thiểu hiện tại: {money(minimum)}. Giá server có thể thay đổi khi kho khác trả giá.</p>
    <label className="block">Tổng tiền bạn muốn trả (đ)<input required type="number" min={minimum} max="1000000000000" step="1" disabled={save.isPending} className={inputClass} value={form.amount} onChange={e => setForm({ ...form, amount: e.target.value })} /></label>
    <label className="flex gap-3"><input required type="checkbox" disabled={save.isPending} checked={form.consent} onChange={e => setForm({ ...form, consent: e.target.checked })} /><span>Tôi xác nhận mua cả lô với mức tiền này nếu trúng; không tự rút hoặc sửa lượt giá đã ghi.</span></label>
    <button className={buttonClass} disabled={save.isPending || !form.consent || refreshing}>{save.isPending ? 'Đang ghi nhận giá…' : 'Xác nhận trả giá cả lô'}</button><MutationError mutation={save} />
  </form>;
}
function MyBids({ id }) {
  const [page, setPage] = useState(1);
  const query = useLiquidationQuery(`/${id}/bids`, { page });
  return <section className="space-y-3"><h3 className="font-semibold">Lịch sử trả giá của bạn</h3><QueryState query={query}>
    {query.data?.items.map(x => <p key={x.id} className="flex flex-wrap justify-between gap-2 rounded-xl border border-d-border-subtle bg-white p-3"><span>{localTime(x.createdAt)}</span><strong>{money(x.amount)}</strong></p>)}
    {!query.data?.items.length && <p className="text-sm">Bạn chưa có lượt trả giá nào.</p>}<Pager page={page} setPage={setPage} total={query.data?.totalCount} />
  </QueryState></section>;
}
