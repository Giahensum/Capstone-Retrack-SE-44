import apiClient, { unwrap } from "./client";

export const sellerApi = {
  getRequests: async (status) => unwrap(await apiClient.get('/seller/pickups', { params: { status } })),
  getRequestById: async (id) => unwrap(await apiClient.get(`/seller/pickups/${id}`)),
  createRequest: async (data) => unwrap(await apiClient.post('/seller/pickups', data)),
  cancelRequest: async (id) => unwrap(await apiClient.post(`/seller/pickups/${id}/cancel`)),
  confirmWeigh: async (id) => unwrap(await apiClient.post(`/seller/pickups/${id}/confirm`)),
  rejectWeigh: async (id) => unwrap(await apiClient.post(`/seller/pickups/${id}/reject`)),
  confirmPayment: async (id) => unwrap(await apiClient.post(`/seller/pickups/${id}/confirm-payment`)),
  getNearbyDepots: async (lat, lng) => unwrap(await apiClient.get('/seller/pickups/depots', { params: { lat, lng } })),
  getStats: async () => unwrap(await apiClient.get('/seller/pickups/stats')),
};
