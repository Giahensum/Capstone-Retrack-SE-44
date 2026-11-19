import api from '@/lib/axios';

// API Thu nhập của người nhận (Seller, Chủ kho, Nhà máy). Mã người dùng lấy từ token ở máy chủ.
const unwrap = (p) => p.then((r) => r.data.data);

export const moneyPayeeApi = {
  getEarnings: () => unwrap(api.get('/money/my/earnings')),
  listPayouts: (params) => unwrap(api.get('/money/my/payouts', { params })),
  getBank: () => unwrap(api.get('/money/my/bank')),
  updateBank: (body) => unwrap(api.put('/money/my/bank', body)),
  getBanks: () => unwrap(api.get('/money/banks')),
};
