import test from 'node:test';
import assert from 'node:assert/strict';
import { profileProblems, staffProblems, shiftProblems, settlementProblems } from './depotFormRules.js';
const data = values => { const result = new FormData(); Object.entries(values).forEach(([key, value]) => result.set(key, value)); return result; };

test('Tọa độ: được bỏ cả hai, giữ 0 hợp lệ và chỉ lỗi ô còn thiếu', () => {
  assert.deepEqual(profileProblems(data({})), {});
  assert.deepEqual(profileProblems(data({ latitude: '0', longitude: '0' })), {});
  assert.deepEqual(Object.keys(profileProblems(data({ latitude: '16.016' }))), ['longitude']);
  assert.deepEqual(Object.keys(profileProblems(data({ longitude: '108.25' }))), ['latitude']);
});
test('Mật khẩu: giới hạn byte UTF-8, không chỉ đếm ký tự', () => {
  assert.deepEqual(staffProblems(data({ password: 'a'.repeat(72) })), {});
  assert.ok(staffProblems(data({ password: 'a'.repeat(73) })).password);
  assert.deepEqual(staffProblems(data({ password: 'ắ'.repeat(24) })), {});
  assert.ok(staffProblems(data({ password: 'ắ'.repeat(25) })).password);
});
test('Ca: chặn cùng giờ, ngược giờ và trên 24 giờ, nhận đúng 24 giờ', () => {
  const startsAt = '2026-11-25T08:00';
  for (const endsAt of [startsAt, '2026-11-25T07:59', '2026-11-26T08:01']) assert.ok(shiftProblems(data({ startsAt, endsAt })).endsAt);
  assert.deepEqual(shiftProblems(data({ startsAt, endsAt: '2026-11-26T08:00' })), {});
  assert.deepEqual(shiftProblems(data({ startsAt: '', endsAt: '' })), {});
  assert.deepEqual(shiftProblems(data({})), {});
});
test('Hai nút quyết toán: đồng ý cần xác nhận, điều chỉnh chỉ cần lý do', () => {
  assert.ok(settlementProblems(data({ reason: 'Xin điều chỉnh' }), null, { value: 'accept' }).confirmation);
  assert.deepEqual(settlementProblems(data({ confirmation: 'on' }), null, { value: 'accept' }), {});
  assert.ok(settlementProblems(data({ confirmation: 'on', reason: '   ' }), null, { value: 'dispute' }).reason);
  assert.deepEqual(settlementProblems(data({ reason: 'Xin điều chỉnh' }), null, { value: 'dispute' }), {});
});
