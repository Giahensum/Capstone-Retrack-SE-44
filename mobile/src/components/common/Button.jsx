import { ActivityIndicator, Pressable, Text } from "react-native";
import { colors } from "../../theme";
export default function Button({
  title,
  onPress,
  loading = false,
  variant = "primary",
  disabled = false,
}) {
  const bg =
    variant === "primary"
      ? colors.lime
      : variant === "danger"
        ? "#ffdad6"
        : "transparent";
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityState={{ disabled: loading || disabled, busy: loading }}
      disabled={loading || disabled}
      onPress={onPress}
      style={({ pressed }) => ({
        minHeight: 54,
        paddingVertical: 15,
        paddingHorizontal: 20,
        borderRadius: 99,
        backgroundColor: bg,
        borderWidth: variant === "secondary" ? 1 : 0,
        borderColor: colors.border,
        opacity: loading || disabled ? 0.55 : pressed ? 0.75 : 1,
        alignItems: "center",
        justifyContent: "center",
      })}
    >
      {loading ? (
        <ActivityIndicator color={colors.primary} />
      ) : (
        <Text
          style={{
            fontFamily: "Inter_700Bold",
            fontSize: 15,
            color: variant === "danger" ? "#93000a" : colors.ink,
          }}
        >
          {title}
        </Text>
      )}
    </Pressable>
  );
}
