import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import api from '@/lib/axios';
import { useAuthStore } from '@/app/store/useAuthStore';
import { useDepot } from '@/features/depot/DepotContext';

export const liquidationLabels = { UPCOMING: 'Sắp mở', SURVEY: 'Đang khảo sát', BIDDING: 'Đang trả giá', CLOSING: 'Đang chốt kết quả', AWARDED: 'Đã có người trúng', NO_SALE: 'Kết thúc, chưa bán', CANCELLED: 'Đã hủy' };
export const localTime = value => value ? new Date(value).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' }) : 'Chưa xác nhận';
export function useLiquidationScope() {
  const user = useAuthStore(s => s.user);
  const depot = useDepot();
  return { userId: user?.userId, depotId: depot?.depotId, base: '/depot/liquidations' };
}
export function useLiquidationQuery(path = '', params = {}, enabled = true, poll = false) {
  const scope = useLiquidationScope();
  return useQuery({ queryKey: ['liquidation', scope.userId, scope.depotId, path, params],
    enabled: enabled && !!scope.userId && !!scope.depotId, refetchInterval: poll ? 5000 : false,
    queryFn: async () => (await api.get(`${scope.base}${path}`, { params: { ...params, depotId: scope.depotId } })).data.data });
}
export function useLiquidationMutation(method, path, onSuccess) {
  const scope = useLiquidationScope();
  const client = useQueryClient();
  return useMutation({ mutationKey: ['liquidation', scope.userId, scope.depotId, path], mutationFn: async body => (await api.request({ method, url: `${scope.base}${path}`,
    params: { depotId: scope.depotId }, data: body,
    ...(body instanceof FormData ? { headers: { 'Content-Type': 'multipart/form-data' } } : {}) })).data.data,
    onSuccess: async value => { await client.invalidateQueries({ queryKey: ['liquidation'] }); onSuccess?.(value); } });
}
