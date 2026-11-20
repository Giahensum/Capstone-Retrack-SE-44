import test from 'node:test';
import assert from 'node:assert/strict';
import { validateBankForm, buildBankBody, bankHelpText, BANK_POLICY_TEXT, BANK_SAVED_TEXT } from './payeeBank.js';

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

test('bankHelpText theo trạng thái, không còn nhắc thời gian giữ', () => {
  assert.match(bankHelpText(null), /chưa nhập/);
  assert.match(bankHelpText({ accountNumberMasked: '***1', status: 'VERIFIED' }), /đã được Admin duyệt/);
  assert.match(bankHelpText({ accountNumberMasked: '***1', status: 'PENDING_VERIFICATION' }), /chờ Admin duyệt/);
  assert.match(bankHelpText({ accountNumberMasked: '***1', status: 'REJECTED' }), /từ chối/);
  // holdUntil cũ (nếu máy chủ còn trả) bị bỏ qua hoàn toàn
  assert.doesNotMatch(bankHelpText({ accountNumberMasked: '***1', status: 'VERIFIED', holdUntil: '2999-01-01T00:00:00' }), /giữ chi|thời gian giữ/);
  assert.match(BANK_POLICY_TEXT, /không còn thời gian chờ/);
  assert.doesNotMatch(BANK_SAVED_TEXT, /tự xác minh/);
});
