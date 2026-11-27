// Nhãn và điều kiện hoàn tiền gói dùng chung cho Chủ kho và Admin. Server vẫn là nơi quyết định cuối cùng.
export const REFUND_KINDS = {
  UNUSED_PERIOD: 'Kỳ gia hạn chưa bắt đầu',
  DUPLICATE_OR_EXCESS: 'Chuyển trùng / chuyển thừa',
  SERVICE_ERROR: 'Lỗi dịch vụ đã xác minh',
  SHORT_PAYMENT: 'Chuyển thiếu (hoàn khoản đã nhận)',
  LATE_PAYMENT: 'Tiền về sau khi đơn đã đóng',
};
export const REFUND_STATUS = {
  REQUESTED: 'Chờ Admin xem xét',
  APPROVED: 'Đã duyệt – chờ chuyển tiền',
  REJECTED: 'Từ chối',
  REFUNDED: 'Đã chuyển tiền hoàn',
};
export const REFUND_TONE = { REQUESTED: 'amber', APPROVED: 'blue', REJECTED: 'red', REFUNDED: 'primary' };
export const REFUND_HINTS = {
  UNUSED_PERIOD: 'Chỉ hoàn kỳ chưa bắt đầu và là kỳ gia hạn cuối cùng; hoàn đủ giá đơn, kỳ này bị rút khỏi hạn dùng. Kỳ đang sử dụng được dùng đến hết hạn.',
  DUPLICATE_OR_EXCESS: 'Cần chứng từ hoặc mã giao dịch ngân hàng của khoản chuyển trùng/thừa.',
  SERVICE_ERROR: 'Admin xác minh lỗi và có thể hoàn một phần; tổng hoàn theo giá gói không vượt số tiền của đơn.',
  SHORT_PAYMENT: 'Hoàn khoản đã chuyển thiếu; đơn chờ sẽ được đóng để lập đơn mới đúng giá.',
  LATE_PAYMENT: 'Tiền về sau khi đơn đã đóng được đưa vào đối soát, không tự kích hoạt gói.',
};

/** Loại hoàn tiền hợp lệ theo trạng thái đơn (khớp điều kiện server). */
export function refundKindsFor(order, now = Date.now()) {
  if (order.status === 'PAID') {
    // Kỳ đã hoàn đủ giá đơn thì chỉ còn trường hợp chuyển trùng/thừa.
    if (order.periodRevokedAt) return ['DUPLICATE_OR_EXCESS'];
    const kinds = ['DUPLICATE_OR_EXCESS', 'SERVICE_ERROR'];
    if (!order.periodRevokedAt && order.periodStart && new Date(order.periodStart).getTime() > now) kinds.unshift('UNUSED_PERIOD');
    return kinds;
  }
  if (order.status === 'CANCELLED') return ['SHORT_PAYMENT', 'LATE_PAYMENT'];
  if (order.status === 'PENDING' && order.proofUrl && !order.payOsStarted) return ['SHORT_PAYMENT'];
  return [];
}
