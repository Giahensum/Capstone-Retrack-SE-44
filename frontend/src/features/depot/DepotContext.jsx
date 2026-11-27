import { createContext, useContext, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useAuthStore } from '@/app/store/useAuthStore';
import api from '@/lib/axios';
import { readDepotSelection, saveDepotSelection } from './depotSelection';

const DepotContext = createContext(null);
export const useDepot = () => useContext(DepotContext);
export { depotError } from './depotError';
import { depotError } from './depotError';

export function DepotProvider({ children }) {
  const userId = useAuthStore((s) => s.user?.userId);
  const [selection, setSelection] = useState(() => ({ userId, id: readDepotSelection(userId) }));
  const selected = selection.userId === userId ? selection.id : readDepotSelection(userId);
  const query = useQuery({
    queryKey: ['depot', userId, 'owned'],
    queryFn: async () => (await api.get('/depot/owned')).data.data,
  });
  const depots = query.data ?? [];
  const depotId = depots.some((d) => d.id === selected) ? selected : depots[0]?.id;
  const setSelected = id => {
    if (!depots.some(d => d.id === id)) return;
    setSelection({ userId, id }); saveDepotSelection(userId, id);
  };
  return <DepotContext.Provider value={{ depotId, depots, setSelected, userId, query }}>{children}</DepotContext.Provider>;
}

export function DepotSelector() {
  const { depots, depotId, setSelected, query } = useDepot();
  if (query.isPending) return <span role="status">Đang tải kho…</span>;
  if (query.isError) return <button onClick={() => query.refetch()} role="alert">{depotError(query.error)} Thử lại</button>;
  if (!depots.length) return <span role="status">Tài khoản chưa có kho.</span>;
  return <select aria-label="Kho đang quản lý" className="min-w-0 max-w-36 sm:max-w-64 rounded-full border border-d-border-subtle bg-white px-3 py-2"
    value={depotId} onChange={(e) => setSelected(e.target.value)}>
    {depots.map((d) => <option value={d.id} key={d.id}>{d.name}</option>)}
  </select>;
}
