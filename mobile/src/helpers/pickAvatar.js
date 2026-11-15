import { Alert } from "react-native";
import * as ImagePicker from "expo-image-picker";
import { ImageManipulator, SaveFormat } from "expo-image-manipulator";

/** @param {string} uri */
async function processImage(uri) {
  const context = ImageManipulator.manipulate(uri);
  context.resize({ width: 512 });
  const image = await context.renderAsync();
  const saved = await image.saveAsync({
    format: SaveFormat.JPEG,
    compress: 0.85,
  });
  return saved.uri;
}

async function pickFromLibrary() {
  const result = await ImagePicker.launchImageLibraryAsync({
    mediaTypes: ["images"],
    allowsEditing: true,
    aspect: [1, 1],
    quality: 0.85,
  });
  if (result.canceled) return null;
  return processImage(result.assets[0].uri);
}

async function pickFromCamera() {
  const { status } = await ImagePicker.requestCameraPermissionsAsync();
  if (status !== "granted") {
    Alert.alert(
      "Cần quyền camera",
      "Vui lòng cấp quyền camera trong cài đặt ứng dụng.",
    );
    return null;
  }
  const result = await ImagePicker.launchCameraAsync({
    mediaTypes: ["images"],
    allowsEditing: true,
    aspect: [1, 1],
    quality: 0.85,
  });
  if (result.canceled) return null;
  return processImage(result.assets[0].uri);
}

/** @returns {Promise<string | null>} */
export function pickAvatar() {
  return new Promise((resolve) => {
    Alert.alert(
      "Chọn ảnh đại diện",
      "Bạn muốn lấy ảnh từ đâu?",
      [
        {
          text: "Chụp ảnh",
          onPress: () => pickFromCamera().then(resolve).catch(() => resolve(null)),
        },
        {
          text: "Chọn từ thư viện",
          onPress: () => pickFromLibrary().then(resolve).catch(() => resolve(null)),
        },
        {
          text: "Hủy",
          style: "cancel",
          onPress: () => resolve(null),
        },
      ],
      { cancelable: true, onDismiss: () => resolve(null) },
    );
  });
}
