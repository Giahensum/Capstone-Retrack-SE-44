import test from "node:test";
import assert from "node:assert/strict";
import { createNotificationFeed, notificationDestination } from "../src/helpers/notificationFeed.js";

const page = (id, unreadCount = 1) => ({ page: 1, pageSize: 20, totalCount: 1, unreadCount, items: [{ id, isRead: false }] });
const deferred = () => { let resolve; const promise = new Promise(r => { resolve = r; }); return { promise, resolve }; };
test("session disposal ignores a delayed list and never contaminates the next account", async () => {
  const old = deferred();
  const first = createNotificationFeed(() => old.promise, async () => {}, String);
  const pending = first.load(); first.dispose();
  const second = createNotificationFeed(async () => page("new-user"), async () => {}, String);
  await second.load(); old.resolve(page("old-user")); await pending;
  assert.equal(first.getSnapshot().data, null);
  assert.equal(second.getSnapshot().data.items[0].id, "new-user");
});
test("newer list response wins and marks read decrement only once", async () => {
  const old = deferred(); let calls = 0;
  const feed = createNotificationFeed(() => ++calls === 1 ? old.promise : Promise.resolve(page("mine")), async () => {}, String);
  const pending = feed.load(); await feed.load(); old.resolve(page("stale")); await pending;
  assert.equal(feed.getSnapshot().data.items[0].id, "mine");
  await feed.read({ id: "mine" }); await feed.read({ id: "mine" });
  assert.equal(feed.getSnapshot().data.unreadCount, 0);
  assert.equal(feed.getSnapshot().data.items[0].isRead, true);
});
test("double tap and stale read after logout cannot navigate", async () => {
  const read = deferred(); let calls = 0;
  const feed = createNotificationFeed(async () => page("mine"), () => { calls++; return read.promise; }, String);
  await feed.load();
  const pending = feed.read({ id: "mine" });
  assert.equal(await feed.read({ id: "mine" }), false);
  feed.dispose(); read.resolve(); assert.equal(await pending, false);
  assert.equal(calls, 1);
});
test("failed mark-read keeps unread state and failed refresh hides stale data", async () => {
  let fail = false;
  const feed = createNotificationFeed(async () => { if (fail) throw new Error("offline"); return page("mine"); },
    async () => { throw new Error("forbidden"); }, e => e.message);
  await feed.load(); assert.equal(await feed.read({ id: "mine" }), false);
  assert.equal(feed.getSnapshot().data.unreadCount, 1);
  assert.equal(feed.getSnapshot().error, "forbidden");
  fail = true; await feed.load();
  assert.equal(feed.getSnapshot().data, null);
  assert.equal(feed.getSnapshot().error, "offline");
});
test("role controls destination and general notices have no invented link", () => {
  const item = { pickupRequestId: "pickup", jobId: "job" };
  assert.deepEqual(notificationDestination("DEPOT_EMPLOYEE", item), { name: "Pool", params: { screen: "PickupDetail", params: { pickupId: "pickup" } } });
  assert.deepEqual(notificationDestination("DRIVER", item), { name: "Jobs", params: { screen: "JobDetail", params: { jobId: "job" } } });
  assert.equal(notificationDestination("SELLER", item), null);
  assert.equal(notificationDestination("DRIVER", {}), null);
});
