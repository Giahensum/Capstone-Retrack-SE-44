import { useCallback, useState } from "react";
import { RefreshControl, Text, View } from "react-native";
import Screen from "../../components/common/Screen";
import Button from "../../components/common/Button";
import ResourceState from "../../components/pickup/ResourceState";
import usePickupResource from "../../hooks/usePickupResource";
import { driverApi } from "../../api/driverApi";
import { styles as s } from "../../theme";

const date = value => new Date(value).toLocaleString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" });
const receipts = [["all", "Tất cả"], ["waiting", "Chờ nhà máy"], ["received", "Đã nhận hàng"]];
export default function HistoryScreen({ navigation }) {
  const [page, setPage] = useState(1);
  const [period, setPeriod] = useState("all");
  const [receipt, setReceipt] = useState("all");
  const loader = useCallback(signal => driverApi.history(page, period, receipt, signal), [page, period, receipt]);
  const history = usePickupResource(loader);
  const statsLoader = useCallback(signal => driverApi.stats(signal), []);
  const stats = usePickupResource(statsLoader);
  return <Screen refreshControl={<RefreshControl refreshing={history.refreshing || stats.refreshing}
    onRefresh={() => { void history.reload(true); void stats.reload(true); }} />}>
    <Text style={s.title}>Lịch sử và thống kê</Text>
    <ResourceState loading={stats.loading} error={stats.error} retry={() => stats.reload()} />
    {!stats.loading && stats.data ? <>
      {[["Tháng này", stats.data.thisMonth], ["Toàn bộ", stats.data.allTime]].map(([title, total]) => <View key={title} style={s.card}>
        <Text style={s.label}>{title}</Text>
        <Text style={s.text}>{total.trips} chuyến đã báo giao</Text>
        <Text style={s.text}>{Number(total.declaredWeightKg).toLocaleString("vi-VN", { maximumFractionDigits: 2 })} kg khai báo</Text>
        <Text style={s.muted}>{total.factoryReceived} chuyến đã được nhà máy xác nhận nhận hàng</Text>
      </View>)}
      <Text style={s.muted}>Khối lượng theo khai báo của lô đã giao, không phải kết quả QC. Tháng và ngày tính theo giờ Việt Nam.</Text>
    </> : null}
    <Text style={s.label}>Chuyến đã báo giao</Text>
    <Button title={period === "all" ? "Khoảng thời gian: Toàn bộ — đổi sang tháng này" : "Khoảng thời gian: Tháng này — đổi sang toàn bộ"}
      variant="secondary" onPress={() => { setPeriod(period === "all" ? "month" : "all"); setPage(1); }} />
    {receipts.map(([value, title]) => <Button key={value} title={`${receipt === value ? "✓ " : ""}${title}`}
      variant="secondary" disabled={receipt === value} onPress={() => { setReceipt(value); setPage(1); }} />)}
    <ResourceState loading={history.loading} error={history.error} retry={() => history.reload()} />
    {!history.loading && history.data ? <>
      {!history.data.items.length ? <Text style={s.muted}>Chưa có chuyến đã báo giao phù hợp bộ lọc.</Text> : null}
      {history.data.items.map(job => <View key={job.id} style={s.card}>
        <Text style={s.label}>{job.batchCode || job.id}</Text>
        <Text style={s.text}>{job.depotName} → {job.factoryName}</Text>
        <Text style={s.text}>{job.materialType} · {job.weightKg} kg khai báo</Text>
        <Text style={s.muted}>Báo giao: {date(job.deliveredAt)}</Text>
        {job.legacyDate ? <Text style={s.muted}>Dữ liệu cũ: thời điểm giao lấy từ lần cập nhật chuyến.</Text> : null}
        <Text style={s.muted}>{job.factoryReceivedAt ? `Nhà máy nhận hàng: ${date(job.factoryReceivedAt)}` : "Chờ nhà máy xác nhận nhận hàng"}</Text>
        <Button title="Xem chi tiết và bằng chứng" onPress={() => navigation.navigate("Jobs", { screen: "JobDetail", params: { jobId: job.id } })} />
      </View>)}
      <Text style={s.muted}>Trang {page} · {history.data.totalCount} chuyến</Text>
      <Button title="Trang trước" variant="secondary" disabled={page <= 1 || history.refreshing} onPress={() => setPage(p => p - 1)} />
      <Button title="Trang sau" variant="secondary" disabled={page * 20 >= history.data.totalCount || history.refreshing} onPress={() => setPage(p => p + 1)} />
    </> : null}
  </Screen>;
}
