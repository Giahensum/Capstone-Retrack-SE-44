const WEEKDAYS = ["CN", "Th 2", "Th 3", "Th 4", "Th 5", "Th 6", "Th 7"];

export function formatViDatetime(isoString) {
  if (!isoString) return "Chưa hẹn lịch";
  const date = new Date(isoString);
  if (Number.isNaN(date.getTime())) return "Chưa hẹn lịch";
  const pad = (n) => String(n).padStart(2, "0");
  return `${pad(date.getHours())}:${pad(date.getMinutes())} ${WEEKDAYS[date.getDay()]} ${pad(date.getDate())}/${pad(date.getMonth() + 1)}/${date.getFullYear()}`;
}

export function formatCurrency(amount) {
  if (amount == null || !Number.isFinite(Number(amount))) return "—";
  return new Intl.NumberFormat("vi-VN", {
    style: "currency",
    currency: "VND",
  }).format(Number(amount));
}
