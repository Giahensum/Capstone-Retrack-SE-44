import { useState } from "react";
import { FlatList, Pressable, Text, View } from "react-native";
import { SafeAreaView } from "react-native-safe-area-context";
import { useIsFocused } from "@react-navigation/native";
import Feather from "@expo/vector-icons/Feather";
import Button from "../../components/common/Button";
import Screen from "../../components/common/Screen";
import PickupCard from "../../components/pickup/PickupCard";
import PickupMap from "../../components/pickup/PickupMap";
import ResourceState from "../../components/pickup/ResourceState";
import usePickupResource from "../../hooks/usePickupResource";
import useAcceptPickup from "../../hooks/useAcceptPickup";
import useEmployeeLocation from "../../hooks/useEmployeeLocation";
import { coordinatesOf } from "../../helpers/coordinates";
import { pickupApi } from "../../api/pickupApi";
import { colors, styles as s } from "../../theme";

export default function PickupPoolScreen({ navigation }) {
  const [mode, setMode] = useState("list");
  const [selectedId, setSelectedId] = useState(null);
  const focused = useIsFocused();
  const { data, setData, loading, refreshing, error, reload } =
    usePickupResource(pickupApi.getPickupPool);
  const pickups = data || [];
  const gps = useEmployeeLocation(mode === "map" && focused);
  const { acceptingId, confirm } = useAcceptPickup(
    (accepted) => {
      setData((items) => items?.filter((item) => item.id !== accepted.id));
      setSelectedId(null);
      void reload(true);
    },
    () => {
      setSelectedId(null);
      void reload(true);
    },
  );
  const detail = (pickup) =>
    navigation.navigate("PickupDetail", { pickupId: pickup.id, pickup });
  const selected = pickups.find((p) => p.id === selectedId);
  const missingCoordinates = pickups.filter((p) => !coordinatesOf(p)).length;
  const card = (item) => (
    <PickupCard
      pickup={item}
      onDetail={() => detail(item)}
      onAccept={confirm}
      busy={acceptingId === item.id}
      disabled={Boolean(acceptingId)}
    />
  );
  const empty = (
    <View style={[s.card, { alignItems: "center" }]}>
      <Feather name="inbox" size={36} color={colors.primary} />
      <Text style={s.muted}>Chưa có đơn nào đang chờ phân công</Text>
    </View>
  );
  return (
    <SafeAreaView edges={["bottom"]} style={s.page}>
      <View style={{ padding: 20, gap: 12 }}>
        <Text style={s.title}>Đơn chờ thu gom</Text>
        <View style={s.row}>
          {[
            ["list", "Danh sách"],
            ["map", "Bản đồ"],
          ].map(([value, label]) => (
            <Pressable
              key={value}
              accessibilityRole="button"
              accessibilityState={{ selected: mode === value }}
              onPress={() => setMode(value)}
              style={{
                flex: 1,
                borderRadius: 16,
                padding: 14,
                alignItems: "center",
                backgroundColor: mode === value ? colors.lime : colors.card,
              }}
            >
              <Text style={s.label}>{label}</Text>
            </Pressable>
          ))}
        </View>
        <ResourceState loading={loading} error={error} retry={() => reload()} />
      </View>
      {!loading && !error && mode === "list" ? (
        <FlatList
          data={pickups}
          keyExtractor={(item) => item.id}
          renderItem={({ item }) => card(item)}
          contentContainerStyle={{
            padding: 20,
            paddingTop: 0,
            gap: 16,
            flexGrow: 1,
          }}
          refreshing={refreshing}
          onRefresh={() => reload(true)}
          ListEmptyComponent={empty}
        />
      ) : null}
      {!loading && !error && mode === "map" ? (
        <Screen>
          <Button
            title="Làm mới đơn chờ"
            variant="secondary"
            onPress={() => reload(true)}
            loading={refreshing}
          />
          {pickups.length === 0 ? (
            empty
          ) : (
            <>
              <PickupMap
                pickups={pickups}
                employee={gps.location}
                onSelect={(item) => setSelectedId(item.id)}
                onAccept={confirm}
              />
              <Text style={s.muted}>
                Chạm ghim để xem đơn; chạm ô thông tin trên bản đồ để nhận đơn.
              </Text>
              {missingCoordinates > 0 ? (
                <Text style={s.muted}>
                  {missingCoordinates} đơn chưa có tọa độ hợp lệ. Chuyển sang
                  Danh sách để xem và nhận các đơn này.
                </Text>
              ) : null}
              {selected ? card(selected) : null}
            </>
          )}
          {gps.error ? <Text style={s.error}>{gps.error}</Text> : null}
          <Button
            title="Cập nhật vị trí của tôi"
            variant="secondary"
            onPress={gps.refresh}
            loading={gps.loading}
          />
        </Screen>
      ) : null}
    </SafeAreaView>
  );
}
