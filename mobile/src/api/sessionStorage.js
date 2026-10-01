import * as SecureStore from "expo-secure-store";

export const TOKEN_KEY = "jwt_token";
const ROLE_KEY = "staff_role";
const LEGACY_KEY = "retrack.staff.session";
let pending = Promise.resolve();
function queue(operation) {
  pending = pending.catch(() => {}).then(operation);
  return pending;
}
export const readToken = () => SecureStore.getItemAsync(TOKEN_KEY);
export async function readSession() {
  // Migrate the previous build without keeping two independent login sessions.
  return queue(async () => {
    let token = await readToken();
    let role = await SecureStore.getItemAsync(ROLE_KEY);
    if (!token) {
      const old = await SecureStore.getItemAsync(LEGACY_KEY);
      if (old) {
        let session;
        try {
          session = JSON.parse(old);
        } catch {
          await SecureStore.deleteItemAsync(LEGACY_KEY);
          return { token: null, role: null };
        }
        if (
          typeof session?.token === "string" &&
          ["DEPOT_EMPLOYEE", "DRIVER"].includes(session.role)
        ) {
          token = session.token;
          role = session.role;
          await SecureStore.setItemAsync(ROLE_KEY, role);
          await SecureStore.setItemAsync(TOKEN_KEY, token);
        }
        await SecureStore.deleteItemAsync(LEGACY_KEY);
      }
    }
    return { token, role };
  });
}
export function saveSession(token, role, isCurrent = () => true) {
  return queue(async () => {
    if (!isCurrent()) return;
    try {
      await SecureStore.setItemAsync(ROLE_KEY, role);
      await SecureStore.setItemAsync(TOKEN_KEY, token);
      await SecureStore.deleteItemAsync(LEGACY_KEY);
    } catch (error) {
      await SecureStore.deleteItemAsync(TOKEN_KEY);
      throw error;
    }
  });
}
export function clearSession() {
  return queue(async () => {
    // Attempt every deletion even when one storage operation fails.
    const results = await Promise.allSettled(
      [TOKEN_KEY, ROLE_KEY, LEGACY_KEY].map((key) =>
        SecureStore.deleteItemAsync(key),
      ),
    );
    const failure = results.find((result) => result.status === "rejected");
    if (failure?.status === "rejected") throw failure.reason;
  });
}
