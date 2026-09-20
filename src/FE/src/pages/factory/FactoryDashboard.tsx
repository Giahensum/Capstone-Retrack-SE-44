import { DashboardLayout } from '@/components/layout/DashboardLayout'
import { StatCard } from '@/components/ui/CommonUI'
import { Package, Truck, BarChart3, Factory } from 'lucide-react'

export default function FactoryDashboard() {
  return (
    <DashboardLayout title="Dashboard Nhà Máy">
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
        <StatCard title="Nhu cầu đang mở" value="0" icon={<Package size={20} />} color="blue" />
        <StatCard title="Lô đang đến" value="0" icon={<Truck size={20} />} color="orange" />
        <StatCard title="Đối tác kho" value="0" icon={<Factory size={20} />} color="purple" />
        <StatCard title="Chi tiêu tháng" value="0 ₫" icon={<BarChart3 size={20} />} color="emerald" />
      </div>
      <div className="bg-slate-900 border border-slate-800 border-dashed rounded-xl p-10 text-center text-slate-500">
        <Factory size={40} className="mx-auto mb-3 opacity-30" />
        <p className="text-sm">Factory Dashboard — quản lý nhu cầu và QC lô hàng.</p>
      </div>
    </DashboardLayout>
  )
}
