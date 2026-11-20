import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { AlertTriangle, ShieldCheck, Save, Percent, Plus, Pencil, Eraser, SlidersHorizontal, Users } from 'lucide-react';
import { Notice, Modal } from '@/components/liquidation/LiquidationUI';
import {
  Panel, Field, inputClass, PrimaryButton, GhostButton, DataTable, Th, Td, TableMessage, Pagination, IconAction, FormFooter,
} from '@/features/admin/components/AdminUI';
import { moneyAdminApi } from '../moneyAdminApi';
import {
  settingsToForm, formToSettingsPatch, validateSettings, COMMISSION_FLOWS, flowLabel, apiError, pct, fmtDateTime, roleLabel, SETTINGS_LIMITS,
} from '../moneyLabels';
import { FieldError, useDebounced } from '../moneyUi';

// Biểu ngữ chế độ/nhà cung cấp hiện tại: luôn hiển thị rõ để không nhầm tiền thật với mô phỏng.
export function ProviderBanner({ settings }) {
  if (!settings) return null;
  const real = settings.payoutProvider === 'PAYOS';
  const platform = settings.mode === 'PLATFORM';
  return (
    <div role="status" className={`flex flex-wrap items-center gap-3 rounded-2xl px-5 py-4 text-sm font-semibold ring-1 ${real ? 'bg-red-50 text-red-800 ring-red-300' : 'bg-blue-50 text-blue-800 ring-blue-200'}`}>
      {real ? <AlertTriangle size={20} aria-hidden="true" /> : <ShieldCheck size={20} aria-hidden="true" />}
      <span className="text-base font-black">{real ? 'payOS thật: tiền thật' : 'Mô phỏng, không chuyển tiền thật'}</span>
      <span className="font-medium">Chế độ thu chi: <strong>{platform ? 'PLATFORM (sàn thu rồi chi)' : 'LEGACY (luồng cũ, chưa qua sàn)'}</strong></span>
      {real && <span className="font-medium">Mọi lệnh chi sẽ chuyển tiền thật từ tài khoản chi của sàn.</span>}
    </div>
  );
}

const toggleClass = 'size-5 cursor-pointer accent-[#446900]';

function OverrideDialog({ initial, onClose }) {
  const qc = useQueryClient();
  const [search, setSearch] = useState('');
  const debounced = useDebounced(search);
  const [userId, setUserId] = useState(initial?.userId ?? '');
  const [userName, setUserName] = useState(initial?.userName ?? '');
  const [flow, setFlow] = useState(initial?.flow ?? 'PICKUP');
  const [rate, setRate] = useState(initial ? String(initial.rate) : '');
  const [err, setErr] = useState('');
  const payees = useQuery({
    queryKey: ['money-admin', 'payees', 'picker', debounced],
    queryFn: () => moneyAdminApi.listPayees({ search: debounced || undefined, page: 1, pageSize: 20 }),
    enabled: !initial,
  });
  const save = useMutation({
    mutationFn: () => moneyAdminApi.setPayeeCommission(userId, flow, Number(rate)),
    onSuccess: () => { toast.success('Đã lưu hoa hồng riêng'); qc.invalidateQueries({ queryKey: ['money-admin', 'commissions'] }); onClose(); },
    onError: (e) => setErr(apiError(e)),
  });
  const submit = (e) => {
    e.preventDefault();
    setErr('');
    const n = Number(rate);
    if (!userId) return setErr('Chọn người nhận.');
    if (rate === '' || !Number.isFinite(n) || n < 0 || n > 100) return setErr('Tỷ lệ phải từ 0 đến 100 (%).');
    save.mutate();
  };
  return (
    <Modal size="md" icon={Percent} title={initial ? 'Sửa hoa hồng riêng' : 'Thêm hoa hồng riêng'} onClose={onClose} busy={save.isPending}
      footer={<FormFooter formId="override-form" onCancel={onClose} submitting={save.isPending} submitLabel="Lưu" icon={Save} />}>
      <form id="override-form" onSubmit={submit} className="flex flex-col gap-4">
        {initial ? (
          <p className="text-sm">Người nhận: <strong>{initial.userName}</strong> ({roleLabel(initial.role)})</p>
        ) : (
          <>
            <Field label="Tìm người nhận (Seller, Chủ kho, Nhà máy)">
              <input type="search" value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Gõ tên người nhận" className={inputClass} />
            </Field>
            <Field label="Người nhận">
              <select value={userId} onChange={(e) => { setUserId(e.target.value); setUserName(payees.data?.items.find((p) => p.userId === e.target.value)?.fullName ?? ''); }} className={`${inputClass} cursor-pointer`}>
                <option value="">{payees.isLoading ? 'Đang tải…' : '— Chọn người nhận —'}</option>
                {userId && !payees.data?.items.some((p) => p.userId === userId) && <option value={userId}>{userName}</option>}
                {payees.data?.items.map((p) => <option key={p.userId} value={p.userId}>{p.fullName} ({roleLabel(p.role)})</option>)}
              </select>
              {payees.isError && <FieldError>{apiError(payees.error, 'Không tải được danh sách người nhận.')}</FieldError>}
            </Field>
          </>
        )}
        <Field label="Dòng tiền">
          <select value={flow} onChange={(e) => setFlow(e.target.value)} disabled={!!initial} className={`${inputClass} cursor-pointer`}>
            {COMMISSION_FLOWS.map((f) => <option key={f} value={f}>{flowLabel(f)}</option>)}
          </select>
        </Field>
        <Field label="Tỷ lệ hoa hồng (%)" hint="Từ 0 đến 100. Áp dụng cho khoản thu mới; đơn cũ giữ mức đã chốt.">
          <input type="number" inputMode="decimal" step="0.01" min="0" max="100" value={rate} onChange={(e) => setRate(e.target.value)} className={inputClass} />
        </Field>
        <FieldError>{err}</FieldError>
      </form>
    </Modal>
  );
}

