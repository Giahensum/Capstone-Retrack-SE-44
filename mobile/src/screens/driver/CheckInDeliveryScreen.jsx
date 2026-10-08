import { useCallback, useRef, useState } from "react";
import { Alert, Image, Linking, Text, TextInput, View } from "react-native";
import Screen from "../../components/common/Screen";
import Button from "../../components/common/Button";
import ResourceState from "../../components/pickup/ResourceState";
import usePickupResource from "../../hooks/usePickupResource";
import { driverApi } from "../../api/driverApi";
import { errorMessage } from "../../api/client";
import { captureCheckInPhoto, captureCheckInLocation } from "../../helpers/checkInCapture";
import { checkInLocationError, distanceToSeller } from "../../helpers/collection";
import { jobStatus } from "../../helpers/driverJobs";
import { styles as s } from "../../theme";

// Mã chống gửi trùng, không dùng làm token xác thực hay bí mật.
function operationId() {
  return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, c => {
    const value = Math.floor(Math.random() * 16);
    return (c === "x" ? value : (value & 3) | 8).toString(16);
  });
}
const labels = { checkin: "Đã lấy hàng tại kho", start: "Đã khởi hành", checkout: "Đã báo giao hàng",
  cancel: "Đã trả chuyến", reject: "Đã từ chối", incident: "Đã báo sự cố" };

