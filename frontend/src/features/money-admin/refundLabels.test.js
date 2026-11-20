import test from 'node:test';
import assert from 'node:assert/strict';
import {
  anomalyKindMeta, anomalyStatusMeta, refundStatusMeta, refundKindLabel, refundActions, anomalyActions, validateReason, validateRefundAmount,
  validateAnomalyRefundForm, validateCollectionRefundForm, buildAnomalyRefundBody, buildCollectionRefundBody, validateMarkPaid,
  isPayerBankProblem, pickCount, PENDING_BANK_KEYS, OPEN_ANOMALY_KEYS, payerRefundStatusMeta, normalizeRefundDetail, normalizeAnomaly,
} from './refundLabels.js';

test('nhãn bất thường và hoàn tiền bằng tiếng Việt, mặc định an toàn', () => {
  assert.equal(anomalyKindMeta('OVERPAID').label, 'Trả thừa');
  assert.equal(anomalyKindMeta('DUPLICATE_PAYMENT').tone, 'red');
  assert.equal(anomalyKindMeta('LẠ').label, 'LẠ');
  assert.equal(anomalyStatusMeta('OPEN').label, 'Chưa xử lý');
  assert.equal(refundStatusMeta('MANUAL_PAID').label, 'Đã hoàn tay');
  assert.equal(refundStatusMeta('UNKNOWN').tone, 'red');
  assert.equal(refundStatusMeta(null).label, '—');
  assert.equal(refundKindLabel('PARTIAL'), 'Hoàn một phần khoản thu');
});

test('refundActions: nút chỉ hiện khi hợp lệ, UNKNOWN không có Thử lại', () => {
  assert.equal(refundActions('AWAITING_APPROVAL').approve, true);
  assert.equal(refundActions('PAID').approve, false);
  assert.equal(refundActions('FAILED').retry, true);
  assert.equal(refundActions('UNKNOWN').retry, false);
  assert.equal(refundActions('UNKNOWN').reconcile, true);
  assert.equal(refundActions('PROCESSING').markPaidNeedsConfirm, true);
  assert.equal(refundActions('FAILED').markPaidNeedsConfirm, false);
  assert.equal(refundActions('PAID').markPaid, false);
  assert.equal(refundActions('CANCELLED').cancel, false);
  assert.equal(refundActions('PROCESSING').cancel, false);
  assert.equal(refundActions('PENDING').cancel, true);
});

test('anomalyActions: hoàn tiền cần OPEN, còn số được hoàn và chưa có lệnh hoàn', () => {
  assert.equal(anomalyActions({ status: 'OPEN', refundableAmount: 5000, hasRefund: false }).refund, true);
  assert.equal(anomalyActions({ status: 'OPEN', refundableAmount: 0 }).refund, false);
  assert.equal(anomalyActions({ status: 'OPEN', refundableAmount: 5000, hasRefund: true }).refund, false);
  assert.equal(anomalyActions({ status: 'RESOLVED', refundableAmount: 5000 }).refund, false);
  assert.equal(anomalyActions({ status: 'RESOLVED' }).resolve, false);
  assert.equal(anomalyActions({ status: 'OPEN' }).resolve, true);
});

test('validateReason: 5 đến 255 ký tự sau khi cắt khoảng trắng', () => {
  assert.ok(validateReason('abcd'));
  assert.ok(validateReason('  abcd  '));
  assert.equal(validateReason('abcde'), null);
  assert.equal(validateReason('a'.repeat(255)), null);
  assert.ok(validateReason('a'.repeat(256)));
  assert.match(validateReason('', 'Lý do từ chối'), /^Lý do từ chối/);
});

test('validateRefundAmount: số nguyên đồng, không vượt tối đa', () => {
  assert.equal(validateRefundAmount('1000', 5000), null);
  assert.equal(validateRefundAmount('5000', 5000), null);
  assert.ok(validateRefundAmount('5001', 5000));
  assert.ok(validateRefundAmount('0', 5000));
  assert.ok(validateRefundAmount('-3', 5000));
  assert.ok(validateRefundAmount('12.5', 5000));
  assert.ok(validateRefundAmount('', 5000));
  assert.ok(validateRefundAmount('abc', 5000));
});

test('validateAnomalyRefundForm và buildAnomalyRefundBody', () => {
  assert.deepEqual(validateAnomalyRefundForm({ amount: '3000', reason: 'Trả thừa 3000' }, 3000), {});
  assert.ok(validateAnomalyRefundForm({ amount: '3001', reason: 'Trả thừa 3000' }, 3000).amount);
  assert.ok(validateAnomalyRefundForm({ amount: '3000', reason: 'ab' }, 3000).reason);
  assert.deepEqual(buildAnomalyRefundBody({ amount: '3000', reason: '  Trả thừa  ' }), { amount: 3000, reason: 'Trả thừa' });
});

