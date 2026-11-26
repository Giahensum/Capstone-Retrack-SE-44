import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Layers } from 'lucide-react';
import { QueryState, Pager, inputClass, buttonClass } from '@/features/depot/components/DepotUI';
import { Badge, PHASE_TONE } from '@/components/liquidation/LiquidationUI';
import { liquidationLabels, localTime } from '../liquidationApi';
import { useV2Query } from './liquidationV2Api';
import LiquidationV2Detail from './LiquidationV2Detail';

const pageSize = 12;
const empty = {
  OPEN: 'Chưa có phiên nhiều hạng mục phù hợp. Thử tìm theo tên hoặc khu vực.',
  PURCHASES: 'Kho đang chọn chưa có hạng mục thắng chờ thanh toán, nhận hàng hoặc nhập kho.',
  HISTORY: 'Kho đang chọn chưa có phiên nhiều hạng mục đã kết thúc từng tham gia.',
};

/** Danh sách phiên nhiều hạng mục theo góc nhìn của kho đang chọn. */
export default function LiquidationV2List({ view }) {
  const [params, setParams] = useSearchParams();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [manual, setManual] = useState(null);
  const selected = params.get('campaignId') || manual;
  const clearLink = () => setParams((prev) => { const next = new URLSearchParams(prev); next.delete('campaignId'); return next; }, { replace: true });
  const query = useV2Query('', { view, page, pageSize, search }, { poll: view === 'OPEN' });

  return <>
    <div className="rounded-2xl border border-d-border-subtle bg-white p-4">
      <label>Tìm theo tên phiên / khu vực
        <input type="search" maxLength={255} className={inputClass} value={search} onChange={(e) => { setSearch(e.target.value); setPage(1); }} />
      </label>
    </div>
    <QueryState query={query}>
      {!query.data?.items.length && <p role="status" className="rounded-2xl border border-d-border-subtle bg-white p-8 text-center">{empty[view]}</p>}
      <div className="grid gap-4 lg:grid-cols-2">
        {query.data?.items.map((c) => <article key={c.id} className="flex flex-col overflow-hidden rounded-2xl border border-d-border-subtle bg-white shadow-sm">
          {c.imageUrls[0] && <img src={c.imageUrls[0]} alt={`Hiện trạng ${c.title}`} className="h-40 w-full object-cover" loading="lazy" />}
          <div className="flex flex-1 flex-col gap-3 p-5">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <h2 className="break-words text-lg font-semibold">{c.title}</h2>
              <Badge tone={PHASE_TONE[c.phase] ?? 'slate'}>{view === 'PURCHASES' ? `Thắng ${c.wonItemCount} hạng mục` : liquidationLabels[c.phase] ?? c.phase}</Badge>
            </div>
            <p className="text-sm text-d-on-surface-variant">{c.publicArea}</p>
            <div className="rounded-xl bg-d-surface p-3 text-sm">
              <p className="flex items-center gap-1.5 font-semibold"><Layers size={15} className="text-d-primary" />{c.itemCount} hạng mục · {c.bidCount} lượt giá</p>
              <p className="mt-1 text-d-on-surface-variant">{c.itemNames.join(' · ')}{c.itemCount > c.itemNames.length ? ' …' : ''}</p>
              {c.registered && <p className="mt-1 text-xs font-semibold text-d-primary">Kho đã đăng ký {c.registeredItemCount} hạng mục</p>}
            </div>
            <dl className="space-y-1 text-sm">
              {view === 'OPEN' && <div><dt className="inline text-d-on-surface-variant">Khảo sát: </dt><dd className="inline">{localTime(c.surveyStart)} → {localTime(c.surveyEnd)}</dd></div>}
              <div><dt className="inline text-d-on-surface-variant">{view === 'OPEN' ? 'Đóng chung: ' : 'Đã đóng: '}</dt><dd className="inline">{localTime(c.biddingEnd)}</dd></div>
            </dl>
            <button type="button" className={buttonClass + ' mt-auto self-start'} onClick={() => { clearLink(); setManual(c.id); }}>
              {view === 'PURCHASES' ? 'Tiếp tục mua và nhận hàng' : 'Xem hạng mục và trả giá'}
            </button>
          </div>
        </article>)}
      </div>
      <Pager page={page} setPage={setPage} size={query.data?.pageSize ?? pageSize} total={query.data?.totalCount} />
    </QueryState>
    {selected && <LiquidationV2Detail key={selected} id={selected} startWithAgreement={view === 'PURCHASES'} onClose={() => { setManual(null); clearLink(); }} />}
  </>;
}
