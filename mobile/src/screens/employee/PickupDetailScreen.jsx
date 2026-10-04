import { useCallback, useEffect, useMemo, useState } from "react";
import { Image, RefreshControl, Text, View } from "react-native";
import { useIsFocused } from "@react-navigation/native";
import Screen from "../../components/common/Screen";
import Button from "../../components/common/Button";
import PickupMap from "../../components/pickup/PickupMap";
import ResourceState from "../../components/pickup/ResourceState";
import RouteOptions from "../../components/pickup/RouteOptions";
import usePickupResource from "../../hooks/usePickupResource";
import useAcceptPickup from "../../hooks/useAcceptPickup";
import useEmployeeLocation from "../../hooks/useEmployeeLocation";
import { pickupApi } from "../../api/pickupApi";
import { formatViDatetime } from "../../helpers/format";
import { coordinatesOf } from "../../helpers/coordinates";
import { getRoutes } from "../../helpers/goongDirections";
import { selectRoute } from "../../helpers/routeSelection";
import {
  ROUTE_MODES,
  ROUTE_VEHICLES,
  formatRouteDistance,
  formatRouteDuration,
} from "../../helpers/routePreferences";
import { callSeller, openSellerMaps } from "../../helpers/pickupLinks";
import { colors, styles as s } from "../../theme";
import { pickupStatus } from "../../helpers/collection";

