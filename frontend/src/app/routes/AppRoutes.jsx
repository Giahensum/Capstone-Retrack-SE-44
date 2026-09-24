import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { PrivateRoute } from './PrivateRoute';
import { ROLES } from '@/lib/utils';
// Lazy imports for code-splitting
import { lazy, Suspense } from 'react';
// Auth
const LoginPage = lazy(() => import('@/features/auth/pages/LoginPage'));
const RegisterPage = lazy(() => import('@/features/auth/pages/RegisterPage'));
// Landing
const LandingPage = lazy(() => import('@/features/landing/pages/LandingPage'));
// Seller
const SellerDashboard = lazy(() => import('@/features/seller/pages/SellerDashboard'));
// Depot

// My Depot Owner New UIs (TV2)
import DepotLayout from '@/components/layout/DepotLayout';
import MyDepotDashboard from '@/features/depot/Dashboard';
import DepotInventory from '@/features/depot/Inventory';
import DepotBatches from '@/features/depot/Batches';
import DepotPartners from '@/features/depot/Partners';
import DepotPayments from '@/features/depot/Payments';
import DepotPlatformFees from '@/features/depot/PlatformFees';
import DepotRevenueReport from '@/features/depot/RevenueReport';
import DepotStaff from '@/features/depot/Staff';
import DepotStaffPerformance from '@/features/depot/StaffPerformance';
import DepotProfile from '@/features/depot/Profile';
// Employee
const EmployeeDashboard = lazy(() => import('@/features/employee/pages/EmployeeDashboard'));
// Driver
const DriverDashboard = lazy(() => import('@/features/driver/pages/DriverDashboard'));
// Factory
const FactoryDashboard = lazy(() => import('@/features/factory/workspace/FactoryApp'));
// Admin
const AdminDashboard = lazy(() => import('@/features/admin/pages/AdminDashboard'));
// Loading fallback
const PageLoader = () => (<div className="flex items-center justify-center min-h-screen bg-slate-900">
    <div className="w-10 h-10 border-4 border-emerald-500 border-t-transparent rounded-full animate-spin"/>
  </div>);
export function AppRoutes() {
    return (<BrowserRouter>
      <Suspense fallback={<PageLoader />}>
        <Routes>
          {/* Public */}
          <Route path="/" element={<LandingPage />}/>
          <Route path="/login" element={<LoginPage />}/>
          <Route path="/register" element={<RegisterPage />}/>

          {/* Seller */}
          <Route path="/seller/*" element={<PrivateRoute allowedRoles={[ROLES.SELLER]}>
                <SellerDashboard />
              </PrivateRoute>}/>

          {/* Depot Owner (Auth Bypassed for UI Preview) */}
          <Route path="/depot" element={<DepotLayout />}>
              <Route path="dashboard" element={<MyDepotDashboard />} />
              <Route path="inventory" element={<DepotInventory />} />
              <Route path="batches" element={<DepotBatches />} />
              <Route path="partners" element={<DepotPartners />} />
              <Route path="payments" element={<DepotPayments />} />
              <Route path="payments/fees" element={<DepotPlatformFees />} />
              <Route path="reports" element={<DepotRevenueReport />} />
              <Route path="staff" element={<DepotStaff />} />
              <Route path="staff/performance" element={<DepotStaffPerformance />} />
              <Route path="profile" element={<DepotProfile />} />
              <Route path="*" element={<Navigate to="dashboard" replace />} />
          </Route>

          {/* Depot Employee */}
          <Route path="/employee/*" element={<PrivateRoute allowedRoles={[ROLES.DEPOT_EMPLOYEE]}>
                <EmployeeDashboard />
              </PrivateRoute>}/>

          {/* Driver */}
          <Route path="/driver/*" element={<PrivateRoute allowedRoles={[ROLES.DRIVER]}>
                <DriverDashboard />
              </PrivateRoute>}/>

          {/* Factory */}
          <Route path="/factory/*" element={<FactoryDashboard />}/>

          {/* Admin */}
          <Route path="/admin/*" element={<PrivateRoute allowedRoles={[ROLES.ADMIN]}>
                <AdminDashboard />
              </PrivateRoute>}/>

          {/* Fallback */}
          <Route path="/unauthorized" element={<div className="flex items-center justify-center min-h-screen text-red-400 text-xl">
              403 — Không có quyền truy cập
            </div>}/>
          <Route path="*" element={<Navigate to="/" replace/>}/>
        </Routes>
      </Suspense>
    </BrowserRouter>);
}

