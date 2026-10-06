import { useCallback, useMemo, useState } from "react";
import { Linking, Text } from "react-native";
import Screen from "../../components/common/Screen";
import Button from "../../components/common/Button";
import PickupMap from "../../components/pickup/PickupMap";
import ResourceState from "../../components/pickup/ResourceState";
import usePickupResource from "../../hooks/usePickupResource";
import { driverApi } from "../../api/driverApi";
import { getRoutes } from "../../helpers/goongDirections";
import { coordinatesOf } from "../../helpers/coordinates";
import { selectRoute } from "../../helpers/routeSelection";
import { ROUTE_MODES } from "../../helpers/routePreferences";
import { styles as s } from "../../theme";
export default function MapScreen({ route }) {
  const id = route.params.jobId;
  const [mode, setMode] = useState("balanced"), [linkError, setLinkError] = useState("");
  const loader = useCallback(async signal => {
    const job = await driverApi.detail(id, signal);
    try {
      const routes = await getRoutes(job.depot.latitude, job.depot.longitude, job.factory.latitude, job.factory.longitude, { vehicle: "truck", signal });
      return { job, routes, routeError: "" };
    } catch { return { job, routes: [], routeError: "Chưa lấy được tuyến xe tải. Kiểm tra tọa độ, kết nối hoặc cấu hình Goong rồi thử lại." }; }
  }, [id]);
  const { data, loading, error, reload } = usePickupResource(loader);
  const markers = useMemo(() => data ? [
    { ...data.job.depot, id: "depot", sellerName: `Kho: ${data.job.depot.name}` },
    { ...data.job.factory, id: "factory", sellerName: `Nhà máy: ${data.job.factory.name}` },
  ] : [], [data]);
  const selected = selectRoute(data?.routes || [], mode);
  return <Screen>
    <Text style={s.title}>Kho → Nhà máy</Text>
    <ResourceState loading={loading} error={error} retry={() => reload()} />
    {data && !loading ? <>
      <Text style={s.text}>Chỉ đường xe tải · Goong</Text>
      <Text style={s.muted}>Xuất phát từ kho, không phải GPS điện thoại. Tuyến tham khảo; kiểm tra biển báo và giới hạn tải trọng thực tế.</Text>
      {ROUTE_MODES.map(m => <Button key={m.value} title={`${mode === m.value ? "✓ " : ""}${m.label}`} variant="secondary" onPress={() => setMode(m.value)} />)}
      <PickupMap pickups={markers} routePoints={selected?.coordinates || []} />
      {!coordinatesOf(data.job.depot) || !coordinatesOf(data.job.factory) ? <Text style={s.error}>Kho hoặc nhà máy chưa có tọa độ hợp lệ. Liên hệ bên quản lý cập nhật địa điểm.</Text> : null}
      {data.routeError ? <Text style={s.error}>{data.routeError}</Text> : null}
      {selected ? <Text style={s.text}>{(selected.distanceMeters / 1000).toFixed(2)} km · khoảng {Math.ceil(selected.durationSeconds / 60)} phút · {data.routes.length} tuyến trả về</Text> : null}
      <Button title="Tải lại tuyến đường" onPress={() => reload()} />
      <Button title="Mở Google Maps tham khảo" variant="secondary" onPress={() => {
        const point = place => { const c = coordinatesOf(place); return c ? `${c.latitude},${c.longitude}` : place.address; };
        setLinkError("");
        void Linking.openURL(`https://www.google.com/maps/dir/?api=1&origin=${encodeURIComponent(point(data.job.depot))}&destination=${encodeURIComponent(point(data.job.factory))}&travelmode=driving`).catch(() => setLinkError("Không mở được Google Maps."));
      }} />
      <Text style={s.muted}>Google Maps mở chế độ ô tô thông thường, không thay thế tuyến xe tải.</Text>
      {linkError ? <Text style={s.error}>{linkError}</Text> : null}
    </> : null}
  </Screen>;
}
