import apiClient, { unwrap } from "./client";

// Prepared for UC-59. The placeholder screen deliberately does not call these yet.
export const getNotifications = async () =>
  unwrap(await apiClient.get("/notifications"));
export const markRead = async (id) =>
  unwrap(
    await apiClient.patch(`/notifications/${encodeURIComponent(id)}/read`),
  );
