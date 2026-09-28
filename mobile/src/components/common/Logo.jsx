import { Image, View } from "react-native";
import { LOGO_URL } from "../../config/branding";
export default function Logo({ large = false }) {
  // Preserve the supplied PNG. Clip its original transparent margins in the view only.
  const size = large ? 112 : 40;
  return (
    <View style={{ width: size, height: size, overflow: "hidden" }}>
      <Image
        accessibilityLabel="Logo ReTrack"
        source={{ uri: LOGO_URL }}
        style={{
          position: "absolute",
          width: size * 3.5,
          height: size * 1.93,
          left: -size * 1.25,
          top: -size * 0.37,
        }}
        resizeMode="contain"
      />
    </View>
  );
}
