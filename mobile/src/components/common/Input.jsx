import { Text, TextInput, View } from "react-native";
import { colors, styles as s } from "../../theme";
export default function Input({ label, error, ...props }) {
  return (
    <View style={{ gap: 8 }}>
      <Text style={s.label}>{label}</Text>
      <TextInput
        accessibilityLabel={label}
        placeholderTextColor="#747968"
        {...props}
        style={[
          s.input,
          props.style,
          error ? { borderColor: colors.error } : null,
        ]}
      />
      {error ? (
        <Text accessibilityLiveRegion="polite" style={s.error}>
          {error}
        </Text>
      ) : null}
    </View>
  );
}
