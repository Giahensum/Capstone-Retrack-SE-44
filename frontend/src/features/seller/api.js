import api from '@/lib/axios';

export const sellerApi = {
  // ── Stats ──
  getStats: () => api.get('/seller/pickups/stats').then(r => r.data.data),

  // ── Pickup CRUD ──
  getRequests: (status) => api.get('/seller/pickups', { params: { status } }).then(r => r.data.data),
  getRequestById: (id) => api.get(`/seller/pickups/${id}`).then(r => r.data.data),
  createRequest: (data) => api.post('/seller/pickups', data).then(r => r.data.data),
  cancelRequest: (id) => api.post(`/seller/pickups/${id}/cancel`).then(r => r.data),
  confirmWeigh: (id) => api.post(`/seller/pickups/${id}/confirm`).then(r => r.data.data),
  rejectWeigh: (id) => api.post(`/seller/pickups/${id}/reject`).then(r => r.data),
  confirmPayment: (id) => api.post(`/seller/pickups/${id}/confirm-payment`).then(r => r.data),

  // ── Upload ảnh (multipart/form-data) ──
  uploadImage: (file) => {
    const formData = new FormData();
    formData.append('file', file);
    return api.post('/seller/pickups/upload', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    }).then(r => r.data.data.url);
  },

  // ── Depots ──
  getNearbyDepots: (lat, lng) =>
    api.get('/seller/pickups/depots', { params: { lat, lng } }).then(r => r.data.data),

  // ── Review ──
  reviewDepot: (id, data) => api.post(`/seller/pickups/${id}/review`, data).then(r => r.data),

  // ── Profile ──
  getProfile: () => api.get('/seller/profile').then(r => r.data.data),
  updateProfile: (data) => api.put('/seller/profile', data).then(r => r.data),

  // ── Lô thanh lý / đấu giá trọn lô ──
  getLiquidations: (params) => api.get('/seller/liquidations', { params }).then(r => r.data.data),
  getLiquidation: (id) => api.get(`/seller/liquidations/${id}`).then(r => r.data.data),
  getLiquidationBids: (id, page = 1) => api.get(`/seller/liquidations/${id}/bids`, { params: { page } }).then(r => r.data.data),
  createLiquidation: (data) => api.post('/seller/liquidations', data).then(r => r.data.data),
  cancelLiquidation: (id, reason) => api.post(`/seller/liquidations/${id}/cancel`, { reason }).then(r => r.data),
  confirmLiquidationPayment: (agreementId) => api.patch(`/seller/liquidations/agreements/${agreementId}/confirm-payment`).then(r => r.data.data),
  confirmLiquidationHandover: (agreementId) => api.patch(`/seller/liquidations/agreements/${agreementId}/confirm-handover`).then(r => r.data.data),
  signLiquidation: (agreementId) => api.patch(`/seller/liquidations/agreements/${agreementId}/sign`).then(r => r.data.data),
  confirmLiquidationDeposit: (agreementId) => api.patch(`/seller/liquidations/agreements/${agreementId}/confirm-deposit`).then(r => r.data.data),

  // ── Notifications ──
  getNotifications: (params) => api.get('/seller/notifications', { params }).then(r => r.data.data),
  getUnreadCount: () => api.get('/seller/notifications/unread-count').then(r => r.data.data),
  markRead: (id) => api.patch(`/seller/notifications/${id}/read`).then(r => r.data),
  markUnread: (id) => api.patch(`/seller/notifications/${id}/unread`).then(r => r.data),
  markAllRead: () => api.patch('/seller/notifications/read-all').then(r => r.data),

  // ── AI Computer Vision ──
  classifyAiImage: (file, upload = false) => {
    const formData = new FormData();
    formData.append('file', file);
    return api.post(`/seller/ai/classify?upload=${upload}`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    }).then(r => r.data.data);
  },
  classifyAiBase64: (imageBase64, uploadToCloud = false) => {
    return api.post('/seller/ai/classify-base64', {
      image_base64: imageBase64,
      upload_to_cloud: uploadToCloud,
    }).then(r => r.data.data);
  },
  getAiStatus: () => api.get('/seller/ai/status').then(r => r.data.data),
  getAiClasses: () => api.get('/seller/ai/classes').then(r => r.data.data),
};

