import { Routes, Route } from 'react-router-dom'
import { DashboardLayout } from '@/components/layout/DashboardLayout'
import { StatCard } from '@/components/ui/CommonUI'
import { Package, CheckCircle, Clock, DollarSign } from 'lucide-react'

function SellerRequestsPage() {
  return (
    <DashboardLayout title="Yêu cầu thu gom">
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
        <StatCard title="Tổng yêu cầu" value="0" icon={<Package size={20} />} color="blue" />
        <StatCard title="Đang xử lý" value="0" icon={<Clock size={20} />} color="orange" />
        <StatCard title="Hoàn thành" value="0" icon={<CheckCircle size={20} />} color="emerald" />
        <StatCard title="Tổng thu nhập" value="0 ₫" icon={<DollarSign size={20} />} color="purple" />
      </div>
      <div className="bg-slate-900 border border-slate-800 border-dashed rounded-xl p-10 text-center text-slate-500">
        <Package size={40} className="mx-auto mb-3 opacity-30" />
        <p className="text-sm">Chưa có yêu cầu thu gom nào. Quay lại trang chủ để tạo yêu cầu!</p>
      </div>
    </DashboardLayout>
  )
}

export default function SellerDashboard() {
  return (
    <Routes>
      <Route index element={<SellerRequestsPage />} />
      <Route path="requests" element={<SellerRequestsPage />} />
      <Route path="*" element={<SellerRequestsPage />} />
    </Routes>
  )
}
