import { ShieldCheck, Info } from 'lucide-react';
import AdminLayout from '@/components/layout/AdminLayout';
import VerificationPanel from '@/features/depot/components/VerificationPanel';
import { PageHeader, Notice } from '../components/AdminUI';

export default function DepotVerifications() {
  return (
    <AdminLayout crumb="Hồ sơ kho vựa">
      <div className="flex w-full flex-col gap-6 p-4 md:p-6">
        <PageHeader icon={ShieldCheck} title="Xác minh kho vựa" description="Đối soát giấy tờ trước khi mở giao dịch mới cho kho." />
        <Notice tone="blue" icon={Info}>Mọi quyết định được lưu cùng thời gian và người duyệt.</Notice>
        <VerificationPanel admin />
      </div>
    </AdminLayout>
  );
}
