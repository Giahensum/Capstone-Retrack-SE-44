import { useState } from "react";
import { Alert, Text, TextInput, View } from "react-native";
import Button from "../common/Button";
import usePickupResource from "../../hooks/usePickupResource";
import { pickupApi } from "../../api/pickupApi";
import { errorMessage } from "../../api/client";
import { styles as s } from "../../theme";

const labels = { AVAILABLE: "Đang nhận đơn", FULL: "Tạm dừng — Đầy xe", STOPPED: "Đã ngừng ca" };

export default function DispatchAvailability({ onChanged }) {
  const { data, loading, error, reload } = usePickupResource(pickupApi.getShifts);
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState("");
  async function update(id, status) {
    if (busy) return;
    setBusy(true); setFailure("");
    try {
      await pickupApi.setAvailability(id, status, reason.trim());
      setReason(""); await reload(); await onChanged();
    } catch (e) { setFailure(errorMessage(e)); }
    finally { setBusy(false); }
  }
  function confirm(id, status) {
    if (status !== "AVAILABLE" && reason.trim().length < 10) {
      setFailure("Nhập lý do ít nhất 10 ký tự."); return;
    }
    Alert.alert("Cập nhật ca thu gom", status === "AVAILABLE"
      ? "Bạn đã bàn giao hàng và có thể nhận thêm đơn?"
      : "Các đơn chưa check-in sẽ được điều phối lại. Đơn đã bắt đầu vẫn thuộc trách nhiệm của bạn.", [
      { text: "Quay lại", style: "cancel" },
      { text: "Xác nhận", onPress: () => update(id, status) },
    ]);
  }
  return <View style={s.card}>
    <Text style={s.label}>Ca và khả năng nhận đơn</Text>
    {loading ? <Text style={s.muted}>Đang tải ca…</Text> : null}
    {error ? <Text style={s.error}>{error}</Text> : null}
    {!loading && !error && !data?.length ? <Text style={s.muted}>Chưa có ca đang hoạt động. Liên hệ chủ kho để bố trí khu vực và ca.</Text> : null}
    {(data || []).map(shift => <View key={shift.id} style={{ gap: 8 }}>
      <Text style={s.text}>{shift.name} · {labels[shift.availability]}</Text>
      <Text style={s.muted}>{shift.vehicle} · tải trọng tham khảo {shift.capacityKg} kg · tối đa {shift.maxPendingOrders} đơn đang xử lý</Text>
      <Text style={s.muted}>Kết thúc: {new Date(shift.endsAt).toLocaleString("vi-VN")}</Text>
      {shift.reason ? <Text style={s.muted}>Lý do: {shift.reason}</Text> : null}
      {shift.availability !== "STOPPED" ? <>
        <TextInput accessibilityLabel="Lý do tạm dừng nhận đơn" placeholder="Lý do (10–1.000 ký tự)"
          multiline maxLength={1000} value={reason} onChangeText={setReason} editable={!busy} style={s.input} />
        {shift.availability === "FULL"
          ? <Button title="Đã bàn giao hàng — Tiếp tục nhận đơn" disabled={busy} onPress={() => confirm(shift.id, "AVAILABLE")} />
          : <Button title="Đầy xe — Tạm dừng và chuyển đơn" disabled={busy} onPress={() => confirm(shift.id, "FULL")} />}
        <Button title="Ngừng ca — Chuyển đơn chưa thu gom" variant="secondary" disabled={busy} onPress={() => confirm(shift.id, "STOPPED")} />
      </> : null}
    </View>)}
    {failure ? <Text accessibilityRole="alert" style={s.error}>{failure}</Text> : null}
    <Button title="Làm mới ca" variant="secondary" disabled={busy} onPress={() => reload()} />
  </View>;
}
