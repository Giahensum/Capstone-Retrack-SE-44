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

/** Quy định chung hiển thị cho người nhận: mọi thay đổi đều cần Admin duyệt, không có thời gian chờ hay tự xác minh. */
export const BANK_POLICY_TEXT = 'Tài khoản mới hoặc đã đổi cần Admin duyệt trước khi nhận tiền hoặc được hoàn tiền; không còn thời gian chờ.';

/** Thông điệp giải thích trạng thái tài khoản cho người nhận (không lộ chi tiết nhà cung cấp). */
export function bankHelpText(bank) {
  if (!bank || !bank.accountNumberMasked) return 'Bạn chưa nhập tài khoản nhận tiền. Cần nhập và được Admin duyệt để nhận tiền hoặc được hoàn tiền.';
  switch (bank.status) {
    case 'VERIFIED': return 'Tài khoản đã được Admin duyệt, sẵn sàng nhận tiền.';
    case 'REJECTED': return 'Tài khoản bị Admin từ chối. Hãy kiểm tra và nhập lại đúng thông tin để được duyệt lại.';
    default: return 'Tài khoản đang chờ Admin duyệt. Khi được duyệt, các khoản đang giữ sẽ được chi.';
  }
}

/** Thông báo sau khi lưu: không bao giờ tự duyệt. */
export const BANK_SAVED_TEXT = 'Đã lưu tài khoản, đang chờ Admin duyệt';
