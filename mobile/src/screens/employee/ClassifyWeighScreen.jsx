import { useCallback, useEffect, useRef, useState } from "react";
import { Alert, Pressable, ScrollView, Text, TextInput, View } from "react-native";
import Screen from "../../components/common/Screen";
import Button from "../../components/common/Button";
import ResourceState from "../../components/pickup/ResourceState";
import { pickupApi } from "../../api/pickupApi";
import { errorMessage } from "../../api/client";
import { itemTotal, money, parsePrice, parseWeight, pickupStatus, validateRows } from "../../helpers/collection";
import { formatViDatetime } from "../../helpers/format";
import { colors, styles as s } from "../../theme";

export default function ClassifyWeighScreen({ route, navigation }) {
  const id = route.params.pickupId;
  const [data, setData] = useState(null);
  const [materials, setMaterials] = useState([]);
  const [rows, setRows] = useState([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [dirty, setDirty] = useState(false);
  const [conflict, setConflict] = useState(false);
  const pending = useRef(false);
  const nextKey = useRef(0);
  const apply = useCallback((snapshot) => {
    setData(snapshot);
    setRows(snapshot.items.map(i => ({ key: String(++nextKey.current), materialType: i.materialType, weight: String(i.weightKg), price: String(i.pricePerKg) })));
    setDirty(false); setConflict(false);
  }, []);
  const load = useCallback(async (signal = undefined) => {
    try {
      const [snapshot, catalog] = await Promise.all([pickupApi.getCollection(id, signal), pickupApi.getMaterials(signal)]);
      if (!signal?.aborted) { apply(snapshot); setMaterials(catalog); setError(""); }
    } catch (e) { if (!signal?.aborted) setError(errorMessage(e)); }
    finally { if (!signal?.aborted) setLoading(false); }
  }, [id, apply]);
  useEffect(() => {
    const controller = new AbortController();
    Promise.all([pickupApi.getCollection(id, controller.signal), pickupApi.getMaterials(controller.signal)])
      .then(([snapshot, catalog]) => {
        if (!controller.signal.aborted) { apply(snapshot); setMaterials(catalog); setError(""); }
      })
      .catch(e => { if (!controller.signal.aborted) setError(errorMessage(e)); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [id, apply]);
  useEffect(() => navigation.addListener("beforeRemove", (event) => {
    if (!dirty && !pending.current) return;
    event.preventDefault();
    if (pending.current) return;
    Alert.alert("Chưa lưu thay đổi", "Rời màn hình sẽ bỏ các thay đổi chưa lưu.", [
      { text: "Ở lại", style: "cancel" }, { text: "Bỏ thay đổi", style: "destructive", onPress: () => navigation.dispatch(event.data.action) },
    ]);
  }), [navigation, dirty]);
  const editable = data?.canEdit && !saving && !loading && !conflict;
  function change(key, field, value) { setRows(old => old.map(r => r.key === key ? { ...r, [field]: value } : r)); setDirty(true); }
  let total = null;
  try { total = validateRows(rows).reduce((sum, i) => sum + itemTotal(i.weightKg, i.pricePerKg), 0); } catch { /* Chưa tính tổng khi còn dòng nhập sai. */ }
  async function save() {
    if (pending.current || !editable) return;
    setError("");
    let items;
    try { items = validateRows(rows); } catch (e) { setError(e.message); return; }
    pending.current = true; setSaving(true);
    try {
      const snapshot = await pickupApi.saveClassification(id, data.revision, items);
      apply(snapshot);
      Alert.alert("Đã lưu kết quả cân", "Bản nháp đã được lưu trên máy chủ, chưa gửi người bán xác nhận và chưa chốt thanh toán.");
    } catch (e) {
      setError(errorMessage(e));
      if (e?.response?.status === 409) setConflict(true);
    } finally { pending.current = false; setSaving(false); }
  }
  function reload() {
    const refresh = () => { setLoading(true); void load(); };
    if (dirty) Alert.alert("Tải dữ liệu máy chủ?", "Các thay đổi chưa lưu sẽ bị bỏ.", [
      { text: "Hủy", style: "cancel" }, { text: "Tải lại", onPress: refresh },
    ]); else refresh();
  }
  return <Screen>
    <Text style={s.title}>Phân loại · Cân · Định giá</Text>
    <ResourceState loading={loading} error={!data ? error : ""} retry={() => load()} />
    {data && !loading ? <>
      <View style={s.card}>
        <Text style={s.text}>{data.address}</Text><Text style={s.label}>{pickupStatus(data.status)}</Text>
        <Text style={s.muted}>Khối lượng theo kg, tối đa 2 số lẻ. Đơn giá đồng/kg. Giá tham khảo không phải giá mua bắt buộc.</Text>
        {!data.canEdit ? <Text style={s.error}>Chỉ được sửa sau check-in và trước khi gửi kết quả cân.</Text> : null}
      </View>
      {rows.length === 0 ? <Text style={s.muted}>Chưa có phế liệu. Thêm loại để bắt đầu phân loại và cân.</Text> : null}
      {rows.map((row, index) => {
        const material = materials.find(m => m.code === row.materialType);
        const weight = parseWeight(row.weight), price = parsePrice(row.price);
        return <View key={row.key} style={s.card}>
          <Text style={s.label}>LOẠI PHẾ LIỆU {index + 1}</Text>
          <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ gap: 8 }}>
            {materials.map(m => <Pressable key={m.code} disabled={!editable || rows.some(r => r.key !== row.key && r.materialType === m.code)}
              accessibilityRole="button" accessibilityState={{ selected: row.materialType === m.code }} onPress={() => change(row.key, "materialType", m.code)}
              style={{ padding: 12, borderRadius: 20, borderWidth: 1, borderColor: colors.border, backgroundColor: row.materialType === m.code ? colors.lime : colors.card }}>
              <Text style={s.label}>{m.label}</Text>
            </Pressable>)}
          </ScrollView>
          <Text style={s.text}>{material?.label || row.materialType}</Text>
          <Text style={s.muted}>{material?.referencePrice != null ? `Tham khảo: ${money(material.referencePrice)}/kg · ${formatViDatetime(material.effectiveDate)}${material.source ? ` · ${material.source}` : ""}` : "Chưa có giá tham khảo cho loại này. Hãy nhập giá đã trao đổi với người bán."}</Text>
          <Text style={s.label}>Khối lượng (kg)</Text>
          <TextInput accessibilityLabel={`Khối lượng ${material?.label || index + 1}`} style={s.input} editable={!!editable} keyboardType="decimal-pad" maxLength={9} value={row.weight} placeholder="Ví dụ: 12,50" onChangeText={v => change(row.key, "weight", v)} />
          <Text style={s.label}>Đơn giá (đồng/kg)</Text>
          <TextInput accessibilityLabel={`Đơn giá ${material?.label || index + 1}`} style={s.input} editable={!!editable} keyboardType="number-pad" maxLength={8} value={row.price} placeholder="Ví dụ: 15000" onChangeText={v => change(row.key, "price", v)} />
          {material?.referencePrice > 0 ? <Button title="Điền giá tham khảo" variant="secondary" disabled={!editable} onPress={() => change(row.key, "price", String(Math.round(material.referencePrice)))} /> : null}
          <Text style={[s.text, { color: colors.primary, fontFamily: "Inter_700Bold" }]}>Thành tiền: {Number.isFinite(weight) && weight > 0 && Number.isFinite(price) && price > 0 ? money(itemTotal(weight, price)) : "—"}</Text>
          <Button title="Xóa loại này" variant="danger" disabled={!editable} onPress={() => Alert.alert("Xóa loại phế liệu?", "Thay đổi chỉ có hiệu lực sau khi lưu.", [
            { text: "Hủy", style: "cancel" }, { text: "Xóa", style: "destructive", onPress: () => { setRows(old => old.filter(r => r.key !== row.key)); setDirty(true); } },
          ])} />
        </View>;
      })}
      <Button title="+ Thêm loại phế liệu" variant="secondary" disabled={!editable || rows.length >= materials.length} onPress={() => {
        const material = materials.find(m => !rows.some(r => r.materialType === m.code));
        if (material) { setRows(old => [...old, { key: String(++nextKey.current), materialType: material.code, weight: "", price: "" }]); setDirty(true); }
      }} />
      <View style={s.card}><Text style={s.title}>Tổng: {total == null ? "—" : money(total)}</Text><Text style={s.muted}>{dirty ? "Có thay đổi chưa lưu." : "Đang hiển thị dữ liệu đã lưu."} Đây là tổng trước phí; chưa phải xác nhận thanh toán.</Text></View>
      {error ? <Text accessibilityRole="alert" style={s.error}>{error}</Text> : null}
      <Button title="Lưu bản nháp kết quả cân" loading={saving} disabled={!editable || !dirty} onPress={() => {
        if (!rows.length) Alert.alert("Xóa toàn bộ các dòng cân?", "Bản nháp sẽ không còn phế liệu.", [{ text: "Hủy", style: "cancel" }, { text: "Xác nhận lưu", onPress: save }]);
        else void save();
      }} />
      <Button title="Tải lại dữ liệu máy chủ" variant="secondary" disabled={saving} onPress={reload} />
    </> : null}
  </Screen>;
}
