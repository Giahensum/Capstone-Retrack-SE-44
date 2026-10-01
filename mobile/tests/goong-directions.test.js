import test, { afterEach, beforeEach } from "node:test";
import assert from "node:assert/strict";
import axios, { AxiosError, CanceledError, isCancel } from "axios";
import { getRoutes } from "../src/helpers/goongDirections.js";

const polyline = "_p~iF~ps|U_ulLnnqC_mqNvxq`@";
const validRoute = {
  legs: [{ distance: { value: 1200 }, duration: { value: 180 } }],
  overview_polyline: { points: polyline },
};
let previousAdapter;
let previousKey;
let requests;
let responseData;

beforeEach(() => {
  previousAdapter = axios.defaults.adapter;
  previousKey = process.env.EXPO_PUBLIC_GOONG_API_KEY;
  process.env.EXPO_PUBLIC_GOONG_API_KEY = "  test-goong-key  ";
  requests = [];
  responseData = { routes: [validRoute] };
  // Intercept Axios at its transport boundary: these tests never use the network.
  axios.defaults.adapter = async (config) => {
    requests.push(config);
    return { data: responseData, status: 200, statusText: "OK", headers: {}, config };
  };
});

afterEach(() => {
  axios.defaults.adapter = previousAdapter;
  if (previousKey === undefined) delete process.env.EXPO_PUBLIC_GOONG_API_KEY;
  else process.env.EXPO_PUBLIC_GOONG_API_KEY = previousKey;
});

test("requests car alternatives with numeric-route normalization and abort signal", async () => {
  const controller = new AbortController();
  const routes = await getRoutes(16.01, 108.24, 16.06836, 108.22443, {
    signal: controller.signal,
  });
  assert.equal(requests.length, 1);
  assert.equal(requests[0].url, "https://rsapi.goong.io/Direction");
  assert.deepEqual(requests[0].params, {
    origin: "16.01,108.24",
    destination: "16.06836,108.22443",
    vehicle: "car",
    alternatives: true,
    api_key: "test-goong-key",
  });
  assert.equal(requests[0].timeout, 15000);
  assert.equal(requests[0].signal, controller.signal);
  assert.equal(requests[0].headers.get("Authorization"), undefined);
  assert.equal(routes.length, 1);
  assert.equal(routes[0].distanceMeters, 1200);
  assert.equal(routes[0].durationSeconds, 180);
  assert.equal(routes[0].polylineEncoded, polyline);
  assert.equal(routes[0].coordinates.length, 3);
});

test("maps motorbike UI preference to Goong bike without a car request", async () => {
  await getRoutes(16.01, 108.24, 16.06836, 108.22443, { vehicle: "motorbike" });
  assert.equal(requests.length, 1);
  assert.equal(requests[0].params.vehicle, "bike");
  assert.equal(requests[0].params.alternatives, true);
});

test("unsupported and inherited property names cannot select a vehicle", async () => {
  for (const vehicle of ["truck", "bike", "toString", "__proto__", "", null])
    await assert.rejects(
      getRoutes(16.01, 108.24, 16.06836, 108.22443, { vehicle }),
      /Phương tiện chỉ đường không hợp lệ/,
    );
  assert.equal(requests.length, 0);
});

test("missing or placeholder keys fail before any transport request", async () => {
  for (const key of [undefined, "", "  ", "your_goong_api_key_here"]) {
    if (key === undefined) delete process.env.EXPO_PUBLIC_GOONG_API_KEY;
    else process.env.EXPO_PUBLIC_GOONG_API_KEY = key;
    await assert.rejects(
      getRoutes(16.01, 108.24, 16.06836, 108.22443),
      /Chưa cấu hình dịch vụ chỉ đường/,
    );
  }
  assert.equal(requests.length, 0);
});

test("invalid endpoints are rejected before transport while zero coordinates are valid", async () => {
  for (const coordinates of [
    [null, 108, 16, 108],
    [16, "", 16, 108],
    [91, 108, 16, 108],
    [16, 108, NaN, 108],
    [16, 108, 16, 181],
  ])
    await assert.rejects(getRoutes(...coordinates), /Chưa có tọa độ hợp lệ/);
  assert.equal(requests.length, 0);
  await getRoutes(0, 0, 1, 1);
  assert.equal(requests[0].params.origin, "0,0");
});

test("malformed first provider route is skipped in favor of a valid later route", async () => {
  responseData = {
    routes: [
      { ...validRoute, overview_polyline: { points: "_" } },
      validRoute,
    ],
  };
  const routes = await getRoutes(16.01, 108.24, 16.06836, 108.22443);
  assert.equal(routes.length, 1);
  assert.equal(routes[0].id, "1");
  assert.equal(routes[0].coordinates.length, 3);
});

test("empty or entirely invalid provider responses produce a useful no-route error", async () => {
  for (const data of [null, {}, { routes: [] }, { routes: [{ legs: [] }] }]) {
    responseData = data;
    await assert.rejects(
      getRoutes(16.01, 108.24, 16.06836, 108.22443),
      /Không tìm được tuyến đường/,
    );
  }
});

test("cancellation propagates without retrying with a different vehicle", async () => {
  const controller = new AbortController();
  axios.defaults.adapter = (config) => {
    requests.push(config);
    return new Promise((resolve, reject) => {
      config.signal.addEventListener(
        "abort",
        () => reject(new CanceledError("Test cancellation", config)),
        { once: true },
      );
    });
  };
  const request = getRoutes(16.01, 108.24, 16.06836, 108.22443, {
    vehicle: "motorbike",
    signal: controller.signal,
  });
  const rejection = assert.rejects(request, (error) => isCancel(error));
  controller.abort();
  await rejection;
  assert.equal(requests.length, 1);
  assert.equal(requests[0].params.vehicle, "bike");
});

test("a pre-aborted request never reaches the transport", async () => {
  const controller = new AbortController();
  controller.abort();
  await assert.rejects(
    getRoutes(16.01, 108.24, 16.06836, 108.22443, { signal: controller.signal }),
    (error) => isCancel(error),
  );
  assert.equal(requests.length, 0);
});

test("provider failure remains an error rather than silently returning a car route", async () => {
  axios.defaults.adapter = async (config) => {
    requests.push(config);
    throw new AxiosError("Service unavailable", "ERR_NETWORK", config);
  };
  await assert.rejects(
    getRoutes(16.01, 108.24, 16.06836, 108.22443, { vehicle: "motorbike" }),
    /Service unavailable/,
  );
  assert.equal(requests.length, 1);
  assert.equal(requests[0].params.vehicle, "bike");
});
