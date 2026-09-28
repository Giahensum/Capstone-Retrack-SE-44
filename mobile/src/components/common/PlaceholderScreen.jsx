import { StyleSheet, Text, View } from "react-native";
import Feather from "@expo/vector-icons/Feather";
export default function PlaceholderScreen({ title, useCase, icon }) {
  return (
    <View style={styles.container}>
      <View style={styles.icon}>
        <Feather name={icon} size={34} color="#446900" />
      </View>
      <Text style={styles.title}>{title}</Text>
      <Text style={styles.subtitle}>
        {useCase} — Sẽ được triển khai trong PR tiếp theo
      </Text>
    </View>
  );
}
const styles = StyleSheet.create({
  container: {
    flex: 1,
    alignItems: "center",
    justifyContent: "center",
    padding: 28,
    backgroundColor: "#f8f9ff",
    gap: 16,
  },
  icon: { padding: 22, backgroundColor: "#eff4ff", borderRadius: 28 },
  title: {
    fontFamily: "Inter_700Bold",
    fontSize: 24,
    color: "#0b1c30",
    textAlign: "center",
  },
  subtitle: {
    fontFamily: "Inter_400Regular",
    fontSize: 15,
    lineHeight: 24,
    color: "#424936",
    textAlign: "center",
  },
});
