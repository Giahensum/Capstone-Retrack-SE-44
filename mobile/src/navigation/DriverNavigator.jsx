import { createBottomTabNavigator } from "@react-navigation/bottom-tabs";
import Feather from "@expo/vector-icons/Feather";
import ProfileNavigator from "./ProfileNavigator";
import DashboardScreen from "../screens/driver/DashboardScreen";
import JobPoolScreen from "../screens/driver/JobPoolScreen";
import Brand from "../components/common/Brand";
import PlaceholderScreen from "../components/common/PlaceholderScreen";
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
        component={JobPoolScreen}
        options={{ title: "Chuyến xe" }}
      />
      <Tabs.Screen name="History" options={{ title: "Lịch sử" }}>
        {() => <PlaceholderScreen title="Lịch sử chuyến" useCase="UC-68/69" icon="clock" />}
      </Tabs.Screen>
      <Tabs.Screen
        name="Profile"
        component={ProfileNavigator}
        options={{ title: "Hồ sơ", headerShown: false }}
      />
    </Tabs.Navigator>
  );
}
