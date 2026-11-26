import { Routes, Route } from 'react-router-dom';
import { lazy, Suspense } from 'react';
import SellerLayout from '@/components/layout/SellerLayout';

const SellerHome = lazy(() => import('./SellerHome'));
const CreateRequest = lazy(() => import('./CreateRequest'));
const RequestList = lazy(() => import('./RequestList'));
const RequestDetail = lazy(() => import('./RequestDetail'));
const SellerIncome = lazy(() => import('./SellerIncome'));
const MoneyEarnings = lazy(() => import('@/features/money-payee/EarningsPage'));
const SellerProfile = lazy(() => import('./SellerProfile'));
const SellerNotifications = lazy(() => import('./SellerNotifications'));
const SellerAiScanner = lazy(() => import('./SellerAiScanner'));
const LiquidationList = lazy(() => import('./LiquidationList'));
const LiquidationCreate = lazy(() => import('./LiquidationCreate'));
const LiquidationDetail = lazy(() => import('./LiquidationDetail'));
const LiquidationV2Detail = lazy(() => import('./LiquidationV2Detail'));

const PageLoader = () => (
  <div className="flex items-center justify-center h-64">
    <div className="w-8 h-8 border-[3px] border-[#446900] border-t-transparent rounded-full animate-spin" />
  </div>
);

export default function SellerDashboard() {
  return (
    <SellerLayout>
      <Suspense fallback={<PageLoader />}>
        <Routes>
          <Route index element={<SellerHome />} />
          <Route path="ai-scanner" element={<SellerAiScanner />} />
          <Route path="create" element={<CreateRequest />} />
          <Route path="requests" element={<RequestList />} />
          <Route path="requests/:id" element={<RequestDetail />} />
          <Route path="liquidations" element={<LiquidationList />} />
          <Route path="liquidations/new" element={<LiquidationCreate />} />
          <Route path="liquidations/v2/:id" element={<LiquidationV2Detail />} />
          <Route path="liquidations/:id" element={<LiquidationDetail />} />
          <Route path="notifications" element={<SellerNotifications />} />
          <Route path="income" element={<SellerIncome />} />
          <Route path="earnings" element={<MoneyEarnings />} />
          <Route path="profile" element={<SellerProfile />} />
          <Route path="*" element={<SellerHome />} />
        </Routes>
      </Suspense>
    </SellerLayout>
  );
}

