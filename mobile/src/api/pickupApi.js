import apiClient, { unwrap } from "./client";

export const pickupApi = {
  getHistory: async (page = 1, status = undefined, signal = undefined) =>
    unwrap(await apiClient.get("/employee/pickup-history", { params: { page, pageSize: 20, status }, signal })),
  getStats: async (signal = undefined) => unwrap(await apiClient.get("/employee/stats", { signal })),
  getNotifications: async (page = 1, signal = undefined) =>
    unwrap(await apiClient.get("/employee/notifications", { params: { page, pageSize: 20 }, signal })),
  readNotification: async (id, signal = undefined) => unwrap(await apiClient.patch(`/employee/notifications/${encodeURIComponent(id)}/read`, undefined, { signal })),
  transition: async (id, action, expectedRevision) =>
    unwrap(await apiClient.post(`/employee/pickup/${encodeURIComponent(id)}/${action}`, { expectedRevision })),
  getActive: async (page = 1, signal = undefined) =>
    unwrap(await apiClient.get("/employee/pickups/active", { params: { page }, signal })),
  getCollection: async (id, signal = undefined) =>
    unwrap(await apiClient.get(`/employee/pickup/${encodeURIComponent(id)}/classification`, { signal })),
  getMaterials: async (signal = undefined) =>
    unwrap(await apiClient.get("/employee/material-prices", { signal })),
  saveClassification: async (id, expectedRevision, items) =>
    unwrap(await apiClient.put(`/employee/pickup/${encodeURIComponent(id)}/classification`, { expectedRevision, items })),
  checkIn: async (id, photo, location) => {
    const body = new FormData();
    // React Native nhận file URI; boundary được Axios/native transport tự tạo.
    body.append("imageFile", /** @type {any} */ ({ uri: photo.uri, name: "checkin.jpg", type: "image/jpeg" }));
    body.append("photoTakenAt", photo.takenAt);
    for (const [key, value] of Object.entries(location)) body.append(key, String(value));
    return unwrap(await apiClient.post(`/employee/pickup/${encodeURIComponent(id)}/checkin`, body, {
      headers: { "Content-Type": "multipart/form-data" }, timeout: 60000,
    }));
  },
  getDashboard: async (signal = undefined) =>
    unwrap(await apiClient.get("/employee/dashboard", { signal })),
  getPickupPool: async (signal = undefined) =>
    unwrap(await apiClient.get("/employee/pickup-pool", { signal })),
  getPickup: async (id, signal = undefined) =>
    unwrap(
      await apiClient.get(`/employee/pickup/${encodeURIComponent(id)}`, {
        signal,
      }),
    ),
  acceptPickup: async (id) =>
    unwrap(
      await apiClient.post(`/employee/pickup/${encodeURIComponent(id)}/accept`),
    ),
};
