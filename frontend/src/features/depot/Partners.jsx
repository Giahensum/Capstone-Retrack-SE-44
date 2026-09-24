import React, { useCallback, useEffect, useState } from 'react';
import { MaterialIcon } from '../../components/ui/MaterialIcon';
import { useAuthStore } from '../../app/store/useAuthStore';

const Partners = () => {
  const [partnerships, setPartnerships] = useState([]);
  const [loadError, setLoadError] = useState('');
  const [busyFactoryId, setBusyFactoryId] = useState('');

  const loadPartnerships = useCallback(async () => {
    try {
      const response = await fetch('/api/depot/partnerships', {
        headers: { Authorization: `Bearer ${useAuthStore.getState().token || ''}` },
      });
      const payload = await response.json();
      if (!response.ok || payload.success === false) throw new Error(payload.message || 'Không tải được yêu cầu hợp tác.');
      setPartnerships(payload.data || []);
      setLoadError('');
    } catch (error) {
      setLoadError(error.message || 'Không tải được yêu cầu hợp tác.');
    }
  }, []);

  useEffect(() => { loadPartnerships(); }, [loadPartnerships]);

  const updatePartnership = async (factoryId, status) => {
    setBusyFactoryId(factoryId);
    try {
      const response = await fetch(`/api/depot/partnerships/${factoryId}/status`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${useAuthStore.getState().token || ''}` },
        body: JSON.stringify({ status }),
      });
      const payload = await response.json();
      if (!response.ok || payload.success === false) throw new Error(payload.message || 'Không cập nhật được yêu cầu.');
      await loadPartnerships();
    } catch (error) {
      setLoadError(error.message || 'Không cập nhật được yêu cầu.');
    } finally {
      setBusyFactoryId('');
    }
  };

  const pendingCount = partnerships.filter((item) => item.status === 'PENDING').length;
  const approvedCount = partnerships.filter((item) => item.status === 'APPROVED').length;

  return (
    <div className="flex flex-col p-4 md:p-6 w-full min-h-[calc(100vh-4rem)] gap-6 bg-d-surface">
      <div>
        <h2 className="font-d-headline-lg text-d-headline-lg text-d-on-surface mb-2">Đối tác nhà máy</h2>
        <p className="font-d-body-md text-d-body-md text-d-on-surface-variant">Xem và duyệt yêu cầu hợp tác do các nhà máy gửi.</p>
      </div>

      {loadError && <div role="alert" className="rounded-xl border border-red-300 bg-red-50 p-4 text-red-800">{loadError}</div>}

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        {[['Tổng yêu cầu', partnerships.length], ['Đang hợp tác', approvedCount], ['Chờ duyệt', pendingCount]].map(([label, value]) => (
          <div key={label} className="bg-d-surface-container-low border border-d-border-subtle rounded-[20px] p-5">
            <p className="font-d-label-sm text-d-on-surface-variant uppercase tracking-wider mb-2">{label}</p>
            <strong className="font-d-headline-xl text-d-on-surface">{value}</strong>
          </div>
        ))}
      </div>

      <div className="bg-d-surface-container-lowest border border-d-border-subtle rounded-[20px] overflow-x-auto">
        <table className="w-full text-left min-w-[760px]">
          <thead className="bg-d-surface-container-low">
            <tr>
              <th className="py-4 px-6">Nhà máy</th>
              <th className="py-4 px-6">Ngày gửi</th>
              <th className="py-4 px-6">Trạng thái</th>
              <th className="py-4 px-6 text-right">Thao tác</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-d-border-subtle bg-white/50">
            {partnerships.map((partnership) => (
              <tr key={partnership.id}>
                <td className="py-4 px-6">
                  <div className="flex items-center gap-3"><MaterialIcon name="factory" /><div><p className="font-medium">{partnership.factoryName}</p><p className="text-sm text-d-on-surface-variant">{partnership.contactPhone}</p></div></div>
                </td>
                <td className="py-4 px-6">{new Date(partnership.createdAt).toLocaleDateString('vi-VN')}</td>
                <td className="py-4 px-6">{partnership.status === 'PENDING' ? 'Chờ vựa duyệt' : partnership.status === 'APPROVED' ? 'Đã duyệt' : 'Đã chặn'}</td>
                <td className="py-4 px-6 text-right">
                  {partnership.status === 'PENDING' ? <div className="flex justify-end gap-2"><button disabled={busyFactoryId === partnership.factoryId} onClick={() => updatePartnership(partnership.factoryId, 'APPROVED')} className="px-4 py-2 rounded-full bg-d-primary text-white disabled:opacity-50">Duyệt</button><button disabled={busyFactoryId === partnership.factoryId} onClick={() => updatePartnership(partnership.factoryId, 'BLOCKED')} className="px-4 py-2 rounded-full border border-d-outline disabled:opacity-50">Từ chối</button></div> : '—'}
                </td>
              </tr>
            ))}
            {!partnerships.length && <tr><td colSpan={4} className="py-12 text-center text-d-on-surface-variant">Chưa có yêu cầu hợp tác.</td></tr>}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default Partners;
