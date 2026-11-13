import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Page, QueryState, Pager, inputClass, buttonClass } from '@/features/depot/components/DepotUI';
import { money } from '@/features/depot/depotApi';
import { useLiquidationQuery, useLiquidationScope, liquidationLabels, localTime } from './liquidationApi';
import LiquidationDetail from './components/LiquidationDetail';

export default function LiquidationMarket() {
  const { depotId } = useLiquidationScope();
  const [params, setParams] = useSearchParams();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('');
  const [participatingOnly, setParticipatingOnly] = useState(false);
  const [manualSelected, setSelected] = useState(null);
  const selected = params.get('lotId') || manualSelected;
  const clearLink = () => setParams(previous => { previous.delete('lotId'); return previous; }, { replace: true });
  const query = useLiquidationQuery('', { page, search, status, participatingOnly }, true, true);
  return <Page title="Lô lớn mua vào" description="Khảo sát ngoài thực tế và trả giá một tổng tiền cho cả lô.">
    <div className="grid sm:grid-cols-2 gap-4 rounded-2xl bg-white border border-d-border-subtle p-4"><label>Tìm theo tên lô / khu vực<input type="search" maxLength={255} className={inputClass} value={search} onChange={e => { setSearch(e.target.value); setPage(1); }} /></label><label>Giai đoạn<select className={inputClass} value={status} onChange={e => { setStatus(e.target.value); setPage(1); }}><option value="">Tất cả giai đoạn</option>{Object.entries(liquidationLabels).map(([key, label]) => <option key={key} value={key}>{label}</option>)}</select></label>
      {<label className="flex items-center gap-3"><input type="checkbox" checked={participatingOnly} onChange={e => { setParticipatingOnly(e.target.checked); setPage(1); }} />Chỉ lô tôi đã đăng ký</label>}</div>
    <QueryState query={query}>
      {!query.data?.items.length && <p role="status" className="text-center p-8 rounded-2xl border border-d-border-subtle bg-white">Chưa có lô phù hợp. Hãy thử bộ lọc khác hoặc quay lại khi có Seller đăng lô.</p>}
      <div className="grid lg:grid-cols-2 gap-4">{query.data?.items.map(lot => <article key={lot.id} className="overflow-hidden rounded-[20px] bg-white border border-d-border-subtle shadow-sm flex flex-col">
        {lot.imageUrls[0] && <img src={lot.imageUrls[0]} alt={`Hiện trạng ${lot.title}`} className="h-44 w-full object-cover" loading="lazy" />}
        <div className="p-5 space-y-3 flex flex-col flex-1"><div className="flex flex-wrap items-start justify-between gap-2"><h2 className="text-lg font-semibold break-words">{lot.title}</h2><span className="rounded-full px-3 py-1 bg-d-surface-accent text-d-primary text-sm">{liquidationLabels[lot.phase] ?? lot.phase}</span></div><p className="text-sm text-d-on-surface-variant">{lot.publicArea}</p>
          <div className="rounded-xl bg-d-surface p-3"><p className="text-sm">{lot.highestPrice == null ? 'Giá khởi điểm — chưa có lượt trả giá' : `Giá cao nhất · ${lot.bidCount} lượt`}</p><p className="text-2xl font-semibold text-d-primary mt-1">{money(lot.highestPrice ?? lot.startingPrice)}<span className="text-sm font-normal"> / cả lô</span></p></div>
          <dl className="text-sm space-y-2"><div><dt className="text-d-on-surface-variant">Khảo sát</dt><dd>{localTime(lot.surveyStart)} → {localTime(lot.surveyEnd)}</dd></div><div><dt className="text-d-on-surface-variant">Trả giá</dt><dd>{localTime(lot.biddingStart)} → {localTime(lot.biddingEnd)}</dd></div></dl>
          <button type="button" className={buttonClass + ' mt-auto self-start'} onClick={() => { clearLink(); setSelected(lot.id); }}>Xem lô và điều kiện</button>
        </div>
      </article>)}</div><Pager page={page} setPage={setPage} total={query.data?.totalCount} />
    </QueryState>
    <p className="text-sm text-d-on-surface-variant">Giờ Việt Nam. Khối lượng và hiện trạng do bên bán cung cấp; chủ kho tự khảo sát. Giá trúng là giá cả lô, không tính lại theo kg cân sau mua.</p>
    {selected && <LiquidationDetail key={`${depotId}-${selected}`} id={selected} onClose={() => { setSelected(null); clearLink(); }} />}
  </Page>;
}
