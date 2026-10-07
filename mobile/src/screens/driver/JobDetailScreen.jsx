import { useCallback, useRef, useState } from "react";
import { Alert, Text, View } from "react-native";
import Screen from "../../components/common/Screen";
import Button from "../../components/common/Button";
import ResourceState from "../../components/pickup/ResourceState";
import usePickupResource from "../../hooks/usePickupResource";
import { driverApi } from "../../api/driverApi";
import { errorMessage } from "../../api/client";
import { jobStatus } from "../../helpers/driverJobs";
import { styles as s } from "../../theme";
export default function JobDetailScreen({ route, navigation }) {
  const id = route.params.jobId;
  const loader = useCallback(signal => driverApi.detail(id, signal), [id]);
  const { data, setData, loading, error, reload } = usePickupResource(loader);
  const pending = useRef(false);
  const [busy, setBusy] = useState(false), [failure, setFailure] = useState("");
  async function accept() {
    if (pending.current) return;
    pending.current = true; setBusy(true); setFailure("");
    try { setData(await driverApi.accept(id)); Alert.alert("Đã nhận chuyến", "Bạn đã được phân công vận chuyển lô hàng này."); }
    catch (e) { setFailure(errorMessage(e)); if (e.response?.status === 409) void reload(); }
    finally { pending.current = false; setBusy(false); }
  }
  return <Screen>
    <ResourceState loading={loading} error={error} retry={() => reload()} />
    {data && !loading ? <>
      <Text style={s.title}>{data.batchCode || "Chi tiết chuyến"}</Text>
      <Text style={s.label}>{jobStatus(data.status)}</Text>
      <View style={s.card}><Text style={s.text}>{data.materialType} · {data.weightKg} kg</Text><Text style={s.muted}>Mã lô: {data.batchId}</Text></View>
      <View style={s.card}><Text style={s.label}>LẤY HÀNG TẠI KHO</Text><Text style={s.text}>{data.depot.name}</Text><Text style={s.muted}>{data.depot.address}</Text></View>
      <View style={s.card}><Text style={s.label}>GIAO ĐẾN NHÀ MÁY</Text><Text style={s.text}>{data.factory.name}</Text><Text style={s.muted}>{data.factory.address}</Text></View>
      <Button title="Bản đồ Kho → Nhà máy" disabled={busy} onPress={() => navigation.navigate("JobMap", { jobId: id })} />
      {!data.isMine && data.status === "PENDING" ? <Button title="Nhận chuyến" loading={busy} onPress={() => Alert.alert("Nhận chuyến này?", "Xác nhận bạn có thể vận chuyển lô hàng tới nhà máy đã chọn.", [{ text: "Hủy", style: "cancel" }, { text: "Nhận chuyến", onPress: accept }])} /> : <Text style={s.muted}>Chuyến của bạn. Check-in lấy hàng và giao hàng sẽ được bổ sung ở bước tiếp theo.</Text>}
    </> : null}
    {failure ? <Text style={s.error}>{failure}</Text> : null}
  </Screen>;
}
