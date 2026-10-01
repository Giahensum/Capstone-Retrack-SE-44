import { useCallback, useEffect, useRef, useState } from "react";
import * as Location from "expo-location";

export default function useEmployeeLocation(enabled = true) {
  const [location, setLocation] = useState(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const generation = useRef(0);
  const refresh = useCallback(async () => {
    const request = ++generation.current;
    let timer;
    setLoading(true);
    setError("");
    setLocation(null);
    try {
      const permission = await Location.requestForegroundPermissionsAsync();
      if (request !== generation.current) return;
      if (permission.status !== "granted")
        throw new Error(
          "Chưa được cấp quyền vị trí. Bạn vẫn có thể xem và nhận đơn; cấp quyền trong cài đặt để tìm đường từ vị trí hiện tại.",
        );
      if (!(await Location.hasServicesEnabledAsync()))
        throw new Error(
          "Hãy bật dịch vụ vị trí/GPS trên thiết bị rồi thử lại.",
        );
      if (request !== generation.current) return;
      const position = await Promise.race([
        Location.getCurrentPositionAsync({
          accuracy: Location.Accuracy.Balanced,
        }),
        new Promise((_, reject) => {
          timer = setTimeout(
            () =>
              reject(
                new Error(
                  "Chưa lấy được vị trí GPS. Vui lòng kiểm tra dịch vụ vị trí và thử lại.",
                ),
              ),
            15000,
          );
        }),
      ]);
      if (request === generation.current) setLocation(position.coords);
    } catch (e) {
      if (request === generation.current)
        setError(e instanceof Error ? e.message : "Không lấy được vị trí.");
    } finally {
      clearTimeout(timer);
      if (request === generation.current) setLoading(false);
    }
  }, []);
  const cancel = useCallback(() => {
    generation.current++;
  }, []);
  useEffect(() => {
    // Synchronize GPS permission/request state when the map becomes visible.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (enabled) void refresh();
    return cancel;
  }, [enabled, refresh, cancel]);
  return { location, error, loading, refresh };
}
