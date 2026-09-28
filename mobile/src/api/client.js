import { create, isAxiosError } from "axios";
import { readToken } from "./sessionStorage";

export const baseURL =
  process.env.EXPO_PUBLIC_API_BASE_URL?.trim() ||
  (__DEV__ ? "http://10.0.2.2:5000/api" : "https://your-api.railway.app/api");
const apiClient = create({ baseURL, timeout: 10000 });
let onUnauthorized = async (_token) => {};
export const setUnauthorizedHandler = (handler) => {
  onUnauthorized = handler;
};

apiClient.interceptors.request.use(async (config) => {
  if (!config.url?.startsWith("/auth/")) {
    const token = await readToken();
    if (token) config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});
apiClient.interceptors.response.use(
  (response) => response,
  async (error) => {
    const authorization = error.config?.headers?.Authorization;
    if (
      error.response?.status === 401 &&
      authorization?.startsWith("Bearer ")
    ) {
      // Do not let an old request invalidate a newly signed-in account.
      await onUnauthorized(authorization.slice(7)).catch(() => {});
    }
    return Promise.reject(error);
  },
);
export function unwrap(response) {
  if (!response.data?.success || response.data.data == null) {
    throw new Error(response.data?.message || "Phản hồi máy chủ không hợp lệ.");
  }
  return response.data.data;
}
export function errorMessage(error) {
  if (isAxiosError(error)) {
    if (!error.response)
      return "Không kết nối được máy chủ. Kiểm tra mạng và thử lại.";
    if (error.response.status === 401)
      return "Thông tin đăng nhập không đúng hoặc phiên đã hết hạn. Vui lòng đăng nhập lại.";
    const body = error.response.data;
    return (
      body?.message ||
      Object.values(body?.errors || {})
        .flat()
        .join("\n") ||
      "Không thể thực hiện yêu cầu. Vui lòng thử lại."
    );
  }
  return error instanceof Error
    ? error.message
    : "Đã xảy ra lỗi. Vui lòng thử lại.";
}
export default apiClient;
