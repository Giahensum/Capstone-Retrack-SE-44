export const ROUTE_VEHICLES = [
  { value: "car", label: "Ô tô" },
  { value: "motorbike", label: "Xe máy" },
];

export const ROUTE_MODES = [
  {
    value: "balanced",
    label: "Cân bằng",
    description: "Ít km nhất trong nhóm tuyến chậm hơn tuyến nhanh nhất không quá 15%.",
  },
  {
    value: "shortest",
    label: "Ít km nhất",
    description: "Ưu tiên quãng đường ngắn nhất, có thể mất nhiều thời gian hơn.",
  },
  {
    value: "fastest",
    label: "Nhanh nhất dự kiến",
    description: "Ưu tiên thời gian ước tính thấp nhất, có thể đi xa hơn.",
  },
];

export function formatRouteDistance(meters) {
  return meters < 1000
    ? `${Math.round(meters)} m`
    : `${(meters / 1000).toLocaleString("vi-VN", { maximumFractionDigits: 2 })} km`;
}

export function formatRouteDuration(seconds) {
  const minutes = Math.max(1, Math.ceil(seconds / 60));
  return minutes < 60
    ? `${minutes} phút`
    : `${Math.floor(minutes / 60)} giờ${minutes % 60 ? ` ${minutes % 60} phút` : ""}`;
}
