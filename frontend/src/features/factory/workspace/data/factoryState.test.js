import test from "node:test";
import assert from "node:assert/strict";
import {
  createInitialState,
  transition,
  weigh,
  settlement,
  today,
} from "./factoryState.js";
const run = (s, type, payload) => transition(s, { type, payload });
test("complete lifecycle persists the same order, locks terminal operations and derives the correct payment", () => {
  let s = createInitialState();
  s = run(s, "ACCEPT_BATCH", { id: "LO-2001" });
  const id = s.orders[0].id;
  assert.throws(() => run(s, "ACCEPT_BATCH", { id: "LO-2001" }));
  assert.throws(() => run(s, "RECEIVE", { id }));
  s = run(s, "SIMULATE_TRANSPORT", { id });
  s = run(s, "SIMULATE_TRANSPORT", { id });
  s = run(s, "RECEIVE", { id });
  assert.throws(() => run(s, "SETTLE", { id, price: 10000, reference: "X" }));
  s = run(s, "WEIGH", { id, gross: 18000, tare: 6000 });
  s = run(s, "QC", {
    id,
    purity: 98,
    moisture: 1,
    contamination: 1,
    grade: "A",
    decision: "accept",
  });
  assert.throws(() => run(s, "WEIGH", { id, gross: 19000, tare: 6000 }));
  s = run(s, "SETTLE", { id, price: 10000, reference: "REF-1" });
  assert.deepEqual(s.orders[0].payment.payable, 118800000);
  assert.equal(s.orders[0].payment.fee, 1200000);
  assert.throws(() => run(s, "SETTLE", { id, price: 10000, reference: "X" }));
  s = run(s, "RATE", {
    id,
    stars: 4,
    partnership: "APPROVED",
    comment: "Đúng hẹn",
  });
  assert.throws(() =>
    run(s, "RATE", { id, stars: 5, partnership: "APPROVED" }),
  );
  const restored = JSON.parse(JSON.stringify(s));
  assert.equal(restored.orders[0].rating.stars, 4);
  assert.equal(restored.orders[0].history.at(-1).status, "PAID");
});
test("weighbridge validates readings, supports tare zero, and flags only above 5%", () => {
  assert.equal(weigh(950, 0, 1000).flagged, false);
  assert.equal(weigh(949, 0, 1000).flagged, true);
  assert.equal(weigh(1051, 0, 1000).flagged, true);
  for (const args of [
    [5, 10, 10],
    [0, 0, 10],
    [10, -1, 10],
    [10, 0, 0],
    ["bad", 0, 10],
  ])
    assert.throws(() => weigh(...args));
  let s = run(createInitialState(), "RECEIVE", { id: "DH-1001" });
  assert.throws(() =>
    run(s, "WEIGH", { id: "DH-1001", gross: 10000, tare: 5000 }),
  );
  s = run(s, "WEIGH", {
    id: "DH-1001",
    gross: 10000,
    tare: 5000,
    note: "Lệch lớn, đã lập biên bản",
  });
  assert.equal(s.orders[0].weight.flagged, true);
});
test("QC rejects missing and invalid quality data; rejected lots cannot settle", () => {
  let s = run(createInitialState(), "RECEIVE", { id: "DH-1001" });
  s = run(s, "WEIGH", { id: "DH-1001", gross: 14200, tare: 6000 });
  const qc = {
    id: "DH-1001",
    purity: 90,
    moisture: 5,
    contamination: 5,
    grade: "C",
    decision: "reject",
    note: "Lẫn tạp chất",
    resolution: "RETURN",
  };
  assert.throws(() => run(s, "QC", { ...qc, purity: "" }));
  assert.throws(() => run(s, "QC", { ...qc, moisture: 101 }));
  assert.throws(() => run(s, "QC", { ...qc, note: "" }));
  s = run(s, "QC", qc);
  assert.equal(s.orders[0].status, "REJECTED");
  assert.throws(() =>
    run(s, "SETTLE", { id: "DH-1001", price: 10000, reference: "X" }),
  );
});
test("demand edits validate dates, quantities and price range without mutating input", () => {
  const s = createInitialState();
  const d = {
    material: "PET",
    kg: 100,
    minPrice: 1,
    maxPrice: 2,
    deadline: today(),
  };
  for (const change of [
    { kg: -1 },
    { minPrice: 3 },
    { deadline: "2020-01-01" },
    { maxPrice: "bad" },
  ])
    assert.throws(() => run(s, "SAVE_DEMAND", { ...d, ...change }));
  let next = run(s, "SAVE_DEMAND", d);
  assert.equal(s.demands.length, 1);
  const id = next.demands[0].id;
  next = run(next, "SAVE_DEMAND", { ...d, id, kg: 200 });
  assert.equal(next.demands[0].kg, 200);
  assert.equal(next.demands.length, 2);
  next = run(next, "DELETE_DEMAND", { id });
  assert.equal(next.demands.length, 1);
});
test("blocked partners cannot send new batches and direct approval establishes partnership", () => {
  const s = createInitialState();
  const blocked = run(s, "PARTNER_STATUS", { id: "D1", status: "BLOCKED" });
  assert.throws(() => run(blocked, "ACCEPT_BATCH", { id: "LO-2001" }));
  const approved = run(s, "ACCEPT_BATCH", { id: "LO-2003" });
  assert.equal(approved.depots.find((d) => d.id === "D2").status, "APPROVED");
  assert.equal(s.depots.find((d) => d.id === "D2").status, "PENDING");
});
test("profile validation and attachments survive serialization; payment rounding remains consistent", () => {
  const s = createInitialState();
  assert.throws(() => run(s, "PROFILE", { ...s.profile, latitude: 91 }));
  assert.throws(() => run(s, "PROFILE", { ...s.profile, materials: [] }));
  assert.throws(() =>
    run(s, "PROFILE", { ...s.profile, primaryMaterial: "COPPER" }),
  );
  const file = {
    name: "invoice.pdf",
    data: "data:application/pdf;base64,VEVTVA==",
    type: "application/pdf",
  };
  const next = run(s, "INVOICE", { id: "DH-1003", file });
  assert.equal(
    JSON.parse(JSON.stringify(next)).orders[2].invoice.name,
    file.name,
  );
  const amount = settlement(2.25, 999);
  assert.equal(amount.total, amount.fee + amount.payable);
});
