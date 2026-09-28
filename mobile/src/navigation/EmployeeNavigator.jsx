import { createBottomTabNavigator } from "@react-navigation/bottom-tabs";
import Feather from "@expo/vector-icons/Feather";
import ProfileNavigator from "./ProfileNavigator";
import DashboardScreen from "../screens/employee/DashboardScreen";
import PickupPoolScreen from "../screens/employee/PickupPoolScreen";
import MapScreen from "../screens/employee/MapScreen";
import NotificationsScreen from "../screens/employee/NotificationsScreen";
const Tabs = createBottomTabNavigator();
const icons = {
  Dashboard: "home",
  Pool: "list",
  Map: "map-pin",
  Notifications: "bell",
  Profile: "user",
};
export default function EmployeeNavigator() {
  return (
    <Tabs.Navigator
      id="EmployeeTabs"
      screenOptions={({ route }) => ({
        tabBarActiveTintColor: "#446900",
        tabBarInactiveTintColor: "#9ca3af",
        tabBarStyle: { backgroundColor: "#ffffff", borderTopColor: "#e5e7eb" },
        tabBarLabelStyle: { fontFamily: "Inter_600SemiBold", fontSize: 11 },
        headerStyle: { backgroundColor: "#f8f9ff" },
        headerTintColor: "#446900",
        tabBarIcon: ({ color, size }) => (
          <Feather name={icons[route.name]} color={color} size={size} />
        ),
      })}
    >
      <Tabs.Screen
        name="Dashboard"
        component={DashboardScreen}
        options={{ title: "Dashboard" }}
      />
      <Tabs.Screen
        name="Pool"
        component={PickupPoolScreen}
        options={{ title: "Đơn hàng" }}
      />
      <Tabs.Screen
        name="Map"
        component={MapScreen}
        options={{ title: "Bản đồ" }}
      />
      <Tabs.Screen
        name="Notifications"
        component={NotificationsScreen}
        options={{ title: "Thông báo" }}
      />
      <Tabs.Screen
        name="Profile"
        component={ProfileNavigator}
        options={{ title: "Hồ sơ", headerShown: false }}
      />
    </Tabs.Navigator>
  );
}
