import { useState } from "react";
import {
  Alert,
  Pressable,
  RefreshControl,
  StyleSheet,
  Text,
  View,
} from "react-native";
import Feather from "@expo/vector-icons/Feather";
import Button from "../../components/common/Button";
import Avatar from "../../components/common/Avatar";
import Screen from "../../components/common/Screen";
import useAuthStore from "../../store/authStore";
import { getProfile, uploadAvatar } from "../../api/authApi";
import { errorMessage } from "../../api/client";
import { pickAvatar } from "../../helpers/pickAvatar";
import { roleLabel } from "../../helpers/validation";
import { colors, styles as s } from "../../theme";

export default function ProfileScreen({ navigation }) {
  const { user, role, token, setUser, logout } = useAuthStore();
  const [refreshing, setRefreshing] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState("");
  if (!user) return null;
  const sessionIsCurrent = () => useAuthStore.getState().token === token;
  async function refresh() {
    setRefreshing(true);
    setError("");
    try {
      const profile = await getProfile(role);
      if (sessionIsCurrent()) setUser(profile);
    } catch (e) {
      if (sessionIsCurrent()) setError(errorMessage(e));
    } finally {
      setRefreshing(false);
    }
  }
  async function changeAvatar() {
    if (uploading) return;
    setUploading(true);
    setError("");
    try {
      const uri = await pickAvatar();
      if (!uri || !sessionIsCurrent()) return;
      const profile = await uploadAvatar(role, uri);
      if (sessionIsCurrent()) {
        setUser(profile);
        Alert.alert("Thành công", "Đã cập nhật ảnh đại diện.");
      }
    } catch (e) {
      if (sessionIsCurrent()) setError(errorMessage(e));
    } finally {
      setUploading(false);
    }
  }
  const signOut = () =>
    Alert.alert(
      "Đăng xuất?",
      "Bạn sẽ cần đăng nhập lại để tiếp tục sử dụng ứng dụng.",
      [
        { text: "Hủy", style: "cancel" },
        {
          text: "Đăng xuất",
          style: "destructive",
          onPress: () =>
            logout().catch(() =>
              Alert.alert(
                "Lỗi lưu trữ",
                "Không thể xóa phiên đã lưu. Vui lòng thử lại hoặc xóa dữ liệu ứng dụng.",
              ),
            ),
        },
      ],
    );
  return (
    <Screen
      refreshControl={
        <RefreshControl
          refreshing={refreshing}
          onRefresh={refresh}
          colors={[colors.primary]}
        />
      }
    >
      <View style={local.heading}>
        <Text style={s.title}>Hồ sơ cá nhân</Text>
        <Text style={s.muted}>{roleLabel(role)}</Text>
      </View>
      <View style={[s.card, local.identity]}>
        <Avatar name={user.fullName} uri={user.avatarUrl} />
        <Text style={[s.title, local.name]}>{user.fullName}</Text>
        <Text style={local.code}>Mã nhân sự: {user.employeeCode}</Text>
        <View style={[local.badge, !user.isActive && local.inactive]}>
          <Text style={s.label}>
            {user.isActive ? "Đang hoạt động" : "Ngừng hoạt động"}
          </Text>
        </View>
        <Button
          title="Đổi ảnh đại diện"
          variant="secondary"
          loading={uploading}
          onPress={changeAvatar}
        />
      </View>
      <Text style={s.label}>THÔNG TIN CÔNG VIỆC</Text>
      <Info
        icon="briefcase"
        label="Kho trực thuộc"
        value={`${user.depotName}\n${user.depotAddress}`}
      />
      <Info
        icon="phone"
        label="Số điện thoại"
        value={user.phone || "Chưa cập nhật"}
        onEdit={() => navigation.navigate("EditProfile")}
      />
      <Info icon="mail" label="Email" value={user.email} />
      {error ? (
        <Text style={s.error} accessibilityLiveRegion="polite">
          {error}
        </Text>
      ) : null}
      <Button
        title="Chỉnh sửa thông tin"
        disabled={uploading}
        onPress={() => navigation.navigate("EditProfile")}
      />
      <Button
        title="Làm mới hồ sơ"
        variant="secondary"
        loading={refreshing}
        disabled={uploading}
        onPress={refresh}
      />
      <Button title="Đăng xuất" variant="danger" onPress={signOut} />
    </Screen>
  );
}
function Info({ icon, label, value, onEdit = undefined }) {
  return (
    <View style={local.info}>
      <View style={local.icon}>
        <Feather name={icon} size={21} color={colors.primary} />
      </View>
      <View style={local.infoText}>
        <Text style={s.label}>{label}</Text>
        <Text selectable style={s.text}>
          {value}
        </Text>
      </View>
      {onEdit ? (
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Sửa số điện thoại"
          onPress={onEdit}
          style={local.edit}
        >
          <Feather name="edit-2" size={20} color={colors.primary} />
        </Pressable>
      ) : null}
    </View>
  );
}
const local = StyleSheet.create({
  heading: { gap: 6 },
  identity: { alignItems: "center" },
  name: { textAlign: "center", fontSize: 23 },
  code: {
    fontFamily: "Inter_600SemiBold",
    color: colors.muted,
    backgroundColor: colors.low,
    paddingHorizontal: 14,
    paddingVertical: 8,
    borderRadius: 20,
  },
  badge: {
    backgroundColor: colors.mint,
    paddingVertical: 6,
    paddingHorizontal: 14,
    borderRadius: 99,
  },
  inactive: { backgroundColor: "#ffdad6" },
  info: {
    flexDirection: "row",
    gap: 14,
    backgroundColor: colors.low,
    padding: 18,
    borderRadius: 24,
    alignItems: "flex-start",
  },
  icon: { backgroundColor: colors.card, padding: 10, borderRadius: 20 },
  infoText: { flex: 1, gap: 5 },
  edit: { padding: 12 },
});
