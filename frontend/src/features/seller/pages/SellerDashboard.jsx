import { Routes, Route } from 'react-router-dom';
import { lazy, Suspense } from 'react';
import SellerLayout from '@/components/layout/SellerLayout';

const SellerHome = lazy(() => import('./SellerHome'));
const CreateRequest = lazy(() => import('./CreateRequest'));
const RequestList = lazy(() => import('./RequestList'));
const RequestDetail = lazy(() => import('./RequestDetail'));
const SellerIncome = lazy(() => import('./SellerIncome'));
const SellerProfile = lazy(() => import('./SellerProfile'));

const PageLoader = () => (
  <div className="flex items-center justify-center h-64">
    <div className="w-8 h-8 border-3 border-emerald-500 border-t-transparent rounded-full animate-spin" />
  </div>
);

export default function SellerDashboard() {
  return (
    <SellerLayout>
      <Suspense fallback={<PageLoader />}>
        <Routes>
          <Route index element={<SellerHome />} />
          <Route path="create" element={<CreateRequest />} />
          <Route path="requests" element={<RequestList />} />
          <Route path="requests/:id" element={<RequestDetail />} />
          <Route path="income" element={<SellerIncome />} />
          <Route path="profile" element={<SellerProfile />} />
          <Route path="*" element={<SellerHome />} />
        </Routes>
      </Suspense>
    </SellerLayout>
  );
}

