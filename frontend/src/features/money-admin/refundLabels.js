// Nhãn, hành động hợp lệ và kiểm tra dữ liệu cho Bất thường và Hoàn tiền (Admin).
// Hàm thuần, không import React/axios để kiểm thử được bằng `node --test`.

export const ANOMALY_KIND = {
  UNDERPAID: { label: 'Trả thiếu', tone: 'amber', hint: 'Người trả chuyển ít hơn số tiền của khoản thu.' },
  OVERPAID: { label: 'Trả thừa', tone: 'blue', hint: 'Người trả chuyển nhiều hơn số tiền của khoản thu; phần dư cần hoàn.' },
  LATE_PAYMENT: { label: 'Thanh toán trễ', tone: 'amber', hint: 'Tiền về sau khi khoản thu đã hết hạn hoặc bị hủy.' },
  DUPLICATE_PAYMENT: { label: 'Thanh toán trùng', tone: 'red', hint: 'Khoản thu đã được thanh toán nhưng có thêm một lần thanh toán nữa.' },
  SOURCE_MISMATCH: { label: 'Lệch nguồn', tone: 'red', hint: 'Giao dịch không khớp nguồn của khoản thu; cần đối chiếu thủ công.' },
};
export const ANOMALY_KIND_OPTIONS = Object.keys(ANOMALY_KIND);
export const anomalyKindMeta = (k) => ANOMALY_KIND[k] ?? { label: k || '—', tone: 'slate', hint: '' };

export const ANOMALY_STATUS = {
  OPEN: { label: 'Chưa xử lý', tone: 'amber' },
  RESOLVED: { label: 'Đã xử lý', tone: 'primary' },
};
export const anomalyStatusMeta = (s) => ANOMALY_STATUS[s] ?? { label: s || '—', tone: 'slate' };

export const REFUND_STATUS = {
  PENDING: { label: 'Chờ gửi', tone: 'amber', hint: 'Chưa gửi được (ví dụ số dư tài khoản chi chưa đủ). Có thể thử lại.' },
  AWAITING_APPROVAL: { label: 'Chờ duyệt', tone: 'amber', hint: 'Cần Admin bấm Duyệt trước khi hoàn tiền cho người trả.' },
  PROCESSING: { label: 'Đang hoàn', tone: 'blue', hint: 'Lệnh hoàn đã gửi, đang chờ ngân hàng xác nhận.' },
  PAID: { label: 'Đã hoàn', tone: 'primary', hint: 'Tiền đã chuyển về tài khoản người trả.' },
  FAILED: { label: 'Hoàn thất bại', tone: 'red', hint: 'Lệnh hoàn bị từ chối. Có thể thử lại.' },
  UNKNOWN: { label: 'Chưa rõ kết quả', tone: 'red', hint: 'Không biết lệnh đã đến ngân hàng chưa. Dùng Đối soát, không tạo lệnh mới.' },
  MANUAL_PAID: { label: 'Đã hoàn tay', tone: 'teal', hint: 'Admin đã hoàn tiền ngoài hệ thống và ghi nhận.' },
  CANCELLED: { label: 'Đã hủy', tone: 'slate', hint: 'Yêu cầu hoàn tiền đã bị hủy.' },
};
export const REFUND_STATUS_OPTIONS = Object.keys(REFUND_STATUS);
export const refundStatusMeta = (s) => REFUND_STATUS[s] ?? { label: s || '—', tone: 'slate', hint: '' };

export const REFUND_KIND = {
  EXCESS: 'Hoàn phần trả thừa',
  DUPLICATE: 'Hoàn thanh toán trùng',
  LATE: 'Hoàn thanh toán trễ',
  FULL: 'Hoàn toàn bộ khoản thu',
  PARTIAL: 'Hoàn một phần khoản thu',
};
export const refundKindLabel = (k) => REFUND_KIND[k] ?? k ?? '—';

/** Hành động hợp lệ theo trạng thái lệnh hoàn tiền (cùng quy tắc với kỳ chi). */
export function refundActions(status) {
  return {
    approve: status === 'AWAITING_APPROVAL',
    retry: status === 'FAILED' || status === 'PENDING',
    reconcile: status === 'PROCESSING' || status === 'UNKNOWN',
    cancel: ['PENDING', 'AWAITING_APPROVAL', 'FAILED'].includes(status),
    markPaid: ['PROCESSING', 'UNKNOWN', 'FAILED', 'PENDING', 'AWAITING_APPROVAL'].includes(status),
    markPaidNeedsConfirm: status === 'PROCESSING' || status === 'UNKNOWN',
  };
}

/** Bất thường có thể hoàn tiền khi còn mở, còn số được hoàn và chưa có lệnh hoàn. */
export function anomalyActions(a) {
  const open = a?.status === 'OPEN';
  const refundable = Number(a?.refundableAmount ?? 0);
  return {
    resolve: open,
    refund: open && refundable > 0 && !a?.hasRefund,
    refundBlockedByExisting: open && !!a?.hasRefund,
  };
}

// ---- Kiểm tra dữ liệu nhập ----
const isWhole = (v) => v !== '' && v !== null && v !== undefined && Number.isFinite(Number(v)) && Number(v) === Math.trunc(Number(v));

export const REASON_LIMITS = { min: 5, max: 255 };

