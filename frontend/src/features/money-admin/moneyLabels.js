// Nhãn, màu trạng thái và hàm thuần dùng chung cho màn Thu chi (Admin) và Thu nhập (người nhận).
// Không import React/axios để kiểm thử được bằng `node --test`.

export const PAYOUT_STATUS = {
  PROCESSING: { label: 'Đang chi', tone: 'blue', hint: 'Lệnh chi đã gửi, đang chờ ngân hàng xác nhận.' },
  AWAITING_APPROVAL: { label: 'Chờ duyệt', tone: 'amber', hint: 'Số tiền vượt hạn mức tự chi, cần Admin bấm Duyệt.' },
  HELD: { label: 'Đang giữ', tone: 'amber', hint: 'Tài khoản ngân hàng chưa được Admin duyệt, chưa chi. Duyệt xong kỳ tự được giải phóng.' },
  DEFERRED: { label: 'Dồn kỳ sau', tone: 'slate', hint: 'Dưới mức chi tối thiểu, sẽ cộng vào kỳ chi tiếp theo.' },
  CARRIED: { label: 'Mang nợ sang', tone: 'red', hint: 'Kỳ âm, khoản nợ được trừ vào kỳ sau.' },
  SETTLED: { label: 'Đã bù trừ', tone: 'teal', hint: 'Thực chi bằng 0, không cần chuyển tiền.' },
  PAID: { label: 'Đã chi', tone: 'primary', hint: 'Tiền đã chuyển vào tài khoản người nhận.' },
  FAILED: { label: 'Chi thất bại', tone: 'red', hint: 'Lệnh chi bị từ chối. Có thể thử lại.' },
  UNKNOWN: { label: 'Chưa rõ kết quả', tone: 'red', hint: 'Không biết lệnh đã đến ngân hàng chưa. Dùng Đối soát, không gửi mới.' },
  PENDING: { label: 'Chờ gửi', tone: 'amber', hint: 'Chưa gửi được (ví dụ số dư tài khoản chi chưa đủ). Có thể thử lại.' },
};

export const PAYOUT_STATUS_OPTIONS = Object.keys(PAYOUT_STATUS);

export function payoutStatusMeta(status) {
  return PAYOUT_STATUS[status] ?? { label: status || '—', tone: 'slate', hint: '' };
}

export const OUTCOME = {
  NEW: { label: 'Sẽ tạo kỳ mới', tone: 'primary' },
  CREATED: { label: 'Đã tạo kỳ chi', tone: 'primary' },
  EXISTS: { label: 'Kỳ đã có (không đổi)', tone: 'slate' },
  NO_LINES: { label: 'Không có khoản phải chi', tone: 'slate' },
  CONFLICT: { label: 'Trùng/lệch kỳ đã có', tone: 'red' },
  SKIPPED: { label: 'Bỏ qua', tone: 'amber' },
  ERROR: { label: 'Lỗi', tone: 'red' },
};

export function outcomeMeta(outcome) {
  return OUTCOME[outcome] ?? { label: outcome || '—', tone: 'slate' };
}

// Mọi thay đổi tài khoản (kể cả lần nhập đầu) đều chờ Admin duyệt; không còn tự xác minh hay thời gian giữ.
export const BANK_STATUS = {
  NONE: { label: 'Chưa có tài khoản', tone: 'slate' },
  VERIFIED: { label: 'Đã duyệt', tone: 'primary' },
  PENDING_VERIFICATION: { label: 'Chờ Admin duyệt', tone: 'amber' },
  REJECTED: { label: 'Bị từ chối', tone: 'red' },
};
export const BANK_STATUS_OPTIONS = ['PENDING_VERIFICATION', 'VERIFIED', 'REJECTED', 'NONE'];

export function bankStatusMeta(status) {
  if (!status) return BANK_STATUS.NONE;
  return BANK_STATUS[status] ?? { label: status, tone: 'slate' };
}

