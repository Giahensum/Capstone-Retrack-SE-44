import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useMutation } from '@tanstack/react-query';
import { GoogleLogin } from '@react-oauth/google';
import toast from 'react-hot-toast';
import { useAuth } from '@/hooks/useAuth';
import api from '@/lib/axios';
import { authApi } from '@/features/auth/api';
import {
  Eye,
  EyeOff,
  ArrowRight,
  CheckCircle2,
  Check,
  X,
  AlertCircle,
  Loader2,
  ShieldCheck,
} from 'lucide-react';
import { BrandMark } from '@/components/brand/BrandLogo';

const ROLE_HOME = {
  SELLER: '/seller',
  DEPOT_OWNER: '/depot',
  DEPOT_EMPLOYEE: '/employee',
  DRIVER: '/driver',
  FACTORY: '/factory',
};

const ROLES = [
  { value: 'SELLER', label: 'Người bán phế liệu', desc: 'Cá nhân, hộ gia đình có phế liệu cần bán' },
  { value: 'DEPOT_OWNER', label: 'Chủ kho vựa', desc: 'Quản lý trạm thu mua, kho phế liệu' },
  { value: 'FACTORY', label: 'Nhà máy tái chế', desc: 'Cơ sở tái chế quy mô lớn' },
];

// Regex chuẩn số di động Việt Nam (10 chữ số bắt đầu bằng 03, 05, 07, 08, 09 hoặc +84)
const VN_PHONE_REGEX = /^(0|\+84)(3[2-9]|5[25689]|7[06-9]|8[1-9]|9[0-9])\d{7}$/;
const EMAIL_REGEX = /^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/;

