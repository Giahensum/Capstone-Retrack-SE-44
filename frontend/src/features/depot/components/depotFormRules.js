// Các ràng buộc phía giao diện đối chiếu DTO/service Depot; server vẫn quyết định cuối cùng.
export const phonePattern = '[+0-9\\(\\) .\\-]*[0-9][+0-9\\(\\) .\\-]*';
export const phoneMessage = 'Nhập số điện thoại bằng chữ số; có thể dùng +, dấu cách, ngoặc hoặc dấu gạch nối.';

export function profileProblems(data) {
  const latitude = String(data.get('latitude') ?? ''), longitude = String(data.get('longitude') ?? '');
  if (!!latitude !== !!longitude) return { [latitude ? 'longitude' : 'latitude']: 'Nhập cả vĩ độ và kinh độ, hoặc để trống cả hai.' };
  return {};
}

export function staffProblems(data) {
  const password = data.get('password');
  return password && new TextEncoder().encode(password).length > 72 ? { password: 'Mật khẩu tối đa 72 byte UTF-8; ký tự tiếng Việt có thể chiếm nhiều byte.' } : {};
}

export function shiftProblems(data) {
  if (!data.get('startsAt') || !data.get('endsAt')) return {};
  const start = new Date(data.get('startsAt')).getTime(), end = new Date(data.get('endsAt')).getTime();
  if (!Number.isFinite(start) || !Number.isFinite(end)) return {};
  return end <= start ? { endsAt: 'Kết thúc ca phải sau thời điểm bắt đầu.' }
    : end - start > 24 * 60 * 60 * 1000 ? { endsAt: 'Một ca tối đa 24 giờ. Chọn lại thời điểm kết thúc.' } : {};
}

export function settlementProblems(data, _form, submitter) {
  return submitter?.value === 'accept'
    ? data.has('confirmation') ? {} : { confirmation: 'Đánh dấu xác nhận đã đối chiếu khối lượng, đơn giá và số tiền.' }
    : String(data.get('reason') || '').trim() ? {} : { reason: 'Nhập nội dung đề nghị điều chỉnh giá.' };
}
