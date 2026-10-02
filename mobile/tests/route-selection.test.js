import test from "node:test";
import assert from "node:assert/strict";
import { normalizeRoutes, selectRoute } from "../src/helpers/routeSelection.js";

const polylineEncoded = "_p~iF~ps|U_ulLnnqC_mqNvxq`@";
const leg = (distance, duration) => ({
  distance: { value: distance, text: "Do not compare this label" },
  duration: { value: duration, text: "Do not compare this label" },
});
const rawRoute = (distance, duration) => ({
  legs: [leg(distance, duration)],
  overview_polyline: { points: polylineEncoded },
});

test("balanced, shortest and fastest choose distinct routes using numeric totals", () => {
  const routes = normalizeRoutes([
    rawRoute(10000, 30 * 60),
    rawRoute(12000, 20 * 60),
    rawRoute(10500, 22 * 60),
  ]);
  assert.equal(selectRoute(routes)?.id, "2");
  assert.equal(selectRoute(routes, "balanced")?.id, "2");
  assert.equal(selectRoute(routes, "shortest")?.id, "0");
  assert.equal(selectRoute(routes, "fastest")?.id, "1");
  assert.deepEqual(routes.map((route) => route.id), ["0", "1", "2"]);
});

test("balanced includes the exact 15% boundary but excludes slower routes", () => {
  const routes = normalizeRoutes([
    rawRoute(1200, 100),
    rawRoute(1000, 115),
    rawRoute(900, 115.001),
  ]);
  assert.equal(selectRoute(routes)?.id, "1");
});

test("shortest and balanced break distance ties by time, then original order", () => {
  const routes = normalizeRoutes([
    rawRoute(1000, 110),
    rawRoute(1000, 100),
    rawRoute(1000, 100),
  ]);
  assert.equal(selectRoute(routes, "shortest")?.id, "1");
  assert.equal(selectRoute(routes, "balanced")?.id, "1");
});

test("fastest breaks time ties by distance, then original order", () => {
  const routes = normalizeRoutes([
    rawRoute(1200, 100),
    rawRoute(1100, 100),
    rawRoute(1100, 100),
  ]);
  assert.equal(selectRoute(routes, "fastest")?.id, "1");
});

test("all legs contribute to comparison, including legitimate zero waypoint legs", () => {
  const multiLegRoute = {
    legs: [leg(1000, 60), leg(4000, 240), leg(0, 0)],
    overview_polyline: { points: polylineEncoded },
  };
  const input = [multiLegRoute, rawRoute(4000, 270)];
  const snapshot = structuredClone(input);
  const routes = normalizeRoutes(input);
  assert.deepEqual(routes[0], {
    id: "0",
    distanceMeters: 5000,
    durationSeconds: 300,
    polylineEncoded,
  });
  assert.equal(selectRoute(routes, "shortest")?.id, "1");
  assert.equal(selectRoute(routes, "fastest")?.id, "1");
  assert.deepEqual(input, snapshot);
});

test("invalid responses yield no candidates and selecting an empty set returns null", () => {
  for (const value of [undefined, null, {}, "routes", []]) {
    assert.deepEqual(normalizeRoutes(value), []);
    assert.equal(selectRoute(normalizeRoutes(value)), null);
  }
  assert.equal(selectRoute(null), null);
});

test("malformed metrics and geometry do not hide a valid alternative", () => {
  const invalid = [
    null,
    {},
    { ...rawRoute(1000, 100), legs: [] },
    { ...rawRoute(1000, 100), legs: [leg(1000, 100), null] },
    rawRoute(undefined, 100),
    rawRoute(1000, undefined),
    rawRoute("1000", 100),
    rawRoute(1000, "100"),
    rawRoute(NaN, 100),
    rawRoute(1000, Infinity),
    rawRoute(-1, 100),
    rawRoute(1000, -1),
    rawRoute(0, 100),
    rawRoute(1000, 0),
    rawRoute(0, 0),
    { ...rawRoute(1000, 100), overview_polyline: null },
    { ...rawRoute(1000, 100), overview_polyline: { points: "_" } },
    { ...rawRoute(1000, 100), overview_polyline: { points: "??" } },
    {
      legs: [leg(Number.MAX_VALUE, 100), leg(Number.MAX_VALUE, 100)],
      overview_polyline: { points: polylineEncoded },
    },
  ];
  const routes = normalizeRoutes([...invalid, rawRoute(2000, 200)]);
  assert.equal(routes.length, 1);
  assert.equal(routes[0].id, String(invalid.length));
  assert.equal(selectRoute(routes)?.id, String(invalid.length));
});

test("a single valid route works in every mode without inventing alternatives", () => {
  const routes = normalizeRoutes([rawRoute(1000, 100)]);
  for (const mode of ["balanced", "shortest", "fastest"])
    assert.equal(selectRoute(routes, mode), routes[0]);
});

test("unsupported preference fails explicitly instead of silently changing policy", () => {
  assert.throws(() => selectRoute([], "traffic"), RangeError);
});
