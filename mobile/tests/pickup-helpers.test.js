import test from "node:test";
import assert from "node:assert/strict";
import { coordinatesOf, decodePolyline } from "../src/helpers/coordinates.js";
import { formatCurrency, formatViDatetime } from "../src/helpers/format.js";

test("coordinates preserve equator/prime meridian and reject missing or invalid GPS", () => {
  assert.deepEqual(coordinatesOf({ latitude: 0, longitude: 0 }), {
    latitude: 0,
    longitude: 0,
  });
  assert.deepEqual(coordinatesOf({ latitude: "10.77", longitude: "106.7" }), {
    latitude: 10.77,
    longitude: 106.7,
  });
  for (const value of [
    null,
    {},
    { latitude: null, longitude: 1 },
    { latitude: 91, longitude: 2 },
    { latitude: 10, longitude: 181 },
    { latitude: "bad", longitude: 2 },
    { latitude: "", longitude: 0 },
  ])
    assert.equal(coordinatesOf(value), null);
});

test("decodes reference polyline, including negative longitude deltas", () => {
  assert.deepEqual(decodePolyline("_p~iF~ps|U_ulLnnqC_mqNvxq`@"), [
    { latitude: 38.5, longitude: -120.2 },
    { latitude: 40.7, longitude: -120.95 },
    { latitude: 43.252, longitude: -126.453 },
  ]);
});

test("malformed/truncated polylines fail instead of looping or drawing invented routes", () => {
  for (const value of [null, "", "_", "~~~~~~~", "\u0000", "?"])
    assert.throws(() => decodePolyline(value));
});

test("formatters handle missing values and legitimate zero currency", () => {
  assert.equal(formatViDatetime(null), "Chưa hẹn lịch");
  assert.equal(formatViDatetime("bad-date"), "Chưa hẹn lịch");
  assert.equal(formatCurrency(null), "—");
  assert.match(formatCurrency(0), /0/);
  assert.match(
    formatViDatetime("2026-09-28T08:00:00"),
    /08:00 Th 2 28\/09\/2026/,
  );
});
