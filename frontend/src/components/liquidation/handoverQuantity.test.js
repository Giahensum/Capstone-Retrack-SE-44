import test from 'node:test';
import assert from 'node:assert/strict';
import { handoverAmount } from './handoverQuantity.js';

test('thành tiền bàn giao làm tròn nửa đồng lên, không lệch vì số thực JS', () => {
  assert.equal(handoverAmount('0.145', 100), 15);
  assert.equal(handoverAmount('0.105', 100), 11);
  assert.equal(handoverAmount('95', 3000), 285000);
  assert.equal(handoverAmount('2', 50000), 100000);
});
test('không tính xem trước với số lượng không hợp lệ', () => {
  for (const value of ['abc', '-1', '1.0001', '1000001']) assert.equal(handoverAmount(value, 3000), 0);
});
