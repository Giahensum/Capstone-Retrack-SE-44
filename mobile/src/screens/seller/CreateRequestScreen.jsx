import React, { useState, useEffect } from 'react';
import { View, Text, StyleSheet, Dimensions, TextInput, TouchableOpacity, ScrollView, Alert, ActivityIndicator } from 'react-native';
import MapView, { Marker, PROVIDER_GOOGLE } from 'react-native-maps';
import Screen from '../../components/common/Screen';
import Button from '../../components/common/Button';
import { styles as globalStyles } from '../../theme';
import { Ionicons } from '@expo/vector-icons';
import { useNavigation } from '@react-navigation/native';
import * as Location from 'expo-location';
import { sellerApi } from '../../api/sellerApi';

const { width, height } = Dimensions.get('window');

export default function CreateRequestScreen() {
  const navigation = useNavigation();
  const [step, setStep] = useState(1);
  const [loading, setLoading] = useState(false);
  const [depots, setDepots] = useState([]);
  const [form, setForm] = useState({
    description: '',
    address: 'Đang lấy vị trí...',
    latitude: 16.07,
    longitude: 108.15,
    targetDepotId: null,
  });

  const [region, setRegion] = useState({
    latitude: 16.07,
    longitude: 108.15,
    latitudeDelta: 0.05,
    longitudeDelta: 0.05,
  });

  useEffect(() => {
    (async () => {
      let { status } = await Location.requestForegroundPermissionsAsync();
      if (status !== 'granted') {
        setForm(f => ({ ...f, address: 'Không thể lấy vị trí' }));
        return;
      }
      let location = await Location.getCurrentPositionAsync({});
      const lat = location.coords.latitude;
      const lng = location.coords.longitude;
      setRegion({ ...region, latitude: lat, longitude: lng });
      
      try {
        let geocode = await Location.reverseGeocodeAsync({ latitude: lat, longitude: lng });
        if (geocode.length > 0) {
          const address = `${geocode[0].street || ''} ${geocode[0].city || geocode[0].subregion || ''}`;
          setForm(f => ({ ...f, latitude: lat, longitude: lng, address: address.trim() }));
        } else {
          setForm(f => ({ ...f, latitude: lat, longitude: lng, address: 'Vị trí hiện tại' }));
        }
      } catch (e) {
        setForm(f => ({ ...f, latitude: lat, longitude: lng, address: 'Vị trí hiện tại' }));
      }
    })();
  }, []);

  const loadDepots = async () => {
    setLoading(true);
    try {
      const data = await sellerApi.getNearbyDepots(form.latitude, form.longitude);
      setDepots(data);
    } catch (e) {
      console.log('Error loading depots', e);
    } finally {
      setLoading(false);
    }
  };

  const submitRequest = async (depotId) => {
    setLoading(true);
    try {
      await sellerApi.createRequest({
        description: form.description,
        address: form.address,
        latitude: form.latitude,
        longitude: form.longitude,
        targetDepotId: depotId || null,
        requestImageUrl: null,
      });
      Alert.alert('Thành công', 'Đã tạo yêu cầu thu gom!', [
        { text: 'OK', onPress: () => navigation.goBack() }
      ]);
    } catch (e) {
      Alert.alert('Lỗi', 'Không thể tạo đơn');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Screen>
      {step === 1 && (
        <ScrollView contentContainerStyle={styles.scroll}>
          <Text style={globalStyles.title}>Mô tả phế liệu</Text>
          <Text style={globalStyles.text}>Nhập thông tin sơ bộ để nhân viên hỗ trợ.</Text>

          <View style={styles.inputGroup}>
            <Text style={styles.label}>Mô tả (tuỳ chọn)</Text>
            <TextInput
              style={[styles.input, { height: 80 }]}
              multiline
              placeholder="VD: Sắt vụn, báo cũ..."
              value={form.description}
              onChangeText={(t) => setForm({ ...form, description: t })}
            />
          </View>

          <View style={styles.inputGroup}>
            <Text style={styles.label}>📍 Địa chỉ thu gom</Text>
            <TextInput
              style={styles.input}
              placeholder="Nhập địa chỉ"
              value={form.address}
              onChangeText={(t) => setForm({ ...form, address: t })}
            />
          </View>

          <Button title="Tiếp tục (Chọn kho)" onPress={() => { setStep(2); loadDepots(); }} />
        </ScrollView>
      )}

      {step === 2 && (
        <View style={styles.container}>
          <MapView
            provider={PROVIDER_GOOGLE}
            style={styles.map}
            initialRegion={region}
            onRegionChangeComplete={(r) => setRegion(r)}
          >
            <Marker coordinate={{ latitude: form.latitude, longitude: form.longitude }} title="Vị trí của bạn" pinColor="orange" />
            {depots.map(d => (
              <Marker key={d.id} coordinate={{ latitude: d.latitude, longitude: d.longitude }} title={d.name} description={d.address} pinColor="blue" />
            ))}
          </MapView>
          
          <View style={styles.card}>
            <View style={{flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8}}>
               <Text style={globalStyles.title}>Chọn kho vựa</Text>
               <TouchableOpacity onPress={() => setStep(1)}><Ionicons name="arrow-back" size={24} color="#000" /></TouchableOpacity>
            </View>
            <Text style={globalStyles.text}>Chọn một kho hoặc gửi cho tất cả kho quanh đây.</Text>
            
            <View style={{ marginTop: 20 }}>
              <Button title={loading ? "Đang xử lý..." : "Nổ đơn (Broadcast)"} onPress={() => submitRequest(null)} disabled={loading} />
              <ScrollView style={{maxHeight: 150, marginTop: 10}}>
                {depots.map(d => (
                  <TouchableOpacity key={d.id} style={styles.depotItem} onPress={() => submitRequest(d.id)} disabled={loading}>
                    <Text style={styles.depotName}>{d.name}</Text>
                    <Text style={styles.depotAddress}>{d.address}</Text>
                  </TouchableOpacity>
                ))}
              </ScrollView>
            </View>
          </View>
        </View>
      )}
    </Screen>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    marginTop: -20,
    marginHorizontal: -16,
  },
  scroll: {
    paddingBottom: 24,
  },
  inputGroup: {
    marginVertical: 12,
  },
  label: {
    fontSize: 14,
    fontWeight: 'bold',
    color: '#334155',
    marginBottom: 6,
  },
  input: {
    backgroundColor: '#f8fafc',
    borderWidth: 1,
    borderColor: '#e2e8f0',
    borderRadius: 8,
    padding: 12,
    fontSize: 15,
  },
  map: {
    width: width,
    height: height * 0.5,
  },
  card: {
    flex: 1,
    padding: 20,
    backgroundColor: '#fff',
    borderTopLeftRadius: 24,
    borderTopRightRadius: 24,
    marginTop: -20,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: -2 },
    shadowOpacity: 0.1,
    shadowRadius: 10,
    elevation: 5,
  },
  depotItem: {
    padding: 12,
    borderWidth: 1,
    borderColor: '#e2e8f0',
    borderRadius: 8,
    marginBottom: 8,
    backgroundColor: '#f8fafc'
  },
  depotName: {
    fontWeight: 'bold',
    color: '#0f172a'
  },
  depotAddress: {
    fontSize: 12,
    color: '#64748b'
  }
});
