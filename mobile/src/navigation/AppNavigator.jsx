import { useEffect } from "react";
import { Alert, AppState, Text } from "react-native";
import { createStackNavigator } from "@react-navigation/stack";
import useAuthStore from "../store/authStore";
import { getProfile } from "../api/authApi";
import LoginScreen from "../screens/auth/LoginScreen";
import EmployeeNavigator from "./EmployeeNavigator";
import DriverNavigator from "./DriverNavigator";
import SellerNavigator from "./SellerNavigator";
import LoadingSpinner from "../components/common/LoadingSpinner";
import Screen from "../components/common/Screen";
import Button from "../components/common/Button";
import NotificationProvider from "../components/common/NotificationProvider";
import { styles } from "../theme";

const LoginStack = createStackNavigator();
export default function AppNavigator() {
  const { token, role, isLoading, restoreError, bootstrapAuth, logout } =
    useAuthStore();
  useEffect(() => {
    bootstrapAuth().catch(() => {});
  }, [bootstrapAuth]);
  useEffect(() => {
    const subscription = AppState.addEventListener("change", (state) => {
      const auth = useAuthStore.getState();
      if (state === "active" && auth.token && auth.user) {
        const currentToken = auth.token;
        getProfile(auth.role)
          .then((user) => {
            if (useAuthStore.getState().token === currentToken)
              useAuthStore.getState().setUser(user);
          })
          .catch(() => {});
      }
    });
    return () => subscription.remove();
  }, []);
  const signOut = () =>
    logout().catch(() =>
      Alert.alert(
        "Lỗi lưu trữ",
        "Không thể xóa phiên đã lưu. Vui lòng thử lại hoặc xóa dữ liệu ứng dụng.",
      ),
    );
  if (isLoading) return <LoadingSpinner />;
  if (restoreError)
    return (
      <Screen>
        <Text style={styles.title}>Chưa thể kết nối</Text>
        <Text style={styles.text}>{restoreError}</Text>
        <Button
          title="Thử lại"
          onPress={() => bootstrapAuth().catch(() => {})}
        />
        <Button
          title="Đăng nhập tài khoản khác"
          variant="secondary"
          onPress={signOut}
        />
      </Screen>
    );
  if (!token)
    return (
      <LoginStack.Navigator
        id="LoginStack"
        screenOptions={{ headerShown: false }}
      >
        <LoginStack.Screen name="Login" component={LoginScreen} />
      </LoginStack.Navigator>
    );
  // Remount the entire navigator when the account changes so Back cannot cross sessions.
  if (role === "DEPOT_EMPLOYEE") return <NotificationProvider key={token} role={role}><EmployeeNavigator /></NotificationProvider>;
  if (role === "DRIVER") return <NotificationProvider key={token} role={role}><DriverNavigator /></NotificationProvider>;
  if (role === "SELLER") return <SellerNavigator key={token} />;
  return (
    <Screen>
      <Text style={styles.title}>
        Ứng dụng này chỉ dành cho nhân viên kho và tài xế
      </Text>
      <Button title="Đăng xuất" onPress={signOut} />
    </Screen>
  );
}
