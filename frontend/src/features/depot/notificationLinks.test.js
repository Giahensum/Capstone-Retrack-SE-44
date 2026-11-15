import { test } from 'node:test';
import assert from 'node:assert/strict';
import { notificationTarget } from './notificationLinks.js';
const id = '00000000-0000-0000-0000-000000000001';
test('đích nội bộ theo kiểu, giữ ưu tiên lô và đơn của thông báo cũ', () => {
  assert.equal(notificationTarget({ batchId: id }).to, `/depot/batches?batchId=${id}`);
  assert.equal(notificationTarget({ pickupId: id }).to, `/depot/payments?requestId=${id}`);
  assert.equal(notificationTarget({ targetType: 'PROFILE' }).to, '/depot/profile');
  assert.equal(notificationTarget({ targetType: 'RECEIPT', targetId: id }).to, `/depot/inventory?tab=receipts&receiptId=${id}`);
  assert.equal(notificationTarget({ targetType: 'LIQUIDATION', targetId: id }).to, `/depot/liquidations?lotId=${id}`);
});
test('không mở URL tùy ý hoặc identifier chèn đường dẫn', () => {
  assert.equal(notificationTarget({ targetType: 'https://example.com' }), null);
  assert.equal(notificationTarget({ targetType: 'LIQUIDATION', targetId: '../admin' }), null);
  assert.equal(notificationTarget({ batchId: 'javascript:alert(1)' }), null);
});
test('thông báo điều phối đi tới đơn chờ, thông báo thanh toán giữ màn thanh toán', () => {
  assert.equal(notificationTarget({ category: 'PICKUP', pickupId: id }).to, `/depot/dispatch?pickupId=${id}`);
  assert.equal(notificationTarget({ category: 'PICKUP' }).to, '/depot/dispatch');
  assert.equal(notificationTarget({ category: 'PAYMENT', pickupId: id }).to, `/depot/payments?requestId=${id}`);
});
