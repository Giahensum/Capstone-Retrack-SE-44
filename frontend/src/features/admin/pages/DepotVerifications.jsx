import AdminLayout from '@/components/layout/AdminLayout';
import VerificationPanel from '@/features/depot/components/VerificationPanel';
export default function DepotVerifications() {
  return <AdminLayout crumb="Hồ sơ kho vựa"><div className="p-4 md:p-6"><h1 className="text-3xl font-bold">Xác minh kho vựa</h1><p className="mt-2">Đối soát giấy tờ trước khi mở giao dịch mới cho kho. Mọi quyết định được lưu cùng thời gian và người duyệt.</p><VerificationPanel admin /></div></AdminLayout>;
}
