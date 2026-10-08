import { useCallback, useRef, useState } from "react";
import { ActivityIndicator, Alert, Modal, Pressable, ScrollView, StyleSheet, Text, View, useWindowDimensions } from "react-native";
import { CommonActions, useFocusEffect, useNavigation } from "@react-navigation/native";
import { useSafeAreaInsets } from "react-native-safe-area-context";
import Feather from "@expo/vector-icons/Feather";
import { useNotifications } from "./NotificationProvider";
import { notificationDestination } from "../../helpers/notificationFeed";
import { colors, styles as s } from "../../theme";

export default function NotificationBell() {
  const context = useNotifications();
  const navigation = useNavigation();
  const [open, setOpen] = useState(false);
  const selection = useRef(0);
  const dismiss = useCallback(() => { selection.current++; setOpen(false); }, []);
  const { width, height } = useWindowDimensions();
  const insets = useSafeAreaInsets();
  useFocusEffect(useCallback(() => dismiss, [dismiss]));
  if (!context) return null;
  const { feed, state, role } = context;
  const { data, loading, error, busy } = state;
  const unread = data?.unreadCount;
  async function select(item) {
    const ticket = selection.current;
    if (!await feed.read(item)) return;
    if (ticket !== selection.current) return;
    const destination = notificationDestination(role, item);
    dismiss();
    if (destination) {
      // API màn chi tiết tiếp tục kiểm tra phạm vi đơn/kho; thông báo không cấp quyền.
      navigation.dispatch(CommonActions.navigate(destination));
    } else Alert.alert(item.title, item.message || "Thông báo không kèm liên kết đơn hàng.");
  }
  const page = data?.page || 1;
  return <>
    <Pressable accessibilityRole="button" accessibilityLabel={unread == null ? "Mở thông báo" : `Thông báo, ${unread} chưa đọc`}
      accessibilityState={{ expanded: open }} style={css.bell}
      onPress={() => { selection.current++; setOpen(true); void feed.load(1); }}>
      <Feather name="bell" size={24} color={colors.primary} />
      {unread > 0 ? <View style={css.badge}><Text style={css.badgeText}>{unread > 99 ? "99+" : unread}</Text></View> : null}
      {error ? <View style={css.errorDot} /> : null}
    </Pressable>
    <Modal transparent statusBarTranslucent visible={open} animationType="fade" onRequestClose={dismiss}>
      <View style={{ flex: 1 }}>
        <Pressable style={[StyleSheet.absoluteFill, css.backdrop]} accessibilityLabel="Đóng thông báo" accessibilityRole="button" onPress={dismiss} />
        <View accessibilityViewIsModal style={[css.panel, { marginTop: insets.top + 52, width: Math.min(400, width - 24), maxHeight: height - insets.top - insets.bottom - 76 }]}>
          <View style={css.row}>
            <Text style={s.label}>Thông báo{unread != null ? ` · ${unread} chưa đọc` : ""}</Text>
            <Pressable style={css.control} accessibilityRole="button" accessibilityLabel="Đóng danh sách thông báo" onPress={dismiss}><Feather name="x" size={22} color={colors.ink} /></Pressable>
          </View>
          {loading ? <ActivityIndicator color={colors.primary} accessibilityLabel="Đang tải thông báo" /> : null}
          {error ? <Text accessibilityLiveRegion="polite" style={s.error}>{error}</Text> : null}
          <ScrollView style={{ flexShrink: 1 }} contentContainerStyle={{ paddingBottom: 8 }}>
            {!loading && data && !data.items.length ? <Text style={s.muted}>Chưa có thông báo ở trang này.</Text> : null}
            {!loading && data?.items.map(item => <Pressable key={item.id} disabled={busy} accessibilityRole="button"
              accessibilityLabel={`${item.isRead ? "" : "Chưa đọc. "}${item.title}`}
              onPress={() => { void select(item); }}
              style={[css.item, !item.isRead && { backgroundColor: colors.low }]}>
              <Text style={[s.label, !item.isRead && { color: colors.primary }]}>{item.isRead ? "" : "● "}{item.title}</Text>
              <Text style={s.text}>{item.message}</Text>
              <Text style={s.muted}>{new Date(item.createdAt).toLocaleString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" })}</Text>
              {notificationDestination(role, item) ? <Text style={{ color: colors.primary }}>{role === "DRIVER" ? "Xem lô hàng / chuyến →" : "Xem đơn thu gom →"}</Text> : null}
            </Pressable>)}
          </ScrollView>
          {busy ? <ActivityIndicator color={colors.primary} accessibilityLabel="Đang mở thông báo" /> : null}
          <View style={css.row}>
            <Pressable accessibilityRole="button" disabled={busy || loading || page <= 1} style={css.control} onPress={() => feed.load(page - 1)}><Text style={[s.label, (page <= 1 || busy || loading) && css.disabled]}>Trước</Text></Pressable>
            <Text style={s.muted}>Trang {page}</Text>
            <Pressable accessibilityRole="button" disabled={busy || loading || !data || page * data.pageSize >= data.totalCount} style={css.control} onPress={() => feed.load(page + 1)}><Text style={[s.label, (!data || page * data.pageSize >= data.totalCount || busy || loading) && css.disabled]}>Sau</Text></Pressable>
          </View>
          <Pressable accessibilityRole="button" disabled={busy || loading} style={css.control} onPress={() => feed.load(page)}><Text style={s.label}>{error ? "Thử lại" : "Làm mới"}</Text></Pressable>
        </View>
      </View>
    </Modal>
  </>;
}
const css = StyleSheet.create({
  bell: { width: 48, height: 44, marginRight: 12, alignItems: "center", justifyContent: "center" },
  badge: { position: "absolute", top: 0, right: 0, minWidth: 19, height: 19, borderRadius: 10, backgroundColor: "#ba1a1a", paddingHorizontal: 4, alignItems: "center", justifyContent: "center" },
  badgeText: { color: "#fff", fontSize: 10, fontWeight: "700" },
  errorDot: { position: "absolute", bottom: 4, right: 7, width: 7, height: 7, borderRadius: 4, backgroundColor: "#ba1a1a" },
  backdrop: { backgroundColor: "rgba(0,0,0,0.2)" },
  panel: { alignSelf: "flex-end", marginRight: 12, backgroundColor: "#fff", borderRadius: 18, padding: 12, elevation: 10, borderWidth: 1, borderColor: colors.border },
  row: { flexDirection: "row", alignItems: "center", justifyContent: "space-between" },
  control: { minHeight: 44, paddingHorizontal: 10, alignItems: "center", justifyContent: "center" },
  item: { padding: 12, gap: 6, borderBottomWidth: 1, borderColor: colors.border, borderRadius: 8 },
  disabled: { opacity: 0.35 },
});
