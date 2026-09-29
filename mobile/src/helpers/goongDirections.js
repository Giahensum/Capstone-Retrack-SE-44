import axios from "axios";
import { coordinatesOf, decodePolyline } from "./coordinates";

export async function getRoute(
  originLat,
  originLng,
  destLat,
  destLng,
  signal = undefined,
) {
  const key = process.env.EXPO_PUBLIC_GOONG_API_KEY?.trim();
  if (!key || key.startsWith("your_"))
    throw new Error(
      "Chưa cấu hình dịch vụ chỉ đường. Bạn có thể mở Google Maps bên dưới.",
    );
  if (
    !coordinatesOf({ latitude: originLat, longitude: originLng }) ||
    !coordinatesOf({ latitude: destLat, longitude: destLng })
  ) {
    throw new Error("Chưa có tọa độ hợp lệ để tìm đường.");
  }
  // Separate client: never send the ReTrack JWT to a third-party map provider.
  const { data } = await axios.get("https://rsapi.goong.io/Direction", {
    params: {
      origin: `${originLat},${originLng}`,
      destination: `${destLat},${destLng}`,
      vehicle: "car",
      api_key: key,
    },
    timeout: 15000,
    signal,
  });
  const route = data.routes?.[0];
  const leg = route?.legs?.[0];
  if (!leg || !route?.overview_polyline?.points)
    throw new Error("Không tìm được tuyến đường. Hãy thử Google Maps.");
  return {
    distanceText: leg.distance?.text || "—",
    durationText: leg.duration?.text || "—",
    polylineEncoded: route.overview_polyline.points,
    coordinates: decodePolyline(route.overview_polyline.points),
  };
}
