import { DashboardLayout } from '@/components/layout/DashboardLayout'
import { StatCard } from '@/components/ui/CommonUI'
import { Users, DollarSign, BarChart3, Settings } from 'lucide-react'

export default function AdminDashboard() {
  return (
    <DashboardLayout title="Admin Dashboard">
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
        <StatCard title="Tổng người dùng" value="0" icon={<Users size={20} />} color="blue" />
        <StatCard title="Giao dịch hôm nay" value="0" icon={<BarChart3 size={20} />} color="orange" />
        <StatCard title="Doanh thu nền tảng" value="0 ₫" icon={<DollarSign size={20} />} color="emerald" />
        <StatCard title="Phí trung bình" value="1%" icon={<Settings size={20} />} color="purple" />
      </div>
      <div className="bg-slate-900 border border-slate-800 border-dashed rounded-xl p-10 text-center text-slate-500">
        <BarChart3 size={40} className="mx-auto mb-3 opacity-30" />
        <p className="text-sm">Admin Dashboard — quản lý toàn bộ hệ thống ReTrack.</p>
      </div>
    </DashboardLayout>
  )
}
