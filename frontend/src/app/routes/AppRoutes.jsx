import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { PrivateRoute } from './PrivateRoute';
import { ROLES } from '@/lib/utils';
// Lazy imports for code-splitting
import { lazy, Suspense } from 'react';
// Auth
const LoginPage = lazy(() => import('@/features/auth/pages/LoginPage'));
const RegisterPage = lazy(() => import('@/features/auth/pages/RegisterPage'));
const ForgotPasswordPage = lazy(() => import('@/features/auth/pages/ForgotPasswordPage'));
// Landing
const LandingPage = lazy(() => import('@/features/landing/pages/LandingPage'));
const DepotPlanPage = lazy(() => import('@/features/landing/pages/DepotPlanPage'));
const TermsPage = lazy(() => import('@/features/landing/pages/TermsPage'));
// Seller
const SellerDashboard = lazy(() => import('@/features/seller/pages/SellerDashboard'));
// Depot

// Giao diện Chủ kho vựa — Ngô Sỹ Giá (TV2)
import DepotLayout from '@/components/layout/DepotLayout';
import MyDepotDashboard from '@/features/depot/Dashboard';
const DepotInventory = lazy(() => import('@/features/depot/Inventory'));
const DepotBatches = lazy(() => import('@/features/depot/Batches'));
import DepotPartners from '@/features/depot/Partners';
import DepotPayments from '@/features/depot/Payments';
import DepotPlatformFees from '@/features/depot/PlatformFees';
const DepotRevenueReport = lazy(() => import('@/features/depot/RevenueReport'));
import DepotStaff from '@/features/depot/Staff';
import DepotStaffPerformance from '@/features/depot/StaffPerformance';
import DepotProfile from '@/features/depot/Profile';
const DepotLiquidations = lazy(() => import('@/features/depot/liquidation/LiquidationMarket'));
const DepotNotifications = lazy(() => import('@/features/depot/Notifications'));
const DepotDispatch = lazy(() => import('@/features/depot/dispatch/DispatchPage'));
const DepotBuyingPrices = lazy(() => import('@/features/depot/BuyingPrices'));
const DepotSubscription = lazy(() => import('@/features/depot/Subscription'));
import { DepotProvider } from '@/features/depot/DepotContext';
// Thu nhập & chi trả của người nhận (Seller, Chủ kho, Nhà máy dùng chung một trang)
const MoneyEarnings = lazy(() => import('@/features/money-payee/EarningsPage'));
// Employee
const StaffMobilePage = lazy(() => import('@/features/auth/pages/StaffMobilePage'));
// Driver
// Factory
const FactoryDashboard = lazy(() => import('@/features/factory/workspace/FactoryApp'));
// Admin
const AdminDashboard = lazy(() => import('@/features/admin/pages/AdminDashboard'));
const PayReturnPage = lazy(() => import('@/features/money/PayReturnPage'));
// Loading fallback
const PageLoader = () => (<div role="status" aria-label="Đang tải" className="flex min-h-screen flex-col items-center justify-center gap-4 bg-d-background">
    <img src="/brand/logo_mark.png" alt="" aria-hidden="true" className="size-12 animate-pulse object-contain"/>
    <div className="size-8 animate-spin rounded-full border-[3px] border-[#446900] border-t-transparent"/>
  </div>);
export function AppRoutes() {
    return (<BrowserRouter>
      <Suspense fallback={<PageLoader />}>
        <Routes>
          {/* Public */}
          <Route path="/" element={<LandingPage />}/>
          <Route path="/goi-dich-vu" element={<DepotPlanPage />}/>
          <Route path="/dieu-khoan" element={<TermsPage />}/>
          <Route path="/login" element={<LoginPage />}/>
          <Route path="/register" element={<RegisterPage />}/>
          <Route path="/forgot-password" element={<ForgotPasswordPage />}/>
          <Route path="/staff-app" element={<StaffMobilePage />}/>

          {/* Seller */}
          <Route path="/seller/*" element={<PrivateRoute allowedRoles={[ROLES.SELLER]}>
                <SellerDashboard />
              </PrivateRoute>}/>

          {/* Depot Owner */}
          <Route path="/depot" element={<PrivateRoute allowedRoles={[ROLES.DEPOT_OWNER]}><DepotProvider><DepotLayout /></DepotProvider></PrivateRoute>}>
              <Route index element={<Navigate to="dashboard" replace />} />
              <Route path="dashboard" element={<MyDepotDashboard />} />
              <Route path="inventory" element={<DepotInventory />} />
              <Route path="batches" element={<DepotBatches />} />
              <Route path="partners" element={<DepotPartners />} />
              <Route path="payments" element={<DepotPayments />} />
              <Route path="payments/fees" element={<DepotPlatformFees />} />
              <Route path="subscription" element={<DepotSubscription />} />
              <Route path="earnings" element={<MoneyEarnings />} />
              <Route path="reports" element={<DepotRevenueReport />} />
              <Route path="staff" element={<DepotStaff />} />
              <Route path="staff/performance" element={<DepotStaffPerformance />} />
              <Route path="profile" element={<DepotProfile />} />
              <Route path="liquidations" element={<DepotLiquidations />} />
              <Route path="notifications" element={<DepotNotifications />} />
              <Route path="dispatch" element={<DepotDispatch />} />
              <Route path="buying-prices" element={<DepotBuyingPrices />} />
              <Route path="*" element={<Navigate to="dashboard" replace />} />
          </Route>

          {/* Depot Employee */}
          <Route path="/employee/*" element={<PrivateRoute allowedRoles={[ROLES.DEPOT_EMPLOYEE]}>
                <StaffMobilePage />
              </PrivateRoute>}/>

          {/* Driver */}
          <Route path="/driver/*" element={<PrivateRoute allowedRoles={[ROLES.DRIVER]}>
                <StaffMobilePage />
              </PrivateRoute>}/>

          {/* Factory */}
          <Route path="/factory/*" element={<PrivateRoute allowedRoles={[ROLES.FACTORY]}><FactoryDashboard /></PrivateRoute>}/>

          {/* Admin */}
          <Route path="/admin/*" element={<PrivateRoute allowedRoles={[ROLES.ADMIN]}>
                <AdminDashboard />
              </PrivateRoute>}/>

          {/* Kết quả thanh toán PayOS: mọi vai trò đã đăng nhập, không giới hạn vai trò */}
          <Route path="/pay/return" element={<PrivateRoute><PayReturnPage /></PrivateRoute>}/>

          {/* Fallback */}
          <Route path="/unauthorized" element={<main className="flex min-h-screen flex-col items-center justify-center gap-4 bg-d-background p-6 text-center">
              <img src="/brand/logo_mark.png" alt="" aria-hidden="true" className="size-16 object-contain"/>
              <p className="text-5xl font-black text-[#446900]">403</p>
              <h1 className="text-xl font-bold text-[#141e18]">Không có quyền truy cập</h1>
              <p className="max-w-sm text-[#424936]">Tài khoản hiện tại không được phép mở trang này. Hãy đăng nhập bằng tài khoản đúng vai trò.</p>
              <a href="/" className="rounded-full bg-[#446900] px-5 py-2.5 font-semibold text-white transition hover:bg-[#385500]">Về trang chủ</a>
            </main>}/>
          <Route path="*" element={<Navigate to="/" replace/>}/>
        </Routes>
      </Suspense>
    </BrowserRouter>);
}

