import axios from "axios";
import { coordinatesOf, decodePolyline } from "./coordinates.js";
import { normalizeRoutes } from "./routeSelection.js";

// The official maps.goong.io motorcycle option uses the legacy Direction API's
// "bike" profile. "motorbike" is our UI value, not a Goong API parameter.
const GOONG_VEHICLES = { car: "car", motorbike: "bike", truck: "truck" };

export async function getRoutes(
  originLat,
  originLng,
  destLat,
  destLng,
  { vehicle = "car", signal = undefined } = {},
) {
  if (!Object.hasOwn(GOONG_VEHICLES, vehicle))
    throw new Error("Phương tiện chỉ đường không hợp lệ.");
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
      vehicle: GOONG_VEHICLES[vehicle],
      alternatives: true,
      api_key: key,
    },
    timeout: 15000,
    signal,
  });
  const routes = normalizeRoutes(data?.routes);
  if (!routes.length)
    throw new Error("Không tìm được tuyến đường. Hãy thử Google Maps.");
  return routes.map((route) => ({
    ...route,
    coordinates: decodePolyline(route.polylineEncoded),
  }));
}
