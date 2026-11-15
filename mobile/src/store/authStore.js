import { create } from "zustand";
import { login, getProfile } from "../api/authApi";
import { clearSession, readSession } from "../api/sessionStorage";
import { setUnauthorizedHandler } from "../api/client";
import { createAuthState } from "./authState";

const useAuthStore = create(
  createAuthState({ login, getProfile, clearSession, readSession }),
);
setUnauthorizedHandler(async (token) => {
  if (useAuthStore.getState().token === token)
    await useAuthStore.getState().logout();
});
export default useAuthStore;
