import apiClient, { baseURL, unwrap } from "./client";
import { saveSession } from "./sessionStorage";
import { isStaffRole } from "../helpers/validation";

function profilePath(role) {
  if (!isStaffRole(role))
    throw new Error("Ứng dụng này chỉ dành cho nhân viên kho và tài xế.");
  return `/${role === "DRIVER" ? "driver" : "employee"}/profile`;
}
export async function login(email, password, isCurrent = () => true) {
  if (
    !__DEV__ &&
    (!baseURL.startsWith("https://") ||
      baseURL.includes("your-api.railway.app"))
  ) {
    throw new Error("Chưa cấu hình địa chỉ API HTTPS cho bản phát hành.");
  }
  const result = unwrap(
    await apiClient.post("/auth/login", { email: email.trim(), password }),
  );
  if (!isStaffRole(result.role))
    throw new Error(
      "Ứng dụng này chỉ dành cho nhân viên kho và tài xế. Vui lòng dùng website cho tài khoản này.",
    );
  if (typeof result.token !== "string" || !result.token)
    throw new Error("Máy chủ không trả về phiên đăng nhập hợp lệ.");
  await saveSession(result.token, result.role, isCurrent);
  return result;
}
export async function getProfile(role) {
  const data = unwrap(await apiClient.get(profilePath(role)));
  // The specification allows staffType without a separate role field.
  const actualRole = data.role || data.staffType;
  if (actualRole !== role || !data.isActive)
    throw Object.assign(
      new Error("Tài khoản hoặc liên kết kho không hoạt động."),
      { response: { status: 401 } },
    );
  return { ...data, role: actualRole };
}
export const updateProfile = async (role, phone) =>
  unwrap(await apiClient.put(profilePath(role), { phone }));
export async function uploadAvatar(role, imageUri) {
  const form = new FormData();
  // React Native implements URI-backed FormData parts, unlike browser FormData.
  /** @type {any} */
  const image = { uri: imageUri, name: "avatar.jpg", type: "image/jpeg" };
  form.append("file", image);
  return unwrap(
    await apiClient.post(`${profilePath(role)}/avatar`, form, {
      headers: { "Content-Type": "multipart/form-data" },
      timeout: 45000,
    }),
  );
}
