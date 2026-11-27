export function pickupScheduleLabel(pickup) {
  if (pickup.preferredDatetime) {
    const format=value=>new Date(value).toLocaleString('vi-VN',{timeZone:'Asia/Ho_Chi_Minh'});
    return `${format(pickup.preferredDatetime)}${pickup.preferredEndDatetime ? ` → ${format(pickup.preferredEndDatetime)}` : ''}`;
  }
  return `${pickup.collectionDate || 'Trong ngày'} · Linh hoạt 07:00–19:00`;
}
export function pickupNotStarted(pickup,now=Date.now()) {
  return Boolean(pickup.preferredDatetime && new Date(pickup.preferredDatetime).getTime()>now);
}

export function pickupWindowClosed(pickup,now=Date.now()) {
  const end=pickup.preferredEndDatetime || (pickup.collectionDate ? `${pickup.collectionDate}T19:00:00+07:00` : null);
  return Boolean(end && new Date(end).getTime()<=now);
}
