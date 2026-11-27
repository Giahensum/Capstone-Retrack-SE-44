import { useQuery } from '@tanstack/react-query';
import api from '@/lib/axios';

/**
 * Chế độ kinh doanh hiện hành: true khi RETRACK chỉ bán gói quản lý Depot (không phí/hoa hồng/thu hộ giao dịch mới).
 * Dùng để khóa các cấu hình chỉ còn ý nghĩa với dữ liệu cũ; server vẫn tự chặn nên đây chỉ là hiển thị rõ ràng.
 */
export function useSubscriptionMode() {
  const q = useQuery({ queryKey: ['billing-model'], staleTime: 5 * 60 * 1000,
    queryFn: async () => (await api.get('/billing/model')).data.data === true });
  return { subscriptionOnly: q.data === true, loading: q.isLoading };
}
