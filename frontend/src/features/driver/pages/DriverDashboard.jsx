import { DashboardLayout } from '@/components/layout/DashboardLayout';
import { StatCard } from '@/components/ui/CommonUI';
import { Truck, CheckCircle } from 'lucide-react';
export default function DriverDashboard() {
    return (<DashboardLayout title="Dashboard Tài Xế">
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 mb-8">
        <StatCard title="Chuyến chờ" value="0" icon={<Truck size={20}/>} color="orange"/>
        <StatCard title="Hoàn thành" value="0" icon={<CheckCircle size={20}/>} color="emerald"/>
      </div>
      <div className="bg-slate-900 border border-slate-800 border-dashed rounded-xl p-10 text-center text-slate-500">
        <Truck size={40} className="mx-auto mb-3 opacity-30"/>
        <p className="text-sm">Driver Dashboard — xem và nhận công việc vận chuyển.</p>
      </div>
    </DashboardLayout>);
}
