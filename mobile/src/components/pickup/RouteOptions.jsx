import { Pressable, StyleSheet, Text, View } from "react-native";
import { ROUTE_MODES, ROUTE_VEHICLES } from "../../helpers/routePreferences";
import { colors, styles as s } from "../../theme";

function Choice({ label, selected, onPress }) {
  return (
    <Pressable
      accessibilityRole="radio"
      accessibilityState={{ checked: selected }}
      onPress={onPress}
      style={({ pressed }) => [
        styles.choice,
        selected && styles.selected,
        pressed && { opacity: 0.7 },
      ]}
    >
      <Text style={[s.label, styles.choiceText]}>
        {selected ? "● " : "○ "}{label}
      </Text>
    </Pressable>
  );
}

export default function RouteOptions({ vehicle, mode, onVehicleChange, onModeChange }) {
  const preference = ROUTE_MODES.find((option) => option.value === mode);
  return (
    <View style={s.card}>
      <Text style={s.text}>Bạn đi bằng phương tiện nào?</Text>
      <View style={styles.choices}>
        {ROUTE_VEHICLES.map((option) => (
          <Choice
            key={option.value}
            label={option.label}
            selected={vehicle === option.value}
            onPress={() => onVehicleChange(option.value)}
          />
        ))}
      </View>
      <Text style={s.text}>Ưu tiên tuyến đường</Text>
      <View style={styles.choices}>
        {ROUTE_MODES.map((option) => (
          <Choice
            key={option.value}
            label={option.label}
            selected={mode === option.value}
            onPress={() => onModeChange(option.value)}
          />
        ))}
      </View>
      <Text style={s.muted}>{preference?.description}</Text>
      {!vehicle ? (
        <Text style={s.muted}>Chọn phương tiện để tìm tuyến từ vị trí của bạn đến người bán.</Text>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  choices: { flexDirection: "row", flexWrap: "wrap", gap: 8 },
  choice: {
    minHeight: 48,
    justifyContent: "center",
    paddingHorizontal: 16,
    paddingVertical: 12,
    borderRadius: 18,
    borderWidth: 1,
    borderColor: colors.border,
  },
  selected: { backgroundColor: colors.lime, borderColor: colors.primary },
  choiceText: { color: colors.ink, fontSize: 14 },
});
