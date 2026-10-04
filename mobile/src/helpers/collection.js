export function parseWeight(text) {
  const value = String(text).trim().replace(",", ".");
  return /^\d+(\.\d{1,2})?$/.test(value) ? Number(value) : NaN;
}
export function parsePrice(text) {
  return /^\d+$/.test(String(text).trim()) ? Number(text) : NaN;
}
export function itemTotal(weight, price) {
  return Math.floor((Math.round(weight * 100) * price + 50) / 100);
}
export function validateRows(rows) {
  const seen = new Set();
  const items = rows.map((row, index) => {
    if (!row.materialType || seen.has(row.materialType)) throw new Error(`Dòng ${index + 1}: hãy chọn loại phế liệu không trùng.`);
    seen.add(row.materialType);
    const weightKg = parseWeight(row.weight);
    const pricePerKg = parsePrice(row.price);
    if (!Number.isFinite(weightKg) || weightKg < 0.01 || weightKg > 10000)
      throw new Error(`Dòng ${index + 1}: khối lượng từ 0,01 đến 10.000 kg, tối đa 2 số lẻ.`);
    if (!Number.isFinite(pricePerKg) || pricePerKg < 1 || pricePerKg > 10000000)
      throw new Error(`Dòng ${index + 1}: đơn giá từ 1 đến 10.000.000 đồng/kg, nhập số nguyên không có dấu phân cách.`);
    return { materialType: row.materialType, weightKg, pricePerKg };
  });
  if (items.reduce((sum, i) => sum + i.weightKg, 0) > 10000) throw new Error("Tổng khối lượng không được vượt 10.000 kg.");
  return items;
}
export const money = (value) => `${Number(value).toLocaleString("vi-VN")} đ`;
export const pickupStatus = (status) => ({
  PENDING: "Chờ nhận", SCHEDULED: "Đã nhận · Chưa check-in", IN_PROGRESS: "Đang phân loại và cân",
  WEIGHED: "Chờ người bán xác nhận", SELLER_CONFIRMED: "Người bán đã xác nhận",
  AWAITING_PAYMENT: "Chờ thanh toán", PAYMENT_SENT: "Đã chuyển tiền", DONE: "Hoàn tất", CANCELLED: "Đã hủy",
})[status] || status;

export function checkInLocationError(location, pickup, now = Date.now()) {
  if (!location) return "Hãy lấy vị trí GPS trước khi xác nhận.";
  const policy = pickup.policy;
  if (location.isMocked) return "Không chấp nhận GPS giả lập để check-in.";
  if (!Number.isFinite(location.accuracyMeters) || location.accuracyMeters < 0 || location.accuracyMeters > policy.maxAccuracyMeters)
    return `GPS cần sai số không quá ${policy.maxAccuracyMeters} m. Hãy ra nơi thoáng và cập nhật lại.`;
  const age = now - Date.parse(location.locationRecordedAt);
  if (!Number.isFinite(age) || age < -30000 || age > policy.maxLocationAgeSeconds * 1000) return "Vị trí đã cũ. Hãy cập nhật lại GPS.";
  const distance = distanceToSeller(location, pickup);
  if (distance == null) return "Đơn hoặc thiết bị chưa có tọa độ hợp lệ. Liên hệ người bán để kiểm tra địa chỉ.";
  return distance > policy.radiusMeters ? `Bạn cách địa điểm khoảng ${Math.round(distance)} m; cần trong bán kính ${policy.radiusMeters} m.` : "";
}
export function distanceToSeller(location, pickup) {
  const values = [location?.latitude, location?.longitude, pickup?.latitude, pickup?.longitude];
  if (values.some(v => v == null || !Number.isFinite(Number(v)))) return null;
  const [lat1, lng1, lat2, lng2] = values.map(Number);
  if (Math.abs(lat1) > 90 || Math.abs(lat2) > 90 || Math.abs(lng1) > 180 || Math.abs(lng2) > 180) return null;
  const r = Math.PI / 180;
  const a = Math.sin((lat2 - lat1) * r / 2) ** 2 + Math.cos(lat1 * r) * Math.cos(lat2 * r) * Math.sin((lng2 - lng1) * r / 2) ** 2;
  return 6371000 * 2 * Math.asin(Math.sqrt(Math.min(1, Math.max(0, a))));
}
