import axios from 'axios';
import AsyncStorage from '@react-native-async-storage/async-storage';

// In Expo, localhost points to the device itself.
// You need to use your computer's local IP address or 10.0.2.2 for Android emulator
// For testing locally on Android emulator, use 10.0.2.2:5000
const API_URL = 'http://10.0.2.2:5000/api';

const api = axios.create({
  baseURL: API_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

api.interceptors.request.use(async (config) => {
  const token = await AsyncStorage.getItem('token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

export const sellerApi = {
  login: async (email, password) => {
    const res = await api.post('/auth/login', { email, password });
    if (res.data?.data?.token) {
      await AsyncStorage.setItem('token', res.data.data.token);
    }
    return res.data;
  },
  
  getNearbyDepots: async (lat, lng) => {
    // If coords exist, we pass them. The backend handles the exact routing distance matrix internally!
    const query = (lat && lng) ? `?lat=${lat}&lng=${lng}` : '';
    const res = await api.get(`/seller/pickups/depots${query}`);
    return res.data?.data || [];
  },

  createRequest: async (data) => {
    const res = await api.post('/seller/pickups', data);
    return res.data;
  },

  getMyRequests: async () => {
    const res = await api.get('/seller/pickups');
    return res.data?.data || [];
  },

  logout: async () => {
    await AsyncStorage.removeItem('token');
  }
};
