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
    WEIGHED: 'WEIGHED',
    SELLER_CONFIRMED: 'SELLER_CONFIRMED',
    AWAITING_PAYMENT: 'AWAITING_PAYMENT',
    PAYMENT_SENT: 'PAYMENT_SENT',
    DONE: 'DONE',
};
export const PICKUP_STATUS_LABEL = {
    PENDING: 'Chờ xử lý',
    SCHEDULED: 'Đã nhận đơn',
    WEIGHED: 'Đã cân',
    SELLER_CONFIRMED: 'Seller xác nhận',
    AWAITING_PAYMENT: 'Chờ thanh toán',
    PAYMENT_SENT: 'Đã thanh toán',
    DONE: 'Hoàn thành',
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
