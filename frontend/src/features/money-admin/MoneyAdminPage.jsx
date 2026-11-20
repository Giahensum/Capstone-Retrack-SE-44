import { useCallback, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { ArrowLeftRight, SlidersHorizontal, Calculator, ClipboardList, Users, BookOpen, FlaskConical, TriangleAlert } from 'lucide-react';
import AdminLayout from '@/components/layout/AdminLayout';
import { PageHeader, FilterTabs } from '@/features/admin/components/AdminUI';
import { moneyAdminApi } from './moneyAdminApi';
import SettingsTab, { ProviderBanner } from './tabs/SettingsTab';
import ReconcileTab from './tabs/ReconcileTab';
import RunsTab from './tabs/RunsTab';
import PayeesTab from './tabs/PayeesTab';
import LedgerTab from './tabs/LedgerTab';
import AnomaliesTab from './tabs/AnomaliesTab';
import TestTransferTab from './tabs/TestTransferTab';

/** Trang Thu chi của Admin: cấu hình, đối soát chi, kỳ chi, người nhận, sổ cái, bất thường và hoàn tiền, chuyển thử. */
export default function MoneyAdminPage() {
  const [tab, setTab] = useState('settings');
  const [openRunId, setOpenRunId] = useState(null);
  const settingsQuery = useQuery({ queryKey: ['money-admin', 'settings'], queryFn: moneyAdminApi.getSettings });
  const settings = settingsQuery.data;
  const clearOpenRun = useCallback(() => setOpenRunId(null), []);
  // Huy hiệu số việc cần xử lý. Lỗi thì không hiện huy hiệu (không bịa số).
  const pendingBank = useQuery({ queryKey: ['money-admin', 'payees', 'pending-count'], queryFn: moneyAdminApi.pendingBankCount, refetchInterval: 60_000, retry: false });
  const openAnomalies = useQuery({ queryKey: ['money-admin', 'anomalies', 'open-count'], queryFn: moneyAdminApi.openAnomalyCount, refetchInterval: 60_000, retry: false });

  const TABS = [
    { key: 'settings', label: 'Cấu hình', icon: SlidersHorizontal },
    { key: 'reconcile', label: 'Đối soát chi', icon: Calculator },
    { key: 'runs', label: 'Kỳ chi', icon: ClipboardList },
    { key: 'payees', label: 'Người nhận', icon: Users, badge: pendingBank.data ?? 0 },
    { key: 'ledger', label: 'Khoản thu & sổ cái', icon: BookOpen },
    { key: 'anomalies', label: 'Bất thường & hoàn tiền', icon: TriangleAlert, badge: openAnomalies.data ?? 0 },
    { key: 'test', label: 'Chuyển thử', icon: FlaskConical },
  ];

  return (
    <AdminLayout crumb="Thu chi">
      <div className="flex w-full flex-col gap-6 p-4 md:p-6">
        <PageHeader icon={ArrowLeftRight} title="Thu chi" description="Sàn thu tiền, trừ hoa hồng rồi chi cho Seller, Chủ kho và Nhà máy; xử lý bất thường và hoàn tiền cho người trả." />
        {tab !== 'settings' && <ProviderBanner settings={settings} />}
        <FilterTabs tabs={TABS} value={tab} onChange={setTab} />
        {tab === 'settings' && <SettingsTab settings={settings} settingsQuery={settingsQuery} />}
        {tab === 'reconcile' && <ReconcileTab settings={settings} onOpenRun={(id) => { setOpenRunId(id); setTab('runs'); }} />}
        {tab === 'runs' && <RunsTab openRunId={openRunId} clearOpenRun={clearOpenRun} />}
        {tab === 'payees' && <PayeesTab />}
        {tab === 'ledger' && <LedgerTab onGoPayees={() => setTab('payees')} />}
        {tab === 'anomalies' && <AnomaliesTab openAnomalyCount={openAnomalies.data ?? 0} onGoPayees={() => setTab('payees')} />}
        {tab === 'test' && <TestTransferTab settings={settings} />}
      </div>
    </AdminLayout>
  );
}
