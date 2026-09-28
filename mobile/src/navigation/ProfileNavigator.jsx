import { createStackNavigator } from "@react-navigation/stack";
import ProfileScreen from "../screens/employee/ProfileScreen";
import EditProfileScreen from "../screens/employee/EditProfileScreen";
import Brand from "../components/common/Brand";

const Stack = createStackNavigator();
export default function ProfileNavigator() {
  return (
    <Stack.Navigator
      id="ProfileStack"
      screenOptions={{
        headerStyle: { backgroundColor: "#f8f9ff" },
        headerTintColor: "#446900",
        headerTitleStyle: { fontFamily: "Inter_700Bold" },
        headerShadowVisible: false,
      }}
    >
      <Stack.Screen
        name="ProfileDetails"
        component={ProfileScreen}
        options={{ headerTitle: () => <Brand /> }}
      />
      <Stack.Screen
        name="EditProfile"
        component={EditProfileScreen}
        options={{ title: "Chỉnh sửa hồ sơ" }}
      />
    </Stack.Navigator>
  );
}
