import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { AlertTriangle, Eye, Play, Users, CalendarRange, ExternalLink } from 'lucide-react';
import { ConfirmModal, Notice } from '@/components/liquidation/LiquidationUI';
import { Panel, Field, inputClass, PrimaryButton, GhostButton, SearchBox, DataTable, Th, Td } from '@/features/admin/components/AdminUI';
import { moneyAdminApi } from '../moneyAdminApi';
import {
  vnd, pct, fmtDateTime, flowLabel, kindLabel, signedForKind, deduct, roleLabel, apiError, vnToday, addDays, vnStartOfDayIso, validatePeriod, summarizePlan, payoutStatusMeta,
} from '../moneyLabels';
import { PayoutStatusChip, BankStatusChip, OutcomeChip, FieldError, useDebounced } from '../moneyUi';

const PAGE = 100;

function PlanCard({ plan, onOpenRun }) {
  return (
    <section className="overflow-hidden rounded-2xl border border-d-border-subtle bg-white shadow-sm">
      <header className="flex flex-wrap items-center justify-between gap-2 border-b border-d-border-subtle bg-d-surface-accent/40 px-5 py-3">
        <div className="min-w-0">
          <h3 className="truncate text-sm font-black text-d-on-surface">{plan.payeeName}</h3>
          {plan.runCode && <p className="text-xs text-d-on-surface-variant">Mã kỳ: {plan.runCode}</p>}
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <OutcomeChip outcome={plan.outcome} />
          {plan.status && <PayoutStatusChip status={plan.status} />}
          {plan.bankStatus && <BankStatusChip status={plan.bankStatus} />}
          {plan.runId && onOpenRun && (
            <button type="button" onClick={() => onOpenRun(plan.runId)} className="inline-flex items-center gap-1 text-xs font-bold text-d-primary hover:underline">
              <ExternalLink size={13} aria-hidden="true" />Mở kỳ chi
            </button>
          )}
        </div>
      </header>
      {(plan.message || plan.reason) && (
        <p className="px-5 pt-3 text-sm text-d-on-surface-variant">
          {plan.message}{plan.message && plan.reason ? ' ' : ''}
          {plan.reason && <span className="font-semibold">Lý do trạng thái: {plan.reason}</span>}
        </p>
      )}
      {plan.lines?.length > 0 && (
        <DataTable minWidth={760} head={<><Th>Dòng tiền</Th><Th>Loại</Th><Th className="text-right">Gộp</Th><Th className="text-right">Tỷ lệ</Th><Th className="text-right">Hoa hồng</Th><Th className="text-right">Thực nhận</Th><Th>Sẵn sàng từ</Th></>}>
          {plan.lines.map((l) => (
            <tr key={l.lineId}>
              <Td>{flowLabel(l.flow)}</Td>
              <Td>{kindLabel(l.kind)}</Td>
              <Td className="text-right">{vnd(signedForKind(l.kind, l.gross))}</Td>
              <Td className="text-right">{pct(l.rate)}</Td>
              <Td className="text-right">{vnd(signedForKind(l.kind, l.commission))}</Td>
              <Td className="text-right font-semibold">{vnd(l.net)}</Td>
              <Td>{fmtDateTime(l.availableAt)}</Td>
            </tr>
          ))}
        </DataTable>
      )}
      <dl className="grid gap-3 border-t border-d-border-subtle px-5 py-4 text-sm sm:grid-cols-5">
        {[['Tổng gộp', plan.gross], ['Hoa hồng', plan.commission], ['Hoàn tiền', deduct(plan.refundAmount)], ['Nợ mang sang', deduct(plan.carriedDebt)]].map(([k, v]) => (
          <div key={k}><dt className="text-xs font-semibold text-d-on-surface-variant">{k}</dt><dd className="font-bold">{vnd(v)}</dd></div>
        ))}
        <div className="rounded-xl bg-d-primary/10 px-3 py-2">
          <dt className="text-xs font-bold text-d-primary">THỰC CHI</dt><dd className="text-lg font-black text-d-primary">{vnd(plan.net)}</dd>
        </div>
      </dl>
    </section>
  );
}

