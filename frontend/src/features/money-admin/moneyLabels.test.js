import test from 'node:test';
import assert from 'node:assert/strict';
import {
  payoutStatusMeta, payoutActions, vnd, vnToday, addDays, vnStartOfDayIso, validatePeriod, validateSettings, settingsToForm,
  formToSettingsPatch, newClientKey, validateTestTransfer, maskAccount, bankStatusMeta, fmtDateTime,
} from './moneyLabels.js';

test('payoutStatusMeta: có nhãn tiếng Việt và mặc định an toàn', () => {
  assert.equal(payoutStatusMeta('PAID').label, 'Đã chi');
  assert.equal(payoutStatusMeta('UNKNOWN').tone, 'red');
  assert.equal(payoutStatusMeta('LẠ').label, 'LẠ');
  assert.equal(payoutStatusMeta(null).label, '—');
});

test('payoutActions: nút chỉ hiện khi hợp lệ', () => {
  assert.equal(payoutActions('AWAITING_APPROVAL').approve, true);
  assert.equal(payoutActions('PAID').approve, false);
  assert.equal(payoutActions('FAILED').retry, true);
  assert.equal(payoutActions('PENDING').retry, true);
  assert.equal(payoutActions('UNKNOWN').retry, false);
  assert.equal(payoutActions('UNKNOWN').reconcile, true);
  assert.equal(payoutActions('PROCESSING').markPaidNeedsConfirm, true);
  assert.equal(payoutActions('FAILED').markPaidNeedsConfirm, false);
  assert.equal(payoutActions('HELD').release, true);
  assert.equal(payoutActions('PAID').markPaid, false);
});

test('vnd: định dạng số nguyên đồng', () => {
  assert.match(vnd(1234567), /^1\.234\.567\s?đ$/);
  assert.equal(vnd(null), '—');
  assert.equal(vnd('abc'), '—');
});

test('vnToday và addDays theo giờ Việt Nam', () => {
  // 2026-01-01 18:00 UTC = 2026-01-02 01:00 giờ Việt Nam
  assert.equal(vnToday(new Date('2026-01-01T18:00:00Z')), '2026-01-02');
  assert.equal(vnToday(new Date('2026-01-01T16:59:00Z')), '2026-01-01');
  assert.equal(addDays('2026-03-01', -1), '2026-02-28');
  assert.equal(vnStartOfDayIso('2026-05-03'), '2026-05-03T00:00:00+07:00');
});

test('validatePeriod', () => {
  assert.match(validatePeriod('', '2026-01-02', '2026-01-05'), /Chọn đủ/);
  assert.match(validatePeriod('2026-01-03', '2026-01-03', '2026-01-05'), /trước/);
  assert.match(validatePeriod('2026-01-01', '2026-01-06', '2026-01-05'), /tương lai/);
  assert.equal(validatePeriod('2026-01-01', '2026-01-05', '2026-01-05'), null);
});

const server = {
  mode: 'PLATFORM', defaultCommission: { PICKUP: 5, BATCH: 3.5, LIQUIDATION: 2 }, payoutAutoEnabled: false, payoutSchedule: 'DAILY',
  payoutHoldMinutes: 1440, payoutMinAmount: 50000, payoutAutoMaxAmount: 5000000, payoutProvider: 'SIMULATED',
  payoutTestEnabled: false, payoutTestMaxAmount: 5000,
};

test('validateSettings: cấu hình hợp lệ và các giới hạn như backend', () => {
  const form = settingsToForm(server);
  assert.deepEqual(validateSettings(form), {});
  assert.ok(validateSettings({ ...form, payoutHoldMinutes: '525601' }).payoutHoldMinutes);
  assert.equal('payoutBankChangeHoldHours' in settingsToForm(server), false);
  assert.ok(validateSettings({ ...form, payoutTestMaxAmount: '999' }).payoutTestMaxAmount);
  assert.ok(validateSettings({ ...form, payoutTestMaxAmount: '100001' }).payoutTestMaxAmount);
  assert.ok(validateSettings({ ...form, payoutMinAmount: '1.5' }).payoutMinAmount);
  assert.ok(validateSettings({ ...form, payoutMinAmount: '' }).payoutMinAmount);
  assert.ok(validateSettings({ ...form, defaultCommission: { ...form.defaultCommission, BATCH: '100.5' } })['commission.BATCH']);
  assert.ok(validateSettings({ ...form, defaultCommission: { ...form.defaultCommission, PICKUP: '-1' } })['commission.PICKUP']);
});

test('formToSettingsPatch đổi chuỗi thành số', () => {
  const patch = formToSettingsPatch(settingsToForm(server));
  assert.equal(patch.payoutMinAmount, 50000);
  assert.equal(patch.defaultCommission.BATCH, 3.5);
  assert.equal(patch.payoutProvider, 'SIMULATED');
});

test('newClientKey: 32 ký tự hex, khác nhau mỗi lần', () => {
  const a = newClientKey(); const b = newClientKey();
  assert.match(a, /^[0-9a-f]{32}$/);
  assert.notEqual(a, b);
});

test('validateTestTransfer', () => {
  assert.deepEqual(validateTestTransfer({ amount: '2000', bin: '970436', account: '0123456789' }, 5000), {});
  assert.ok(validateTestTransfer({ amount: '999', bin: '970436', account: '0123456789' }, 5000).amount);
  assert.ok(validateTestTransfer({ amount: '5001', bin: '970436', account: '0123456789' }, 5000).amount);
  assert.ok(validateTestTransfer({ amount: '2000', bin: '', account: '0123456789' }, 5000).bin);
  assert.ok(validateTestTransfer({ amount: '2000', bin: '970436', account: '12ab' }, 5000).account);
});

test('summarizePlan: chỉ tính người nhận sẽ tạo kỳ mới', async () => {
  const { summarizePlan } = await import('./moneyLabels.js');
  const s = summarizePlan([
    { outcome: 'NEW', status: 'PROCESSING', net: 100000 },
    { outcome: 'NEW', status: 'AWAITING_APPROVAL', net: 50000 },
    { outcome: 'NO_LINES', status: null, net: 0 },
  ]);
  assert.equal(s.runnable.length, 2);
  assert.equal(s.totalNet, 150000);
  assert.equal(s.sendNow.length, 1);
  assert.equal(s.sendNowNet, 100000);
  assert.equal(s.skipped, 1);
});

test('maskAccount, bankStatusMeta, fmtDateTime', () => {
  assert.equal(maskAccount('0123456789'), '••••••6789');
  assert.equal(maskAccount(''), '—');
  assert.equal(bankStatusMeta('VERIFIED').label, 'Đã duyệt');
  assert.equal(bankStatusMeta('PENDING_VERIFICATION').label, 'Chờ Admin duyệt');
  assert.equal(bankStatusMeta('REJECTED').tone, 'red');
  assert.equal(bankStatusMeta(null).label, 'Chưa có tài khoản');
  // 2026-01-01T17:00Z = 00:00 ngày 02/01 giờ Việt Nam (backend không gắn hậu tố Z)
  assert.match(fmtDateTime('2026-01-01T17:00:00'), /02\/01\/2026/);
  assert.equal(fmtDateTime(null), '—');
});
