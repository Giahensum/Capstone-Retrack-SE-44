import { useAuthStore } from '@/app/store/useAuthStore';
export function useAuth() {
    const { token, user, isAuthenticated, login, logout } = useAuthStore();
    return {
        token,
        user,
        isAuthenticated,
        login,
        logout,
        role: user?.role ?? null,
    };
}