export default function CheckInDeliveryScreen({ route, navigation }) {
  const id = route.params.jobId;
  const loader = useCallback(signal => driverApi.delivery(id, signal), [id]);
  const { data, setData, loading, error, reload } = usePickupResource(loader);
  const [capturedPhoto, setPhoto] = useState(null), [location, setLocation] = useState(null);
  const [reason, setReason] = useState(""), [busy, setBusy] = useState(""), [failure, setFailure] = useState("");
  const pending = useRef(false), operation = useRef(null);
  async function run(name, work) {
    if (pending.current) return;
    pending.current = true; setBusy(name); setFailure("");
    try { await work(); }
    catch (e) { setFailure(errorMessage(e)); }
    finally { pending.current = false; setBusy(""); }
  }
  const job = data?.job;
  const stage = job?.status === "ACCEPTED" ? "checkin" : ["IN_TRANSIT", "ON_THE_WAY"].includes(job?.status) ? "checkout" : null;
  // Không dùng lại ảnh lấy hàng nếu chuyến chuyển sang bước giao hàng ở phiên khác.
  const photo = capturedPhoto?.stage === stage ? capturedPhoto : null;
  const destination = job ? { ...(stage === "checkin" ? job.depot : job.factory), policy: data.policy } : null;
  const distance = location && destination ? distanceToSeller(location, destination) : null;
  const reasonAction = job?.isMine ? (job.status === "ACCEPTED" ? "cancel" : "incident") : "reject";
  const canReason = job && ((!job.isMine && job.status === "PENDING") || (job.isMine && ["ACCEPTED", "PICKED_UP", "IN_TRANSIT", "ON_THE_WAY"].includes(job.status)));
  async function submit(action) {
    await run(action, async () => {
      const text = ["cancel", "reject", "incident"].includes(action) ? reason.trim() : undefined;
      if (text !== undefined && (text.length < 10 || text.length > 1000)) throw new Error("Lý do cần từ 10 đến 1.000 ký tự.");
      const signature = JSON.stringify([action, text, photo?.uri, location]);
      if (operation.current?.signature !== signature) operation.current = { signature, id: operationId() };
      if (action === "checkin" || action === "checkout") {
        if (!photo) throw new Error("Hãy chụp ảnh tại địa điểm.");
        const validation = checkInLocationError(location, destination);
        if (validation) throw new Error(validation);
        if (Date.now() - Date.parse(photo.takenAt) > 600000) throw new Error("Ảnh đã quá 10 phút. Hãy chụp lại.");
        await driverApi.evidence(id, action, operation.current.id, photo, location);
      } else await driverApi.action(id, action, operation.current.id, text);
      if (action === "cancel" || action === "reject") {
        Alert.alert(labels[action], action === "cancel" ? "Chuyến đã về pool. Chủ kho được thông báo." : "Chuyến vẫn dành cho tài xế khác.");
        navigation.popToTop(); return;
      }
      // Tải lại dữ liệu đã lưu để bước tiếp theo và bằng chứng phản ánh backend.
      setData(await driverApi.delivery(id));
      operation.current = null; setPhoto(null); setLocation(null); setReason("");
      Alert.alert(labels[action], action === "checkout" ? "Chờ nhà máy xác nhận nhận hàng và kiểm định." : action === "incident" ? "Chủ kho và nhà máy đã được thông báo. Giữ hàng an toàn và liên hệ chủ kho." : "Đã lưu thành công.");
    });
  }
  const confirm = (action, title, message) => Alert.alert(title, message, [
    { text: "Quay lại", style: "cancel" }, { text: "Xác nhận", onPress: () => submit(action) },
  ]);
  return <Screen>
    <Text style={s.title}>Lấy hàng và giao hàng</Text>
    <ResourceState loading={loading} error={error} retry={() => reload()} />
    {job && !loading ? <>
      <View style={s.card}><Text style={s.label}>{job.batchCode || job.batchId}</Text><Text style={s.text}>{jobStatus(job.status)}</Text></View>
      <Button title="Tải lại trạng thái chuyến" variant="secondary" disabled={!!busy} onPress={() => reload()} />
      {stage && job.isMine ? <>
        <Text style={s.label}>{stage === "checkin" ? "Check-in lấy hàng tại kho" : "Check-out tại nhà máy"}</Text>
        <Text style={s.text}>{destination.name} · {destination.address}</Text>
        <Text style={s.muted}>Cần ảnh camera mới chụp; GPS trong bán kính {data.policy.radiusMeters} m, sai số tối đa {data.policy.maxAccuracyMeters} m.</Text>
        {photo ? <Image source={{ uri: photo.uri }} accessibilityLabel="Ảnh bằng chứng vận chuyển" style={{ width: "100%", height: 260 }} resizeMode="contain" /> : null}
        <Button title={photo ? "Chụp lại ảnh" : "Chụp ảnh tại địa điểm"} disabled={!!busy} loading={busy === "photo"} onPress={() => run("photo", async () => {
          const value = await captureCheckInPhoto();
          if (value) { setPhoto({ ...value, stage }); setLocation(null); setLocation(await captureCheckInLocation()); }
        })} />
        {location ? <Text style={s.text}>Sai số GPS: {Math.round(location.accuracyMeters ?? 0)} m{distance != null ? ` · Cách địa điểm: ${Math.round(distance)} m` : ""}</Text> : null}
        <Button title="Cập nhật GPS chính xác" variant="secondary" disabled={!!busy} loading={busy === "gps"} onPress={() => run("gps", async () => setLocation(await captureCheckInLocation()))} />
        <Button title="Cài đặt quyền camera/GPS" variant="secondary" disabled={!!busy} onPress={() => run("settings", () => Linking.openSettings())} />
        <Button title={stage === "checkin" ? "Xác nhận đã lấy hàng" : "Xác nhận đã giao hàng"} disabled={!photo || !location || !!busy} loading={busy === stage}
          onPress={() => confirm(stage, "Xác nhận bàn giao", stage === "checkin" ? "Bạn đã nhận đúng lô và chịu trách nhiệm vận chuyển?" : "Bạn đã bàn giao lô tại nhà máy? Nhà máy vẫn cần xác nhận nhận hàng riêng.")} />
      </> : null}
      {job.isMine && job.status === "PICKED_UP" ? <Button title="Bắt đầu vận chuyển" disabled={!!busy} loading={busy === "start"}
        onPress={() => confirm("start", "Khởi hành", "Xác nhận đã xếp hàng an toàn và bắt đầu đến nhà máy?")} /> : null}
      {job.status === "DELIVERED" ? <Text style={s.text}>Đã báo giao hàng. Nhà máy thực hiện xác nhận nhận hàng và kiểm định; tài xế không chốt cân, thanh toán hay hoàn tồn kho.</Text> : null}
      {canReason ? <View style={s.card}>
        <Text style={s.label}>{reasonAction === "cancel" ? "Trả chuyến trước khi lấy hàng" : reasonAction === "reject" ? "Từ chối chuyến chờ" : "Báo sự cố sau khi lấy hàng"}</Text>
        {reasonAction === "incident" ? <Text style={s.muted}>Không tự hủy hoặc trả chuyến về pool khi đang giữ hàng. Hãy mô tả sự cố và liên hệ chủ kho để xử lý.</Text> : null}
        <TextInput accessibilityLabel="Lý do" placeholder="Lý do (10–1.000 ký tự)" multiline maxLength={1000} editable={!busy} value={reason} onChangeText={setReason} style={s.input} />
        <Button title={reasonAction === "incident" ? "Gửi báo cáo sự cố" : reasonAction === "cancel" ? "Trả chuyến" : "Từ chối chuyến"} variant="secondary" disabled={!!busy || reason.trim().length < 10} loading={busy === reasonAction}
          onPress={() => confirm(reasonAction, "Xác nhận thao tác", "Lý do sẽ được lưu và gửi đến bên phụ trách.")} />
      </View> : null}
      <Text style={s.label}>Bằng chứng và thao tác của bạn</Text>
      {!data.events.length ? <Text style={s.muted}>Chưa có bằng chứng vận chuyển.</Text> : data.events.map((event, index) => <View key={`${event.createdAt}-${index}`} style={s.card}>
        <Text style={s.label}>{labels[event.action] || event.action}</Text>
        <Text style={s.muted}>{new Date(event.createdAt).toLocaleString("vi-VN")}</Text>
        {event.reason ? <Text style={s.text}>{event.reason}</Text> : null}
        {event.imageUrl ? <Image source={{ uri: event.imageUrl }} accessibilityLabel="Bằng chứng đã lưu" style={{ height: 200, width: "100%" }} resizeMode="contain" /> : null}
        {event.distanceMeters != null ? <Text style={s.muted}>Cách địa điểm {Math.round(event.distanceMeters)} m · Sai số {Math.round(event.accuracyMeters)} m</Text> : null}
      </View>)}
    </> : null}
    {failure ? <Text accessibilityRole="alert" style={s.error}>{failure}</Text> : null}
  </Screen>;
}
