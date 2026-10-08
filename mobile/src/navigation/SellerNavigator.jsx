import React from 'react';
import { createStackNavigator } from '@react-navigation/stack';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { Ionicons } from '@expo/vector-icons';
import { useTheme } from '@react-navigation/native';
import SellerHomeScreen from '../screens/seller/SellerHomeScreen';
import CreateRequestScreen from '../screens/seller/CreateRequestScreen';
import ProfileNavigator from './ProfileNavigator';
import { styles } from '../theme';

const Tab = createBottomTabNavigator();
const Stack = createStackNavigator();

function SellerTabs() {
  const { colors } = useTheme();
  return (
    <Tab.Navigator
      screenOptions={({ route }) => ({
        headerShown: true,
        headerStyle: { backgroundColor: '#fff' },
        headerTitleStyle: { fontWeight: 'bold' },
        tabBarStyle: { paddingBottom: 5, height: 60, backgroundColor: '#fff' },
        tabBarActiveTintColor: '#22c55e',
        tabBarInactiveTintColor: '#94a3b8',
        tabBarIcon: ({ focused, color, size }) => {
          let iconName;
          if (route.name === 'Home') iconName = focused ? 'home' : 'home-outline';
          else if (route.name === 'ProfileTab') iconName = focused ? 'person' : 'person-outline';
          return <Ionicons name={iconName} size={size} color={color} />;
        },
      })}
    >
      <Tab.Screen name="Home" component={SellerHomeScreen} options={{ title: 'Bảng điều khiển' }} />
      <Tab.Screen name="ProfileTab" component={ProfileNavigator} options={{ title: 'Tài khoản' }} />
    </Tab.Navigator>
  );
}

export default function SellerNavigator() {
  return (
    <Stack.Navigator screenOptions={{ headerShown: false }}>
      <Stack.Screen name="SellerTabs" component={SellerTabs} />
      <Stack.Screen name="CreateRequest" component={CreateRequestScreen} options={{ headerShown: true, title: 'Tạo đơn thu gom', headerBackTitle: 'Quay lại' }} />
    </Stack.Navigator>
  );
}
