import { Text, View } from "react-native";
import Screen from "../../components/common/Screen";
import Button from "../../components/common/Button";
import Avatar from "../../components/common/Avatar";
import useAuthStore from "../../store/authStore";
import { styles as s } from "../../theme";

export default function DashboardScreen({ navigation }) {
  const user = useAuthStore(state => state.user);
  if (!user) return null;
  return <Screen>
    <Text style={s.title}>Xin chào, {user.fullName}</Text>
    <Text style={s.muted}>Không gian làm việc của tài xế</Text>
    <View style={[s.card, { alignItems: "center" }]}>
      <Avatar name={user.fullName} uri={user.avatarUrl} />
      <Text style={s.label}>Mã tài xế: {user.employeeCode}</Text>
      <Text style={s.text}>{user.depotName}</Text>
      <Text style={s.muted}>{user.depotAddress}</Text>
    </View>
    <Button title="Xem và cập nhật hồ sơ" onPress={() => navigation.navigate("Profile")} />
    <View style={s.card}>
      <Text style={s.label}>Chuyến vận chuyển</Text>
      <Text style={s.muted}>Xem chuyến chờ, nhận chuyến và tra cứu tuyến đường từ kho đến nhà máy.</Text>
      <Button title="Xem chuyến vận chuyển" onPress={() => navigation.navigate("Jobs")} />
    </View>
  </Screen>;
}
