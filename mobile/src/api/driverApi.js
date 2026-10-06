import client, { unwrap } from "./client";
export const driverApi = {
  list: async (mine, page, signal) => unwrap(await client.get(mine ? "/driver/jobs/mine" : "/driver/job-pool", { params: { page }, signal })),
  detail: async (id, signal) => unwrap(await client.get(`/driver/job/${encodeURIComponent(id)}`, { signal })),
  accept: async id => unwrap(await client.post(`/driver/job/${encodeURIComponent(id)}/accept`)),
  notices: async (page, signal) => unwrap(await client.get("/driver/notifications", { params: { page }, signal })),
  read: async id => unwrap(await client.put(`/driver/notifications/${encodeURIComponent(id)}/read`)),
};
