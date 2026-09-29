import { useEffect, useMemo, useRef, useState } from "react";
import { Text, View } from "react-native";
import MapView, { Marker, Polyline, PROVIDER_GOOGLE } from "react-native-maps";
import Constants from "expo-constants";
import { coordinatesOf } from "../../helpers/coordinates";
import { colors, styles as s } from "../../theme";

export default function PickupMap({
  pickups,
  employee = null,
  routePoints = [],
  onSelect = undefined,
  onAccept = undefined,
}) {
  const map = useRef(null);
  const [ready, setReady] = useState(false);
  const [laidOut, setLaidOut] = useState(false);
  const markers = useMemo(
    () =>
      pickups
        .map((pickup) => ({ pickup, point: coordinatesOf(pickup) }))
        .filter((item) => item.point),
    [pickups],
  );
  const points = useMemo(
    () => [
      ...markers.map((m) => m.point),
      ...(employee ? [employee] : []),
      ...routePoints,
    ],
    [markers, employee, routePoints],
  );
  const configured =
    Constants.executionEnvironment === "storeClient" ||
    Constants.expoConfig?.extra?.googleMapsConfigured;
  useEffect(() => {
    if (ready && laidOut && points.length)
      map.current?.fitToCoordinates(points, {
        edgePadding: { top: 65, bottom: 65, left: 45, right: 45 },
        animated: true,
      });
  }, [ready, laidOut, points]);
  if (!configured)
    return (
      <View style={[s.card, { minHeight: 220, justifyContent: "center" }]}>
        <Text style={s.muted}>
          Bản đồ trong ứng dụng chưa sẵn sàng. Bạn vẫn có thể xem, nhận đơn và
          mở Google Maps từ chi tiết đơn.
        </Text>
      </View>
    );
  const center = points[0] || { latitude: 16.064, longitude: 108.22 };
  return (
    <MapView
      ref={map}
      provider={PROVIDER_GOOGLE}
      mapType="standard"
      userInterfaceStyle="light"
      style={{ height: 320, width: "100%" }}
      onLayout={() => setLaidOut(true)}
      onMapReady={() => {
        setReady(true);
        if (__DEV__) console.info("[PickupMap] native map ready");
      }}
      onMapLoaded={() => {
        if (__DEV__) console.info("[PickupMap] map finished rendering");
      }}
      initialRegion={{ ...center, latitudeDelta: 0.08, longitudeDelta: 0.08 }}
    >
      {markers.map(({ pickup, point }) => (
        <Marker
          key={pickup.id}
          coordinate={point}
          pinColor={colors.primary}
          title={pickup.sellerName}
          description={onAccept ? `${pickup.address}\nChạm để nhận đơn` : pickup.address}
          onPress={() => onSelect?.(pickup)}
          onCalloutPress={() => (onAccept ? onAccept(pickup) : onSelect?.(pickup))}
        />
      ))}
      {employee ? (
        <Marker
          coordinate={employee}
          pinColor="#2563eb"
          title="Vị trí của bạn"
        />
      ) : null}
      {routePoints.length > 1 ? (
        <Polyline
          coordinates={routePoints}
          strokeColor={colors.primary}
          strokeWidth={4}
        />
      ) : null}
    </MapView>
  );
}
