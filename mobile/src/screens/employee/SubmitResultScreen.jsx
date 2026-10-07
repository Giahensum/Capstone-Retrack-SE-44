import { useCallback, useRef, useState } from "react";
import { Alert, Text, View } from "react-native";
import Screen from "../../components/common/Screen";
import Button from "../../components/common/Button";
import ResourceState from "../../components/pickup/ResourceState";
import usePickupResource from "../../hooks/usePickupResource";
import { pickupApi } from "../../api/pickupApi";
import { errorMessage } from "../../api/client";
import { money, pickupStatus } from "../../helpers/collection";
import { styles as s } from "../../theme";

export default function SubmitResultScreen({ route, navigation }) {
  const id = route.params.pickupId;
  const loader = useCallback(signal => pickupApi.getCollection(id, signal), [id]);
  const { data, setData, loading, error, reload } = usePickupResource(loader);
  const [busy, setBusy] = useState(false);
  const [actionError, setActionError] = useState("");
  const pending = useRef(false);
  function confirm(action, title, message) {
    Alert.alert(title, message, [{ text: "Hủy", style: "cancel" }, { text: "Xác nhận", onPress: async () => {
      if (pending.current) return;
      pending.current = true; setBusy(true); setActionError("");
      try { setData(await pickupApi.transition(id, action, data.revision)); }
      catch (e) { setActionError(errorMessage(e)); }
      finally { pending.current = false; setBusy(false); }
    } }]);
  }
  return <Screen>
    <Text style={s.title}>Kết quả cân và bàn giao</Text>
    <ResourceState loading={loading} error={error} retry={() => reload()} />
    {data && !loading ? <>
      <View style={s.card}><Text style={s.text}>{data.address}</Text><Text style={s.label}>{pickupStatus(data.status)}</Text>
        <Text style={s.muted}>Phiên bản cân: {data.revision}. Nhân viên chỉ gửi kết quả và bàn giao; chủ kho thanh toán cho người bán.</Text></View>
      {data.items.map(i => <View key={i.id} style={s.card}>
        <Text style={s.label}>{i.materialType}</Text><Text style={s.text}>{i.weightKg} kg × {money(i.pricePerKg)}/kg</Text>
        <Text style={s.text}>{money(i.subTotal)}</Text>
      </View>)}
      <View style={s.card}>
        <Text style={s.text}>Tổng khối lượng: {data.totalWeightKg} kg</Text>
        <Text style={s.text}>Tiền phế liệu: {money(data.grossAmount)}</Text>
        <Text style={s.text}>Phí nền tảng ({data.platformFeePercentage}%): {money(data.platformFeeAmount)}</Text>
        <Text style={s.title}>Người bán nhận: {money(data.netAmount)}</Text>
        <Text style={s.muted}>Theo mức phí đã lưu trên đơn. Đây chưa phải xác nhận đã trả tiền.</Text>
      </View>
      {data.status === "IN_PROGRESS" ? <Button title="Xác nhận và gửi người bán" disabled={busy || !data.items.length} loading={busy}
        onPress={() => confirm("submit-weigh", "Gửi kết quả cân?", "Kiểm tra từng loại, số kg và số tiền. Sau khi gửi, kết quả bị khóa chờ người bán xác nhận.")} /> : null}
      {data.status === "WEIGHED" ? <Text style={s.muted}>Đã gửi kết quả. Đang chờ người bán đồng ý hoặc trả lại để xem xét.</Text> : null}
      {data.status === "SELLER_CONFIRMED" ? <Button title="Hoàn tất nghiệp vụ · Bàn giao chủ kho" disabled={busy} loading={busy}
        onPress={() => confirm("finalize", "Bàn giao chủ kho?", "Người bán đã đồng ý. Đơn sẽ chờ chủ kho thanh toán; bạn không thu/chi tiền và đơn chưa DONE.")} /> : null}
      {data.status === "SCHEDULED" && data.checkIn ? <Button title="Mở lại kết quả để cân/định giá lại" disabled={busy} loading={busy}
        onPress={() => confirm("reopen-weigh", "Mở lại bản cân?", "Chỉ áp dụng đơn đã gửi và được trả lại. Bằng chứng check-in và bản gửi cũ được giữ nguyên.")} /> : null}
      {data.canEdit ? <Button title="Sửa bản cân" variant="secondary" disabled={busy}
        onPress={() => navigation.navigate("ClassifyWeigh", { pickupId: id })} /> : null}
      {actionError ? <Text style={s.error} accessibilityRole="alert">{actionError}</Text> : null}
      <Button title="Cập nhật trạng thái" variant="secondary" disabled={busy} onPress={() => reload()} />
    </> : null}
  </Screen>;
}
