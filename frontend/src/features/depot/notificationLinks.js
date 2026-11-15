export const notificationGroups = {
  FACTORY: { label: 'Nhà máy', icon: 'factory' }, PAYMENT: { label: 'Thanh toán', icon: 'payments' },
  PICKUP: { label: 'Điều phối thu gom', icon: 'badge' },
  TRANSPORT: { label: 'Vận chuyển', icon: 'local_shipping' }, VERIFICATION: { label: 'Hồ sơ kho', icon: 'fact_check' },
  STOCK: { label: 'Nhập kho', icon: 'inventory_2' }, LIQUIDATION: { label: 'Lô lớn', icon: 'shopping_cart' },
  SYSTEM: { label: 'Thông báo chung', icon: 'notifications' },
};
const uuid = value => typeof value === 'string' && /^[\da-f]{8}(-[\da-f]{4}){3}-[\da-f]{12}$/i.test(value);
export function notificationTarget(notice) {
  if (notice.category === 'PICKUP') return uuid(notice.pickupId || notice.targetId)
    ? { to: `/depot/dispatch?pickupId=${notice.pickupId || notice.targetId}`, label: 'Xem đơn điều phối' }
    : { to: '/depot/dispatch', label: 'Xem điều phối nhân viên' };
  if (uuid(notice.batchId)) return { to: `/depot/batches?batchId=${notice.batchId}`, label: 'Xem lô / nhật ký xử lý' };
  if (uuid(notice.pickupId)) return { to: `/depot/payments?requestId=${notice.pickupId}`, label: 'Xem đơn thanh toán' };
  switch (notice.targetType) {
    case 'PROFILE': return { to: '/depot/profile', label: 'Xem hồ sơ kho' };
    case 'PARTNERS': return { to: '/depot/partners', label: 'Xem nhà máy đối tác' };
    case 'FEES': return { to: '/depot/payments/fees', label: 'Xem phí nền tảng' };
    case 'RECEIPT': return uuid(notice.targetId) ? { to: `/depot/inventory?tab=receipts&receiptId=${notice.targetId}`, label: 'Xem phiếu nhập kho' } : null;
    case 'LIQUIDATION': return uuid(notice.targetId) ? { to: `/depot/liquidations?lotId=${notice.targetId}`, label: 'Xem lô lớn' } : null;
    default: return null;
  }
}
export const noticeTime = value => new Date(value).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric' });
