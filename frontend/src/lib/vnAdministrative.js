// Danh mục đơn vị hành chính Việt Nam (cấu trúc 2 cấp sau 01/07/2025: Tỉnh/Thành phố → Phường/Xã).
// Nguồn công khai, không cần khóa API: https://provinces.open-api.vn (bản /api/v2).
// Dùng fetch thuần, KHÔNG dùng `src/lib/axios` để không gửi JWT của hệ thống sang máy chủ bên thứ ba.
const BASE = 'https://provinces.open-api.vn/api/v2';

async function getJson(url, signal) {
  const res = await fetch(url, { signal, headers: { Accept: 'application/json' } });
  if (!res.ok) throw new Error(`Không tải được danh mục địa chỉ (HTTP ${res.status}).`);
  return res.json();
}

const byName = (a, b) => a.name.localeCompare(b.name, 'vi');

/** Toàn bộ tỉnh/thành phố: [{ code, name }] sắp theo tên. */
export async function fetchProvinces(signal) {
  const rows = await getJson(`${BASE}/`, signal);
  return rows.map(({ code, name }) => ({ code, name })).sort(byName);
}

/** Phường/xã của một tỉnh/thành: [{ code, name }] sắp theo tên. */
export async function fetchWards(provinceCode, signal) {
  const province = await getJson(`${BASE}/p/${encodeURIComponent(provinceCode)}?depth=2`, signal);
  return (province.wards ?? []).map(({ code, name }) => ({ code, name })).sort(byName);
}

/** Chuẩn hóa để tìm kiếm không phân biệt dấu/hoa thường ("da nang" khớp "Đà Nẵng"). */
export const normalizeText = (text) => String(text ?? '')
  .normalize('NFD').replace(/\p{M}/gu, '')
  .replace(/đ/g, 'd').replace(/Đ/g, 'D')
  .toLowerCase().trim();
