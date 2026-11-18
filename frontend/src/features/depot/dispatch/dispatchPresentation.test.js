import test from 'node:test';
import assert from 'node:assert/strict';
import { dispatchPoint, isDispatchDue } from './dispatchPresentation.js';
test('Tọa độ thiếu hoặc sai không tạo điểm giả trên bản đồ', () => {
  for (const pair of [[null,108],['',108],[undefined,0],[91,108],[16,181],['lỗi',108]]) assert.equal(dispatchPoint(...pair),null);
  assert.deepEqual(dispatchPoint('16.02','108.25'),[16.02,108.25]);
  assert.deepEqual(dispatchPoint(0,0),[0,0]);
});
test('Đơn ngày mai chỉ cho xem, phân định ngày theo giờ Việt Nam', () => {
  const now = Date.parse('2026-11-17T16:59:00Z');
  assert.equal(isDispatchDue('2026-11-17T16:59:59Z',now),true);
  assert.equal(isDispatchDue('2026-11-17T17:00:00Z',now),false);
  assert.equal(isDispatchDue(null,now),true);
  assert.equal(isDispatchDue('2026-11-16T01:00:00Z',now),true);
});
