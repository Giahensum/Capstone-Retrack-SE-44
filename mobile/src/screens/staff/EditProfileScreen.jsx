import { useEffect, useState } from "react";
import { Alert, BackHandler, Text, View } from "react-native";

import { pickAvatar } from "../../helpers/pickAvatar";

import Avatar from "../../components/common/Avatar";
import Button from "../../components/common/Button";
import Field from "../../components/common/Input";
import Screen from "../../components/common/Screen";
import useAuthStore from "../../store/authStore";
import { updateProfile, uploadAvatar } from "../../api/authApi";
import { errorMessage } from "../../api/client";
import { roleLabel, validPhone } from "../../helpers/validation";
import { styles as s } from "../../theme";
export default function EditProfileScreen({ navigation }) {
  const { user: profile, role, token, setUser } = useAuthStore();
  const [phone, setPhone] = useState(profile?.phone || "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [submitted, setSubmitted] = useState(false);
  useEffect(() => {
    const listener = BackHandler.addEventListener(
      "hardwareBackPress",
      () => busy,
    );
    return () => listener.remove();
  }, [busy]);
  if (!profile || !role) return null;
  async function choosePhoto() {
    if (!role || busy) return;
    setBusy(true);
    setError("");
    try {
      const uri = await pickAvatar();
      if (!uri || useAuthStore.getState().token !== token) return;
      const updated = await uploadAvatar(role, uri);
      if (useAuthStore.getState().token !== token) return;
      setUser(updated);
      Alert.alert("Thành công", "Đã cập nhật ảnh đại diện.");
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      setBusy(false);
    }
  }
  async function save() {
    setSubmitted(true);
    if (!role || !validPhone(phone.trim()) || busy) return;
    setBusy(true);
    setError("");
    try {
      const updated = await updateProfile(role, phone.trim());
      if (useAuthStore.getState().token !== token) return;
      setUser(updated);
      navigation.goBack();
      Alert.alert("Thành công", "Đã lưu số điện thoại của bạn.");
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      setBusy(false);
    }
  }
  return (
    <Screen>
      <Text style={s.title}>Chỉnh sửa hồ sơ</Text>
      <View style={[s.card, { alignItems: "center" }]}>
        <Avatar name={profile.fullName} uri={profile.avatarUrl} />
        <Text style={s.title}>{profile.fullName}</Text>
        <Text style={s.muted}>{roleLabel(profile.role)}</Text>
        <Button
          title="Đổi ảnh đại diện"
          variant="secondary"
          disabled={busy}
          onPress={() => {
            void choosePhoto();
          }}
        />
        <Text style={s.muted}>Ảnh được lưu ngay sau khi chọn.</Text>
      </View>
      <Field
        label="Số điện thoại"
        value={phone}
        onChangeText={setPhone}
        keyboardType="phone-pad"
        autoComplete="tel"
        maxLength={12}
        editable={!busy}
        error={
          submitted && !validPhone(phone.trim())
            ? "Nhập số điện thoại Việt Nam hợp lệ."
            : undefined
        }
      />
      <View style={s.card}>
        <Text style={s.label}>THÔNG TIN DO CHỦ KHO QUẢN LÝ</Text>
        <Text style={s.text}>{role === "DRIVER" ? "Mã tài xế" : "Mã nhân sự"}: {profile.employeeCode}</Text>
        <Text style={s.text}>Kho: {profile.depotName}</Text>
        <Text style={s.muted}>
          Liên hệ chủ kho nếu bạn cần thay đổi tên, email hoặc kho trực thuộc.
        </Text>
      </View>
      {error ? (
        <Text style={s.error} accessibilityLiveRegion="polite">
          {error}
        </Text>
      ) : null}
      <Button
        title="Lưu thay đổi"
        loading={busy}
        onPress={() => {
          void save();
        }}
      />
      <Button
        title="Hủy"
        variant="secondary"
        disabled={busy}
        onPress={() => navigation.goBack()}
      />
    </Screen>
  );
}