export default function RegisterPage() {
  const navigate = useNavigate();
  const { login } = useAuth();

  const [step, setStep] = useState(1);
  const [showPass, setShowPass] = useState(false);
  const [showConfirmPass, setShowConfirmPass] = useState(false);
  const [isPassFocused, setIsPassFocused] = useState(false);

  const [form, setForm] = useState({
    fullName: '',
    email: '',
    phone: '',
    password: '',
    confirmPassword: '',
    role: 'SELLER',
  });

  const [errors, setErrors] = useState({});
  const [touched, setTouched] = useState({});
  const [checkingAvailability, setCheckingAvailability] = useState({ phone: false, email: false });
  const [availabilityStatus, setAvailabilityStatus] = useState({ phone: null, email: null });

  // ── 1. Đánh giá chi tiết độ mạnh và quy tắc mật khẩu ──────────────────
  const passwordRules = [
    {
      id: 'length',
      label: 'Tối thiểu 8 ký tự (tối đa 64)',
      valid: form.password.length >= 8 && form.password.length <= 64,
    },
    {
      id: 'upper',
      label: 'Có ít nhất 1 chữ in hoa (A-Z)',
      valid: /[A-Z]/.test(form.password),
    },
    {
      id: 'lower',
      label: 'Có ít nhất 1 chữ thường (a-z)',
      valid: /[a-z]/.test(form.password),
    },
    {
      id: 'number',
      label: 'Có ít nhất 1 chữ số (0-9)',
      valid: /[0-9]/.test(form.password),
    },
    {
      id: 'special',
      label: 'Có ít nhất 1 ký tự đặc biệt (@, #, $, %, !...)',
      valid: /[^A-Za-z0-9\s]/.test(form.password),
    },
    {
      id: 'nospace',
      label: 'Không chứa khoảng trắng',
      valid: form.password.length > 0 && !/\s/.test(form.password),
    },
  ];

  const satisfiedRulesCount = passwordRules.filter((r) => r.valid).length;
  const isPasswordValid = satisfiedRulesCount === passwordRules.length;

  const getPasswordStrength = () => {
    if (!form.password) return { level: 0, text: '', color: 'bg-gray-200', textClass: 'text-gray-400' };
    if (satisfiedRulesCount <= 2) return { level: 1, text: 'Mật khẩu yếu', color: 'bg-red-500', textClass: 'text-red-500' };
    if (satisfiedRulesCount <= 4) return { level: 2, text: 'Mật khẩu trung bình', color: 'bg-amber-500', textClass: 'text-amber-600' };
    if (satisfiedRulesCount === 5) return { level: 3, text: 'Mật khẩu khá', color: 'bg-lime-500', textClass: 'text-lime-600' };
    return { level: 4, text: 'Mật khẩu rất mạnh', color: 'bg-emerald-500', textClass: 'text-emerald-600' };
  };

  const strength = getPasswordStrength();

  // ── 2. Các hàm kiểm tra tính hợp lệ từng trường ───────────────────────
  const validateFullName = (value) => {
    const trimmed = value.trim();
    if (!trimmed) return 'Vui lòng nhập họ và tên.';
    if (trimmed.length < 2) return 'Họ và tên phải có ít nhất 2 ký tự.';
    if (trimmed.length > 100) return 'Họ và tên không được vượt quá 100 ký tự.';
    if (!/^[\p{L}\s.'-]+$/u.test(trimmed)) return 'Họ và tên không được chứa số hoặc ký tự đặc biệt lạ.';
    return null;
  };

  const validateEmail = (value) => {
    const trimmed = value.trim();
    if (!trimmed) return 'Vui lòng nhập địa chỉ email.';
    if (!EMAIL_REGEX.test(trimmed)) return 'Email không đúng định dạng (VD: example@email.com).';
    return null;
  };

  const validatePhone = (value) => {
    const clean = value.trim().replace(/[\s.-]/g, '');
    if (!clean) return 'Vui lòng nhập số điện thoại.';
    if (!VN_PHONE_REGEX.test(clean)) {
      return 'Số điện thoại không hợp lệ (cần 10 chữ số thuộc các mạng di động Việt Nam, VD: 0912345678).';
    }
    return null;
  };

  const validateConfirmPassword = (confirmVal, passVal) => {
    if (!confirmVal) return 'Vui lòng xác nhận lại mật khẩu.';
    if (confirmVal !== passVal) return 'Mật khẩu xác nhận không khớp.';
    return null;
  };

  // ── 3. Kiểm tra kiểm tra trùng lặp trên Backend (Async on Blur) ────────
  const handlePhoneBlur = async () => {
    setTouched((prev) => ({ ...prev, phone: true }));
    const cleanPhone = form.phone.trim().replace(/[\s.-]/g, '');
    const err = validatePhone(cleanPhone);

    if (err) {
      setErrors((prev) => ({ ...prev, phone: err }));
      setAvailabilityStatus((prev) => ({ ...prev, phone: null }));
      return;
    }

    setCheckingAvailability((prev) => ({ ...prev, phone: true }));
    try {
      const res = await authApi.checkPhone(cleanPhone);
      if (res && res.available === false) {
        setErrors((prev) => ({
          ...prev,
          phone: res.message || 'Số điện thoại này đã được đăng ký trong hệ thống.',
        }));
        setAvailabilityStatus((prev) => ({
          ...prev,
          phone: { available: false, message: res.message },
        }));
      } else {
        setErrors((prev) => ({ ...prev, phone: null }));
        setAvailabilityStatus((prev) => ({
          ...prev,
          phone: { available: true, message: 'Số điện thoại hợp lệ và khả dụng.' },
        }));
      }
    } catch {
      // Bỏ qua lỗi mạng khi check blur, validation submit sẽ là chốt chặn cuối
    } finally {
      setCheckingAvailability((prev) => ({ ...prev, phone: false }));
    }
  };

  const handleEmailBlur = async () => {
    setTouched((prev) => ({ ...prev, email: true }));
    const cleanEmail = form.email.trim().toLowerCase();
    const err = validateEmail(cleanEmail);

    if (err) {
      setErrors((prev) => ({ ...prev, email: err }));
      setAvailabilityStatus((prev) => ({ ...prev, email: null }));
      return;
    }

    setCheckingAvailability((prev) => ({ ...prev, email: true }));
    try {
      const res = await authApi.checkEmail(cleanEmail);
      if (res && res.available === false) {
        setErrors((prev) => ({
          ...prev,
          email: res.message || 'Email này đã được sử dụng. Vui lòng đăng nhập.',
        }));
        setAvailabilityStatus((prev) => ({
          ...prev,
          email: { available: false, message: res.message },
        }));
      } else {
        setErrors((prev) => ({ ...prev, email: null }));
        setAvailabilityStatus((prev) => ({
          ...prev,
          email: { available: true, message: 'Email khả dụng.' },
        }));
      }
    } catch {
      // Bỏ qua lỗi mạng
    } finally {
      setCheckingAvailability((prev) => ({ ...prev, email: false }));
    }
  };

  // ── 4. Xử lý thành công & Mutation ────────────────────────────────────
  const handleSuccess = (data) => {
    login(data.token, {
      userId: data.userId,
      email: data.email,
      fullName: data.fullName,
      role: data.role,
    });
    toast.success(`Đăng ký thành công! Chào mừng ${data.fullName}`);
    navigate(ROLE_HOME[data.role] ?? '/');
  };

  const registerMutation = useMutation({
    mutationFn: async (payload) => {
      const res = await api.post('/auth/register', payload);
      return res.data.data;
    },
    onSuccess: handleSuccess,
    onError: (err) => {
      const msg = err?.response?.data?.message || 'Đăng ký thất bại. Vui lòng kiểm tra lại thông tin.';
      toast.error(msg);

      // Tự động gán lỗi về đúng trường input
      const lower = msg.toLowerCase();
      if (lower.includes('số điện thoại') || lower.includes('phone')) {
        setErrors((prev) => ({ ...prev, phone: msg }));
        setAvailabilityStatus((prev) => ({ ...prev, phone: { available: false, message: msg } }));
      } else if (lower.includes('email')) {
        setErrors((prev) => ({ ...prev, email: msg }));
        setAvailabilityStatus((prev) => ({ ...prev, email: { available: false, message: msg } }));
      } else if (lower.includes('mật khẩu') || lower.includes('password')) {
        setErrors((prev) => ({ ...prev, password: msg }));
      }
    },
  });

  // ── 5. Submit Form ───────────────────────────────────────────────────
  const handleSubmit = (e) => {
    e.preventDefault();

    const fullName = form.fullName.trim();
    const email = form.email.trim().toLowerCase();
    const phone = form.phone.trim().replace(/[\s.-]/g, '');
    const password = form.password;
    const confirmPassword = form.confirmPassword;

    const newErrors = {};

    const nameErr = validateFullName(fullName);
    if (nameErr) newErrors.fullName = nameErr;

    const emailErr = validateEmail(email);
    if (emailErr) newErrors.email = emailErr;

    const phoneErr = validatePhone(phone);
    if (phoneErr) newErrors.phone = phoneErr;

    if (!isPasswordValid) {
      newErrors.password = 'Mật khẩu chưa đáp ứng đầy đủ các yêu cầu bảo mật.';
    }

    const confirmErr = validateConfirmPassword(confirmPassword, password);
    if (confirmErr) newErrors.confirmPassword = confirmErr;

    setErrors(newErrors);
    setTouched({
      fullName: true,
      email: true,
      phone: true,
      password: true,
      confirmPassword: true,
    });

    if (Object.keys(newErrors).length > 0) {
      toast.error('Vui lòng hoàn thiện đúng các trường thông tin trước khi tiếp tục.');
      return;
    }

    registerMutation.mutate({
      fullName,
      email,
      phone,
      password,
      role: form.role,
    });
  };

  return (
    <div className="min-h-screen bg-[#f8f9ff] flex">
      {/* Left panel - Branding */}
      <div className="hidden lg:flex lg:w-[45%] bg-gradient-to-br from-[#1f6c3a] to-[#0d3d20] relative overflow-hidden flex-col items-center justify-center p-12">
        <div className="absolute top-0 right-0 w-96 h-96 bg-white/5 rounded-full -translate-y-1/2 translate-x-1/2" />
        <div className="absolute bottom-0 left-0 w-64 h-64 bg-[#a4f1b2]/10 rounded-full translate-y-1/2 -translate-x-1/2" />

        <div className="relative z-10 text-white max-w-md">
          <div className="w-24 h-24 bg-white rounded-3xl flex items-center justify-center mx-auto mb-8 shadow-2xl">
            <BrandMark size={72} />
          </div>
          <h2 className="text-3xl font-black text-center mb-6">Tham gia RETRACK</h2>
          <div className="space-y-4">
            {[
              'Bán phế liệu giá tốt, thu gom tận nơi nhanh chóng',
              'Minh bạch cân đo, công khai bảng giá thị trường',
              'Trợ lý AI nhận diện phế liệu thông minh trong 1 chạm',
              'Thanh toán điện tử bảo mật, đối soát minh bạch',
              'Chung tay thúc đẩy Nền kinh tế Tuần hoàn',
            ].map((item) => (
              <div key={item} className="flex items-center gap-3">
                <CheckCircle2 size={20} className="text-[#a4f1b2] flex-shrink-0" />
                <span className="text-white/90 font-medium text-sm">{item}</span>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* Right panel - Registration Form */}
      <div className="flex-1 flex flex-col items-center justify-center p-6 lg:p-12 overflow-y-auto">
        <div className="flex items-center gap-3 mb-6 lg:hidden">
          <BrandMark size={40} />
          <span className="text-xl font-black text-[#1f2937]">RETRACK</span>
        </div>

        <div className="w-full max-w-md">
          <div className="mb-6">
            <h2 className="text-2xl lg:text-3xl font-black text-[#0b1c30] mb-1.5">Tạo tài khoản</h2>
            <p className="text-sm text-[#424936] font-medium">
              Đã có tài khoản?{' '}
              <Link to="/login" className="text-[#446900] font-bold hover:underline">
                Đăng nhập ngay
              </Link>
            </p>
          </div>

          {/* Step indicator */}
          <div className="flex items-center gap-3 mb-6">
            {[1, 2].map((s) => (
              <div key={s} className="flex items-center gap-2">
                <div
                  className={`w-7 h-7 rounded-full flex items-center justify-center text-xs font-bold transition-colors ${
                    step >= s ? 'bg-[#a3e635] text-[#0b1c30]' : 'bg-[#e5e7eb] text-[#9ca3af]'
                  }`}
                >
                  {step > s ? '✓' : s}
                </div>
                <span
                  className={`text-xs font-semibold ${
                    step >= s ? 'text-[#446900]' : 'text-[#9ca3af]'
                  }`}
                >
                  {s === 1 ? 'Loại tài khoản' : 'Thông tin cá nhân'}
                </span>
                {s < 2 && <div className="h-px w-8 bg-[#e5e7eb]" />}
              </div>
            ))}
          </div>

          {step === 1 ? (
            /* ── Step 1: Chọn vai trò ───────────────────────────────── */
            <div className="space-y-4">
              <p className="text-sm font-semibold text-[#424936] mb-3">
                Bạn muốn tham gia ReTrack với tư cách:
              </p>
              {ROLES.map((r) => (
                <button
                  key={r.value}
                  type="button"
                  onClick={() => setForm({ ...form, role: r.value })}
                  className={`w-full text-left p-4 rounded-2xl border-2 transition-all duration-200 ${
                    form.role === r.value
                      ? 'border-[#446900] bg-[#a3e635]/10 shadow-sm'
                      : 'border-[#e5e7eb] bg-white hover:border-[#d1d5db]'
                  }`}
                >
                  <div className="flex items-center gap-3">
                    <div
                      className={`w-5 h-5 rounded-full border-2 flex items-center justify-center flex-shrink-0 ${
                        form.role === r.value ? 'border-[#446900]' : 'border-[#d1d5db]'
                      }`}
                    >
                      {form.role === r.value && (
                        <div className="w-2.5 h-2.5 rounded-full bg-[#446900]" />
                      )}
                    </div>
                    <div>
                      <div className="font-bold text-[#0b1c30] text-sm">{r.label}</div>
                      <div className="text-xs text-[#6b7280] mt-0.5">{r.desc}</div>
                    </div>
                  </div>
                </button>
              ))}

              <button
                type="button"
                onClick={() => setStep(2)}
                className="w-full bg-[#a3e635] hover:bg-[#bef264] text-[#0b1c30] font-bold py-3.5 rounded-2xl transition-all duration-200 flex items-center justify-center gap-2 shadow-lg shadow-[#a3e635]/30 mt-6 text-sm"
              >
                Tiếp theo <ArrowRight size={18} />
              </button>

              {/* Google OAuth Login */}
              <div className="relative flex items-center my-4">
                <div className="flex-1 border-t border-[#e5e7eb]" />
                <span className="px-3 text-xs text-[#9ca3af] font-medium">hoặc</span>
                <div className="flex-1 border-t border-[#e5e7eb]" />
              </div>
              <div className="flex justify-center w-full">
                <div className="w-full [&>div]:w-full [&>div>div]:w-full flex justify-center">
                  <GoogleLogin
                    onSuccess={async (credentialResponse) => {
                      try {
                        const res = await api.post('/auth/google', {
                          idToken: credentialResponse.credential,
                        });
                        handleSuccess(res.data.data);
                      } catch (err) {
                        const msg =
                          err?.response?.data?.message || 'Đăng ký Google thất bại. Vui lòng thử lại.';
                        toast.error(msg);
                      }
                    }}
                    onError={() => toast.error('Đăng ký Google bị hủy')}
                    size="large"
                    theme="outline"
                    text="signup_with"
                    shape="rectangular"
                  />
                </div>
              </div>
            </div>
          ) : (
            /* ── Step 2: Điền thông tin với Validation chuyên nghiệp ─── */
            <form onSubmit={handleSubmit} className="space-y-4" noValidate>
              <div className="flex items-center gap-2 mb-2">
                <button
                  type="button"
                  onClick={() => setStep(1)}
                  className="text-xs text-[#446900] font-semibold hover:underline"
                >
                  ← Chọn vai trò khác
                </button>
                <span className="text-xs text-[#9ca3af]">·</span>
                <span className="text-xs font-bold text-[#446900] bg-[#a3e635]/20 px-2 py-0.5 rounded-full">
                  {ROLES.find((r) => r.value === form.role)?.label}
                </span>
              </div>

              {/* Họ và tên */}
              <div>
                <label className="block text-xs font-bold text-[#1f2937] mb-1">
                  Họ và tên <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  placeholder="Nguyễn Văn A"
                  value={form.fullName}
                  onChange={(e) => {
                    setForm({ ...form, fullName: e.target.value });
                    if (touched.fullName) {
                      setErrors((prev) => ({ ...prev, fullName: validateFullName(e.target.value) }));
                    }
                  }}
                  onBlur={() => {
                    setTouched((prev) => ({ ...prev, fullName: true }));
                    setErrors((prev) => ({ ...prev, fullName: validateFullName(form.fullName) }));
                  }}
                  className={`w-full px-3.5 py-2.5 rounded-xl border text-sm text-[#1f2937] bg-white transition-colors outline-none ${
                    errors.fullName
                      ? 'border-red-500 bg-red-50/20 focus:border-red-600 focus:ring-1 focus:ring-red-500'
                      : 'border-[#e5e7eb] focus:border-[#446900] focus:ring-1 focus:ring-[#446900]'
                  }`}
                />
                {errors.fullName && (
                  <p className="flex items-center gap-1 text-xs text-red-600 mt-1 font-medium">
                    <AlertCircle size={14} className="flex-shrink-0" />
                    <span>{errors.fullName}</span>
                  </p>
                )}
              </div>

              {/* Email */}
              <div>
                <label className="block text-xs font-bold text-[#1f2937] mb-1">
                  Địa chỉ Email <span className="text-red-500">*</span>
                </label>
                <div className="relative">
                  <input
                    type="email"
                    placeholder="tennguoidung@gmail.com"
                    value={form.email}
                    onChange={(e) => {
                      setForm({ ...form, email: e.target.value });
                      setAvailabilityStatus((prev) => ({ ...prev, email: null }));
                      if (touched.email) {
                        setErrors((prev) => ({ ...prev, email: validateEmail(e.target.value) }));
                      }
                    }}
                    onBlur={handleEmailBlur}
                    className={`w-full px-3.5 py-2.5 rounded-xl border text-sm text-[#1f2937] bg-white transition-colors outline-none pr-10 ${
                      errors.email
                        ? 'border-red-500 bg-red-50/20 focus:border-red-600 focus:ring-1 focus:ring-red-500'
                        : availabilityStatus.email?.available
                        ? 'border-emerald-500 focus:border-emerald-600 focus:ring-1 focus:ring-emerald-500'
                        : 'border-[#e5e7eb] focus:border-[#446900] focus:ring-1 focus:ring-[#446900]'
                    }`}
                  />
                  <div className="absolute right-3 top-2.5 text-gray-400">
                    {checkingAvailability.email ? (
                      <Loader2 size={16} className="animate-spin text-emerald-600" />
                    ) : availabilityStatus.email?.available ? (
                      <Check size={16} className="text-emerald-600 font-bold" />
                    ) : null}
                  </div>
                </div>
                {errors.email ? (
                  <p className="flex items-center gap-1 text-xs text-red-600 mt-1 font-medium">
                    <AlertCircle size={14} className="flex-shrink-0" />
                    <span>{errors.email}</span>
                  </p>
                ) : availabilityStatus.email?.available ? (
                  <p className="flex items-center gap-1 text-xs text-emerald-600 mt-1 font-medium">
                    <Check size={14} className="flex-shrink-0" />
                    <span>{availabilityStatus.email.message}</span>
                  </p>
                ) : null}
              </div>

              {/* Số điện thoại */}
              <div>
                <label className="block text-xs font-bold text-[#1f2937] mb-1">
                  Số điện thoại di động <span className="text-red-500">*</span>
                </label>
                <div className="relative">
                  <input
                    type="tel"
                    placeholder="0912345678 (10 chữ số)"
                    value={form.phone}
                    onChange={(e) => {
                      const val = e.target.value.replace(/[^0-9+]/g, '');
                      setForm({ ...form, phone: val });
                      setAvailabilityStatus((prev) => ({ ...prev, phone: null }));
                      if (touched.phone) {
                        setErrors((prev) => ({ ...prev, phone: validatePhone(val) }));
                      }
                    }}
                    onBlur={handlePhoneBlur}
                    className={`w-full px-3.5 py-2.5 rounded-xl border text-sm text-[#1f2937] bg-white transition-colors outline-none pr-10 ${
                      errors.phone
                        ? 'border-red-500 bg-red-50/20 focus:border-red-600 focus:ring-1 focus:ring-red-500'
                        : availabilityStatus.phone?.available
                        ? 'border-emerald-500 focus:border-emerald-600 focus:ring-1 focus:ring-emerald-500'
                        : 'border-[#e5e7eb] focus:border-[#446900] focus:ring-1 focus:ring-[#446900]'
                    }`}
                  />
                  <div className="absolute right-3 top-2.5 text-gray-400">
                    {checkingAvailability.phone ? (
                      <Loader2 size={16} className="animate-spin text-emerald-600" />
                    ) : availabilityStatus.phone?.available ? (
                      <Check size={16} className="text-emerald-600 font-bold" />
                    ) : null}
                  </div>
                </div>
                {errors.phone ? (
                  <p className="flex items-center gap-1 text-xs text-red-600 mt-1 font-medium">
                    <AlertCircle size={14} className="flex-shrink-0" />
                    <span>{errors.phone}</span>
                  </p>
                ) : availabilityStatus.phone?.available ? (
                  <p className="flex items-center gap-1 text-xs text-emerald-600 mt-1 font-medium">
                    <Check size={14} className="flex-shrink-0" />
                    <span>{availabilityStatus.phone.message}</span>
                  </p>
                ) : (
                  <p className="text-[11px] text-gray-500 mt-0.5">
                    Hỗ trợ tất cả nhà mạng Việt Nam (Viettel, VinaPhone, MobiFone, Vietnamobile...)
                  </p>
                )}
              </div>

              {/* Mật khẩu */}
              <div>
                <label className="block text-xs font-bold text-[#1f2937] mb-1">
                  Mật khẩu đăng nhập <span className="text-red-500">*</span>
                </label>
                <div className="relative">
                  <input
                    type={showPass ? 'text' : 'password'}
                    placeholder="Nhập mật khẩu an toàn..."
                    value={form.password}
                    onChange={(e) => {
                      const val = e.target.value;
                      setForm({ ...form, password: val });
                      if (form.confirmPassword) {
                        setErrors((prev) => ({
                          ...prev,
                          confirmPassword: validateConfirmPassword(form.confirmPassword, val),
                        }));
                      }
                    }}
                    onFocus={() => setIsPassFocused(true)}
                    onBlur={() => {
                      setIsPassFocused(false);
                      setTouched((prev) => ({ ...prev, password: true }));
                      if (!isPasswordValid) {
                        setErrors((prev) => ({
                          ...prev,
                          password: 'Mật khẩu chưa đáp ứng đủ tiêu chuẩn bảo mật.',
                        }));
                      } else {
                        setErrors((prev) => ({ ...prev, password: null }));
                      }
                    }}
                    className={`w-full px-3.5 py-2.5 rounded-xl border text-sm text-[#1f2937] bg-white transition-colors outline-none pr-10 ${
                      errors.password && touched.password
                        ? 'border-red-500 bg-red-50/20 focus:border-red-600 focus:ring-1 focus:ring-red-500'
                        : isPasswordValid
                        ? 'border-emerald-500 focus:border-emerald-600 focus:ring-1 focus:ring-emerald-500'
                        : 'border-[#e5e7eb] focus:border-[#446900] focus:ring-1 focus:ring-[#446900]'
                    }`}
                  />
                  <button
                    type="button"
                    onClick={() => setShowPass(!showPass)}
                    className="absolute right-3 top-2.5 text-gray-400 hover:text-[#446900] transition-colors"
                  >
                    {showPass ? <EyeOff size={18} /> : <Eye size={18} />}
                  </button>
                </div>

                {/* Password Strength Meter */}
                {form.password.length > 0 && (
                  <div className="mt-2 space-y-1.5">
                    <div className="flex items-center justify-between text-xs">
                      <span className="text-gray-500 font-medium">Độ an toàn:</span>
                      <span className={`font-bold ${strength.textClass}`}>{strength.text}</span>
                    </div>
                    <div className="grid grid-cols-4 gap-1.5 h-1.5">
                      {[1, 2, 3, 4].map((stepLvl) => (
                        <div
                          key={stepLvl}
                          className={`rounded-full transition-colors duration-200 ${
                            strength.level >= stepLvl ? strength.color : 'bg-gray-200'
                          }`}
                        />
                      ))}
                    </div>
                  </div>
                )}

                {/* Checklist yêu cầu mật khẩu (Hiển thị khi đang focus hoặc mật khẩu chưa đạt) */}
                {(isPassFocused || (form.password.length > 0 && !isPasswordValid)) && (
                  <div className="mt-2.5 p-3 rounded-xl bg-slate-50 border border-slate-200 space-y-1.5 text-xs animate-in fade-in duration-200">
                    <div className="flex items-center gap-1.5 font-bold text-slate-700 mb-1">
                      <ShieldCheck size={14} className="text-emerald-600" />
                      <span>Yêu cầu mật khẩu tiêu chuẩn:</span>
                    </div>
                    {passwordRules.map((rule) => (
                      <div
                        key={rule.id}
                        className={`flex items-center gap-2 transition-colors ${
                          rule.valid ? 'text-emerald-700 font-medium' : 'text-slate-500'
                        }`}
                      >
                        {rule.valid ? (
                          <div className="w-3.5 h-3.5 rounded-full bg-emerald-100 flex items-center justify-center text-emerald-600 flex-shrink-0">
                            <Check size={10} strokeWidth={3} />
                          </div>
                        ) : (
                          <div className="w-3.5 h-3.5 rounded-full border border-slate-300 flex-shrink-0" />
                        )}
                        <span>{rule.label}</span>
                      </div>
                    ))}
                  </div>
                )}
              </div>

              {/* Xác nhận mật khẩu */}
              <div>
                <label className="block text-xs font-bold text-[#1f2937] mb-1">
                  Xác nhận mật khẩu <span className="text-red-500">*</span>
                </label>
                <div className="relative">
                  <input
                    type={showConfirmPass ? 'text' : 'password'}
                    placeholder="Nhập lại chính xác mật khẩu trên..."
                    value={form.confirmPassword}
                    onChange={(e) => {
                      const val = e.target.value;
                      setForm({ ...form, confirmPassword: val });
                      if (touched.confirmPassword || val.length >= form.password.length) {
                        setErrors((prev) => ({
                          ...prev,
                          confirmPassword: validateConfirmPassword(val, form.password),
                        }));
                      }
                    }}
                    onBlur={() => {
                      setTouched((prev) => ({ ...prev, confirmPassword: true }));
                      setErrors((prev) => ({
                        ...prev,
                        confirmPassword: validateConfirmPassword(form.confirmPassword, form.password),
                      }));
                    }}
                    className={`w-full px-3.5 py-2.5 rounded-xl border text-sm text-[#1f2937] bg-white transition-colors outline-none pr-10 ${
                      errors.confirmPassword
                        ? 'border-red-500 bg-red-50/20 focus:border-red-600 focus:ring-1 focus:ring-red-500'
                        : form.confirmPassword && form.confirmPassword === form.password
                        ? 'border-emerald-500 focus:border-emerald-600 focus:ring-1 focus:ring-emerald-500'
                        : 'border-[#e5e7eb] focus:border-[#446900] focus:ring-1 focus:ring-[#446900]'
                    }`}
                  />
                  <button
                    type="button"
                    onClick={() => setShowConfirmPass(!showConfirmPass)}
                    className="absolute right-3 top-2.5 text-gray-400 hover:text-[#446900] transition-colors"
                  >
                    {showConfirmPass ? <EyeOff size={18} /> : <Eye size={18} />}
                  </button>
                </div>
                {errors.confirmPassword ? (
                  <p className="flex items-center gap-1 text-xs text-red-600 mt-1 font-medium">
                    <X size={14} className="flex-shrink-0" />
                    <span>{errors.confirmPassword}</span>
                  </p>
                ) : form.confirmPassword && form.confirmPassword === form.password ? (
                  <p className="flex items-center gap-1 text-xs text-emerald-600 mt-1 font-medium">
                    <Check size={14} className="flex-shrink-0" />
                    <span>Mật khẩu xác nhận trùng khớp hoàn toàn.</span>
                  </p>
                ) : null}
              </div>

              {/* Điều khoản */}
              <p className="text-xs text-[#6b7280] leading-relaxed pt-1">
                Bằng việc nhấn "Tạo tài khoản", bạn xác nhận đồng ý với{' '}
                <a href="#" className="text-[#446900] font-semibold hover:underline">
                  Điều khoản sử dụng
                </a>{' '}
                và{' '}
                <a href="#" className="text-[#446900] font-semibold hover:underline">
                  Chính sách bảo mật
                </a>{' '}
                của hệ thống RETRACK.
              </p>

              {/* Nút Submit */}
              <button
                type="submit"
                disabled={registerMutation.isPending}
                className="w-full bg-[#a3e635] hover:bg-[#bef264] text-[#0b1c30] font-bold py-3.5 rounded-2xl transition-all duration-200 flex items-center justify-center gap-2 shadow-lg shadow-[#a3e635]/30 disabled:opacity-60 disabled:cursor-not-allowed text-sm mt-3"
              >
                {registerMutation.isPending ? (
                  <div className="flex items-center gap-2">
                    <Loader2 size={18} className="animate-spin" />
                    <span>Đang xử lý đăng ký...</span>
                  </div>
                ) : (
                  <>
                    <span>Tạo tài khoản ReTrack</span>
                    <ArrowRight size={18} />
                  </>
                )}
              </button>
            </form>
          )}
        </div>
      </div>
    </div>
  );
}