export const FLOW_LABEL = {
  PICKUP: 'Thu gom',
  BATCH: 'Lô xuất kho',
  LIQUIDATION: 'Thanh lý',
  LIQUIDATION_DEPOSIT: 'Cọc thanh lý',
  LIQUIDATION_BALANCE: 'Thanh toán thanh lý',
};
export const COMMISSION_FLOWS = ['PICKUP', 'BATCH', 'LIQUIDATION'];
export const flowLabel = (f) => FLOW_LABEL[f] ?? f ?? '—';

export const SOURCE_LABEL = { PICKUP_REQUEST: 'Đơn thu gom', INVENTORY_BATCH: 'Lô hàng', LIQUIDATION_AGREEMENT: 'Hợp đồng thanh lý' };
export const sourceLabel = (s) => SOURCE_LABEL[s] ?? s ?? '—';

export const KIND_LABEL = { SALE: 'Bán hàng', REFUND: 'Hoàn tiền' };
export const kindLabel = (k) => KIND_LABEL[k] ?? k ?? '—';

export const LINE_STATE = {
  HELD: { label: 'Đang giữ', tone: 'amber' },
  AVAILABLE: { label: 'Sẵn sàng chi', tone: 'teal' },
  IN_PAYOUT: { label: 'Trong kỳ chi', tone: 'blue' },
  PAID: { label: 'Đã chi', tone: 'primary' },
  REVERSED: { label: 'Đã hoàn', tone: 'red' },
};
export const lineStateMeta = (s) => LINE_STATE[s] ?? { label: s || '—', tone: 'slate' };

export const COLLECTION_STATUS = {
  PENDING: { label: 'Chờ thanh toán', tone: 'amber' },
  PAID: { label: 'Đã thu', tone: 'primary' },
  EXPIRED: { label: 'Hết hạn', tone: 'slate' },
  CANCELLED: { label: 'Đã hủy', tone: 'slate' },
  REFUNDING: { label: 'Đang hoàn', tone: 'amber' },
  REFUNDED: { label: 'Đã hoàn', tone: 'red' },
};
export const collectionStatusMeta = (s) => COLLECTION_STATUS[s] ?? { label: s || '—', tone: 'slate' };

export const ROLE_LABEL = { SELLER: 'Người bán', DEPOT_OWNER: 'Chủ kho vựa', FACTORY: 'Nhà máy' };
export const roleLabel = (r) => ROLE_LABEL[r] ?? r ?? '—';

export const PROVIDER_LABEL = { SIMULATED: 'Mô phỏng', PAYOS: 'payOS' };

// ---- Hành động hợp lệ theo trạng thái kỳ chi ----
export function payoutActions(status) {
  return {
    approve: status === 'AWAITING_APPROVAL',
    retry: status === 'FAILED' || status === 'PENDING',
    reconcile: status === 'PROCESSING' || status === 'UNKNOWN',
    release: status === 'HELD',
    markPaid: ['PROCESSING', 'UNKNOWN', 'FAILED', 'PENDING', 'AWAITING_APPROVAL', 'HELD'].includes(status),
    markPaidNeedsConfirm: status === 'PROCESSING' || status === 'UNKNOWN',
  };
}

// ---- Định dạng ----
const nf = new Intl.NumberFormat('vi-VN');
export function vnd(n) {
  if (n === null || n === undefined || n === '' || Number.isNaN(Number(n))) return '—';
  return `${nf.format(Math.round(Number(n)))} đ`;
}

export function pct(n) {
  if (n === null || n === undefined || Number.isNaN(Number(n))) return '—';
  return `${Number(n).toLocaleString('vi-VN', { maximumFractionDigits: 4 })}%`;
}

export function fmtDateTime(iso) {
  if (!iso) return '—';
  // Backend trả DateTime UTC không có hậu tố Z: coi là UTC.
  const s = /[zZ]|[+-]\d\d:?\d\d$/.test(iso) ? iso : `${iso}Z`;
  const d = new Date(s);
  if (Number.isNaN(d.getTime())) return '—';
  return new Intl.DateTimeFormat('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' }).format(d);
}

