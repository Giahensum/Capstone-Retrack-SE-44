import { Link } from 'react-router-dom';
import { date, useDepotQuery } from '../depotApi';

export default function SubscriptionBanner() {
  const q = useDepotQuery('subscription');
  if (!q.data || q.data.status === 'DISABLED' || q.data.status === 'ACTIVE') return null;
  const label = { TRIAL: 'Đang dùng thử', NOT_STARTED: 'Chưa bắt đầu', EXPIRED: 'Đã hết hạn' }[q.data.status];
  return <div role="status" className="m-4 rounded-xl border border-d-border-subtle bg-white p-4 text-sm"><strong>Gói quản lý: {label}.</strong>{q.data.trialEndsAt && q.data.status === 'TRIAL' ? ` Dùng thử đến ${date(q.data.trialEndsAt)}.` : ' Giao dịch cũ và dữ liệu vẫn được giữ.'} <Link className="font-semibold text-d-primary underline" to="/depot/subscription">Xem gói / gia hạn</Link></div>;
}
