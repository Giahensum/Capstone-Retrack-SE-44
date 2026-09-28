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
};
