import { createBottomTabNavigator } from "@react-navigation/bottom-tabs";
import Feather from "@expo/vector-icons/Feather";
import ProfileNavigator from "./ProfileNavigator";
import DashboardScreen from "../screens/driver/DashboardScreen";
import JobPoolScreen from "../screens/driver/JobPoolScreen";
import MapScreen from "../screens/driver/MapScreen";
import NotificationsScreen from "../screens/driver/NotificationsScreen";
const Tabs = createBottomTabNavigator();
const icons = {
  Dashboard: "home",
  Jobs: "list",
  Map: "map-pin",
  Notifications: "bell",
  Profile: "user",
};
export default function DriverNavigator() {
  return (
    <Tabs.Navigator
      id="DriverTabs"
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
        name="Jobs"
        component={JobPoolScreen}
        options={{ title: "Chuyến xe" }}
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
