// Các hằng số và utility functions dùng chung
export const ROLES = {
    SELLER: 'SELLER',
    DEPOT_OWNER: 'DEPOT_OWNER',
    DEPOT_EMPLOYEE: 'DEPOT_EMPLOYEE',
    DRIVER: 'DRIVER',
    FACTORY: 'FACTORY',
    ADMIN: 'ADMIN',
};
export const PICKUP_STATUS = {
    PENDING: 'PENDING',
    SCHEDULED: 'SCHEDULED',
    IN_PROGRESS: 'IN_PROGRESS',
    WEIGHED: 'WEIGHED',
    SELLER_CONFIRMED: 'SELLER_CONFIRMED',
    AWAITING_PAYMENT: 'AWAITING_PAYMENT',
    PAYMENT_SENT: 'PAYMENT_SENT',
    DONE: 'DONE',
    CANCELLED: 'CANCELLED',
};
// Cùng một trạng thái DB có cùng ý nghĩa ở Seller và Depot.
// PAYMENT_SENT chỉ xác nhận chủ kho đã gửi tiền; Seller chưa xác nhận nhận tiền.
export const PICKUP_STATUS_LABEL = {
    PENDING: 'Chờ nhận đơn',
    SCHEDULED: 'Đã nhận đơn',
    IN_PROGRESS: 'Đang thu gom và cân',
    WEIGHED: 'Chờ người bán xác nhận cân',
    SELLER_CONFIRMED: 'Người bán đã xác nhận cân',
    AWAITING_PAYMENT: 'Chờ chủ kho chuyển tiền',
    PAYMENT_SENT: 'Đã chuyển tiền · chờ người bán xác nhận',
    DONE: 'Hoàn tất · người bán đã nhận tiền',
    CANCELLED: 'Đã hủy',
};
export const PICKUP_STATUS_STEPS = [
    PICKUP_STATUS.PENDING, PICKUP_STATUS.SCHEDULED, PICKUP_STATUS.IN_PROGRESS,
    PICKUP_STATUS.WEIGHED, PICKUP_STATUS.SELLER_CONFIRMED,
    PICKUP_STATUS.AWAITING_PAYMENT, PICKUP_STATUS.PAYMENT_SENT, PICKUP_STATUS.DONE,
];
export const PICKUP_STATUS_STYLE = {
    PENDING: 'bg-yellow-500/15 text-yellow-700 border-yellow-500/30',
    SCHEDULED: 'bg-blue-500/15 text-blue-700 border-blue-500/30',
    IN_PROGRESS: 'bg-indigo-500/15 text-indigo-700 border-indigo-500/30',
    WEIGHED: 'bg-purple-500/15 text-purple-700 border-purple-500/30',
    SELLER_CONFIRMED: 'bg-cyan-500/15 text-cyan-700 border-cyan-500/30',
    AWAITING_PAYMENT: 'bg-orange-500/15 text-orange-700 border-orange-500/30',
    PAYMENT_SENT: 'bg-teal-500/15 text-teal-700 border-teal-500/30',
    DONE: 'bg-green-200/15 text-green-700 border-emerald-500/30',
    CANCELLED: 'bg-red-500/15 text-red-700 border-red-500/30',
};
export const BATCH_STATUS_LABEL = {
    MARKETPLACE: 'Thị trường',
    PENDING_APPROVAL: 'Chờ duyệt',
    TRANSPORT_READY: 'Sẵn sàng vận chuyển',
    COMPLETED: 'Hoàn thành',
};
export const ROLE_LABEL = {
    ADMIN: 'Quản trị viên',
    SELLER: 'Người bán',
    DEPOT_OWNER: 'Chủ kho vựa',
    DEPOT_EMPLOYEE: 'Nhân viên thu gom',
    DRIVER: 'Tài xế',
    FACTORY: 'Nhà máy',
};
// Khớp 1-1 với enum Retrack.API.Models.Enums.MaterialType ở backend.
// Bảng giá thị trường lưu đúng các giá trị này, không nhận chuỗi tự do.
export const MATERIAL_TYPE_LABEL = {
    PET: 'Nhựa PET',
    HDPE: 'Nhựa HDPE',
    PVC: 'Nhựa PVC',
    PAPER: 'Giấy',
    CARDBOARD: 'Bìa carton',
    ALUMINUM: 'Nhôm',
    IRON: 'Sắt',
    STEEL: 'Thép',
    COPPER: 'Đồng',
    ELECTRONIC_WASTE: 'Rác điện tử',
    OTHER: 'Khác',
};
export const INVOICE_STATUS_LABEL = {
    PENDING: 'Chưa thanh toán',
    PAID: 'Đã thanh toán',
};
// Format tiền VND
export function formatCurrency(amount) {
    return new Intl.NumberFormat('vi-VN', {
        style: 'currency',
        currency: 'VND',
    }).format(amount);
}
// Format ngày giờ
export function formatDate(date) {
    return new Intl.DateTimeFormat('vi-VN', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
    }).format(new Date(date));
}
// Format số kg
export function formatWeight(kg) {
    return `${kg.toLocaleString('vi-VN')} kg`;
}
// Lấy màu badge theo status
export function getStatusColor(status) {
    const colors = {
        PENDING: 'bg-yellow-500/20 text-yellow-400 border-yellow-500/30',
        SCHEDULED: 'bg-blue-500/20 text-blue-400 border-blue-500/30',
        WEIGHED: 'bg-purple-500/20 text-purple-400 border-purple-500/30',
        SELLER_CONFIRMED: 'bg-indigo-500/20 text-indigo-400 border-indigo-500/30',
        AWAITING_PAYMENT: 'bg-orange-500/20 text-orange-400 border-orange-500/30',
        PAYMENT_SENT: 'bg-cyan-500/20 text-cyan-400 border-cyan-500/30',
        DONE: 'bg-emerald-500/20 text-emerald-400 border-emerald-500/30',
        APPROVED: 'bg-emerald-500/20 text-emerald-400 border-emerald-500/30',
        BLOCKED: 'bg-red-500/20 text-red-400 border-red-500/30',
        IN_TRANSIT: 'bg-blue-500/20 text-blue-400 border-blue-500/30',
        DELIVERED: 'bg-emerald-500/20 text-emerald-400 border-emerald-500/30',
        PAID: 'bg-emerald-500/20 text-emerald-400 border-emerald-500/30',
        ADMIN: 'bg-red-500/20 text-red-400 border-red-500/30',
        SELLER: 'bg-cyan-500/20 text-cyan-400 border-cyan-500/30',
        DEPOT_OWNER: 'bg-blue-500/20 text-blue-400 border-blue-500/30',
        DEPOT_EMPLOYEE: 'bg-purple-500/20 text-purple-400 border-purple-500/30',
        DRIVER: 'bg-orange-500/20 text-orange-400 border-orange-500/30',
        FACTORY: 'bg-indigo-500/20 text-indigo-400 border-indigo-500/30',
    };
    return colors[status] ?? 'bg-slate-500/20 text-slate-400 border-slate-500/30';
}