test('validateCollectionRefundForm: FULL không cần số tiền, PARTIAL phải nhỏ hơn khoản thu', () => {
  assert.deepEqual(validateCollectionRefundForm({ kind: 'FULL', amount: '', reason: 'Khách hủy đơn' }, 100000), {});
  assert.deepEqual(validateCollectionRefundForm({ kind: 'PARTIAL', amount: '40000', reason: 'Hoàn một phần' }, 100000), {});
  assert.ok(validateCollectionRefundForm({ kind: 'PARTIAL', amount: '', reason: 'Hoàn một phần' }, 100000).amount);
  assert.ok(validateCollectionRefundForm({ kind: 'PARTIAL', amount: '100000', reason: 'Hoàn một phần' }, 100000).amount);
  assert.ok(validateCollectionRefundForm({ kind: 'PARTIAL', amount: '100001', reason: 'Hoàn một phần' }, 100000).amount);
  assert.ok(validateCollectionRefundForm({ kind: 'X', amount: '', reason: 'Hoàn một phần' }, 100000).kind);
  assert.ok(validateCollectionRefundForm({ kind: 'FULL', amount: '', reason: '' }, 100000).reason);
});

test('buildCollectionRefundBody: FULL không gửi amount', () => {
  assert.deepEqual(buildCollectionRefundBody({ kind: 'FULL', amount: '999', reason: ' Hủy đơn ' }), { kind: 'FULL', reason: 'Hủy đơn' });
  assert.deepEqual(buildCollectionRefundBody({ kind: 'PARTIAL', amount: '40000', reason: 'Một phần' }), { kind: 'PARTIAL', reason: 'Một phần', amount: 40000 });
});

test('validateMarkPaid: bắt buộc mã tham chiếu, và tick xác nhận khi PROCESSING/UNKNOWN', () => {
  assert.ok(validateMarkPaid({ reference: ' ', confirm: true }, false));
  assert.equal(validateMarkPaid({ reference: 'FT123', confirm: false }, false), null);
  assert.ok(validateMarkPaid({ reference: 'FT123', confirm: false }, true));
  assert.equal(validateMarkPaid({ reference: 'FT123', confirm: true }, true), null);
});

test('isPayerBankProblem nhận ra lỗi tài khoản ngân hàng người trả', () => {
  assert.equal(isPayerBankProblem('Người trả chưa có tài khoản được duyệt.'), true);
  assert.equal(isPayerBankProblem('Tiền đã chi cho người nhận, không thể hoàn.'), false);
  assert.equal(isPayerBankProblem(undefined), false);
});

test('pickCount: chịu được số thuần, object và dữ liệu hỏng', () => {
  assert.equal(pickCount(7, PENDING_BANK_KEYS), 7);
  assert.equal(pickCount({ pendingBankCount: 3 }, PENDING_BANK_KEYS), 3);
  assert.equal(pickCount({ openAnomalyCount: 0 }, OPEN_ANOMALY_KEYS), 0);
  assert.equal(pickCount({ open: 4 }, OPEN_ANOMALY_KEYS), 4);
  assert.equal(pickCount({ x: 1 }, OPEN_ANOMALY_KEYS), null);
  assert.equal(pickCount(null, OPEN_ANOMALY_KEYS), null);
  assert.equal(pickCount({ count: null }, OPEN_ANOMALY_KEYS), null);
});

test('payerRefundStatusMeta không lộ chi tiết nội bộ', () => {
  assert.equal(payerRefundStatusMeta('PAID').label, 'Đã hoàn');
  assert.equal(payerRefundStatusMeta('MANUAL_PAID').label, 'Đã hoàn');
  assert.equal(payerRefundStatusMeta('PROCESSING').label, 'Đang hoàn tiền');
  for (const s of ['PENDING', 'AWAITING_APPROVAL', 'FAILED', 'UNKNOWN']) assert.equal(payerRefundStatusMeta(s).label, 'Đang xử lý');
  assert.equal(payerRefundStatusMeta('CANCELLED').label, 'Đã hủy');
});

test('normalizeRefundDetail và normalizeAnomaly chịu cả dạng phẳng lẫn dạng bọc', () => {
  const flat = { id: 1, code: 'R1', bank: { bankName: 'VCB' }, idempotencyKey: 'k' };
  assert.deepEqual(normalizeRefundDetail(flat), { refund: flat, bank: flat.bank, idempotencyKey: 'k' });
  const wrapped = normalizeRefundDetail({ refund: { id: 2 }, bank: { bankName: 'ACB' }, idempotencyKey: 'x' });
  assert.equal(wrapped.refund.id, 2);
  assert.equal(wrapped.bank.bankName, 'ACB');
  assert.equal(normalizeRefundDetail(null), null);
  assert.deepEqual(normalizeAnomaly({ id: 1 }), { id: 1 });
  assert.equal(normalizeAnomaly({ anomaly: { id: 9 }, payer: { name: 'A' } }).payer.name, 'A');
});
