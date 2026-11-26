export const paymentMethods = { BANK_TRANSFER: 'Chuyển khoản', CASH: 'Tiền mặt', OTHER: 'Khác theo thỏa thuận' };
export const paymentMethodLabel = method => paymentMethods[method ?? 'BANK_TRANSFER'] ?? 'Chưa ghi nhận';
