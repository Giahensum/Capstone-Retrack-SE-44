import apiClient, { unwrap } from "./client";

export const pickupApi = {
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
