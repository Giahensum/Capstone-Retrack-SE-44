// Nhãn và màu cho phiên thanh lý (giá trị là định danh kỹ thuật từ API, không đổi).
export const LIQUIDATION_PHASE_LABEL = {
  UPCOMING: 'Sắp mở',
  SURVEY: 'Đang khảo sát',
  BIDDING: 'Đang trả giá',
  CLOSING: 'Đang chốt phiên',
  AWARDED: 'Đã có người trúng',
  NO_SALE: 'Không bán được',
  CANCELLED: 'Đã hủy',
};

export const LIQUIDATION_PHASE_STYLE = {
  UPCOMING: 'bg-d-surface-container text-d-on-surface-variant border-d-border-subtle',
  SURVEY: 'bg-blue-50 text-blue-700 border-blue-200',
  BIDDING: 'bg-emerald-50 text-emerald-700 border-emerald-200',
  CLOSING: 'bg-amber-50 text-amber-700 border-amber-200',
  AWARDED: 'bg-d-primary text-d-on-primary border-d-primary',
  NO_SALE: 'bg-d-surface-container text-d-on-surface-variant border-d-border-subtle',
  CANCELLED: 'bg-red-50 text-red-700 border-red-200',
};

export const AGREEMENT_STAGE_LABEL = {
  WAITING_SIGNATURES: 'Chờ hai bên ký hợp đồng đặt cọc',
  WAITING_DEPOSIT: 'Đã ký — chờ chủ kho chuyển tiền cọc',
  WAITING_DEPOSIT_CONFIRM: 'Chủ kho đã báo chuyển cọc — chờ bạn xác nhận',
  WAITING_BUYER_PAYMENT: 'Chờ chủ kho chuyển tiền',
  WAITING_SELLER_CONFIRM: 'Chủ kho đã báo chuyển tiền — chờ bạn xác nhận',
  WAITING_HANDOVER: 'Đã nhận tiền — chờ bạn xác nhận bàn giao',
  WAITING_OWNER_COLLECT: 'Đã bàn giao — chờ chủ kho xác nhận thu gom',
  COMPLETED: 'Hoàn tất',
};

export const LIQUIDATION_FILTERS = [
  { key: '', label: 'Tất cả' },
  { key: 'SURVEY', label: 'Đang khảo sát' },
  { key: 'UPCOMING', label: 'Sắp mở' },
  { key: 'BIDDING', label: 'Đang trả giá' },
  { key: 'CLOSING', label: 'Đang chốt' },
  { key: 'AWARDED', label: 'Đã có người trúng' },
  { key: 'NO_SALE', label: 'Không bán được' },
  { key: 'CANCELLED', label: 'Đã hủy' },
];