/** Lý do 5 đến 255 ký tự (giống backend). Trả chuỗi lỗi hoặc null. */
export function validateReason(reason, label = 'Lý do') {
  const r = (reason ?? '').trim();
  if (r.length < REASON_LIMITS.min) return `${label} tối thiểu ${REASON_LIMITS.min} ký tự.`;
  if (r.length > REASON_LIMITS.max) return `${label} tối đa ${REASON_LIMITS.max} ký tự.`;
  return null;
}

/** Số tiền hoàn: số nguyên đồng từ 1 đến max (nếu có). Trả chuỗi lỗi hoặc null. */
export function validateRefundAmount(raw, max) {
  if (!isWhole(raw) || Number(raw) < 1) return 'Số tiền hoàn phải là số nguyên đồng, tối thiểu 1 đ.';
  if (max != null && Number(raw) > Number(max)) return `Số tiền hoàn không được vượt quá ${Number(max).toLocaleString('vi-VN')} đ.`;
  return null;
}

/** Form hoàn từ bất thường: số tiền (mặc định = số được hoàn) và lý do. */
export function validateAnomalyRefundForm({ amount, reason }, refundable) {
  const errors = {};
  const a = validateRefundAmount(amount, refundable);
  if (a) errors.amount = a;
  const r = validateReason(reason);
  if (r) errors.reason = r;
  return errors;
}

/** Form hoàn từ khoản thu: kind FULL (hoàn toàn bộ, không gửi số tiền) hoặc PARTIAL (một phần, bắt buộc số tiền). */
export function validateCollectionRefundForm({ kind, amount, reason }, collectionAmount) {
  const errors = {};
  if (!['FULL', 'PARTIAL'].includes(kind)) errors.kind = 'Chọn hoàn toàn bộ hoặc một phần.';
  if (kind === 'PARTIAL') {
    const a = validateRefundAmount(amount, collectionAmount);
    if (a) errors.amount = a;
    else if (collectionAmount != null && Number(amount) >= Number(collectionAmount)) errors.amount = 'Hoàn một phần phải nhỏ hơn số tiền khoản thu; hãy chọn hoàn toàn bộ.';
  }
  const r = validateReason(reason);
  if (r) errors.reason = r;
  return errors;
}

export function buildAnomalyRefundBody({ amount, reason }) {
  return { amount: Number(amount), reason: reason.trim() };
}

export function buildCollectionRefundBody({ kind, amount, reason }) {
  const body = { kind, reason: reason.trim() };
  if (kind === 'PARTIAL') body.amount = Number(amount);
  return body;
}

export function validateMarkPaid({ reference, confirm }, needsConfirm) {
  if (!(reference ?? '').trim()) return 'Nhập mã tham chiếu giao dịch (ví dụ mã chuyển khoản ngân hàng).';
  if (needsConfirm && !confirm) return 'Tick xác nhận bạn đã chuyển tiền ngoài hệ thống.';
  return null;
}

/** Thông báo lỗi 409 liên quan tài khoản ngân hàng của người trả (để hiện lối tắt sang tab Người nhận). */
export function isPayerBankProblem(message) {
  return /tài khoản|ngân hàng/i.test(message ?? '');
}

/** Lấy số đếm từ nhiều dạng phản hồi: số thuần, hoặc object có một trong các khóa. */
export function pickCount(data, keys) {
  if (typeof data === 'number' && Number.isFinite(data)) return data;
  if (data && typeof data === 'object') {
    for (const k of keys) if (Number.isFinite(Number(data[k])) && data[k] !== null && data[k] !== '') return Number(data[k]);
  }
  return null;
}

export const PENDING_BANK_KEYS = ['pendingBankCount', 'pendingCount', 'count', 'total'];
export const OPEN_ANOMALY_KEYS = ['openAnomalyCount', 'openCount', 'open', 'count', 'total'];

// ---- Chuẩn hóa phản hồi chi tiết (chịu được cả dạng phẳng lẫn dạng bọc { refund, bank, idempotencyKey }) ----
export function normalizeRefundDetail(d) {
  if (!d) return null;
  const refund = d.refund ?? d;
  return { refund, bank: d.bank ?? refund.bank ?? null, idempotencyKey: d.idempotencyKey ?? refund.idempotencyKey ?? null };
}

export function normalizeAnomaly(d) {
  if (!d) return null;
  if (!d.anomaly) return d;
  return { ...d.anomaly, collection: d.anomaly.collection ?? d.collection, payer: d.anomaly.payer ?? d.payer, payee: d.anomaly.payee ?? d.payee, payerBank: d.anomaly.payerBank ?? d.payerBank };
}

// ---- Phía người trả: nhãn trạng thái "Hoàn tiền cho tôi" (không lộ chi tiết nhà cung cấp) ----
export function payerRefundStatusMeta(status) {
  switch (status) {
    case 'PAID':
    case 'MANUAL_PAID': return { label: 'Đã hoàn', tone: 'primary' };
    case 'PROCESSING': return { label: 'Đang hoàn tiền', tone: 'blue' };
    case 'CANCELLED': return { label: 'Đã hủy', tone: 'slate' };
    case 'PENDING':
    case 'AWAITING_APPROVAL':
    case 'FAILED':
    case 'UNKNOWN': return { label: 'Đang xử lý', tone: 'amber' };
    default: return { label: status || '—', tone: 'slate' };
  }
}
