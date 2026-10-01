import { ActivityIndicator, StyleSheet, Text, View } from "react-native";
export default function LoadingSpinner() {
  return (
    <View style={styles.container}>
      <ActivityIndicator size="large" color="#446900" />
      <Text style={styles.label}>Đang tải...</Text>
    </View>
  );
}
const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: "#f8f9ff",
    alignItems: "center",
    justifyContent: "center",
    gap: 16,
  },
  label: { color: "#424936", fontSize: 15 },
});
