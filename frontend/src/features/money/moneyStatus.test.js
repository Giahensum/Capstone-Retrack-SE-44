// Chạy: node --test src/features/money (repo chưa dùng Vitest nên dùng bộ chạy test có sẵn của Node).
import test from 'node:test';
import assert from 'node:assert/strict';
import { isAwaitingPayos, isPlatformCollection, payButtonLabel, returnOutcome, returnPathFor, statusLabel } from './moneyStatus.js';

test('nhãn trạng thái tiếng Việt', () => {
  assert.equal(statusLabel('NONE'), 'Chưa thanh toán');
  assert.equal(statusLabel('PENDING'), 'Đang chờ PayOS xác nhận');
  assert.equal(statusLabel('PAID'), 'Đã thanh toán');
  assert.equal(statusLabel('EXPIRED'), 'Hết hạn');
  assert.equal(statusLabel('REFUNDING'), 'Đang hoàn tiền');
});

test('chỉ PLATFORM mới dùng giao diện thanh toán mới', () => {
  assert.equal(isPlatformCollection({ moneyMode: 'PLATFORM' }), true);
  assert.equal(isPlatformCollection({ moneyMode: 'LEGACY' }), false);
  assert.equal(isPlatformCollection(undefined), false);
});

test('nhãn nút thanh toán phụ thuộc dữ liệu máy chủ', () => {
  assert.equal(payButtonLabel({ role: 'PAYER', canPay: true, status: 'NONE' }), 'Thanh toán PayOS');
  assert.equal(payButtonLabel({ role: 'PAYER', canPay: true, status: 'PENDING' }), 'Tiếp tục thanh toán PayOS');
  assert.equal(payButtonLabel({ role: 'PAYER', canPay: false, status: 'PAID' }), null);
  assert.equal(payButtonLabel({ role: 'PAYEE', canPay: true, status: 'NONE' }), null);
  assert.equal(payButtonLabel(null), null);
});

test('chỉ theo dõi định kỳ khi PENDING có mã đơn', () => {
  assert.equal(isAwaitingPayos({ status: 'PENDING', orderCode: 123 }), true);
  assert.equal(isAwaitingPayos({ status: 'PENDING', orderCode: null }), false);
  assert.equal(isAwaitingPayos({ status: 'PAID', orderCode: 123 }), false);
});

test('kết quả trang /pay/return lấy từ máy chủ, tham số cancel chỉ chọn lời nhắn', () => {
  assert.equal(returnOutcome({ status: 'PAID' }, true), 'paid');
  assert.equal(returnOutcome({ status: 'PENDING' }, true), 'cancelled');
  assert.equal(returnOutcome({ status: 'PENDING' }, false), 'pending');
  assert.equal(returnOutcome({ status: 'EXPIRED' }, false), 'expired');
  assert.equal(returnOutcome({ status: 'REFUNDING' }, false), 'refund');
});

test('đường dẫn quay lại theo luồng và vai trò', () => {
  assert.equal(returnPathFor('PICKUP', 'DEPOT_OWNER'), '/depot/payments');
  assert.equal(returnPathFor('BATCH', 'FACTORY'), '/factory');
  assert.equal(returnPathFor('LIQUIDATION_DEPOSIT', 'DEPOT_OWNER'), '/depot/liquidations?view=PURCHASES');
  assert.equal(returnPathFor('LIQUIDATION_BALANCE', 'DEPOT_OWNER', 'abc'), '/depot/liquidations?view=PURCHASES&lotId=abc');
  assert.equal(returnPathFor('PICKUP', 'FACTORY'), null);
});
