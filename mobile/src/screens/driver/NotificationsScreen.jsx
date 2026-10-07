import { useCallback, useState } from "react";
import { Text, View } from "react-native";
import Screen from "../../components/common/Screen";
import Button from "../../components/common/Button";
import ResourceState from "../../components/pickup/ResourceState";
import usePickupResource from "../../hooks/usePickupResource";
import { driverApi } from "../../api/driverApi";
import { errorMessage } from "../../api/client";
import { styles as s } from "../../theme";
export default function NotificationsScreen({ navigation }) {
  const [page, setPage] = useState(1), [busy, setBusy] = useState(false), [failure, setFailure] = useState("");
  const loader = useCallback(signal => driverApi.notices(page, signal), [page]);
  const { data, loading, error, reload } = usePickupResource(loader);
  async function read(item, open) {
    setBusy(true); setFailure("");
    try { await driverApi.read(item.id); if (open && item.jobId) navigation.navigate("JobDetail", { jobId: item.jobId }); else await reload(); }
    catch (e) { setFailure(errorMessage(e)); } finally { setBusy(false); }
  }
  return <Screen>
    <Text style={s.title}>Thông báo</Text>
    <ResourceState loading={loading} error={error} retry={() => reload()} />
    {data && !loading ? <>
      <Text style={s.muted}>{data.unreadCount} chưa đọc</Text>
      {!data.items.length ? <Text style={s.muted}>Chưa có thông báo ở trang này.</Text> : null}
      {data.items.map(item => <View key={item.id} style={s.card}>
        <Text style={s.label}>{item.isRead ? "" : "● "}{item.title}</Text><Text style={s.text}>{item.message}</Text>
        <Text style={s.muted}>{new Date(item.createdAt).toLocaleString("vi-VN")}</Text>
        {!item.isRead ? <Button title="Đánh dấu đã đọc" disabled={busy} variant="secondary" onPress={() => read(item, false)} /> : null}
        {item.jobId ? <Button title="Xem chuyến liên quan" disabled={busy} onPress={() => read(item, true)} /> : null}
      </View>)}
      <Button title="Làm mới" disabled={busy} onPress={() => reload()} />
      <Button title="Trang trước" disabled={busy || page === 1} onPress={() => setPage(p => p - 1)} />
      <Button title="Trang sau" disabled={busy || page * 20 >= data.totalCount} onPress={() => setPage(p => p + 1)} />
    </> : null}
    {failure ? <Text style={s.error}>{failure}</Text> : null}
  </Screen>;
}
