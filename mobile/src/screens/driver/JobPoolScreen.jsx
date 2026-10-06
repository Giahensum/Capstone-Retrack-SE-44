import { useCallback, useState } from "react";
import { RefreshControl, Text, View } from "react-native";
import Screen from "../../components/common/Screen";
import Button from "../../components/common/Button";
import ResourceState from "../../components/pickup/ResourceState";
import usePickupResource from "../../hooks/usePickupResource";
import { driverApi } from "../../api/driverApi";
import { styles as s } from "../../theme";
import { jobStatus } from "../../helpers/driverJobs";
export default function JobPoolScreen({ navigation }) {
  const [mine, setMine] = useState(false), [page, setPage] = useState(1);
  const loader = useCallback(signal => driverApi.list(mine, page, signal), [mine, page]);
  const { data, loading, error, refreshing, reload } = usePickupResource(loader);
  const noticeLoader = useCallback(signal => driverApi.notices(1, signal), []);
  const notices = usePickupResource(noticeLoader);
  return <Screen refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => { void reload(true); void notices.reload(true); }} />}>
    <Text style={s.title}>Chuyến vận chuyển</Text>
    <Button title={`Thông báo${notices.data ? ` (${notices.data.unreadCount} chưa đọc)` : ""}`} variant="secondary" onPress={() => navigation.navigate("DriverNotices")} />
    <Button title={mine ? "Xem chuyến chờ nhận" : "Xem chuyến của tôi"} variant="secondary" onPress={() => { setMine(!mine); setPage(1); }} />
    <Text style={s.label}>{mine ? "CHUYẾN CỦA TÔI" : "CHUYẾN CHỜ NHẬN"}</Text>
    <ResourceState loading={loading} error={error} retry={() => reload()} />
    {!loading && data ? <>
      {!data.items.length ? <Text style={s.muted}>Không có chuyến ở danh sách này.</Text> : null}
      {data.items.map(job => <View key={job.id} style={s.card}>
        <Text style={s.label}>{job.batchCode || job.batchId}</Text>
        <Text style={s.text}>{job.depot.name} → {job.factory.name}</Text>
        <Text style={s.muted}>{job.materialType} · {job.weightKg} kg · {jobStatus(job.status)}</Text>
        <Button title="Xem chuyến" onPress={() => navigation.navigate("JobDetail", { jobId: job.id })} />
      </View>)}
      <Text style={s.muted}>Trang {page} · {data.totalCount} chuyến</Text>
      <Button title="Trang trước" disabled={page <= 1} variant="secondary" onPress={() => setPage(p => p - 1)} />
      <Button title="Trang sau" disabled={page * 20 >= data.totalCount} variant="secondary" onPress={() => setPage(p => p + 1)} />
    </> : null}
  </Screen>;
}
