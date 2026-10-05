import { useCallback, useState } from "react";
import { ScrollView, Text, View } from "react-native";
import Screen from "../../components/common/Screen";
import Button from "../../components/common/Button";
import ResourceState from "../../components/pickup/ResourceState";
import usePickupResource from "../../hooks/usePickupResource";
import { pickupApi } from "../../api/pickupApi";
import { money, pickupStatus } from "../../helpers/collection";
import { formatViDatetime } from "../../helpers/format";
import { styles as s } from "../../theme";

export default function HistoryStatsScreen({ navigation }) {
  const [tab, setTab] = useState("history");
  const [status, setStatus] = useState(undefined);
  const [page, setPage] = useState(1);
  const loader = useCallback(signal => tab === "stats" ? pickupApi.getStats(signal) : pickupApi.getHistory(page, status, signal), [tab, page, status]);
  const { data, loading, error, reload } = usePickupResource(loader);
  return <Screen>
    <Text style={s.title}>Lịch sử và thống kê</Text>
    <View style={{ flexDirection: "row", gap: 8 }}>
      <Button title="Lịch sử" variant={tab === "history" ? "primary" : "secondary"} onPress={() => setTab("history")} />
      <Button title="Thống kê" variant={tab === "stats" ? "primary" : "secondary"} onPress={() => setTab("stats")} />
    </View>
    {tab === "history" ? <ScrollView horizontal contentContainerStyle={{ gap: 8 }}>
      {[undefined, "SCHEDULED", "IN_PROGRESS", "WEIGHED", "SELLER_CONFIRMED", "AWAITING_PAYMENT", "PAYMENT_SENT", "DONE", "CANCELLED"].map(value =>
        <Button key={value || "all"} title={value ? pickupStatus(value) : "Tất cả"} variant={status === value ? "primary" : "secondary"}
          onPress={() => { setStatus(value); setPage(1); }} />)}
    </ScrollView> : null}
    <ResourceState loading={loading} error={error} retry={() => reload()} />
    {!loading && data ? tab === "history" && data.items ? <>
      {!data.items.length ? <Text style={s.muted}>Chưa có đơn phù hợp.</Text> : null}
      {data.items.map(item => <View key={item.id} style={s.card}>
        <Text style={s.label}>{item.sellerName}</Text><Text style={s.text}>{item.address}</Text>
        <Text style={s.label}>{pickupStatus(item.status)}</Text><Text style={s.muted}>{formatViDatetime(item.updatedAt)}</Text>
        <Button title="Xem kết quả và trạng thái" onPress={() => navigation.navigate("Pool", { screen: "SubmitResult", params: { pickupId: item.id }, initial: false })} />
      </View>)}
      <Text style={s.muted}>Trang {page} · {data.totalCount} đơn</Text>
      <Button title="Trang trước" disabled={page === 1} variant="secondary" onPress={() => setPage(p => p - 1)} />
      <Button title="Trang sau" disabled={page * data.pageSize >= data.totalCount} variant="secondary" onPress={() => setPage(p => p + 1)} />
    </> : tab === "stats" && data.allTime ? <>
      <Text style={s.muted}>Chỉ tính đơn DONE trong phạm vi kho đang liên kết. Giá trị phế liệu không phải thu nhập của nhân viên.</Text>
      {[["Tháng này", data.thisMonth], ["Toàn thời gian", data.allTime]].map(([label, value]) => <View key={label} style={s.card}>
        <Text style={s.title}>{label}</Text><Text style={s.text}>{value.pickups} đơn hoàn tất</Text>
        <Text style={s.text}>{value.weightKg} kg phế liệu</Text><Text style={s.text}>Giá trị phế liệu: {money(value.grossAmount)}</Text>
      </View>)}
      <Text style={s.muted}>Ngày/tháng theo giờ Việt Nam (UTC+7), dựa trên thời điểm cập nhật của đơn DONE.</Text>
    </> : null : null}
    <Button title="Làm mới" variant="secondary" disabled={loading} onPress={() => reload()} />
  </Screen>;
}
