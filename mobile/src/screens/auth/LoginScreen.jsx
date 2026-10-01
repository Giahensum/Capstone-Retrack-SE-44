import { useState } from "react";
import { Alert, Pressable, Text, View } from "react-native";
import { SafeAreaView } from "react-native-safe-area-context";
import Feather from "@expo/vector-icons/Feather";
import Button from "../../components/common/Button";
import Field from "../../components/common/Input";
import Logo from "../../components/common/Logo";
import Screen from "../../components/common/Screen";
import useAuthStore from "../../store/authStore";
import { errorMessage } from "../../api/client";
import { validEmail } from "../../helpers/validation";
import { colors, styles as s } from "../../theme";
export default function LoginScreen() {
  const { login } = useAuthStore();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [visible, setVisible] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [submitted, setSubmitted] = useState(false);
  async function submit() {
    setSubmitted(true);
    setError("");
    if (!validEmail(email.trim()) || !password || busy) return;
    setBusy(true);
    try {
      await login(email, password);
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      setBusy(false);
    }
  }
  return (
    <SafeAreaView style={s.page} edges={["top"]}>
      <Screen>
        <View style={{ alignItems: "center", paddingTop: 30, gap: 12 }}>
          <Logo large />
          <Text style={[s.title, { letterSpacing: 3, color: colors.primary }]}>
            RETRACK
          </Text>
          <Text style={s.muted}>Kết nối hành trình tái chế</Text>
        </View>
        <View style={{ gap: 8, marginTop: 20 }}>
          <Text style={s.title}>Chào mừng trở lại</Text>
          <Text style={s.muted}>
            Đăng nhập để bắt đầu ngày làm việc của bạn.
          </Text>
        </View>
        <View style={[s.card, { gap: 22 }]}>
          <Field
            label="Email"
            value={email}
            onChangeText={setEmail}
            keyboardType="email-address"
            autoCapitalize="none"
            autoCorrect={false}
            autoComplete="email"
            placeholder="Email do chủ kho cung cấp"
            editable={!busy}
            error={
              submitted && !validEmail(email.trim())
                ? "Vui lòng nhập email hợp lệ."
                : undefined
            }
          />
          <View>
            <Field
              label="Mật khẩu"
              value={password}
              onChangeText={setPassword}
              secureTextEntry={!visible}
              autoCapitalize="none"
              autoCorrect={false}
              autoComplete="current-password"
              placeholder="Nhập mật khẩu"
              editable={!busy}
              returnKeyType="go"
              onSubmitEditing={() => {
                void submit();
              }}
              style={{ paddingRight: 55 }}
              error={
                submitted && !password ? "Vui lòng nhập mật khẩu." : undefined
              }
            />
            <Pressable
              accessibilityRole="button"
              accessibilityLabel={visible ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
              onPress={() => setVisible(!visible)}
              style={{ position: "absolute", right: 4, top: 31, padding: 16 }}
            >
              <Feather
                name={visible ? "eye-off" : "eye"}
                size={20}
                color={colors.muted}
              />
            </Pressable>
          </View>
          {error ? (
            <Text style={s.error} accessibilityLiveRegion="polite">
              {error}
            </Text>
          ) : null}
          <Button
            title="Đăng nhập"
            loading={busy}
            onPress={() => {
              void submit();
            }}
          />
          <Pressable
            accessibilityRole="button"
            onPress={() =>
              Alert.alert(
                "Hỗ trợ tài khoản",
                "Liên hệ chủ kho để được cấp lại mật khẩu hoặc kích hoạt tài khoản.",
              )
            }
          >
            <Text
              style={[
                s.label,
                { textAlign: "center", color: colors.primary, padding: 8 },
              ]}
            >
              Bạn cần hỗ trợ đăng nhập?
            </Text>
          </Pressable>
        </View>
        <View
          style={[
            s.row,
            { justifyContent: "center", flexWrap: "wrap", padding: 10 },
          ]}
        >
          <Feather name="shield" size={17} color={colors.primary} />
          <Text style={s.muted}>Dành cho nhân viên kho và tài xế</Text>
        </View>
        <Text style={[s.muted, { textAlign: "center", fontSize: 12 }]}>
          Tài khoản được cấp và quản lý bởi chủ kho.
        </Text>
      </Screen>
    </SafeAreaView>
  );
}
