import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { PrivateRoute } from './PrivateRoute'
import { ROLES } from '@/lib/utils'

// Lazy imports for code-splitting
import { lazy, Suspense } from 'react'

// Auth
const LoginPage = lazy(() => import('@/pages/auth/LoginPage'))
const RegisterPage = lazy(() => import('@/pages/auth/RegisterPage'))

// Landing
const LandingPage = lazy(() => import('@/pages/landing/LandingPage'))

// Seller
const SellerDashboard = lazy(() => import('@/pages/seller/SellerDashboard'))

// Depot
const DepotDashboard = lazy(() => import('@/pages/depot/DepotDashboard'))

// Employee
const EmployeeDashboard = lazy(() => import('@/pages/employee/EmployeeDashboard'))

// Driver
const DriverDashboard = lazy(() => import('@/pages/driver/DriverDashboard'))

// Factory
const FactoryDashboard = lazy(() => import('@/pages/factory/FactoryDashboard'))

// Admin
const AdminDashboard = lazy(() => import('@/pages/admin/AdminDashboard'))

// Loading fallback
const PageLoader = () => (
  <div className="flex items-center justify-center min-h-screen bg-slate-900">
    <div className="w-10 h-10 border-4 border-emerald-500 border-t-transparent rounded-full animate-spin" />
  </div>
)

export function AppRoutes() {
  return (
    <BrowserRouter>
      <Suspense fallback={<PageLoader />}>
        <Routes>
          {/* Public */}
          <Route path="/" element={<LandingPage />} />
          <Route path="/login" element={<LoginPage />} />
          <Route path="/register" element={<RegisterPage />} />

          {/* Seller */}
          <Route
            path="/seller/*"
            element={
              <PrivateRoute allowedRoles={[ROLES.SELLER]}>
                <SellerDashboard />
              </PrivateRoute>
            }
          />

          {/* Depot Owner */}
          <Route
            path="/depot/*"
            element={
              <PrivateRoute allowedRoles={[ROLES.DEPOT_OWNER]}>
                <DepotDashboard />
              </PrivateRoute>
            }
          />

          {/* Depot Employee */}
          <Route
            path="/employee/*"
            element={
              <PrivateRoute allowedRoles={[ROLES.DEPOT_EMPLOYEE]}>
                <EmployeeDashboard />
              </PrivateRoute>
            }
          />

          {/* Driver */}
          <Route
            path="/driver/*"
            element={
              <PrivateRoute allowedRoles={[ROLES.DRIVER]}>
                <DriverDashboard />
              </PrivateRoute>
            }
          />

          {/* Factory */}
          <Route
            path="/factory/*"
            element={
              <PrivateRoute allowedRoles={[ROLES.FACTORY]}>
                <FactoryDashboard />
              </PrivateRoute>
            }
          />

          {/* Admin */}
          <Route
            path="/admin/*"
            element={
              <PrivateRoute allowedRoles={[ROLES.ADMIN]}>
                <AdminDashboard />
              </PrivateRoute>
            }
          />

          {/* Fallback */}
          <Route path="/unauthorized" element={
            <div className="flex items-center justify-center min-h-screen text-red-400 text-xl">
              403 — Không có quyền truy cập
            </div>
          } />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </Suspense>
    </BrowserRouter>
  )
}
