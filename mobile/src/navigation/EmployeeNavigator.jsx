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
import PlaceholderScreen from "../components/common/PlaceholderScreen";
import Brand from "../components/common/Brand";
const Tabs = createBottomTabNavigator();
const Stack = createStackNavigator();
const icons = {
  Dashboard: "home",
  Pool: "list",
  History: "clock",
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
      <Tabs.Screen name="History" options={{ title: "Lịch sử" }}>
        {() => (
          <PlaceholderScreen title="Lịch sử" useCase="UC-57/58" icon="clock" />
        )}
      </Tabs.Screen>
      <Tabs.Screen
        name="Profile"
        component={ProfileNavigator}
        options={{ title: "Cá nhân", headerShown: false }}
      />
    </Tabs.Navigator>
  );
}
