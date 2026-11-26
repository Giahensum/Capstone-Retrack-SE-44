import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import api from '@/lib/axios';
import { useAuthStore } from '@/app/store/useAuthStore';
import { useDepot } from '@/features/depot/DepotContext';

// Phiên thanh lý nhiều hạng mục (V2): cùng Axios/React Query/auth store hiện hành, phạm vi theo kho đang chọn.
const base = '/depot/liquidations/v2';
export const UNIT_LABELS = { kg: 'kg', cai: 'cái', bo: 'bộ' };
export const unitLabel = (unit) => UNIT_LABELS[unit] ?? unit;
export const ITEM_STATUS = { OPEN: 'Đang mở', WON: 'Đã có kho thắng', UNSOLD: 'Không bán được' };
export const AGREEMENT_STAGE = {
  WAITING_BUYER_PAYMENT: 'Chờ kho thanh toán', WAITING_SELLER_CONFIRM: 'Chờ Seller xác nhận tiền', WAITING_HANDOVER: 'Chờ Seller bàn giao',
  WAITING_OWNER_RECEIVE: 'Chờ kho xác nhận nhận hàng', COMPLETED: 'Đã nhận hàng', DISPUTED: 'Chờ Admin xử lý báo bỏ mua', CANCELLED: 'Admin đã hủy',
};
export const quantity = (value, unit) => `${Number(value ?? 0).toLocaleString('vi-VN', { maximumFractionDigits: 3 })} ${unitLabel(unit)}`;

// randomUUID chỉ có trên ngữ cảnh an toàn (https/localhost); dự phòng cho http trong mạng nội bộ.
export const newOperationId = () => (globalThis.crypto?.randomUUID ? globalThis.crypto.randomUUID()
  : 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => { const r = (Math.random() * 16) | 0; return (c === 'x' ? r : (r & 0x3) | 0x8).toString(16); }));

export function useV2Scope() {
  const user = useAuthStore((s) => s.user);
  const depot = useDepot();
  return { userId: user?.userId, depotId: depot?.depotId };
}

export function useV2Query(path = '', params = {}, { enabled = true, poll = false } = {}) {
  const scope = useV2Scope();
  return useQuery({
    queryKey: ['liquidation-v2', scope.userId, scope.depotId, path, params],
    enabled: enabled && !!scope.userId && !!scope.depotId,
    refetchInterval: poll ? 5000 : false, refetchOnWindowFocus: true, refetchOnReconnect: true,
    queryFn: async ({ signal }) => (await api.get(`${base}${path}`, { params: { ...params, depotId: scope.depotId }, signal, timeout: 30000 })).data.data,
  });
}

export function useV2Mutation(method, path, onSuccess, key = path) {
  const scope = useV2Scope();
  const client = useQueryClient();
  return useMutation({
    mutationKey: ['liquidation-v2', scope.userId, scope.depotId, key],
    mutationFn: async (body) => (await api.request({ method, url: `${base}${typeof path === 'function' ? path(body) : path}`,
      params: { depotId: scope.depotId }, data: body?.payload ?? body })).data.data,
    onSuccess: async (value, variables) => {
      await client.invalidateQueries({ queryKey: ['liquidation-v2'] });
      await client.invalidateQueries({ queryKey: ['depot'] });
      onSuccess?.(value, variables);
    },
  });
}
