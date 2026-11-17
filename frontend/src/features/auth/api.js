import api from '@/lib/axios';

export const authApi = {
  forgotPassword: async (email) => {
    const res = await api.post('/auth/forgot-password', { email });
    return res.data;
  },
  verifyOtp: async (email, otp) => {
    const res = await api.post('/auth/verify-otp', { email, otp });
    return res.data;
  },
  resetPassword: async (email, otp, newPassword) => {
    const res = await api.post('/auth/reset-password', { email, otp, newPassword });
    return res.data;
  },
  checkPhone: async (phone) => {
    const res = await api.get('/auth/check-phone', { params: { phone } });
    return res.data?.data;
  },
  checkEmail: async (email) => {
    const res = await api.get('/auth/check-email', { params: { email } });
    return res.data?.data;
  },
};
