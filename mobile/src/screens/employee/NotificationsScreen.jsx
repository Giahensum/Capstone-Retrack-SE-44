import { useCallback, useRef, useState } from "react";
import { Text, View } from "react-native";
import Screen from "../../components/common/Screen";
import Button from "../../components/common/Button";
import ResourceState from "../../components/pickup/ResourceState";
import usePickupResource from "../../hooks/usePickupResource";
import { pickupApi } from "../../api/pickupApi";
import { errorMessage } from "../../api/client";
import { formatViDatetime } from "../../helpers/format";
import { colors, styles as s } from "../../theme";
import useEmployeeNoticeStore from "../../store/employeeNoticeStore";

export default function NotificationsScreen() {
  const [page, setPage] = useState(1);
  const [busy, setBusy] = useState(null);
  const [actionError, setActionError] = useState("");
  const pending = useRef(false);
  const loader = useCallback(async signal => {
    const result = await pickupApi.getNotifications(page, signal);
    if (!signal.aborted) useEmployeeNoticeStore.getState().setUnread(result.unreadCount);
    return result;
  }, [page]);
  const { data, loading, error, reload } = usePickupResource(loader);
  async function read(id) {
    if (pending.current) return;
    pending.current = true; setBusy(id); setActionError("");
    try { await pickupApi.readNotification(id); await reload(true); }
    catch (e) { setActionError(errorMessage(e)); }
    finally { pending.current = false; setBusy(null); }
  }
  return <Screen>
    <Text style={s.title}>Thông báo</Text>
    <Text style={s.muted}>Thông báo trong ứng dụng. Bấm làm mới để cập nhật.</Text>
    <ResourceState loading={loading} error={error} retry={() => reload()} />
    {actionError ? <Text style={s.error}>{actionError}</Text> : null}
    {!loading && data ? <>
      <Text style={s.label}>{data.unreadCount} chưa đọc</Text>
      {!data.items.length ? <Text style={s.muted}>Chưa có thông báo ở trang này.</Text> : null}
      {data.items.map(n => <View key={n.id} style={[s.card, !n.isRead && { borderColor: colors.primary, borderWidth: 2 }]}>
        <Text style={s.label}>{n.isRead ? "" : "● "}{n.title}</Text><Text style={s.text}>{n.message}</Text>
        <Text style={s.muted}>{formatViDatetime(n.createdAt)}</Text>
        {!n.isRead ? <Button title="Đánh dấu đã đọc" loading={busy === n.id} disabled={busy != null} onPress={() => read(n.id)} /> : null}
      </View>)}
      <Text style={s.muted}>Trang {page} · {data.totalCount} thông báo</Text>
      <Button title="Trang trước" variant="secondary" disabled={page === 1 || busy != null} onPress={() => setPage(p => p - 1)} />
      <Button title="Trang sau" variant="secondary" disabled={page * data.pageSize >= data.totalCount || busy != null} onPress={() => setPage(p => p + 1)} />
    </> : null}
    <Button title="Làm mới thông báo" variant="secondary" disabled={busy != null || loading} onPress={() => reload()} />
  </Screen>;
}
