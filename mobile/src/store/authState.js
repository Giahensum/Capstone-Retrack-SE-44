// Plain JS state creator; injected services keep session behavior testable without native modules.
export function createAuthState(api) {
  let revision = 0;
  const anonymous = {
    token: null,
    user: null,
    role: null,
    isLoading: false,
    restoreError: null,
  };
  return (set, get) => ({
    ...anonymous,
    isLoading: true,
    setAuth: (token, user) =>
      set({
        token,
        user,
        role: user.role || user.staffType,
        isLoading: false,
        restoreError: null,
      }),
    setUser: (user) => {
      if (get().user?.userId === user.userId)
        set({ user: { ...user, role: get().role } });
    },
    login: async (email, password) => {
      const current = ++revision;
      try {
        const result = await api.login(
          email,
          password,
          () => current === revision,
        );
        if (current !== revision) return;
        const user = await api.getProfile(result.role);
        if (current === revision) get().setAuth(result.token, user);
      } catch (error) {
        if (current === revision) {
          set(anonymous);
          await api.clearSession();
        }
        throw error;
      }
    },
    logout: async () => {
      ++revision;
      set(anonymous);
      await api.clearSession();
    },
    bootstrapAuth: async () => {
      const current = ++revision;
      set({ isLoading: true, restoreError: null });
      try {
        const { token, role } = await api.readSession();
        if (current !== revision) return;
        if (!token || !["DEPOT_EMPLOYEE", "DRIVER"].includes(role)) {
          await api.clearSession();
          if (current === revision) set(anonymous);
          return;
        }
        const user = await api.getProfile(role);
        if (current === revision) get().setAuth(token, user);
      } catch (error) {
        if (current !== revision) return;
        if ([401, 403].includes(error.response?.status)) {
          set(anonymous);
          await api.clearSession();
        } else {
          // Preserve the stored token when offline; never display an unverified profile.
          set({
            ...anonymous,
            restoreError:
              "Không thể xác nhận phiên đăng nhập. Kiểm tra kết nối và thử lại.",
          });
        }
      } finally {
        if (current === revision) set({ isLoading: false });
      }
    },
  });
}
