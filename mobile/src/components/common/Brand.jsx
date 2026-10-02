import { Text, View } from "react-native";
import { colors, styles as s } from "../../theme";
import Logo from "./Logo";
export default function Brand() {
  return (
    <View style={s.row}>
      <Logo />
      <Text style={[s.title, { fontSize: 20, color: colors.primary }]}>
        RETRACK
      </Text>
    </View>
  );
}
