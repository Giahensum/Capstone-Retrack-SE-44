import { createBottomTabNavigator } from "@react-navigation/bottom-tabs";
import Feather from "@expo/vector-icons/Feather";
import ProfileNavigator from "./ProfileNavigator";
import DashboardScreen from "../screens/driver/DashboardScreen";
import JobPoolScreen from "../screens/driver/JobPoolScreen";
import Brand from "../components/common/Brand";
import HistoryScreen from "../screens/driver/HistoryScreen";
import { createStackNavigator } from "@react-navigation/stack";
import JobDetailScreen from "../screens/driver/JobDetailScreen";
import MapScreen from "../screens/driver/MapScreen";
import NotificationBell from "../components/common/NotificationBell";
import CheckInDeliveryScreen from "../screens/driver/CheckInDeliveryScreen";
const Stack = createStackNavigator();
function JobNavigator() {
  return <Stack.Navigator id="DriverJobs" screenOptions={{ headerRight: () => <NotificationBell />, headerTintColor: "#446900", headerStyle: { backgroundColor: "#f8f9ff" }, headerShadowVisible: false }}>
    <Stack.Screen name="JobPool" component={JobPoolScreen} options={{ title: "Chuyến vận chuyển" }} />
    <Stack.Screen name="JobDetail" component={JobDetailScreen} options={{ title: "Chi tiết chuyến" }} />
    <Stack.Screen name="JobMap" component={MapScreen} options={{ title: "Bản đồ chuyến" }} />
    <Stack.Screen name="DriverDelivery" component={CheckInDeliveryScreen} options={{ title: "Lấy hàng / Giao hàng" }} />
  </Stack.Navigator>;
}
const Tabs = createBottomTabNavigator();
const icons = {
  Dashboard: "home",
  Jobs: "list",
  Profile: "user",
  History: "clock",
};
export default function DriverNavigator() {
  return (
    <Tabs.Navigator
      id="DriverTabs"
      screenOptions={({ route }) => ({
        headerRight: () => <NotificationBell />,
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
        name="Jobs"
        component={JobNavigator}
        options={{ title: "Chuyến xe", headerShown: false }}
      />
      <Tabs.Screen name="History" component={HistoryScreen} options={{ title: "Lịch sử" }} />
      <Tabs.Screen
        name="Profile"
        component={ProfileNavigator}
        options={{ title: "Hồ sơ", headerShown: false }}
      />
    </Tabs.Navigator>
  );
}
