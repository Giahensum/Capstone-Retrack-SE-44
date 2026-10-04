import { useCallback, useRef, useState } from "react";
import { Alert, Image, Linking, Text, View } from "react-native";
import Screen from "../../components/common/Screen";
import Button from "../../components/common/Button";
import ResourceState from "../../components/pickup/ResourceState";
import usePickupResource from "../../hooks/usePickupResource";
import { pickupApi } from "../../api/pickupApi";
import { errorMessage } from "../../api/client";
import { captureCheckInPhoto, captureCheckInLocation } from "../../helpers/checkInCapture";
import { checkInLocationError, distanceToSeller, pickupStatus } from "../../helpers/collection";
import { formatViDatetime } from "../../helpers/format";
import { styles as s } from "../../theme";

export default function CheckInScreen({ route, navigation }) {
  const id = route.params.pickupId;
  const loader = useCallback((signal) => pickupApi.getCollection(id, signal), [id]);
  const { data, setData, loading, error, reload } = usePickupResource(loader);
  const [photo, setPhoto] = useState(null);
  const [location, setLocation] = useState(null);
  const [busy, setBusy] = useState("");
  const [actionError, setActionError] = useState("");
  const pending = useRef(false);
  async function action(name, work) {
    if (pending.current) return;
    pending.current = true; setBusy(name); setActionError("");
    try { await work(); } catch (e) { setActionError(errorMessage(e)); }
    finally { pending.current = false; setBusy(""); }
  }
  const distance = data && location ? distanceToSeller(location, data) : null;
  return <Screen>
    <ResourceState loading={loading} error={error} retry={() => reload()} />
    {data && !loading ? <>
      <Text style={s.title}>Check-in tại nhà người bán</Text>
      <View style={s.card}><Text style={s.text}>{data.address}</Text><Text style={s.label}>{pickupStatus(data.status)}</Text></View>
      {data.checkIn ? <>
        <Image source={{ uri: data.checkIn.imageUrl }} style={{ width: "100%", height: 300, borderRadius: 24 }} resizeMode="contain" accessibilityLabel="Ảnh check-in đã lưu" />
        <View style={s.card}>
          <Text style={s.text}>Đã check-in: {formatViDatetime(data.checkIn.checkedInAt)}</Text>
          <Text style={s.muted}>Cách địa điểm {Math.round(data.checkIn.distanceMeters)} m · Sai số GPS {Math.round(data.checkIn.accuracyMeters)} m</Text>
        </View>
        <Button title={data.canEdit ? "Tiếp tục phân loại và cân" : "Xem kết quả cân"} onPress={() => navigation.replace("ClassifyWeigh", { pickupId: id })} />
      </> : data.status !== "SCHEDULED" ? <Text style={s.error}>Đơn không ở trạng thái cho phép check-in. Hãy quay lại để kiểm tra.</Text> : <>
        <Text style={s.muted}>Chụp ảnh tại địa điểm và lấy GPS chính xác. Cần trong bán kính {data.policy.radiusMeters} m, sai số tối đa {data.policy.maxAccuracyMeters} m.</Text>
        {photo ? <Image source={{ uri: photo.uri }} style={{ width: "100%", height: 300, borderRadius: 24 }} resizeMode="contain" accessibilityLabel="Ảnh vừa chụp để check-in" /> : <View style={[s.card, { minHeight: 180, justifyContent: "center" }]}><Text style={s.muted}>Chụp rõ địa điểm hoặc phế liệu với sự đồng ý của người bán.</Text></View>}
        <Button title={photo ? "Chụp lại ảnh" : "Chụp ảnh tại địa điểm"} loading={busy === "photo"} disabled={!!busy} onPress={() => action("photo", async () => {
          const captured = await captureCheckInPhoto();
          if (captured) { setPhoto(captured); setLocation(null); setLocation(await captureCheckInLocation()); }
        })} />
        <View style={s.card}>
          <Text style={s.text}>{location ? `Sai số GPS: ${Math.round(location.accuracyMeters ?? 0)} m` : "Chưa lấy vị trí GPS cho lần check-in này."}</Text>
          {distance != null ? <Text style={s.text}>Cách địa điểm: {Math.round(distance)} m</Text> : null}
          {location ? <Text style={s.muted}>{checkInLocationError(location, data) || "Vị trí phù hợp để check-in."}</Text> : null}
          <Button title="Lấy lại GPS chính xác" variant="secondary" loading={busy === "gps"} disabled={!!busy} onPress={() => action("gps", async () => setLocation(await captureCheckInLocation()))} />
          <Button title="Mở cài đặt quyền ứng dụng" variant="secondary" disabled={!!busy} onPress={() => Linking.openSettings()} />
        </View>
        <Button title="Xác nhận có mặt" loading={busy === "save"} disabled={!photo || !location || !!busy} onPress={() => action("save", async () => {
          const validation = checkInLocationError(location, data);
          if (validation) throw new Error(validation);
          if (Date.now() - Date.parse(photo.takenAt) > 600000) throw new Error("Ảnh đã quá 10 phút. Hãy chụp lại.");
          const result = await pickupApi.checkIn(id, photo, location);
          setData(result); setPhoto(null);
          Alert.alert("Check-in thành công", "Bạn có thể bắt đầu phân loại và cân phế liệu.");
        })} />
      </>}
      {actionError ? <Text accessibilityRole="alert" style={s.error}>{actionError}</Text> : null}
    </> : null}
  </Screen>;
}
