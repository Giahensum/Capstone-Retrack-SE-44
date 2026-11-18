// Chỉ dùng dữ liệu vị trí đã có, không biến giá trị thiếu thành tọa độ 0,0.
export function dispatchPoint(latitude, longitude) {
  if (latitude == null || longitude == null || latitude === '' || longitude === '') return null;
  const lat = Number(latitude), lng = Number(longitude);
  return Number.isFinite(lat) && Number.isFinite(lng) && Math.abs(lat) <= 90 && Math.abs(lng) <= 180 ? [lat, lng] : null;
}
export function isDispatchDue(preferredDatetime, now = Date.now()) {
  if (!preferredDatetime) return true;
  const day = new Date(now + 7 * 3600000).toISOString().slice(0, 10);
  return new Date(preferredDatetime).getTime() < new Date(`${day}T00:00:00+07:00`).getTime() + 86400000;
}
export function dispatchReason(value) {
  if (value === 'ASSIGNED') return 'Hệ thống đã phân công';
  if (value === 'ASSIGNED_MANUAL') return 'Chủ kho đã phân công';
  return value || 'Đang chờ hệ thống xét nhân viên phù hợp';
}
