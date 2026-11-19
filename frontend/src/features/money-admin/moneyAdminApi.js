import api from '@/lib/axios';

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
  setAutoPayout: (userId, autoEnabled) => unwrap(api.put(`${base}/payees/${userId}/auto-payout`, { autoEnabled })),

  // Danh mục ngân hàng dùng chung (mọi người dùng đã đăng nhập)
  getBanks: () => unwrap(api.get('/money/banks')),
};
