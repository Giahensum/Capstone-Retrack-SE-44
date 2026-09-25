import { useCallback, useEffect, useState } from 'react';
import { DashboardLayout } from '@/components/layout/DashboardLayout';
import { StatCard } from '@/components/ui/CommonUI';
import { Truck, CheckCircle, MapPin, PackageCheck, LoaderCircle } from 'lucide-react';
import { useAuthStore } from '@/app/store/useAuthStore';

const token = () => useAuthStore.getState().token || '';

export default function DriverDashboard() {
  const [jobs, setJobs] = useState([]);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState('');

  const loadJobs = useCallback(async () => {
    try {
      const response = await fetch('/api/driver/transport', { headers: { Authorization: `Bearer ${token()}` } });
      const result = await response.json();
      if (!response.ok || result.success === false) throw new Error(result.message || 'Không tải được chuyến xe.');
      setJobs(result.data || []);
      setError('');
    } catch (e) {
      setError(e.message || 'Không tải được chuyến xe.');
    }
  }, []);

  useEffect(() => { loadJobs(); }, [loadJobs]);

  const act = async (job, action) => {
    setBusy(job.id);
    try {
      const response = await fetch(`/api/driver/transport/${job.id}/${action}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token()}` },
        body: JSON.stringify({ imageUrl: null }),
      });
      const result = await response.json();
      if (!response.ok || result.success === false) throw new Error(result.message || 'Không cập nhật được chuyến xe.');
      await loadJobs();
    } catch (e) {
      setError(e.message || 'Không cập nhật được chuyến xe.');
    } finally {
      setBusy('');
    }
  };

  const pending = jobs.filter((job) => ['PENDING', 'ACCEPTED', 'PICKED_UP', 'IN_TRANSIT', 'ON_THE_WAY'].includes(job.status)).length;
  const completed = jobs.filter((job) => job.status === 'DELIVERED').length;

  return (
    <DashboardLayout title="Dashboard Tài Xế">
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 mb-8">
        <StatCard title="Chuyến đang xử lý" value={String(pending)} icon={<Truck size={20} />} color="orange" />
        <StatCard title="Đã giao" value={String(completed)} icon={<CheckCircle size={20} />} color="emerald" />
      </div>
      {error && <p role="alert" className="mb-4 rounded-lg border border-red-800 bg-red-950 p-3 text-red-200">{error}</p>}
      <div className="space-y-4">
        {jobs.map((job) => (
          <article key={job.id} className="rounded-xl border border-slate-800 bg-slate-900 p-5 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <h2 className="font-semibold text-white">Lô {job.batchId.slice(0, 8).toUpperCase()} · {job.materialType}</h2>
              <p className="mt-1 text-sm text-slate-300"><MapPin size={14} className="mr-1 inline" />{job.depotName} · {job.depotAddress}</p>
              <p className="mt-1 text-sm text-slate-400">{Number(job.weightKg).toLocaleString('vi-VN')} kg · Trạng thái: {job.status}</p>
            </div>
            <div className="flex flex-wrap gap-2">
              {job.status === 'PENDING' && <button disabled={busy === job.id} onClick={() => act(job, 'accept')} className="rounded-lg bg-blue-600 px-4 py-2 text-white disabled:opacity-50"><PackageCheck size={16} className="mr-2 inline" />Nhận chuyến</button>}
              {job.status === 'ACCEPTED' && <button disabled={busy === job.id} onClick={() => act(job, 'pickup')} className="rounded-lg bg-amber-600 px-4 py-2 text-white disabled:opacity-50">Xác nhận lấy hàng</button>}
              {['PICKED_UP', 'IN_TRANSIT', 'ON_THE_WAY'].includes(job.status) && <button disabled={busy === job.id} onClick={() => act(job, 'deliver')} className="rounded-lg bg-emerald-600 px-4 py-2 text-white disabled:opacity-50">Xác nhận đã giao</button>}
              {busy === job.id && <LoaderCircle className="animate-spin self-center text-slate-300" size={20} />}
              {job.status === 'DELIVERED' && <span className="self-center text-emerald-300">Đã giao cho nhà máy</span>}
            </div>
          </article>
        ))}
        {!jobs.length && <div className="rounded-xl border border-dashed border-slate-700 p-10 text-center text-slate-400">Hiện chưa có chuyến vận chuyển được giao.</div>}
      </div>
    </DashboardLayout>
  );
}
