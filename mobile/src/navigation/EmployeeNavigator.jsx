import { createBottomTabNavigator } from "@react-navigation/bottom-tabs";
import { createStackNavigator } from "@react-navigation/stack";
import Feather from "@expo/vector-icons/Feather";
import ProfileNavigator from "./ProfileNavigator";
import DashboardScreen from "../screens/employee/DashboardScreen";
import PickupPoolScreen from "../screens/employee/PickupPoolScreen";
import PickupDetailScreen from "../screens/employee/PickupDetailScreen";
import CheckInScreen from "../screens/employee/CheckInScreen";
import ClassifyWeighScreen from "../screens/employee/ClassifyWeighScreen";
import ActivePickupsScreen from "../screens/employee/ActivePickupsScreen";
import SubmitResultScreen from "../screens/employee/SubmitResultScreen";
import HistoryStatsScreen from "../screens/employee/HistoryStatsScreen";
import NotificationsScreen from "../screens/employee/NotificationsScreen";
import Brand from "../components/common/Brand";
import { useEffect, useRef } from "react";
import { pickupApi } from "../api/pickupApi";
import useEmployeeNoticeStore from "../store/employeeNoticeStore";
const Tabs = createBottomTabNavigator();
const Stack = createStackNavigator();
const icons = {
  Dashboard: "home",
  Pool: "list",
  History: "clock",
  Notifications: "bell",
  Profile: "user",
};
function PickupStackNavigator() {
  return (
    <Stack.Navigator
      id="PickupStack"
      initialRouteName="PickupPool"
      screenOptions={{
        headerStyle: { backgroundColor: "#f8f9ff" },
        headerTintColor: "#446900",
        headerTitleStyle: { fontFamily: "Inter_700Bold" },
        headerShadowVisible: false,
      }}
    >
      <Stack.Screen name="ActivePickups" component={ActivePickupsScreen} options={{ title: "Đơn đang thực hiện" }} />
      <Stack.Screen name="SubmitResult" component={SubmitResultScreen} options={{ title: "Kết quả và bàn giao" }} />
      <Stack.Screen name="CheckIn" component={CheckInScreen} options={{ title: "Check-in điểm bán" }} />
      <Stack.Screen name="ClassifyWeigh" component={ClassifyWeighScreen} options={{ title: "Phân loại và cân" }} />
      <Stack.Screen
        name="PickupPool"
        component={PickupPoolScreen}
        options={{ headerTitle: () => <Brand /> }}
      />
      <Stack.Screen
        name="PickupDetail"
        component={PickupDetailScreen}
        options={{ title: "Chi tiết đơn" }}
      />
    </Stack.Navigator>
  );
}
export default function EmployeeNavigator() {
  const unread = useEmployeeNoticeStore(state => state.unread);
  const pendingBadge = useRef(null);
  useEffect(() => {
    useEmployeeNoticeStore.getState().setUnread(null);
    return () => pendingBadge.current?.abort();
  }, []);
  async function refreshBadge() {
    pendingBadge.current?.abort();
    const controller = new AbortController(); pendingBadge.current = controller;
    try {
      const data = await pickupApi.getNotifications(1, controller.signal);
      if (!controller.signal.aborted) useEmployeeNoticeStore.getState().setUnread(data.unreadCount);
    } catch { /* Màn hình thông báo có lỗi/thử lại riêng; không chặn chuyển tab. */ }
  }
  return (
    <Tabs.Navigator
      id="EmployeeTabs"
      screenListeners={{ focus: refreshBadge }}
      screenOptions={({ route }) => ({
        tabBarActiveTintColor: "#446900",
        tabBarInactiveTintColor: "#9ca3af",
        tabBarStyle: { backgroundColor: "#ffffff", borderTopColor: "#e5e7eb" },
        tabBarLabelStyle: { fontFamily: "Inter_600SemiBold", fontSize: 11 },
        headerStyle: { backgroundColor: "#f8f9ff" },
        headerTintColor: "#446900",
        headerTitle: () => <Brand />,
        tabBarIcon: ({ color, size }) => (
          <Feather name={icons[route.name]} color={color} size={size} />
        ),
      })}
    >
      <Tabs.Screen
        name="Dashboard"
        component={DashboardScreen}
        options={{ title: "Trang chủ" }}
      />
      <Tabs.Screen
        name="Pool"
        component={PickupStackNavigator}
        options={{ title: "Đơn chờ", headerShown: false }}
      />
      <Tabs.Screen name="History" component={HistoryStatsScreen} options={{ title: "Lịch sử" }} />
      <Tabs.Screen name="Notifications" component={NotificationsScreen} options={{ title: "Thông báo", tabBarBadge: unread || undefined }} />
      <Tabs.Screen
        name="Profile"
        component={ProfileNavigator}
        options={{ title: "Cá nhân", headerShown: false }}
      />
    </Tabs.Navigator>
  );
}
