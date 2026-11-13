import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Page, QueryState, Pager, inputClass, buttonClass } from '@/features/depot/components/DepotUI';
import { money } from '@/features/depot/depotApi';
import { useLiquidationQuery, useLiquidationScope, liquidationLabels, localTime } from './liquidationApi';
import LiquidationDetail from './components/LiquidationDetail';

const pageSize = 12;
const views = {
  OPEN: { label: 'Tìm lô', empty: 'Chưa có phiên phù hợp. Hãy thử tìm theo tên hoặc khu vực.',
    phases: ['UPCOMING', 'SURVEY', 'BIDDING', 'CLOSING'] },
  PURCHASES: { label: 'Đang mua', empty: 'Kho đang chọn chưa có lô trúng chờ thanh toán, nhận hàng hoặc ghi sổ nhập kho.',
    phases: [] },
  HISTORY: { label: 'Lịch sử', empty: 'Kho đang chọn chưa có lô đã nhập kho hoặc phiên đã kết thúc từng tham gia.',
    phases: ['AWARDED', 'NO_SALE', 'CANCELLED'] },
};

export default function LiquidationMarket() {
  const { depotId } = useLiquidationScope();
  const [params, setParams] = useSearchParams();
  const requestedView = params.get('view');
  const view = Object.hasOwn(views, requestedView) ? requestedView : 'OPEN';

  function changeView(nextView) {
    setParams(previous => {
      const next = new URLSearchParams(previous);
      next.set('view', nextView);
      next.delete('lotId');
      return next;
    });
  }

  return <Page title="Lô lớn mua vào" description="Tìm phiên trả giá, theo dõi lô trúng và tra lại giao dịch đã kết thúc của kho.">
    <div role="group" aria-label="Danh sách lô lớn" className="flex flex-wrap gap-2 border-b border-d-border-subtle">
      {Object.entries(views).map(([key, item]) =>
        <button key={key} type="button" aria-pressed={view === key} onClick={() => changeView(key)}
          className={`min-h-11 rounded-t-xl px-4 py-3 font-semibold ${view === key ? 'bg-d-surface-accent text-d-primary' : 'text-d-on-surface-variant'}`}>
          {item.label}
        </button>)}
    </div>
    <LiquidationList key={`${depotId}-${view}`} view={view} depotId={depotId} />
  </Page>;
}

function LiquidationList({ view, depotId }) {
  const [params, setParams] = useSearchParams();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('');
  const [participatingOnly, setParticipatingOnly] = useState(false);
  const [manualSelected, setSelected] = useState(null);
  const selected = params.get('lotId') || manualSelected;
  const clearLink = () => setParams(previous => {
    const next = new URLSearchParams(previous);
    next.delete('lotId');
    return next;
  }, { replace: true });
  const query = useLiquidationQuery('', { view, page, pageSize, search, status, participatingOnly }, true, view !== 'HISTORY');
  const { phases, empty } = views[view];

  return <>
    <div className="grid gap-4 rounded-2xl border border-d-border-subtle bg-white p-4 sm:grid-cols-2">
      <label>Tìm theo tên lô / khu vực
        <input type="search" maxLength={255} className={inputClass} value={search}
          onChange={e => { setSearch(e.target.value); setPage(1); }} />
      </label>
      {phases.length > 0 && <label>Giai đoạn
        <select className={inputClass} value={status} onChange={e => { setStatus(e.target.value); setPage(1); }}>
          <option value="">Tất cả giai đoạn</option>
          {phases.map(key => <option key={key} value={key}>{key === 'AWARDED' ? 'Phiên đã có người trúng' : liquidationLabels[key]}</option>)}
        </select>
      </label>}
      {view === 'OPEN' && <label className="flex items-center gap-3">
        <input type="checkbox" checked={participatingOnly} onChange={e => { setParticipatingOnly(e.target.checked); setPage(1); }} />
        Chỉ lô kho này đã đăng ký
      </label>}
    </div>
    <QueryState query={query}>
      {!query.data?.items.length && <p role="status" className="rounded-2xl border border-d-border-subtle bg-white p-8 text-center">{empty}</p>}
      <div className="grid gap-4 lg:grid-cols-2">
        {query.data?.items.map(lot => {
          const phaseLabel = lot.purchaseStatus === 'COMPLETED' ? 'Hoàn thành · đã nhập kho'
            : lot.purchaseStatus === 'IN_PROGRESS' ? 'Đang thực hiện mua'
            : view === 'HISTORY' && lot.phase === 'AWARDED' ? 'Không trúng'
            : liquidationLabels[lot.phase] ?? lot.phase;
          return <article key={lot.id} className="flex flex-col overflow-hidden rounded-[20px] border border-d-border-subtle bg-white shadow-sm">
            {lot.imageUrls[0] && <img src={lot.imageUrls[0]} alt={`Hiện trạng ${lot.title}`} className="h-44 w-full object-cover" loading="lazy" />}
            <div className="flex flex-1 flex-col space-y-3 p-5">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <h2 className="break-words text-lg font-semibold">{lot.title}</h2>
                <span className="rounded-full bg-d-surface-accent px-3 py-1 text-sm text-d-primary">{phaseLabel}</span>
              </div>
              <p className="text-sm text-d-on-surface-variant">{lot.publicArea}</p>
              <div className="rounded-xl bg-d-surface p-3">
                <p className="text-sm">{lot.phase === 'AWARDED' ? 'Giá trúng'
                  : lot.highestPrice == null ? 'Giá khởi điểm' : `Giá cao nhất · ${lot.bidCount} lượt`}</p>
                <p className="text-2xl font-semibold text-d-primary">{money(lot.highestPrice ?? lot.startingPrice)}
                  <span className="text-sm font-normal"> / cả lô</span></p>
              </div>
              <dl className="space-y-2 text-sm">
                {view === 'OPEN' && <div><dt className="text-d-on-surface-variant">Khảo sát</dt><dd>{localTime(lot.surveyStart)} → {localTime(lot.surveyEnd)}</dd></div>}
                <div><dt className="text-d-on-surface-variant">{view === 'OPEN' ? 'Hạn trả giá' : 'Kết thúc trả giá'}</dt><dd>{localTime(lot.biddingEnd)}</dd></div>
              </dl>
              <button type="button" className={buttonClass + ' mt-auto self-start'} onClick={() => { clearLink(); setSelected(lot.id); }}>
                Xem lô và điều kiện
              </button>
            </div>
          </article>;
        })}
      </div>
      <Pager page={page} setPage={setPage} size={query.data?.pageSize ?? pageSize} total={query.data?.totalCount} />
    </QueryState>
    {selected && <LiquidationDetail key={`${depotId}-${selected}`} id={selected} onClose={() => { setSelected(null); clearLink(); }} />}
  </>;
}
