import { test } from "node:test";
import assert from "node:assert/strict";
import { createStore } from "zustand/vanilla";
import { createAuthState } from "../src/store/authState.js";

const employee = {
  userId: "employee",
  fullName: "Nhân viên",
  role: "DEPOT_EMPLOYEE",
  phone: "0933333333",
};
function setup(overrides = {}) {
  let saved = null;
  const api = {
    login: async (_email, _password, isCurrent) => {
      if (isCurrent()) saved = { token: "employee-jwt", role: employee.role };
      return { token: "employee-jwt", role: employee.role };
    },
    getProfile: async () => employee,
    clearSession: async () => {
      saved = null;
    },
    readSession: async () => saved || { token: null, role: null },
    ...overrides,
  };
  return { store: createStore(createAuthState(api)), saved: () => saved };
}
test("login populates token/user/role; logout clears memory and secure session", async () => {
  const { store, saved } = setup();
  await store.getState().login("employee@retrack.vn", "password");
  assert.equal(store.getState().token, "employee-jwt");
  assert.equal(store.getState().role, "DEPOT_EMPLOYEE");
  assert.deepEqual(store.getState().user, employee);
  await store.getState().logout();
  assert.equal(saved(), null);
  assert.equal(store.getState().user, null);
  assert.equal(store.getState().token, null);
});
test("fresh install finishes loading and shows login", async () => {
  const { store } = setup();
  await store.getState().bootstrapAuth();
  assert.equal(store.getState().isLoading, false);
  assert.equal(store.getState().token, null);
});
test("driver bootstrap requests driver profile and restores driver navigation role", async () => {
  const { store } = setup({
    readSession: async () => ({ token: "driver-jwt", role: "DRIVER" }),
    getProfile: async (role) => {
      assert.equal(role, "DRIVER");
      return { userId: "driver", staffType: "DRIVER" };
    },
  });
  await store.getState().bootstrapAuth();
  assert.equal(store.getState().role, "DRIVER");
  assert.equal(store.getState().token, "driver-jwt");
});
test("profile failure after login removes the saved credentials", async () => {
  const { store, saved } = setup({
    getProfile: async () => {
      throw { response: { status: 401 } };
    },
  });
  await assert.rejects(
    store.getState().login("employee@retrack.vn", "password"),
  );
  assert.equal(saved(), null);
  assert.equal(store.getState().token, null);
});
test("offline bootstrap preserves stored session and blocks profile rendering", async () => {
  let removed = false;
  const { store } = setup({
    readSession: async () => ({ token: "saved", role: "DRIVER" }),
    getProfile: async () => {
      throw new Error("offline");
    },
    clearSession: async () => {
      removed = true;
    },
  });
  await store.getState().bootstrapAuth();
  assert.ok(store.getState().restoreError);
  assert.equal(store.getState().user, null);
  assert.equal(removed, false);
});
test("expired bootstrap clears credentials and returns to login", async () => {
  let removed = false;
  const { store } = setup({
    readSession: async () => ({ token: "saved", role: "DRIVER" }),
    getProfile: async () => {
      throw { response: { status: 401 } };
    },
    clearSession: async () => {
      removed = true;
    },
  });
  await store.getState().bootstrapAuth();
  assert.equal(store.getState().restoreError, null);
  assert.equal(store.getState().token, null);
  assert.equal(removed, true);
});
test("unsupported saved role is removed without calling a profile API", async () => {
  const { store } = setup({
    readSession: async () => ({ token: "saved", role: "ADMIN" }),
    getProfile: async () => {
      assert.fail("Unsupported role must not fetch a profile");
    },
  });
  await store.getState().bootstrapAuth();
  assert.equal(store.getState().token, null);
  assert.equal(store.getState().isLoading, false);
});
test("logout wins over an in-flight login/profile request", async () => {
  let finish;
  const { store, saved } = setup({
    getProfile: () =>
      new Promise((resolve) => {
        finish = resolve;
      }),
  });
  const pending = store.getState().login("employee@retrack.vn", "password");
  await Promise.resolve();
  await store.getState().logout();
  finish(employee);
  await pending;
  assert.equal(store.getState().token, null);
  assert.equal(saved(), null);
});
test("profile response belonging to another account is ignored", async () => {
  const { store } = setup();
  await store.getState().login("employee@retrack.vn", "password");
  store.getState().setUser({ userId: "driver", role: "DRIVER" });
  assert.equal(store.getState().user.userId, "employee");
});
