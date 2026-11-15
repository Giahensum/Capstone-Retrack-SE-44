import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import api from '@/lib/axios';
import { useDepot } from '../DepotContext';
export function useDispatchQuery(path, params = {}, enabled = true) {
  const { userId, depotId } = useDepot();
  return useQuery({ queryKey: ['depot', userId, depotId, 'dispatch', path, params], enabled: !!depotId && enabled, refetchInterval: 15000,
    queryFn: async () => (await api.get(`/depot/${depotId}/employee-dispatch/${path}`, { params })).data.data });
}
export function useDispatchMutation(method, path, onSuccess) {
  const { depotId } = useDepot(), client = useQueryClient();
  return useMutation({ mutationFn: async body => (await api.request({ method, url: `/depot/${depotId}/employee-dispatch/${path}`, data: body })).data.data,
    onSuccess: async () => { await client.invalidateQueries({ queryKey: ['depot'] }); onSuccess?.(); } });
}
export const dispatchTime = value => value ? new Date(value).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' }) : 'Chưa có lịch hẹn';
export const availability = { AVAILABLE: 'Sẵn sàng', FULL: 'Đã đầy / tạm dừng nhận', STOPPED: 'Đã ngừng ca' };
export const vehicleLabels = { motorcycle: 'Xe máy', car: 'Ô tô', truck: 'Xe tải' };
export const initialLocalTime = offset => new Date(Date.now() + (7 + offset) * 3600000).toISOString().slice(0, 16);
