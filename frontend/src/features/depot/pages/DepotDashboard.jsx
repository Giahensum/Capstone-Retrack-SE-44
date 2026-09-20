import { DashboardLayout } from '@/components/layout/DashboardLayout';
import { StatCard } from '@/components/ui/CommonUI';
import { Package, Users, Truck, TrendingUp } from 'lucide-react';
export default function DepotDashboard() {
    return (<DashboardLayout title="Dashboard Kho Phế Liệu">
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
        <StatCard title="Yêu cầu chờ" value="0" icon={<Package size={20}/>} color="orange"/>
        <StatCard title="Lô hàng" value="0" icon={<Truck size={20}/>} color="blue"/>
        <StatCard title="Nhân viên" value="0" icon={<Users size={20}/>} color="purple"/>
        <StatCard title="Doanh thu tháng" value="0 ₫" icon={<TrendingUp size={20}/>} color="emerald"/>
      </div>
      <div className="bg-slate-900 border border-slate-800 border-dashed rounded-xl p-10 text-center text-slate-500">
        <Truck size={40} className="mx-auto mb-3 opacity-30"/>
        <p className="text-sm">Depot Owner Dashboard — sẽ được phát triển ở bước tiếp theo.</p>
      </div>
    </DashboardLayout>);
}
