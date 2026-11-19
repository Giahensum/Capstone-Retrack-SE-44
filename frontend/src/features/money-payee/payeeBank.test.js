import test from 'node:test';
import assert from 'node:assert/strict';
import { validateBankForm, buildBankBody, bankHelpText } from './payeeBank.js';

test('validateBankForm', () => {
  assert.deepEqual(validateBankForm({ bankBin: '970436', accountNumber: '0123456789', accountName: 'NGUYEN VAN A' }), {});
  assert.ok(validateBankForm({ bankBin: '', accountNumber: '0123456789', accountName: 'A B' }).bankBin);
  assert.ok(validateBankForm({ bankBin: '970436', accountNumber: '12 34', accountName: 'A B' }).accountNumber);
  assert.ok(validateBankForm({ bankBin: '970436', accountNumber: '0123456789', accountName: ' ' }).accountName);
});

test('buildBankBody lấy tên ngân hàng theo BIN và cắt khoảng trắng', () => {
  const body = buildBankBody({ bankBin: '970436', accountNumber: ' 0123456789 ', accountName: ' A B ' }, [{ bin: '970436', name: 'Vietcombank' }]);
  assert.deepEqual(body, { bankName: 'Vietcombank', bankBin: '970436', accountNumber: '0123456789', accountName: 'A B' });
});

test('bankHelpText theo trạng thái', () => {
  const now = Date.parse('2026-01-01T00:00:00Z');
  assert.match(bankHelpText(null, now), /chưa nhập/);
  assert.match(bankHelpText({ accountNumberMasked: '***1', status: 'VERIFIED', holdUntil: '2026-01-02T00:00:00' }, now), /giữ chi/);
  assert.match(bankHelpText({ accountNumberMasked: '***1', status: 'VERIFIED', holdUntil: '2025-12-31T00:00:00' }, now), /đã được xác minh/);
  assert.match(bankHelpText({ accountNumberMasked: '***1', status: 'PENDING_VERIFICATION' }, now), /chờ Admin/);
});
