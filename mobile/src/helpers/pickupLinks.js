import { Alert, Linking, Platform } from "react-native";
import { coordinatesOf } from "./coordinates";

export async function callSeller(phone) {
  const number = String(phone || "").replace(/[\s().-]/g, "");
  if (!/^\+?\d{6,15}$/.test(number)) {
    Alert.alert("Chưa có số điện thoại hợp lệ");
    return;
  }
  try {
    await Linking.openURL(`tel:${number}`);
  } catch {
    Alert.alert(
      "Không thể mở cuộc gọi",
      "Thiết bị này không có ứng dụng gọi điện. Số người bán: " + number,
    );
  }
}

export async function openSellerMaps(pickup, vehicle = "car") {
  const point = coordinatesOf(pickup);
  const destination = point
    ? `${point.latitude},${point.longitude}`
    : pickup.address;
  if (!destination) return;
  const encoded = encodeURIComponent(destination);
  const motorcycle = vehicle === "motorbike";
  if (Platform.OS === "android") {
    try {
      await Linking.openURL(`google.navigation:q=${encoded}&mode=${motorcycle ? "l" : "d"}`);
      return;
    } catch {
      /* Browser fallback. */
    }
  }
  try {
    await Linking.openURL(
      `https://www.google.com/maps/dir/?api=1&destination=${encoded}&travelmode=${motorcycle ? "two-wheeler" : "driving"}`,
    );
  } catch {
    Alert.alert(
      "Không thể mở bản đồ",
      "Vui lòng kiểm tra ứng dụng bản đồ hoặc trình duyệt.",
    );
  }
}
