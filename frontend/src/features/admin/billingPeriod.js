// Kỳ hóa đơn theo UTC+7, không phụ thuộc múi giờ máy người quản trị.
export function previousClosedBillingMonth(now = new Date()) {
  const vietnam = new Date(now.getTime() + 7 * 60 * 60 * 1000);
  const previous = new Date(Date.UTC(vietnam.getUTCFullYear(), vietnam.getUTCMonth() - 1, 1));
  return { year: previous.getUTCFullYear(), month: previous.getUTCMonth() + 1 };
}

export function isClosedBillingPeriod(period, now = new Date()) {
  if (!Number.isInteger(period.year) || period.year < 1900 || period.year > 9998 ||
      !Number.isInteger(period.month) || period.month < 1 || period.month > 12) return false;
  return Date.UTC(period.year, period.month, 1) - 7 * 60 * 60 * 1000 <= now.getTime();
}