export function fmtDate(iso) {
  if (!iso) return '—';
  const s = /[zZ]|[+-]\d\d:?\d\d$/.test(iso) ? iso : `${iso}Z`;
  const d = new Date(s);
  if (Number.isNaN(d.getTime())) return '—';
  return new Intl.DateTimeFormat('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', day: '2-digit', month: '2-digit', year: 'numeric' }).format(d);
}

// ---- Ngày theo giờ Việt Nam (UTC+7, không có DST) ----
const VN_OFFSET_MS = 7 * 3600 * 1000;

/** Ngày hôm nay theo giờ Việt Nam dạng yyyy-MM-dd. */
export function vnToday(now = new Date()) {
  return new Date(now.getTime() + VN_OFFSET_MS).toISOString().slice(0, 10);
}

/** yyyy-MM-dd lùi n ngày (tính trên lịch, không phụ thuộc múi giờ máy). */
export function addDays(dateStr, n) {
  const d = new Date(`${dateStr}T00:00:00Z`);
  d.setUTCDate(d.getUTCDate() + n);
  return d.toISOString().slice(0, 10);
}

/** 00:00 giờ Việt Nam của ngày đó, dạng ISO có offset +07:00. */
export function vnStartOfDayIso(dateStr) {
  return `${dateStr}T00:00:00+07:00`;
}

/**
 * Kiểm tra khoảng đối soát: kỳ là [từ 00:00 ngày bắt đầu, 00:00 ngày kết thúc) theo giờ Việt Nam.
 * Ngày kết thúc không được sau hôm nay (để mốc kết thúc không ở tương lai).
 */
export function validatePeriod(start, end, today = vnToday()) {
  if (!start || !end) return 'Chọn đủ ngày bắt đầu và ngày kết thúc.';
  if (!(start < end)) return 'Ngày bắt đầu phải trước ngày kết thúc.';
  if (end > today) return 'Ngày kết thúc không được ở tương lai (tối đa là hôm nay).';
  return null;
}

// ---- Cấu hình: kiểm tra khoảng giá trị giống backend ----
const isWhole = (v) => v !== '' && v !== null && v !== undefined && Number.isFinite(Number(v)) && Number(v) === Math.trunc(Number(v));

export const SETTINGS_LIMITS = {
  commission: [0, 100],
  payoutHoldMinutes: [0, 525600],
  payoutMinAmount: [0, 1_000_000_000],
  payoutAutoMaxAmount: [0, 100_000_000_000],
  payoutTestMaxAmount: [1000, 100_000],
};

function checkWhole(errors, key, value, label) {
  const [min, max] = SETTINGS_LIMITS[key];
  if (!isWhole(value) || Number(value) < min || Number(value) > max) errors[key] = `${label} phải là số nguyên từ ${nf.format(min)} đến ${nf.format(max)}.`;
}

export function validateSettings(form) {
  const errors = {};
  for (const flow of COMMISSION_FLOWS) {
    const v = form.defaultCommission?.[flow];
    const [min, max] = SETTINGS_LIMITS.commission;
    if (v === '' || v === null || v === undefined || !Number.isFinite(Number(v)) || Number(v) < min || Number(v) > max) errors[`commission.${flow}`] = 'Tỷ lệ phải từ 0 đến 100 (%).';
  }
  checkWhole(errors, 'payoutHoldMinutes', form.payoutHoldMinutes, 'Thời gian giữ tiền (phút)');
  checkWhole(errors, 'payoutMinAmount', form.payoutMinAmount, 'Số chi tối thiểu');
  checkWhole(errors, 'payoutAutoMaxAmount', form.payoutAutoMaxAmount, 'Hạn mức tự chi');
  if (!errors.payoutMinAmount && !errors.payoutAutoMaxAmount && Number(form.payoutAutoMaxAmount) < Number(form.payoutMinAmount)) errors.payoutAutoMaxAmount = 'Hạn mức tự chi phải lớn hơn hoặc bằng số chi tối thiểu.';
  checkWhole(errors, 'payoutTestMaxAmount', form.payoutTestMaxAmount, 'Số tiền chuyển thử tối đa');
  if (!['DAILY', 'WEEKLY'].includes(form.payoutSchedule)) errors.payoutSchedule = 'Chọn lịch chi hằng ngày hoặc hằng tuần.';
  if (!['LEGACY', 'PLATFORM'].includes(form.mode)) errors.mode = 'Chọn chế độ thu chi.';
  if (!['SIMULATED', 'PAYOS'].includes(form.payoutProvider)) errors.payoutProvider = 'Chọn nhà cung cấp chi.';
  return errors;
}

