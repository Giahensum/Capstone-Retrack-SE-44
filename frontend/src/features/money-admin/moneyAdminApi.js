import api from '@/lib/axios';
import { pickCount, PENDING_BANK_KEYS, OPEN_ANOMALY_KEYS, normalizeRefundDetail, normalizeAnomaly } from './refundLabels';

// API Thu chi dành cho Admin. Mỏng: chỉ bóc res.data.data theo ApiResponse<T>.
const base = '/admin/money';
const unwrap = (p) => p.then((r) => r.data.data);

export const moneyAdminApi = {
  getSettings: () => unwrap(api.get(`${base}/settings`)),
  updateSettings: (dto) => unwrap(api.put(`${base}/settings`, dto)),

  getCommissions: (params) => unwrap(api.get(`${base}/commissions`, { params })),
  setDefaultCommission: (flow, rate) => unwrap(api.put(`${base}/commissions/default`, { flow, rate })),
  setPayeeCommission: (userId, flow, rate) => unwrap(api.put(`${base}/commissions/${userId}`, { flow, rate })),

  previewPayouts: (body) => unwrap(api.post(`${base}/payouts/preview`, body)),
  runPayouts: (body) => unwrap(api.post(`${base}/payouts/run`, body)),
  listPayouts: (params) => unwrap(api.get(`${base}/payouts`, { params })),
  getPayout: (id) => unwrap(api.get(`${base}/payouts/${id}`)),
  approvePayout: (id) => unwrap(api.post(`${base}/payouts/${id}/approve`)),
  retryPayout: (id) => unwrap(api.post(`${base}/payouts/${id}/retry`)),
  reconcilePayout: (id) => unwrap(api.post(`${base}/payouts/${id}/reconcile`)),
  releasePayout: (id) => unwrap(api.post(`${base}/payouts/${id}/release`)),
  markPaid: (id, body) => unwrap(api.post(`${base}/payouts/${id}/mark-paid`, body)),

  getBalance: () => unwrap(api.get(`${base}/balance`)),
  testTransfer: (body) => unwrap(api.post(`${base}/test-transfer`, body)),
  testTransferStatus: (key) => unwrap(api.get(`${base}/test-transfer/status`, { params: { key } })),

  listCollections: (params) => unwrap(api.get(`${base}/collections`, { params })),
  listLedger: (params) => unwrap(api.get(`${base}/ledger`, { params })),

  listPayees: (params) => unwrap(api.get(`${base}/payees`, { params })),
  getPayeeBank: (userId) => unwrap(api.get(`${base}/payees/${userId}/bank`)),
  verifyPayeeBank: (userId, expectedAccountNumber) => unwrap(api.post(`${base}/payees/${userId}/bank/verify`, { expectedAccountNumber })),
  rejectPayeeBank: (userId, expectedAccountNumber, reason) => unwrap(api.post(`${base}/payees/${userId}/bank/reject`, { expectedAccountNumber, reason })),
  // Số tài khoản đang chờ duyệt (huy hiệu trên tab). Ưu tiên endpoint riêng, lùi về totalCount của danh sách đã lọc.
  pendingBankCount: async () => {
    try {
      const n = pickCount(await unwrap(api.get(`${base}/payees/pending-bank-count`)), PENDING_BANK_KEYS);
      if (n !== null) return n;
    } catch { /* endpoint có thể chưa có: dùng danh sách */ }
    const list = await unwrap(api.get(`${base}/payees`, { params: { bankStatus: 'PENDING_VERIFICATION', page: 1, pageSize: 1 } }));
    return pickCount(list, ['pendingBankCount']) ?? list?.totalCount ?? 0;
  },
  setAutoPayout: (userId, autoEnabled) => unwrap(api.put(`${base}/payees/${userId}/auto-payout`, { autoEnabled })),

  // Danh mục ngân hàng dùng chung (mọi người dùng đã đăng nhập)
  getBanks: () => unwrap(api.get('/money/banks')),

  // ---- Bất thường thu tiền ----
  listAnomalies: (params) => unwrap(api.get(`${base}/anomalies`, { params })),
  getAnomaly: (id) => unwrap(api.get(`${base}/anomalies/${id}`)).then(normalizeAnomaly),
  resolveAnomaly: (id, note) => unwrap(api.post(`${base}/anomalies/${id}/resolve`, { note })),
  refundAnomaly: (id, body) => unwrap(api.post(`${base}/anomalies/${id}/refund`, body)),
  // Số bất thường đang mở (huy hiệu trên tab). Ưu tiên anomalies/summary, lùi về totalCount của danh sách OPEN.
  openAnomalyCount: async () => {
    try {
      const n = pickCount(await unwrap(api.get(`${base}/anomalies/summary`)), OPEN_ANOMALY_KEYS);
      if (n !== null) return n;
    } catch { /* endpoint có thể chưa có: dùng danh sách */ }
    const list = await unwrap(api.get(`${base}/anomalies`, { params: { status: 'OPEN', page: 1, pageSize: 1 } }));
    return pickCount(list, ['openAnomalyCount']) ?? list?.totalCount ?? 0;
  },

  // ---- Hoàn tiền cho người trả ----
  refundCollection: (id, body) => unwrap(api.post(`${base}/collections/${id}/refund`, body)),
  listRefunds: (params) => unwrap(api.get(`${base}/refunds`, { params })),
  getRefund: (id) => unwrap(api.get(`${base}/refunds/${id}`)).then(normalizeRefundDetail),
  approveRefund: (id) => unwrap(api.post(`${base}/refunds/${id}/approve`)),
  retryRefund: (id) => unwrap(api.post(`${base}/refunds/${id}/retry`)),
  reconcileRefund: (id) => unwrap(api.post(`${base}/refunds/${id}/reconcile`)),
  cancelRefund: (id) => unwrap(api.post(`${base}/refunds/${id}/cancel`)),
  markRefundPaid: (id, body) => unwrap(api.post(`${base}/refunds/${id}/mark-paid`, body)),
};
