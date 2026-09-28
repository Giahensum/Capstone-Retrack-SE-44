export const isStaffRole = (role) =>
  role === "DEPOT_EMPLOYEE" || role === "DRIVER";
export const validPhone = (phone) =>
  /^(0[35789]\d{8}|\+84[35789]\d{8})$/.test(phone);
export const validEmail = (email) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);
export const roleLabel = (role) =>
  role === "DRIVER" ? "Tài xế vận chuyển" : "Nhân viên thu gom";
