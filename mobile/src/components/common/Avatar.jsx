import { Image, Text, View } from "react-native";
import { colors, styles as s } from "../../theme";
export default function Avatar({ name, uri }) {
  return uri ? (
    <Image
      source={{ uri }}
      accessibilityLabel="Ảnh đại diện"
      style={{
        width: 100,
        height: 100,
        borderRadius: 50,
        backgroundColor: colors.low,
      }}
    />
  ) : (
    <View
      style={{
        width: 100,
        height: 100,
        borderRadius: 50,
        backgroundColor: colors.mint,
        alignItems: "center",
        justifyContent: "center",
      }}
    >
      <Text style={[s.title, { color: colors.primary }]}>
        {name
          .trim()
          .split(/\s+/)
          .slice(-2)
          .map((x) => x[0])
          .join("")
          .toUpperCase()}
      </Text>
    </View>
  );
}