function OverridesPanel() {
  const qc = useQueryClient();
  const [page, setPage] = useState(1);
  const [dialog, setDialog] = useState(null);
  const q = useQuery({
    queryKey: ['money-admin', 'commissions', page],
    queryFn: () => moneyAdminApi.getCommissions({ page, pageSize: 10 }),
    placeholderData: (p) => p,
  });
  const clear = useMutation({
    mutationFn: (o) => moneyAdminApi.setPayeeCommission(o.userId, o.flow, null),
    onSuccess: () => { toast.success('Đã xóa hoa hồng riêng, quay về mặc định'); qc.invalidateQueries({ queryKey: ['money-admin', 'commissions'] }); },
    onError: (e) => toast.error(apiError(e)),
  });
  const o = q.data?.overrides;
  const items = o?.items ?? [];
  return (
    <Panel icon={Users} title="Hoa hồng riêng theo người nhận"
      toolbar={<PrimaryButton icon={Plus} onClick={() => setDialog({})}>Thêm</PrimaryButton>}>
      <DataTable minWidth={720} head={<><Th>Người nhận</Th><Th>Vai trò</Th><Th>Dòng tiền</Th><Th className="text-right">Tỷ lệ</Th><Th>Cập nhật</Th><Th className="text-right">Thao tác</Th></>}>
        <TableMessage cols={6} loading={q.isLoading} error={q.isError} empty={!q.isLoading && !q.isError && items.length === 0}>
          {q.isError ? apiError(q.error, 'Không tải được hoa hồng riêng.') : 'Chưa có hoa hồng riêng nào. Mọi người nhận dùng mức mặc định.'}
        </TableMessage>
        {items.map((r) => (
          <tr key={`${r.userId}-${r.flow}`}>
            <Td className="font-semibold">{r.userName}</Td>
            <Td>{roleLabel(r.role)}</Td>
            <Td>{flowLabel(r.flow)}</Td>
            <Td className="text-right font-bold">{pct(r.rate)}</Td>
            <Td>{fmtDateTime(r.updatedAt)}</Td>
            <Td className="text-right">
              <div className="inline-flex gap-1">
                <IconAction icon={Pencil} title={`Sửa hoa hồng của ${r.userName}`} onClick={() => setDialog(r)} disabled={clear.isPending} />
                <IconAction icon={Eraser} tone="danger" title={`Xóa hoa hồng riêng của ${r.userName}`} onClick={() => clear.mutate(r)} disabled={clear.isPending} />
              </div>
            </Td>
          </tr>
        ))}
      </DataTable>
      {o && <Pagination page={page} pageSize={o.pageSize} totalCount={o.totalCount} onPageChange={setPage} />}
      {dialog && <OverrideDialog initial={dialog.userId ? dialog : null} onClose={() => setDialog(null)} />}
    </Panel>
  );
}

