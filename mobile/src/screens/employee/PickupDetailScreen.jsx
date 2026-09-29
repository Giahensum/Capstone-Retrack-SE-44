import { useCallback, useEffect, useState } from "react";
import { Image, RefreshControl, Text, View } from "react-native";
import { useIsFocused } from "@react-navigation/native";
import Screen from "../../components/common/Screen";
import Button from "../../components/common/Button";
import PickupMap from "../../components/pickup/PickupMap";
import ResourceState from "../../components/pickup/ResourceState";
import usePickupResource from "../../hooks/usePickupResource";
import useAcceptPickup from "../../hooks/useAcceptPickup";
import useEmployeeLocation from "../../hooks/useEmployeeLocation";
import { pickupApi } from "../../api/pickupApi";
import { formatViDatetime } from "../../helpers/format";
import { coordinatesOf } from "../../helpers/coordinates";
import { getRoute } from "../../helpers/goongDirections";
import { callSeller, openSellerMaps } from "../../helpers/pickupLinks";
import { colors, styles as s } from "../../theme";

export default function PickupDetailScreen({ route }) {
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
  const [directions, setDirections] = useState(null);
  const [routeError, setRouteError] = useState("");
  const [routeLoading, setRouteLoading] = useState(false);
  const [retry, setRetry] = useState(0);
  const destination = coordinatesOf(pickup);
  const destLat = destination?.latitude,
    destLng = destination?.longitude;
  const originLat = gps.location?.latitude,
    originLng = gps.location?.longitude;
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
    const controller = new AbortController();
    // Clear the previous route before requesting directions for a new origin/destination.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setDirections(null);
    setRouteError("");
    setRouteLoading(false);
    if (
      !focused ||
      destLat == null ||
      destLng == null ||
      originLat == null ||
      originLng == null
    )
      return () => controller.abort();
    setRouteLoading(true);
    getRoute(originLat, originLng, destLat, destLng, controller.signal)
      .then((result) => {
        if (!controller.signal.aborted) setDirections(result);
      })
      .catch((e) => {
        if (!controller.signal.aborted)
          setRouteError(
            e?.response
              ? "Dịch vụ chỉ đường chưa phản hồi được. Hãy thử lại hoặc mở Google Maps."
              : e.message || "Không tải được tuyến đường.",
          );
      })
      .finally(() => {
        if (!controller.signal.aborted) setRouteLoading(false);
      });
    return () => controller.abort();
  }, [focused, destLat, destLng, originLat, originLng, retry]);
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
              {pickup.isAcceptedByMe ? "Bạn đã nhận đơn này" : "Đang chờ nhận"}
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
          <Text style={[s.title, { fontSize: 21 }]}>Bản đồ và tuyến đường</Text>
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
              <Text style={s.text}>Khoảng cách: {directions.distanceText}</Text>
              <Text style={s.text}>
                Thời gian ước tính: {directions.durationText}
              </Text>
              <Text style={s.muted}>
                Tuyến ô tô theo đường phố từ Goong; cập nhật vị trí để tính lại.
              </Text>
            </View>
          ) : null}
          <Button
            title="Mở trên Google Maps"
            variant="secondary"
            onPress={() => openSellerMaps(pickup)}
          />
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
