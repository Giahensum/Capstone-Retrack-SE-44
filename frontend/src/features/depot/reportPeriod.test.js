import test from 'node:test';
import assert from 'node:assert/strict';
import { vietnamMonth, monthBounds } from './reportPeriod.js';
test('biên tháng theo giờ Việt Nam, gồm tháng 2 năm nhuận', () => {
  assert.equal(vietnamMonth(new Date('2026-09-30T18:00:00Z')), '2026-10');
  assert.deepEqual(monthBounds('2024-02'), { from: '2024-02-01', to: '2024-02-29' });
  assert.deepEqual(monthBounds('2026-12'), { from: '2026-12-01', to: '2026-12-31' });
  for (const value of ['', '2026-13', '0099-01', '2026-1']) assert.equal(monthBounds(value), null);
});
