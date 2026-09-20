import { DashboardLayout } from '@/components/layout/DashboardLayout';
import { StatCard } from '@/components/ui/CommonUI';
import { Package, CheckCircle } from 'lucide-react';
export default function EmployeeDashboard() {
    return (<DashboardLayout title="Dashboard Nhân Viên Kho">
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 mb-8">
        <StatCard title="Đơn chờ xử lý" value="0" icon={<Package size={20}/>} color="orange"/>
        <StatCard title="Đơn hôm nay" value="0" icon={<CheckCircle size={20}/>} color="emerald"/>
      </div>
      <div className="bg-slate-900 border border-slate-800 border-dashed rounded-xl p-10 text-center text-slate-500">
        <Package size={40} className="mx-auto mb-3 opacity-30"/>
        <p className="text-sm">Employee Dashboard — nhận đơn và cân phế liệu.</p>
      </div>
    </DashboardLayout>);
}
