import { RefreshControl, Text, View } from "react-native";
import Screen from "../../components/common/Screen";
import Button from "../../components/common/Button";
import PickupCard from "../../components/pickup/PickupCard";
import ResourceState from "../../components/pickup/ResourceState";
import useAuthStore from "../../store/authStore";
import usePickupResource from "../../hooks/usePickupResource";
import { pickupApi } from "../../api/pickupApi";
import { colors, styles as s } from "../../theme";

export default function DashboardScreen({ navigation }) {
  const user = useAuthStore((state) => state.user);
  const { data, loading, refreshing, error, reload } = usePickupResource(
    pickupApi.getDashboard,
  );
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
      <View style={{ gap: 7 }}>
        <Text style={s.title}>Xin chào, {user?.fullName} 👋</Text>
        <Text style={s.muted}>
          Hôm nay là {new Date().toLocaleDateString("vi-VN")}
        </Text>
      </View>
      <ResourceState loading={loading} error={error} retry={() => reload()} />
      {!loading && data ? (
        <>
          <View style={{ flexDirection: "row", gap: 8 }}>
            {[
              ["Đơn chờ", data.availableCount],
              ["Hôm nay", data.completedToday],
              ["Tổng", data.totalPickupsCompleted],
            ].map(([label, value]) => (
              <View
                key={label}
                style={{
                  flex: 1,
                  backgroundColor: colors.card,
                  borderRadius: 20,
                  padding: 12,
                  alignItems: "center",
                  gap: 6,
                  borderWidth: 1,
                  borderColor: "#e2e7db",
                  shadowColor: "#000",
                  shadowOffset: { width: 0, height: 1 },
                  shadowOpacity: 0.06,
                }}
              >
                <Text
                  style={{
                    color: colors.primary,
                    fontFamily: "Inter_700Bold",
                    fontSize: 32,
                  }}
                >
                  {value}
                </Text>
                <Text style={s.label}>{label}</Text>
              </View>
            ))}
          </View>
          <Text style={s.muted}>Hôm nay: đơn DONE theo giờ Việt Nam (UTC+7).</Text>
          <View style={s.card}>
            <Text style={s.text}>Đang cân sau check-in: {data.checkedInCount}</Text>
            <Text style={s.text}>Chờ người bán xác nhận: {data.waitingSellerCount}</Text>
            <Text style={s.text}>Cần bàn giao chủ kho: {data.readyToHandoverCount}</Text>
            <Text style={s.text}>Đang chờ thanh toán/nhận tiền: {data.waitingPaymentCount}</Text>
            <Button title={`Thông báo (${data.unreadCount} chưa đọc)`} variant="secondary" onPress={() => navigation.navigate("Notifications")} />
          </View>
          <Text style={[s.title, { fontSize: 21 }]}>Đơn đang xử lý</Text>
          {data.activePickup ? (
            <PickupCard
              pickup={data.activePickup}
              onDetail={() =>
                navigation.navigate("Pool", {
                  screen: "PickupDetail",
                  params: { pickupId: data.activePickup.id },
                  initial: false,
                })
              }
            />
          ) : (
            <View style={s.card}>
              <Text style={s.muted}>Chưa có đơn đang xử lý</Text>
            </View>
          )}
        </>
      ) : null}
      <Text style={[s.title, { fontSize: 21 }]}>Thao tác nhanh</Text>
      <Button title="Tất cả đơn tôi đang thực hiện" onPress={() => navigation.navigate("Pool", { screen: "ActivePickups", initial: false })} />
      <Button
        title="Xem đơn chờ →"
        onPress={() => navigation.navigate("Pool", { screen: "PickupPool" })}
      />
    </Screen>
  );
}