function PayeePicker({ selected, setSelected }) {
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const debounced = useDebounced(search);
  const [loadingAll, setLoadingAll] = useState(false);
  const q = useQuery({
    queryKey: ['money-admin', 'payees', 'run-picker', debounced, page],
    queryFn: () => moneyAdminApi.listPayees({ search: debounced || undefined, page, pageSize: PAGE }),
    placeholderData: (p) => p,
  });
  const items = q.data?.items ?? [];
  const total = q.data?.totalCount ?? 0;
  const toggle = (p) => setSelected((m) => { const n = new Map(m); if (n.has(p.userId)) n.delete(p.userId); else n.set(p.userId, p.fullName); return n; });
  const pageAllSelected = items.length > 0 && items.every((p) => selected.has(p.userId));

  const selectAllMatching = async () => {
    setLoadingAll(true);
    try {
      const next = new Map(selected);
      let pg = 1; let seen = 0; let count = 0;
      do {
        const r = await moneyAdminApi.listPayees({ search: debounced || undefined, page: pg, pageSize: PAGE });
        r.items.forEach((p) => next.set(p.userId, p.fullName));
        seen += r.items.length; count = r.totalCount; pg += 1;
        if (r.items.length === 0) break;
      } while (seen < count && pg <= 50);
      setSelected(next);
      toast.success(`Đã chọn ${next.size} người nhận`);
    } catch (e) {
      toast.error(apiError(e, 'Không tải được toàn bộ danh sách người nhận.'));
    } finally { setLoadingAll(false); }
  };

  return (
    <Panel icon={Users} title={`Người nhận (đã chọn ${selected.size})`}
      toolbar={(
        <>
          <GhostButton onClick={() => setSelected((m) => { const n = new Map(m); items.forEach((p) => (pageAllSelected ? n.delete(p.userId) : n.set(p.userId, p.fullName))); return n; })} disabled={items.length === 0}>
            {pageAllSelected ? 'Bỏ chọn trang này' : `Chọn cả trang này (${items.length})`}
          </GhostButton>
          <GhostButton onClick={selectAllMatching} disabled={loadingAll || total === 0}>{loadingAll ? 'Đang chọn…' : `Chọn tất cả ${total} kết quả`}</GhostButton>
          <GhostButton onClick={() => setSelected(new Map())} disabled={selected.size === 0}>Bỏ chọn hết</GhostButton>
        </>
      )}>
      <div className="p-5 pb-3"><SearchBox value={search} onChange={(e) => { setSearch(e.target.value); setPage(1); }} placeholder="Tìm người nhận theo tên" /></div>
      <ul className="max-h-72 divide-y divide-d-border-subtle overflow-y-auto border-t border-d-border-subtle" aria-label="Danh sách người nhận">
        {q.isLoading && <li className="px-5 py-6 text-center text-sm text-d-on-surface-variant" role="status">Đang tải…</li>}
        {q.isError && <li role="alert" className="px-5 py-6 text-center text-sm font-semibold text-d-error">{apiError(q.error, 'Không tải được danh sách người nhận.')}</li>}
        {!q.isLoading && !q.isError && items.length === 0 && <li className="px-5 py-6 text-center text-sm text-d-on-surface-variant">Không có người nhận phù hợp.</li>}
        {items.map((p) => (
          <li key={p.userId}>
            <label className="flex cursor-pointer items-center gap-3 px-5 py-2.5 hover:bg-d-surface-accent/40">
              <input type="checkbox" checked={selected.has(p.userId)} onChange={() => toggle(p)} className="size-4 accent-[#446900]" />
              <span className="min-w-0 flex-1"><span className="block truncate text-sm font-semibold">{p.fullName}</span><span className="text-xs text-d-on-surface-variant">{roleLabel(p.role)} · sẵn sàng chi {vnd(p.availableAmount)}</span></span>
              <BankStatusChip status={p.bank?.status} />
            </label>
          </li>
        ))}
      </ul>
      {total > PAGE && (
        <div className="flex items-center justify-between border-t border-d-border-subtle px-5 py-3 text-sm">
          <GhostButton onClick={() => setPage((p) => Math.max(1, p - 1))} disabled={page <= 1}>Trang trước</GhostButton>
          <span>Trang {page}/{Math.ceil(total / PAGE)}</span>
          <GhostButton onClick={() => setPage((p) => p + 1)} disabled={page >= Math.ceil(total / PAGE)}>Trang sau</GhostButton>
        </div>
      )}
    </Panel>
  );
}

