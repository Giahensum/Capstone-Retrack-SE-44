import { useCallback, useState } from "react";
import { Text, View } from "react-native";
import Screen from "../../components/common/Screen";
import Button from "../../components/common/Button";
import ResourceState from "../../components/pickup/ResourceState";
import usePickupResource from "../../hooks/usePickupResource";
import { pickupApi } from "../../api/pickupApi";
import { pickupStatus } from "../../helpers/collection";
import { styles as s } from "../../theme";

export default function ActivePickupsScreen({ navigation }) {
  const [page, setPage] = useState(1);
  const loader = useCallback(signal => pickupApi.getActive(page, signal), [page]);
  const { data, loading, error, reload } = usePickupResource(loader);
  return <Screen>
    <Text style={s.title}>Đơn tôi đang thực hiện</Text>
    <ResourceState loading={loading} error={error} retry={() => reload()} />
    {!loading && data ? <>
      {data.items.length === 0 ? <Text style={s.muted}>Không có đơn đang thực hiện ở trang này.</Text> : null}
      {data.items.map(p => <View key={p.id} style={s.card}>
        <Text style={s.text}>{p.sellerName}</Text><Text style={s.muted}>{p.address}</Text><Text style={s.label}>{pickupStatus(p.status)}</Text>
        <Button title="Mở đơn" onPress={() => navigation.navigate("PickupDetail", { pickupId: p.id })} />
      </View>)}
      <Text style={s.muted}>Trang {page} · {data.total} đơn</Text>
      <Button title="Trang trước" variant="secondary" disabled={page === 1} onPress={() => setPage(p => p - 1)} />
      <Button title="Trang sau" variant="secondary" disabled={page * data.pageSize >= data.total} onPress={() => setPage(p => p + 1)} />
    </> : null}
  </Screen>;
}