export default function PickupDetailScreen({ route, navigation }) {
  const id = route.params?.pickupId || route.params?.pickup?.id;
  const loader = useCallback((signal) => pickupApi.getPickup(id, signal), [id]);
  const {
    data: pickup,
    setData,
    loading,
    refreshing,
    error,
    reload,
  } = usePickupResource(loader);
  const focused = useIsFocused();
  const gps = useEmployeeLocation(focused);
  const [vehicle, setVehicle] = useState(null);
  const [mode, setMode] = useState("balanced");
  const [routeResult, setRouteResult] = useState(null);
  const [retry, setRetry] = useState(0);
  const destination = coordinatesOf(pickup);
  const destLat = destination?.latitude,
    destLng = destination?.longitude;
  const originLat = gps.location?.latitude,
    originLng = gps.location?.longitude;
  // Match results to the complete request, so an old car route cannot appear
  // under the motorcycle label, even before the effect cleans up its request.
  const requestKey = focused && vehicle && destination && gps.location
    ? JSON.stringify([id, originLat, originLng, destLat, destLng, vehicle, retry])
    : null;
  const currentResult = requestKey && routeResult?.key === requestKey ? routeResult : null;
  const routeLoading = Boolean(requestKey && !currentResult);
  const routeError = currentResult?.error;
  const directions = useMemo(
    () => selectRoute(currentResult?.routes, mode),
    [currentResult, mode],
  );
  const vehicleLabel = ROUTE_VEHICLES.find((option) => option.value === vehicle)?.label;
  const modeLabel = ROUTE_MODES.find((option) => option.value === mode)?.label;
  const { acceptingId, confirm } = useAcceptPickup(
    () => {
      setData((previous) => ({
        ...previous,
        status: "SCHEDULED",
        isAcceptedByMe: true,
      }));
      void reload(true);
    },
    () => {
      void reload(true);
    },
  );
  useEffect(() => {
    if (!requestKey) return;
    const controller = new AbortController();
    getRoutes(originLat, originLng, destLat, destLng, { vehicle, signal: controller.signal })
      .then((routes) => {
        if (!controller.signal.aborted)
          setRouteResult({ key: requestKey, routes, error: "" });
      })
      .catch((e) => {
        if (!controller.signal.aborted)
          setRouteResult({
            key: requestKey,
            routes: [],
            error: e?.response
              ? "Dịch vụ chỉ đường chưa phản hồi được. Hãy thử lại hoặc mở Google Maps."
              : e.message || "Không tải được tuyến đường.",
          });
      });
    return () => controller.abort();
  }, [requestKey, destLat, destLng, originLat, originLng, vehicle]);
  return (
    <Screen
      refreshControl={
        <RefreshControl
          refreshing={refreshing}
          onRefresh={() => reload(true)}
          colors={[colors.primary]}
        />
      }
    >
      <ResourceState loading={loading} error={error} retry={() => reload()} />
      {!loading && pickup ? (
        <>
          <Text style={s.title}>Thông tin đơn</Text>
          <View style={s.card}>
            <Text
              style={[s.text, { fontFamily: "Inter_700Bold", fontSize: 20 }]}
            >
              {pickup.sellerName}
            </Text>
            <Text style={s.text}>📍 {pickup.address}</Text>
            <Text selectable style={s.text}>
              SĐT: {pickup.sellerPhone || "Chưa cập nhật"}
            </Text>
            {pickup.sellerPhone ? (
              <Button
                title="Gọi người bán"
                variant="secondary"
                onPress={() => callSeller(pickup.sellerPhone)}
              />
            ) : null}
            <Text style={s.muted}>
              Hẹn lịch: {formatViDatetime(pickup.preferredDatetime)}
            </Text>
            <Text style={s.text}>{pickup.description || "Chưa có mô tả."}</Text>
            <Text style={[s.label, { color: colors.primary }]}>
              {pickupStatus(pickup.status)}
            </Text>
            {pickup.requestImageUrl ? (
              <Image
                source={{ uri: pickup.requestImageUrl }}
                accessibilityLabel="Ảnh phế liệu người bán cung cấp"
                style={{ width: "100%", height: 220, borderRadius: 16 }}
                resizeMode="contain"
              />
            ) : null}
          </View>
          {pickup.isAcceptedByMe ? <View style={s.card}>
            <Button title={pickup.status === "SCHEDULED" ? "Check-in tại địa điểm" : "Xem bằng chứng check-in"}
              onPress={() => navigation.navigate("CheckIn", { pickupId: id })} />
            {pickup.status !== "SCHEDULED" ? <Button title={pickup.status === "IN_PROGRESS" ? "Phân loại, cân và định giá" : "Xem kết quả cân"}
              onPress={() => navigation.navigate("ClassifyWeigh", { pickupId: id })} /> : null}
          </View> : null}
          <Text style={[s.title, { fontSize: 21 }]}>Bản đồ và tuyến đường</Text>
          <RouteOptions
            vehicle={vehicle}
            mode={mode}
            onVehicleChange={setVehicle}
            onModeChange={setMode}
          />
          {destination ? (
            <PickupMap
              pickups={[pickup]}
              employee={gps.location}
              routePoints={directions?.coordinates || []}
            />
          ) : (
            <Text style={s.muted}>
              Đơn chưa có tọa độ hợp lệ. Bạn có thể tìm địa chỉ bằng Google
              Maps.
            </Text>
          )}
          {gps.error ? <Text style={s.error}>{gps.error}</Text> : null}
          <Button
            title="Cập nhật vị trí của tôi"
            variant="secondary"
            loading={gps.loading}
            onPress={gps.refresh}
          />
          {routeLoading ? (
            <Text style={s.muted}>Đang tìm tuyến đường…</Text>
          ) : null}
          {routeError ? (
            <View style={s.card}>
              <Text style={s.error}>{routeError}</Text>
              <Button
                title="Thử lại chỉ đường"
                variant="secondary"
                onPress={() => setRetry((n) => n + 1)}
              />
            </View>
          ) : null}
          {directions ? (
            <View style={s.card}>
              <Text style={[s.label, { color: colors.primary }]}>
                {vehicleLabel} · {modeLabel}
              </Text>
              <Text style={s.text}>Khoảng cách: {formatRouteDistance(directions.distanceMeters)}</Text>
              <Text style={s.text}>
                Thời gian ước tính: {formatRouteDuration(directions.durationSeconds)}
              </Text>
              <Text style={s.muted}>
                {currentResult.routes.length === 1
                  ? "Goong trả về 1 tuyến hợp lệ; các ưu tiên đang dùng chung tuyến này."
                  : `Đã so sánh ${currentResult.routes.length} tuyến Goong trả về theo ưu tiên của bạn.`}
              </Text>
              <Text style={s.muted}>
                Thời gian mang tính ước tính, chưa xác nhận tình trạng kẹt xe hiện tại.
              </Text>
            </View>
          ) : null}
          <Button
            title="Mở trên Google Maps"
            variant="secondary"
            disabled={!vehicle}
            onPress={() => openSellerMaps(pickup, vehicle)}
          />
          <Text style={s.muted}>
            Google Maps sẽ tự tính tuyến cho phương tiện đã chọn; tuyến có thể khác đề xuất ở đây.
          </Text>
          {!pickup.isAcceptedByMe && pickup.status === "PENDING" ? (
            <Button
              title="Nhận đơn này"
              loading={acceptingId === pickup.id}
              onPress={() => confirm(pickup)}
            />
          ) : null}
        </>
      ) : null}
    </Screen>
  );
}
