import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import api from '@/lib/axios';
import { useDepot } from './DepotContext';

export function useDepotQuery(path, params = {}, enabled = true) {
  const { depotId, userId } = useDepot();
  return useQuery({ queryKey: ['depot', userId, depotId, path, params], enabled: !!depotId && enabled,
    queryFn: async () => (await api.get(`/depot/${path}`, { params: { depotId, ...params } })).data.data });
}
export function useDepotMutation(method, path, onSuccess) {
  const { depotId } = useDepot();
  const client = useQueryClient();
  return useMutation({ mutationFn: ({ body, id } = {}) => api.request({ method,
    url: `/depot/${typeof path === 'function' ? path(id, body) : path}`, params: { depotId }, data: body,
    ...(body instanceof FormData ? { headers: { 'Content-Type': 'multipart/form-data' } } : {}) }),
    onSuccess: async () => { await client.invalidateQueries({ queryKey: ['depot'] }); onSuccess?.(); } });
}
export const money = (n) => Number(n ?? 0).toLocaleString('vi-VN') + ' đ';
export const number = (n) => Number(n ?? 0).toLocaleString('vi-VN');
export const date = (value) => new Date(value).toLocaleDateString('vi-VN');
export const batchLabels = { DRAFT: 'Nháp', LISTED: 'Đang đăng', MARKETPLACE: 'Đang đăng', PENDING_APPROVAL: 'Chờ nhà máy duyệt',
  TRANSPORT_READY: 'Chờ vận chuyển', ACCEPTED: 'Đã nhận', READY_FOR_PICKUP: 'Chờ lấy hàng', IN_PROGRESS: 'Đang vận chuyển', IN_TRANSIT: 'Đang vận chuyển',
  DELIVERED: 'Đã giao', RECEIVED: 'Nhà máy đã nhận', WEIGHED: 'Đã cân tại nhà máy', PENDING_FACTORY: 'Chờ nhà máy duyệt',
  VERIFIED: 'Đã QC', COMPLETED: 'Hoàn tất', PAID: 'Đã quyết toán', REJECTED: 'Bị từ chối', CANCELLED: 'Đã hủy' };