export default function ReconcileTab({ settings, onOpenRun }) {
  const qc = useQueryClient();
  const today = vnToday();
  const [selected, setSelected] = useState(() => new Map());
  const [start, setStart] = useState(addDays(today, -7));
  const [end, setEnd] = useState(today);
  // Chế độ thử: chốt kỳ tại thời điểm hiện tại (lùi 1 phút để không vượt giờ máy chủ), gồm cả tiền phát sinh trong ngày hôm nay.
  const [untilNow, setUntilNow] = useState(false);
  const [nowIso, setNowIso] = useState('');
  const stampNow = () => { const d = new Date(Date.now() - 60_000); d.setUTCSeconds(0, 0); const iso = d.toISOString(); setNowIso(iso); return iso; };
  const [preview, setPreview] = useState(null); // { key, items }
  const [result, setResult] = useState(null);
  const [confirming, setConfirming] = useState(false);
  const [error, setError] = useState('');

  const ids = useMemo(() => [...selected.keys()].sort(), [selected]);
  const paramKey = `${ids.join(',')}|${start}|${untilNow ? `now:${nowIso}` : end}`;
  const periodError = untilNow
    ? (!start ? 'Chọn ngày bắt đầu.' : start > today ? 'Ngày bắt đầu không được ở tương lai.' : null)
    : validatePeriod(start, end, today);
  const body = (endIso) => ({ payeeIds: ids, periodStart: vnStartOfDayIso(start), periodEnd: untilNow ? (endIso ?? nowIso) : vnStartOfDayIso(end) });
  const fresh = preview && preview.key === paramKey;
  const real = settings?.payoutProvider === 'PAYOS';

  const previewMut = useMutation({
    mutationFn: (endIso) => moneyAdminApi.previewPayouts(body(endIso)),
    onSuccess: (data, endIso) => { setPreview({ key: `${ids.join(',')}|${start}|${untilNow ? `now:${endIso}` : end}`, items: data.items }); setResult(null); setError(''); },
    onError: (e) => setError(apiError(e, 'Không xem trước được.')),
  });
  const runMut = useMutation({
    mutationFn: () => moneyAdminApi.runPayouts(body()),
    onSuccess: (data) => {
      setResult(data.items); setPreview(null); setConfirming(false); setError('');
      toast.success('Đã chạy đối soát');
      qc.invalidateQueries({ queryKey: ['money-admin', 'payouts'] });
      qc.invalidateQueries({ queryKey: ['money-admin', 'payees'] });
      qc.invalidateQueries({ queryKey: ['money-admin', 'ledger'] });
      qc.invalidateQueries({ queryKey: ['money-admin', 'balance'] });
    },
    onError: (e) => { setConfirming(false); setError(apiError(e, 'Chạy đối soát thất bại.')); },
  });

  const summary = fresh ? summarizePlan(preview.items) : null;
  const canPreview = ids.length > 0 && !periodError && !previewMut.isPending && !runMut.isPending;

  return (
    <div className="flex flex-col gap-6">
      <PayeePicker selected={selected} setSelected={setSelected} />

      <Panel icon={CalendarRange} title="Kỳ đối soát (giờ Việt Nam)">
        <div className="flex flex-wrap items-end gap-4 p-5">
          <Field label="Từ ngày (00:00)"><input type="date" value={start} max={today} onChange={(e) => setStart(e.target.value)} className={`${inputClass} w-48`} /></Field>
          <Field label="Đến ngày (00:00, không gồm ngày này)">
            <input type="date" value={end} max={today} disabled={untilNow} onChange={(e) => setEnd(e.target.value)} className={`${inputClass} w-48`} />
          </Field>
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" checked={untilNow} onChange={(e) => { setUntilNow(e.target.checked); setPreview(null); if (e.target.checked) stampNow(); }} className="size-4 accent-d-primary" />
            <span><strong>Gồm cả hôm nay</strong> (chốt kỳ tại thời điểm hiện tại, dùng khi test)</span>
          </label>
          <PrimaryButton icon={Eye} onClick={() => previewMut.mutate(untilNow ? stampNow() : undefined)} disabled={!canPreview}>{previewMut.isPending ? 'Đang xem trước…' : 'Xem trước'}</PrimaryButton>
        </div>
        <p className="px-5 pb-3 text-xs text-d-on-surface-variant">Tối đa là hôm nay. Muốn gồm cả hôm qua, chọn hôm nay.</p>
        <div className="px-5 pb-4"><FieldError>{periodError && (start || end) ? periodError : ''}</FieldError></div>
      </Panel>

      {error && <Notice tone="red" icon={AlertTriangle}>{error}</Notice>}

      {preview && !fresh && <Notice tone="amber" icon={AlertTriangle}>Bạn đã đổi người nhận hoặc ngày sau lần xem trước. Bấm Xem trước lại trước khi chạy.</Notice>}

      {fresh && (
        <section className="flex flex-col gap-4" aria-label="Kết quả xem trước">
          <div className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-d-border-subtle bg-white px-5 py-4 shadow-sm">
            <div className="text-sm">
              <p className="font-black">Xem trước: {preview.items.length} người nhận, {summary.runnable.length} sẽ tạo kỳ mới</p>
              <p className="text-d-on-surface-variant">Tổng thực chi các kỳ mới: <strong>{vnd(summary.totalNet)}</strong>{summary.skipped > 0 && ` · ${summary.skipped} người không tạo kỳ`}</p>
            </div>
            <PrimaryButton icon={Play} onClick={() => setConfirming(true)} disabled={summary.runnable.length === 0 || runMut.isPending}>Chạy đối soát</PrimaryButton>
          </div>
          {preview.items.map((p) => <PlanCard key={p.payeeId} plan={p} />)}
        </section>
      )}

      {result && (
        <section className="flex flex-col gap-4" aria-label="Kết quả chạy">
          <Notice tone="blue">Kết quả chạy đối soát cho {result.length} người nhận.</Notice>
          {result.map((p) => <PlanCard key={p.payeeId} plan={p} onOpenRun={onOpenRun} />)}
        </section>
      )}

      {confirming && summary && (
        <ConfirmModal tone={real && summary.sendNow.length > 0 ? 'red' : 'primary'} icon={Play} title="Chạy đối soát?"
          confirmLabel="Chạy đối soát" cancelLabel="Quay lại" busy={runMut.isPending} onConfirm={() => runMut.mutate()} onCancel={() => setConfirming(false)}>
          <div className="flex flex-col gap-3 text-sm">
            {real && summary.sendNow.length > 0 && (
              <Notice tone="red" icon={AlertTriangle}><strong>Cảnh báo: sẽ gửi tiền thật qua payOS</strong> cho {summary.sendNow.length} người nhận, tổng {vnd(summary.sendNowNet)}. Không thể thu hồi từ giao diện này.</Notice>
            )}
            <p>Kỳ: {start} đến {end} (giờ Việt Nam). Tổng thực chi các kỳ sẽ tạo: <strong>{vnd(summary.totalNet)}</strong>.</p>
            <ul className="max-h-56 divide-y divide-d-border-subtle overflow-y-auto rounded-xl border border-d-border-subtle">
              {summary.runnable.map((p) => (
                <li key={p.payeeId} className="flex items-center justify-between gap-3 px-3 py-2">
                  <span className="min-w-0"><span className="block truncate font-semibold">{p.payeeName}</span><span className="text-xs text-d-on-surface-variant">{payoutStatusMeta(p.status).label}</span></span>
                  <span className="font-bold">{vnd(p.net)}</span>
                </li>
              ))}
            </ul>
          </div>
        </ConfirmModal>
      )}
    </div>
  );
}
