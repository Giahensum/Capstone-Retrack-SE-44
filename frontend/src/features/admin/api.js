import api from '@/lib/axios';

// Admin API — mỏng, chỉ unwrap res.data.data theo ApiResponse<T> convention của backend
export const adminApi = {
  // Dashboard (UC-7.1)
  getDashboardStats: () => api.get('/admin/dashboard/stats').then((r) => r.data.data),

  // Users (UC-7.2, 7.3, 7.4)
  searchUsers: (params) => api.get('/admin/users', { params }).then((r) => r.data.data),
  getUser: (id) => api.get(`/admin/users/${id}`).then((r) => r.data.data),
  createUser: (dto) => api.post('/admin/users', dto).then((r) => r.data.data),
  updateUser: (id, dto) => api.put(`/admin/users/${id}`, dto).then((r) => r.data.data),
  setUserActive: (id, isActive) => api.patch(`/admin/users/${id}/status`, { isActive }).then((r) => r.data.data),
  deleteUser: (id) => api.delete(`/admin/users/${id}`).then((r) => r.data.data),

  // Market prices (UC-7.7)
  getMarketPrices: (materialType) => api.get('/admin/market-prices', { params: { materialType } }).then((r) => r.data.data),
  createMarketPrice: (dto) => api.post('/admin/market-prices', dto).then((r) => r.data.data),
  updateMarketPrice: (id, dto) => api.put(`/admin/market-prices/${id}`, dto).then((r) => r.data.data),
  deleteMarketPrice: (id) => api.delete(`/admin/market-prices/${id}`).then((r) => r.data.data),

  // Billing: fee config (7.8), revenue (7.9), transactions (7.10), invoices (7.11, 7.12)
  getFeeConfig: () => api.get('/admin/billing/fee-config').then((r) => r.data.data),
  updateFeeConfig: (dto) => api.put('/admin/billing/fee-config', dto).then((r) => r.data.data),
  getRevenue: (params) => api.get('/admin/billing/revenue', { params }).then((r) => r.data.data),
  getTransactions: (params) => api.get('/admin/billing/transactions', { params }).then((r) => r.data.data),
  generateInvoices: (dto) => api.post('/admin/billing/invoices/generate', dto).then((r) => r.data.data),
  getInvoices: (params) => api.get('/admin/billing/invoices', { params }).then((r) => r.data.data),
  markInvoicePaid: (id) => api.patch(`/admin/billing/invoices/${id}/mark-paid`).then((r) => r.data.data),
  remindInvoice: (id) => api.post(`/admin/billing/invoices/${id}/remind`).then((r) => r.data.data),

  // Audit logs (UC-7.6)
  getAuditLogs: (params) => api.get('/admin/audit-logs', { params }).then((r) => r.data.data),
};

export function apiErrorMessage(error, fallback = 'Đã xảy ra lỗi, vui lòng thử lại.') {
  return error?.response?.data?.message ?? fallback;
}
