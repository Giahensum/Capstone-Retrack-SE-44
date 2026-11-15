// Gợi ý địa chỉ bằng Goong Place AutoComplete. Khóa lấy từ biến Vite VITE_GOONG_API_KEY (frontend/.env.local, đã ignore).
// Khóa VITE_* nằm trong mã chạy trên trình duyệt: nên giới hạn theo domain và hạn mức trong trang quản lý Goong.
const KEY = import.meta.env.VITE_GOONG_API_KEY;
const BASE = 'https://rsapi.goong.io';

export const GOONG_ENABLED = Boolean(KEY) && KEY !== 'your_goong_api_key_here';

/** Trả về [{ id, main, secondary }]; ném lỗi khi mạng/khóa/hạn mức có vấn đề. */
export async function searchPlaces(input, signal) {
  const res = await fetch(`${BASE}/Place/AutoComplete?input=${encodeURIComponent(input)}&api_key=${encodeURIComponent(KEY)}`, { signal });
  if (!res.ok) throw new Error(`Goong trả lỗi HTTP ${res.status}.`);
  const data = await res.json();
  if (data.status && data.status !== 'OK' && data.status !== 'ZERO_RESULTS') throw new Error(`Goong: ${data.status}`);
  return (data.predictions ?? []).map((p) => {
    const main = p.structured_formatting?.main_text ?? p.description;
    const secondary = p.structured_formatting?.secondary_text ?? '';
    return { id: p.place_id ?? p.description, main, secondary, line: toStreetLine(main, secondary) };
  });
}

// Với địa điểm (quán, cửa hàng…) phần phụ bắt đầu bằng số nhà/đường: ghép vào để không mất số nhà.
// Với tên đường thì phần phụ bắt đầu bằng đơn vị hành chính (Phường/Quận/...): bỏ qua, khu vực lấy từ bộ chọn Tỉnh/Phường.
const ADMIN_UNIT = /^(phường|xã|quận|huyện|thị xã|thị trấn|thành phố|tỉnh)\b/i;
function toStreetLine(main, secondary) {
  const first = secondary.split(',')[0]?.trim();
  return first && !ADMIN_UNIT.test(first) ? `${main}, ${first}` : main;
}
