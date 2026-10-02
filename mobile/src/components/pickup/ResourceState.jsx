import { ActivityIndicator, Text, View } from "react-native";
import Button from "../common/Button";
import { colors, styles as s } from "../../theme";

export default function ResourceState({ loading, error, retry }) {
  if (loading)
    return (
      <View style={{ padding: 35 }}>
        <ActivityIndicator
          size="large"
          color={colors.primary}
          accessibilityLabel="Đang tải đơn thu gom"
        />
      </View>
    );
  if (error)
    return (
      <View style={s.card}>
        <Text style={s.error} accessibilityLiveRegion="polite">
          {error}
        </Text>
        <Button title="Thử lại" onPress={retry} />
      </View>
    );
  return null;
}
