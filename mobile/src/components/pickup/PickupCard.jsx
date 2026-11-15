import { Pressable, Text, View } from "react-native";
import Feather from "@expo/vector-icons/Feather";
import Button from "../common/Button";
import { formatViDatetime } from "../../helpers/format";
import { callSeller } from "../../helpers/pickupLinks";
import { colors, styles as s } from "../../theme";

export default function PickupCard({
  pickup,
  onDetail,
  onAccept = undefined,
  busy = false,
  disabled = false,
}) {
  return (
    <View
      style={[
        s.card,
        {
          borderLeftWidth: 5,
          borderLeftColor: colors.lime,
          borderRadius: 20,
          padding: 18,
          gap: 12,
        },
      ]}
    >
      <View style={s.row}>
        <Text
          style={[
            s.text,
            { flex: 1, fontFamily: "Inter_700Bold", fontSize: 18 },
          ]}
        >
          {pickup.sellerName}
        </Text>
        {pickup.sellerPhone || pickup.phone ? (
          <Pressable
            accessibilityRole="button"
            accessibilityLabel={`Gọi ${pickup.sellerName}`}
            style={{ padding: 12 }}
            onPress={() => callSeller(pickup.sellerPhone || pickup.phone)}
          >
            <Feather name="phone" size={21} color={colors.primary} />
          </Pressable>
        ) : null}
      </View>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel="Xem chi tiết đơn"
        onPress={onDetail}
        style={{ gap: 9 }}
      >
        <Text style={s.text}>📍 {pickup.address}</Text>
        <Text style={s.muted}>
          ◷ {formatViDatetime(pickup.preferredDatetime)}
        </Text>
        {pickup.description ? (
          <Text style={s.muted} numberOfLines={2}>
            {pickup.description}
          </Text>
        ) : null}
        <Text style={[s.label, { color: colors.primary }]}>Xem chi tiết →</Text>
      </Pressable>
      {onAccept ? (
        <Button
          title="Nhận đơn →"
          onPress={() => onAccept(pickup)}
          loading={busy}
          disabled={disabled}
        />
      ) : null}
    </View>
  );
}
