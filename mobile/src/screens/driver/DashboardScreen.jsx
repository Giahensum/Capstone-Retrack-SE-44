import { useCallback } from "react";
import { RefreshControl, Text, View } from "react-native";
import Screen from "../../components/common/Screen";
import Button from "../../components/common/Button";
import ResourceState from "../../components/pickup/ResourceState";
import usePickupResource from "../../hooks/usePickupResource";
import useAuthStore from "../../store/authStore";
import { driverApi } from "../../api/driverApi";
import { jobStatus } from "../../helpers/driverJobs";
import { styles as s } from "../../theme";

export default function DashboardScreen({ navigation }) {
  const user = useAuthStore(state => state.user);
  const loader = useCallback(signal => driverApi.dashboard(signal), []);
  const { data, loading, error, refreshing, reload } = usePickupResource(loader);
  const openJob = id => navigation.navigate("Jobs", { screen: "JobDetail", params: { jobId: id } });
  return <Screen refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => reload(true)} />}>
    <Text style={s.title}>Xin chào, {user?.fullName}</Text>
    <Text style={s.muted}>Tổng quan vận chuyển · Hôm nay theo giờ Việt Nam</Text>
    <ResourceState loading={loading} error={error} retry={() => reload()} />
    {!loading && data ? <>
      <View style={s.card}>
        <Text style={s.label}>{data.availableJobs} chuyến chờ nhận</Text>
        <Text style={s.text}>{data.activeJobs} chuyến đang thực hiện</Text>
        <Text style={s.text}>{data.deliveredToday} chuyến đã báo giao hôm nay</Text>
        <Text style={s.text}>{data.awaitingFactory} chuyến chờ nhà máy xác nhận nhận hàng</Text>
      </View>
      <Text style={s.label}>Chuyến cần tiếp tục</Text>
      {!data.activePreview.length ? <Text style={s.muted}>Bạn chưa có chuyến đang thực hiện.</Text> : null}
      {data.activePreview.map(job => <View key={job.id} style={s.card}>
        <Text style={s.label}>{job.batchCode || job.id}</Text>
        <Text style={s.text}>{job.depotName} → {job.factoryName}</Text>
        <Text style={s.muted}>{jobStatus(job.status)}</Text>
        <Button title="Tiếp tục chuyến" onPress={() => openJob(job.id)} />
      </View>)}
      {data.activeJobs > 3 ? <Text style={s.muted}>Hiển thị 3 chuyến ưu tiên. Xem các chuyến còn lại trong Chuyến xe → Chuyến của tôi.</Text> : null}
    </> : null}
    <Button title="Xem chuyến vận chuyển" onPress={() => navigation.navigate("Jobs", { screen: "JobPool" })} />
    <Button title="Lịch sử và thống kê" variant="secondary" onPress={() => navigation.navigate("History")} />
    <Button title="Hồ sơ tài xế" variant="secondary" onPress={() => navigation.navigate("Profile")} />
  </Screen>;
}
