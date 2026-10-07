import * as ImagePicker from "expo-image-picker";
import * as Location from "expo-location";
import { ImageManipulator, SaveFormat } from "expo-image-manipulator";

export async function captureCheckInPhoto() {
  if (!(await ImagePicker.requestCameraPermissionsAsync()).granted)
    throw new Error("Cần quyền camera. Hãy cấp quyền trong cài đặt ứng dụng để check-in.");
  const result = await ImagePicker.launchCameraAsync({ mediaTypes: ["images"], allowsEditing: false, quality: 0.85 });
  if (result.canceled || !result.assets?.[0]) return null;
  const takenAt = new Date().toISOString();
  const asset = result.assets[0];
  const context = ImageManipulator.manipulate(asset.uri);
  if (Math.max(asset.width, asset.height) > 1600)
    context.resize(asset.width >= asset.height ? { width: 1600 } : { height: 1600 });
  const rendered = await context.renderAsync();
  const saved = await rendered.saveAsync({ format: SaveFormat.JPEG, compress: 0.8 });
  return { uri: saved.uri, takenAt };
}

export async function captureCheckInLocation() {
  if (!(await Location.requestForegroundPermissionsAsync()).granted)
    throw new Error("Cần quyền vị trí chính xác để check-in. Hãy cấp quyền trong cài đặt ứng dụng.");
  if (!(await Location.hasServicesEnabledAsync())) throw new Error("Hãy bật GPS/dịch vụ vị trí trên điện thoại.");
  let timer;
  try {
    const position = await Promise.race([
      Location.getCurrentPositionAsync({ accuracy: Location.Accuracy.High }),
      new Promise((_, reject) => { timer = setTimeout(() => reject(new Error("Chưa lấy được GPS. Hãy ra nơi thoáng và thử lại.")), 30000); }),
    ]);
    return {
      latitude: position.coords.latitude, longitude: position.coords.longitude,
      accuracyMeters: position.coords.accuracy, locationRecordedAt: new Date(position.timestamp).toISOString(),
      isMocked: position.mocked === true,
    };
  } finally { clearTimeout(timer); }
}