export default function SettingsTab({ settings, settingsQuery }) {
  const qc = useQueryClient();
  const [form, setForm] = useState(null);
  const [errors, setErrors] = useState({});
  useEffect(() => { if (settings) setForm(settingsToForm(settings)); }, [settings]);

  const save = useMutation({
    mutationFn: (f) => moneyAdminApi.updateSettings(formToSettingsPatch(f)),
    onSuccess: (data) => {
      toast.success('Đã lưu cấu hình thu chi');
      qc.setQueryData(['money-admin', 'settings'], data);
      qc.invalidateQueries({ queryKey: ['money-admin', 'balance'] });
    },
    onError: (e) => toast.error(apiError(e, 'Không lưu được cấu hình.')),
  });

  if (settingsQuery.isLoading || !form) {
    return <p role="status" className="py-12 text-center text-sm text-d-on-surface-variant">{settingsQuery.isError ? apiError(settingsQuery.error, 'Không tải được cấu hình.') : 'Đang tải cấu hình…'}</p>;
  }
  const set = (patch) => setForm((f) => ({ ...f, ...patch }));
  const submit = (e) => {
    e.preventDefault();
    const errs = validateSettings(form);
    setErrors(errs);
    if (Object.keys(errs).length) { toast.error('Cấu hình chưa hợp lệ, kiểm tra các ô báo đỏ.'); return; }
    if (form.payoutProvider === 'PAYOS' && settings.payoutProvider !== 'PAYOS'
      && !window.confirm('Bạn đang chuyển sang payOS THẬT: các lệnh chi sau đó sẽ chuyển tiền thật. Tiếp tục lưu?')) return;
    save.mutate(form);
  };
  const [tMin, tMax] = SETTINGS_LIMITS.payoutTestMaxAmount;
  const num = (key, label, hint, step = 1) => (
    <Field label={label} hint={hint}>
      <input type="number" inputMode="numeric" step={step} min={SETTINGS_LIMITS[key][0]} max={SETTINGS_LIMITS[key][1]} value={form[key]}
        onChange={(e) => set({ [key]: e.target.value })} aria-invalid={!!errors[key]} className={inputClass} />
      <FieldError>{errors[key]}</FieldError>
    </Field>
  );

  return (
    <div className="flex flex-col gap-6">
      <ProviderBanner settings={settings} />
      <form onSubmit={submit} className="flex flex-col gap-6" noValidate>
        <Panel icon={SlidersHorizontal} title="Chế độ và nhà cung cấp chi">
          <div className="grid gap-5 p-5 md:grid-cols-2">
            <Field label="Chế độ thu chi" hint="PLATFORM: người trả nộp tiền vào sàn, sàn trừ hoa hồng rồi chi cho người nhận. LEGACY: luồng cũ.">
              <select value={form.mode} onChange={(e) => set({ mode: e.target.value })} className={`${inputClass} cursor-pointer`}>
                <option value="LEGACY">LEGACY (luồng cũ)</option>
                <option value="PLATFORM">PLATFORM (sàn thu rồi chi)</option>
              </select>
              <FieldError>{errors.mode}</FieldError>
            </Field>
            <Field label="Nhà cung cấp chi"
              hint={settings.payosPayoutConfigured ? 'PAYOS chuyển tiền thật; SIMULATED chỉ giả lập.' : 'Chưa chọn được PAYOS vì máy chủ chưa có đủ 3 khóa kênh chi payOS (PayOSPayout__ClientId, __ApiKey, __ChecksumKey).'}>
              <select value={form.payoutProvider} onChange={(e) => set({ payoutProvider: e.target.value })} className={`${inputClass} cursor-pointer`}>
                <option value="SIMULATED">SIMULATED (mô phỏng, không chuyển tiền thật)</option>
                <option value="PAYOS" disabled={!settings.payosPayoutConfigured}>PAYOS (tiền thật){settings.payosPayoutConfigured ? '' : ' - chưa cấu hình khóa'}</option>
              </select>
              <FieldError>{errors.payoutProvider}</FieldError>
            </Field>
          </div>
          {form.payoutProvider === 'PAYOS' && (
            <div className="px-5 pb-5"><Notice icon={AlertTriangle} tone="red">Đang chọn <strong>payOS thật</strong>: khi lưu, các kỳ chi từ lúc này sẽ chuyển tiền thật.</Notice></div>
          )}
        </Panel>

        <Panel icon={Percent} title="Hoa hồng mặc định (%)">
          <div className="grid gap-5 p-5 md:grid-cols-3">
            {COMMISSION_FLOWS.map((f) => (
              <Field key={f} label={`Hoa hồng ${flowLabel(f).toLowerCase()}`} hint="Từ 0 đến 100. Đơn cũ giữ nguyên mức đã chốt.">
                <input type="number" inputMode="decimal" step="0.01" min="0" max="100" value={form.defaultCommission[f]}
                  onChange={(e) => set({ defaultCommission: { ...form.defaultCommission, [f]: e.target.value } })}
                  aria-invalid={!!errors[`commission.${f}`]} className={inputClass} />
                <FieldError>{errors[`commission.${f}`]}</FieldError>
              </Field>
            ))}
          </div>
        </Panel>

        <Panel icon={SlidersHorizontal} title="Chi tự động, giữ tiền và hạn mức">
          <div className="grid gap-5 p-5 md:grid-cols-2">
            <label className="flex items-center gap-3 md:col-span-2">
              <input type="checkbox" checked={form.payoutAutoEnabled} onChange={(e) => set({ payoutAutoEnabled: e.target.checked })} className={toggleClass} />
              <span className="text-sm"><strong>Bật chi tự động</strong> theo lịch bên dưới (bỏ qua người nhận đã tắt tự chi).</span>
            </label>
            <Field label="Lịch chi tự động">
              <select value={form.payoutSchedule} onChange={(e) => set({ payoutSchedule: e.target.value })} className={`${inputClass} cursor-pointer`}>
                <option value="DAILY">Hằng ngày</option>
                <option value="WEEKLY">Hằng tuần</option>
              </select>
              <FieldError>{errors.payoutSchedule}</FieldError>
            </Field>
            {num('payoutHoldMinutes', 'Giữ tiền trước khi chi (phút)', 'Từ 0 đến 525.600. Khoản thu chỉ sẵn sàng chi sau thời gian này.')}
            {num('payoutMinAmount', 'Số chi tối thiểu (đ)', 'Kỳ thực chi thấp hơn mức này sẽ dồn sang kỳ sau.')}
            {num('payoutAutoMaxAmount', 'Hạn mức tự chi (đ)', 'Kỳ thực chi vượt mức này cần Admin duyệt.')}
          </div>
        </Panel>

        <Panel icon={SlidersHorizontal} title="Chuyển thử">
          <div className="grid gap-5 p-5 md:grid-cols-2">
            <label className="flex items-center gap-3">
              <input type="checkbox" checked={form.payoutTestEnabled} onChange={(e) => set({ payoutTestEnabled: e.target.checked })} className={toggleClass} />
              <span className="text-sm"><strong>Cho phép chuyển thử</strong> (tab Chuyển thử).</span>
            </label>
            {num('payoutTestMaxAmount', 'Số tiền chuyển thử tối đa (đ)', `Từ ${tMin.toLocaleString('vi-VN')} đến ${tMax.toLocaleString('vi-VN')}.`)}
          </div>
        </Panel>

        <div className="flex flex-wrap items-center justify-end gap-2">
          <GhostButton onClick={() => { setForm(settingsToForm(settings)); setErrors({}); }} disabled={save.isPending}>Hoàn tác</GhostButton>
          <PrimaryButton type="submit" icon={Save} disabled={save.isPending}>{save.isPending ? 'Đang lưu…' : 'Lưu cấu hình'}</PrimaryButton>
        </div>
      </form>
      <OverridesPanel />
    </div>
  );
}