/** Chuyển dữ liệu server thành giá trị form (chuỗi cho ô nhập). */
export function settingsToForm(s) {
  const str = (v) => (v === null || v === undefined ? '' : String(v));
  return {
    mode: s.mode,
    defaultCommission: Object.fromEntries(COMMISSION_FLOWS.map((f) => [f, str(s.defaultCommission?.[f])])),
    payoutAutoEnabled: !!s.payoutAutoEnabled,
    payoutSchedule: s.payoutSchedule,
    payoutHoldMinutes: str(s.payoutHoldMinutes),
    payoutMinAmount: str(s.payoutMinAmount),
    payoutAutoMaxAmount: str(s.payoutAutoMaxAmount),
    payoutProvider: s.payoutProvider,
    payoutTestEnabled: !!s.payoutTestEnabled,
    payoutTestMaxAmount: str(s.payoutTestMaxAmount),
  };
}

/** Chuyển giá trị form thành body PUT /settings. */
export function formToSettingsPatch(f) {
  return {
    mode: f.mode,
    defaultCommission: Object.fromEntries(COMMISSION_FLOWS.map((k) => [k, Number(f.defaultCommission[k])])),
    payoutAutoEnabled: f.payoutAutoEnabled,
    payoutSchedule: f.payoutSchedule,
    payoutHoldMinutes: Number(f.payoutHoldMinutes),
    payoutMinAmount: Number(f.payoutMinAmount),
    payoutAutoMaxAmount: Number(f.payoutAutoMaxAmount),
    payoutProvider: f.payoutProvider,
    payoutTestEnabled: f.payoutTestEnabled,
    payoutTestMaxAmount: Number(f.payoutTestMaxAmount),
  };
}

// ---- Chuyển thử ----
/** Khóa idempotency 32 ký tự hex (backend nhận 8 đến 32). */
export function newClientKey(randomBytes = (n) => crypto.getRandomValues(new Uint8Array(n))) {
  return Array.from(randomBytes(16), (b) => b.toString(16).padStart(2, '0')).join('');
}

export function validateTestTransfer({ amount, bin, account }, maxAmount) {
  const errors = {};
  const a = Number(amount);
  if (!isWhole(amount) || a < 1000 || a > maxAmount) errors.amount = `Số tiền phải là số nguyên từ 1.000 đến ${nf.format(maxAmount)} đ.`;
  if (!bin) errors.bin = 'Chọn ngân hàng nhận.';
  if (!/^\d{6,19}$/.test((account ?? '').trim())) errors.account = 'Số tài khoản gồm 6 đến 19 chữ số.';
  return errors;
}

/** Che số tài khoản: giữ 4 số cuối. */
export function maskAccount(account) {
  if (!account) return '—';
  const s = String(account);
  return s.length <= 4 ? s : `${'•'.repeat(s.length - 4)}${s.slice(-4)}`;
}

/**
 * Tổng hợp kết quả xem trước để hiện trong hộp xác nhận.
 * runnable: người nhận chạy sẽ tạo kỳ mới (NEW); sendNow: trong đó kỳ sẽ vào PROCESSING (gửi tiền ngay).
 */
export function summarizePlan(items = []) {
  const runnable = items.filter((p) => p.outcome === 'NEW');
  const sendNow = runnable.filter((p) => p.status === 'PROCESSING');
  const sum = (arr) => arr.reduce((s, p) => s + Number(p.net || 0), 0);
  return { runnable, sendNow, totalNet: sum(runnable), sendNowNet: sum(sendNow), skipped: items.length - runnable.length };
}

export function apiError(error, fallback = 'Đã xảy ra lỗi, vui lòng thử lại.') {
  return error?.response?.data?.message ?? fallback;
}
