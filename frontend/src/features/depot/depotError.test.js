import test from 'node:test';
import assert from 'node:assert/strict';
import { depotError } from './depotError.js';

test('lỗi hệ thống giữ mã tra cứu và ẩn thông tin kỹ thuật', () => {
  const result = depotError({ response: { status: 500, data: { message: 'private-database-detail', traceId: 'server-123' } } });
  assert.match(result, /Mã tra cứu server-123/);
  assert.doesNotMatch(result, /private/);
});

test('validation theo trường được ưu tiên và lỗi vượt tồn giữ hướng dẫn', () => {
  assert.equal(depotError({ response: { status: 400, data: { message: 'Validation error', errors: { weight: ['Khối lượng phải dương.'] } } } }), 'Khối lượng phải dương.');
  assert.equal(depotError({ response: { status: 409, data: { message: 'Tồn kho khả dụng không đủ.' } } }), 'Tồn kho khả dụng không đủ.');
});

test('mất mạng, timeout, provider, quyền và giới hạn tần suất có thông báo riêng', () => {
  assert.match(depotError({ code: 'ERR_NETWORK' }), /kiểm tra mạng/);
  assert.match(depotError({ code: 'ECONNABORTED' }), /kiểm tra kết quả/);
  assert.match(depotError({ response: { status: 502 } }), /gián đoạn/);
  assert.match(depotError({ response: { status: 401 } }), /đăng nhập lại/);
  assert.match(depotError({ response: { status: 403 } }), /không có quyền/);
  assert.match(depotError({ response: { status: 429 } }), /thao tác quá nhanh/);
});
