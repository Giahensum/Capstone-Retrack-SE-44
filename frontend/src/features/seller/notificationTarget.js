// Chỉ tạo đường dẫn nội bộ từ loại đích được hỗ trợ.
export function sellerNotificationTarget(notice) {
  const isId = (id) => typeof id === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id);
  if (notice.targetType === 'LIQUIDATION' && isId(notice.targetId)) {
    return `/seller/liquidations/${notice.targetId}`;
  }
  const pickupId = notice.pickupRequestId || (notice.targetType === 'PICKUP_REQUEST' ? notice.targetId : null);
  return isId(pickupId) ? `/seller/requests/${pickupId}` : null;
}
