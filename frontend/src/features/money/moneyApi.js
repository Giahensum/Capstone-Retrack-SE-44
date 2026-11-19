import { useEffect, useRef } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import api from '@/lib/axios';
import { isAwaitingPayos, isPaid } from './moneyStatus';

const base = (flow, sourceId) => `/money/collections/${encodeURIComponent(flow)}/${encodeURIComponent(sourceId)}`;
export const collectionKey = (flow, sourceId) => ['money', 'collection', flow, sourceId];

export const fetchCollection = async (flow, sourceId) => (await api.get(base(flow, sourceId))).data.data;
// Trả về { checkoutUrl, orderCode }.
export const createPayosLink = async (flow, sourceId) => (await api.post(`${base(flow, sourceId)}/payos-link`)).data.data;
export const syncPayos = async (flow, sourceId) => (await api.post(`${base(flow, sourceId)}/payos-sync`)).data.data;

/** Khoản thu của một nguồn. Tự tải lại mỗi 10 giây khi đang chờ PayOS xác nhận (dừng khi gỡ component). */
export function useCollection(flow, sourceId, { enabled = true } = {}) {
  return useQuery({
    queryKey: collectionKey(flow, sourceId),
    queryFn: () => fetchCollection(flow, sourceId),
    enabled: Boolean(enabled && flow && sourceId),
    retry: false,
    refetchInterval: (query) => (isAwaitingPayos(query.state.data) ? 10000 : false),
  });
}

/** Khi khoản thu chuyển sang PAID, làm mới dữ liệu màn hình liên quan (đơn thu gom, lô, thanh lý). */
export function useInvalidateOnPaid(dto) {
  const client = useQueryClient();
  const wasPaid = useRef(null);
  useEffect(() => {
    if (!dto) return;
    const paid = isPaid(dto);
    if (wasPaid.current === false && paid) {
      for (const key of [['depot'], ['liquidation'], ['factory'], ['seller']]) client.invalidateQueries({ queryKey: key });
    }
    wasPaid.current = paid;
  }, [dto, client]);
}

export function useCollectionMutations(flow, sourceId) {
  const client = useQueryClient();
  const key = collectionKey(flow, sourceId);
  const link = useMutation({
    mutationFn: () => createPayosLink(flow, sourceId),
    onSuccess: () => client.invalidateQueries({ queryKey: key }),
  });
  const sync = useMutation({
    mutationFn: () => syncPayos(flow, sourceId),
    onSuccess: (dto) => { if (dto?.status) client.setQueryData(key, dto); },
  });
  return { link, sync };
}
