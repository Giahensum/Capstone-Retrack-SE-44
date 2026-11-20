import { useState } from 'react';
import { FilterTabs } from '@/features/admin/components/AdminUI';
import AnomaliesPanel from './AnomaliesPanel';
import RefundsPanel from './RefundsPanel';

/** Tab "Bất thường & hoàn tiền": hai chế độ xem Bất thường và Hoàn tiền. */
export default function AnomaliesTab({ openAnomalyCount, onGoPayees }) {
  const [view, setView] = useState('anomalies');
  return (
    <div className="flex flex-col gap-4">
      <FilterTabs value={view} onChange={setView} tabs={[
        { key: 'anomalies', label: 'Bất thường', badge: openAnomalyCount },
        { key: 'refunds', label: 'Hoàn tiền' },
      ]} />
      {view === 'anomalies' ? <AnomaliesPanel onGoPayees={onGoPayees} /> : <RefundsPanel onGoPayees={onGoPayees} />}
    </div>
  );
}
