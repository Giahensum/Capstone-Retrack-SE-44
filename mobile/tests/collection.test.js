import test from "node:test";
import assert from "node:assert/strict";
import { parseWeight, parsePrice, validateRows, itemTotal, checkInLocationError, distanceToSeller } from "../src/helpers/collection.js";

test("khối lượng hỗ trợ dấu phẩy nhưng từ chối nhập thiếu/sai độ chính xác", () => {
  assert.equal(parseWeight("12,50"), 12.5);
  for (const value of ["", "1e3", "1.001", "1,000.5", "Infinity"]) assert.ok(Number.isNaN(parseWeight(value)));
  for (const value of ["1.5", "10,000", ""]) assert.ok(Number.isNaN(parsePrice(value)));
});
test("validation khối lượng, giá, dòng trùng và tổng đơn", () => {
  const row = { materialType: "PET", weight: "1,25", price: "1001" };
  assert.equal(itemTotal(...[1.25, 1001]), 1251);
  assert.equal(validateRows([row])[0].weightKg, 1.25);
  for (const bad of [{ weight: "0" }, { weight: "-1" }, { price: "0" }, { price: "10000001" }, { weight: "10001" }])
    assert.throws(() => validateRows([{ ...row, ...bad }]));
  assert.throws(() => validateRows([row, row]));
  assert.throws(() => validateRows([{ ...row, weight: "6000" }, { ...row, materialType: "IRON", weight: "6000" }]));
  assert.deepEqual(validateRows([]), []);
});
test("GPS phải mới, chính xác và nằm gần người bán", () => {
  const now = Date.now();
  const pickup = { latitude: 16.05, longitude: 108.2, policy: { radiusMeters: 200, maxAccuracyMeters: 50, maxLocationAgeSeconds: 120 } };
  const location = { latitude: 16.05, longitude: 108.2, accuracyMeters: 10, isMocked: false, locationRecordedAt: new Date(now).toISOString() };
  assert.equal(distanceToSeller(location, pickup), 0);
  assert.equal(checkInLocationError(location, pickup, now), "");
  for (const bad of [{ latitude: 16.1 }, { accuracyMeters: null }, { accuracyMeters: 51 }, { isMocked: true }, { locationRecordedAt: new Date(now - 121000).toISOString() }])
    assert.notEqual(checkInLocationError({ ...location, ...bad }, pickup, now), "");
  assert.notEqual(checkInLocationError(location, { ...pickup, latitude: null }, now), "");
});
