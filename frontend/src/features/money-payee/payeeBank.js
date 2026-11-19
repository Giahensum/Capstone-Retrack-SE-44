// Hàm thuần cho form "Tài khoản nhận tiền" của người nhận (Seller, Chủ kho, Nhà máy).

export function validateBankForm({ bankBin, accountNumber, accountName }) {
  const errors = {};
  if (!bankBin) errors.bankBin = 'Chọn ngân hàng.';
  if (!/^\d{6,19}$/.test((accountNumber ?? '').trim())) errors.accountNumber = 'Số tài khoản gồm 6 đến 19 chữ số, không có khoảng trắng.';
  const name = (accountName ?? '').trim();
  if (name.length < 2) errors.accountName = 'Nhập tên chủ tài khoản đúng như trên ngân hàng.';
  else if (name.length > 100) errors.accountName = 'Tên chủ tài khoản tối đa 100 ký tự.';
  return errors;
}

export function buildBankBody(f, banks = []) {
  const bank = banks.find((b) => b.bin === f.bankBin);
  return { bankName: bank?.name ?? '', bankBin: f.bankBin, accountNumber: f.accountNumber.trim(), accountName: f.accountName.trim() };
}

/** Thông điệp giải thích trạng thái tài khoản cho người nhận (không lộ chi tiết nhà cung cấp). */
export function bankHelpText(bank, now = Date.now()) {
  if (!bank || !bank.accountNumberMasked) return 'Bạn chưa nhập tài khoản nhận tiền. Cần nhập để được chi tiền.';
  const hold = bank.holdUntil ? Date.parse(/[zZ]|[+-]\d\d:?\d\d$/.test(bank.holdUntil) ? bank.holdUntil : `${bank.holdUntil}Z`) : null;
  if (hold && hold > now) return 'Tài khoản mới đổi nên đang trong thời gian giữ chi để an toàn.';
  if (bank.status === 'VERIFIED') return 'Tài khoản đã được xác minh, sẵn sàng nhận tiền.';
  return 'Tài khoản đang chờ Admin xác minh. Tên chủ tài khoản trùng họ tên của bạn thì hệ thống tự xác minh.';
}
