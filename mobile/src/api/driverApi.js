import client, { unwrap } from "./client";
export const driverApi = {
  list: async (mine, page, signal) => unwrap(await client.get(mine ? "/driver/jobs/mine" : "/driver/job-pool", { params: { page }, signal })),
  detail: async (id, signal) => unwrap(await client.get(`/driver/job/${encodeURIComponent(id)}`, { signal })),
  accept: async id => unwrap(await client.post(`/driver/job/${encodeURIComponent(id)}/accept`)),
  delivery: async (id, signal) => unwrap(await client.get(`/driver/job/${encodeURIComponent(id)}/delivery`, { signal })),
  action: async (id, action, operationId, reason) => unwrap(await client.post(`/driver/job/${encodeURIComponent(id)}/${action}`, { operationId, reason })),
  evidence: async (id, action, operationId, photo, location) => {
    const body = new FormData();
    body.append("operationId", operationId);
    body.append("imageFile", /** @type {any} */ ({ uri: photo.uri, name: "driver.jpg", type: "image/jpeg" }));
    body.append("photoTakenAt", photo.takenAt);
    for (const [key, value] of Object.entries(location)) body.append(key, String(value));
    return unwrap(await client.post(`/driver/job/${encodeURIComponent(id)}/${action}`, body, {
      headers: { "Content-Type": "multipart/form-data" }, timeout: 60000,
    }));
  },
  notices: async (page, signal) => unwrap(await client.get("/driver/notifications", { params: { page }, signal })),
  read: async id => unwrap(await client.put(`/driver/notifications/${encodeURIComponent(id)}/read`)),
};
