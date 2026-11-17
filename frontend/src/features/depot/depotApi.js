import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import api from '@/lib/axios';
import { useDepot } from './DepotContext';

export function useDepotQuery(path, params = {}, enabled = true, refetchInterval = false) {
  const { depotId, userId } = useDepot();
  return useQuery({ queryKey: ['depot', userId, depotId, path, params], enabled: !!depotId && enabled, refetchInterval,
    queryFn: async ({ signal }) => (await api.get(`/depot/${path}`, { params: { depotId, ...params }, signal, timeout: 30000 })).data.data });
}
export function useDepotMutation(method, path, onSuccess) {
  const { depotId } = useDepot();
  const client = useQueryClient();
  return useMutation({ mutationFn: ({ body, id } = {}) => api.request({ method,
    url: `/depot/${typeof path === 'function' ? path(id, body) : path}`, params: { depotId }, data: body,
    timeout: body instanceof FormData ? 120000 : 60000,
    ...(body instanceof FormData ? { headers: { 'Content-Type': 'multipart/form-data' } } : {}) }),
    onSuccess: async () => { await client.invalidateQueries({ queryKey: ['depot'] }); onSuccess?.(); } });
}
export const money = (n) => Number(n ?? 0).toLocaleString('vi-VN') + ' đ';
export const number = (n) => Number(n ?? 0).toLocaleString('vi-VN');
export const date = (value) => new Date(value).toLocaleDateString('vi-VN');
export const batchLabels = { DRAFT: 'Nháp', LISTED: 'Đang đăng', MARKETPLACE: 'Đang đăng', PENDING_APPROVAL: 'Chờ nhà máy duyệt',
  TRANSPORT_READY: 'Chờ vận chuyển', ACCEPTED: 'Đã nhận', READY_FOR_PICKUP: 'Chờ lấy hàng', IN_PROGRESS: 'Đang vận chuyển', IN_TRANSIT: 'Đang vận chuyển',
  DELIVERED: 'Đã giao', RECEIVED: 'Nhà máy đã nhận', WEIGHED: 'Đã cân tại nhà máy', PENDING_FACTORY: 'Chờ nhà máy duyệt',
  VERIFIED: 'Đã QC', SETTLEMENT_PENDING: 'Chờ kho đồng ý giá', SETTLEMENT_DISPUTED: 'Kho yêu cầu điều chỉnh giá', SETTLEMENT_ACCEPTED: 'Đã đồng ý giá, chờ chuyển tiền', COMPLETED: 'Chờ xác nhận nhận tiền', PAID: 'Đã nhận tiền', NEGOTIATING: 'KCS không đạt, đang thương lượng', RETURN_REQUESTED: 'Chờ nhà máy bàn giao trả', RETURNING: 'Đang trả về kho', RETURNED: 'Đã nhận đủ lô trả', REJECTED: 'Bị từ chối', CANCELLED: 'Đã hủy' };
