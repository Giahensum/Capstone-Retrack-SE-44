import { test } from 'node:test';
import assert from 'node:assert/strict';
import { previousClosedBillingMonth, isClosedBillingPeriod } from './billingPeriod.js';

test('Kỳ mặc định đổi tại đúng nửa đêm Việt Nam và đúng năm khi tháng một', () => {
  assert.deepEqual(previousClosedBillingMonth(new Date('2026-11-30T16:59:59Z')), { year: 2026, month: 10 });
  assert.deepEqual(previousClosedBillingMonth(new Date('2026-11-30T17:00:00Z')), { year: 2026, month: 11 });
  assert.deepEqual(previousClosedBillingMonth(new Date('2026-01-15T00:00:00Z')), { year: 2025, month: 12 });
});
test('Không phát hành khi kỳ chưa đóng, kể cả một mili giây trước mốc', () => {
  const period = { year: 2026, month: 11 };
  assert.equal(isClosedBillingPeriod(period, new Date('2026-11-30T16:59:59.999Z')), false);
  assert.equal(isClosedBillingPeriod(period, new Date('2026-11-30T17:00:00Z')), true);
  assert.equal(isClosedBillingPeriod({ year: 2026, month: 13 }), false);
});
