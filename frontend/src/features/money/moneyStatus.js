// Hàm thuần cho khoản thu qua sàn (PayOS): nhãn trạng thái, đường dẫn quay về, định dạng tiền.
// Không phụ thuộc React để dễ kiểm thử.

export const MONEY_FLOWS = ['PICKUP', 'BATCH', 'LIQUIDATION_DEPOSIT', 'LIQUIDATION_BALANCE'];

export const COLLECTION_STATUS_LABEL = {
  NONE: 'Chưa thanh toán',
  PENDING: 'Đang chờ PayOS xác nhận',
  PAID: 'Đã thanh toán',
  EXPIRED: 'Hết hạn',
  CANCELLED: 'Đã hủy',
  REFUNDING: 'Đang hoàn tiền',
  REFUNDED: 'Đã hoàn tiền',
};

// Tông màu cho chip: neutral | warn | ok | bad
export const COLLECTION_STATUS_TONE = {
  NONE: 'neutral', PENDING: 'warn', PAID: 'ok', EXPIRED: 'bad', CANCELLED: 'bad', REFUNDING: 'warn', REFUNDED: 'neutral',
};

export const FLOW_LABEL = {
  PICKUP: 'Thanh toán đơn thu gom',
  BATCH: 'Thanh toán lô hàng',
  LIQUIDATION_DEPOSIT: 'Đặt cọc thanh lý',
  LIQUIDATION_BALANCE: 'Thanh toán phần còn lại thanh lý',
};

export const statusLabel = (status) => COLLECTION_STATUS_LABEL[status] ?? status ?? 'Chưa thanh toán';
export const statusTone = (status) => COLLECTION_STATUS_TONE[status] ?? 'neutral';

export const formatVnd = (value) => Number(value ?? 0).toLocaleString('vi-VN') + ' đ';

/** Chỉ khi máy chủ báo PLATFORM mới dùng giao diện thanh toán mới; mọi trường hợp khác giữ giao diện cũ. */
export const isPlatformCollection = (dto) => dto?.moneyMode === 'PLATFORM';

/** Khoản thu có link PayOS đang chờ (cần theo dõi định kỳ). */
export const isAwaitingPayos = (dto) => dto?.status === 'PENDING' && dto?.orderCode != null;

export const isPaid = (dto) => dto?.status === 'PAID';

/**
 * Nhãn nút chính: null nếu người dùng không thể thanh toán.
 * Chỉ dựa trên dữ liệu máy chủ (canPay, status), không tin tham số URL.
 */
export function payButtonLabel(dto) {
  if (!dto || dto.role !== 'PAYER' || !dto.canPay) return null;
  return dto.status === 'PENDING' ? 'Tiếp tục thanh toán PayOS' : 'Thanh toán PayOS';
}

/**
 * Đường dẫn quay lại màn hình phù hợp theo luồng và vai trò.
 * lotId (nếu biết) chỉ dùng cho thanh lý.
 */
export function returnPathFor(flow, role, lotId) {
  const f = String(flow ?? '').toUpperCase();
  if (f === 'PICKUP') return role === 'DEPOT_OWNER' ? '/depot/payments' : null;
  if (f === 'LIQUIDATION_DEPOSIT' || f === 'LIQUIDATION_BALANCE') {
    if (role !== 'DEPOT_OWNER') return null;
    const q = new URLSearchParams({ view: 'PURCHASES' });
    if (lotId) q.set('lotId', lotId);
    return `/depot/liquidations?${q.toString()}`;
  }
  if (f === 'BATCH') return role === 'FACTORY' ? '/factory' : null;
  return null;
}

export const homePathFor = (role) => ({
  DEPOT_OWNER: '/depot/dashboard', FACTORY: '/factory', SELLER: '/seller', ADMIN: '/admin',
  DEPOT_EMPLOYEE: '/employee', DRIVER: '/driver',
}[role] ?? '/');

/**
 * Kết quả hiển thị trang /pay/return, suy ra từ phản hồi máy chủ.
 * cancelParam chỉ dùng để chọn lời nhắn khi máy chủ chưa ghi nhận thanh toán.
 */
export function returnOutcome(dto, cancelParam) {
  if (isPaid(dto)) return 'paid';
  if (dto?.status === 'REFUNDING' || dto?.status === 'REFUNDED') return 'refund';
  if (cancelParam) return 'cancelled';
  if (dto?.status === 'EXPIRED' || dto?.status === 'CANCELLED') return 'expired';
  return 'pending';
}
