import React, { useState, useEffect } from 'react';
import { View, Text, StyleSheet, TextInput, TouchableOpacity, ScrollView, ActivityIndicator, Alert } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { sellerApi } from '../api';

export default function CreateRequestScreen({ navigation }) {
  const insets = useSafeAreaInsets();
  const [description, setDescription] = useState('');
  const [address, setAddress] = useState('123 Đường Tạm, Quận 1, TP.HCM'); // Demo address
  const [latitude, setLatitude] = useState(10.7769); // Demo lat (HCM)
  const [longitude, setLongitude] = useState(106.7009); // Demo lng
  
  const [depots, setDepots] = useState([]);
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    // Fetch nearby depots when screen loads
    const loadDepots = async () => {
      setLoading(true);
      try {
        const data = await sellerApi.getNearbyDepots(latitude, longitude);
        setDepots(data);
      } catch (err) {
        console.log(err);
      } finally {
        setLoading(false);
      }
    };
    loadDepots();
  }, [latitude, longitude]);

  const handleCreate = async (targetDepotId = null) => {
    setSubmitting(true);
    try {
      await sellerApi.createRequest({
        description,
        address,
        latitude,
        longitude,
        targetDepotId,
        imageUrls: [],
        preferredDate: new Date().toISOString(),
        preferredTimeSlot: 'Linh hoạt'
      });
      Alert.alert('Thành công', 'Đã tạo đơn thu gom!', [
        { text: 'OK', onPress: () => navigation.goBack() }
      ]);
    } catch (err) {
      Alert.alert('Lỗi', 'Không thể tạo đơn');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <ScrollView style={[styles.container, { paddingTop: insets.top }]}>
      <View style={styles.header}>
        <TouchableOpacity onPress={() => navigation.goBack()} style={styles.backBtn}>
          <Text style={styles.backBtnText}>← Trở về</Text>
        </TouchableOpacity>
        <Text style={styles.title}>Tạo đơn mới</Text>
      </View>

      <View style={styles.form}>
        <Text style={styles.label}>Mô tả phế liệu</Text>
        <TextInput
          style={[styles.input, { height: 80, textAlignVertical: 'top' }]}
          placeholder="Ví dụ: Giấy báo cũ, vỏ chai nhựa..."
          placeholderTextColor="#64748b"
          multiline
          value={description}
          onChangeText={setDescription}
        />

        <Text style={styles.label}>Địa chỉ lấy hàng</Text>
        <TextInput
          style={styles.input}
          value={address}
          onChangeText={setAddress}
          placeholderTextColor="#64748b"
        />

        <Text style={styles.sectionTitle}>Chọn phương thức nổ đơn</Text>
        
        <TouchableOpacity 
          style={styles.broadcastBtn}
          onPress={() => handleCreate(null)}
          disabled={submitting}
        >
          <Text style={styles.broadcastBtnText}>🔥 Nổ đơn (Tất cả kho gần đây)</Text>
          <Text style={styles.broadcastBtnSubText}>Đơn sẽ được gửi đến mọi kho trong bán kính 10km</Text>
        </TouchableOpacity>

        <Text style={styles.sectionTitle}>Hoặc gửi đích danh cho kho vựa:</Text>
        
        {loading ? (
          <ActivityIndicator color="#34d399" style={{ margin: 20 }} />
        ) : (
          depots.map(depot => (
            <TouchableOpacity 
              key={depot.id} 
              style={styles.depotCard}
              onPress={() => handleCreate(depot.id)}
              disabled={submitting}
            >
              <Text style={styles.depotName}>{depot.name}</Text>
              <Text style={styles.depotAddress}>{depot.address}</Text>
              {depot.distanceKm != null && (
                <Text style={styles.depotDistance}>
                  📍 {depot.distanceKm.toFixed(1)} km {depot.routingDurationText ? `(${depot.routingDurationText})` : ''}
                </Text>
              )}
            </TouchableOpacity>
          ))
        )}
      </View>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#0f172a',
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: 20,
    borderBottomWidth: 1,
    borderBottomColor: '#1e293b',
  },
  backBtn: {
    padding: 8,
    marginRight: 16,
    backgroundColor: '#1e293b',
    borderRadius: 8,
  },
  backBtnText: {
    color: '#94a3b8',
    fontWeight: 'bold',
  },
  title: {
    fontSize: 20,
    fontWeight: 'bold',
    color: '#fff',
  },
  form: {
    padding: 16,
  },
  label: {
    color: '#94a3b8',
    marginBottom: 8,
    fontWeight: 'bold',
  },
  input: {
    backgroundColor: '#1e293b',
    color: '#fff',
    borderRadius: 12,
    padding: 16,
    marginBottom: 20,
    borderWidth: 1,
    borderColor: '#334155',
  },
  sectionTitle: {
    color: '#fff',
    fontSize: 16,
    fontWeight: 'bold',
    marginTop: 10,
    marginBottom: 12,
  },
  broadcastBtn: {
    backgroundColor: '#f9731615',
    borderWidth: 1,
    borderColor: '#f9731650',
    padding: 20,
    borderRadius: 16,
    alignItems: 'center',
    marginBottom: 24,
  },
  broadcastBtnText: {
    color: '#f97316',
    fontSize: 18,
    fontWeight: 'bold',
    marginBottom: 4,
  },
  broadcastBtnSubText: {
    color: '#fb923c80',
    fontSize: 12,
  },
  depotCard: {
    backgroundColor: '#1e293b',
    padding: 16,
    borderRadius: 16,
    marginBottom: 12,
    borderWidth: 1,
    borderColor: '#334155',
  },
  depotName: {
    color: '#f8fafc',
    fontSize: 16,
    fontWeight: 'bold',
    marginBottom: 4,
  },
  depotAddress: {
    color: '#94a3b8',
    fontSize: 13,
    marginBottom: 8,
  },
  depotDistance: {
    color: '#34d399',
    fontSize: 13,
    fontWeight: 'bold',
  }
});
