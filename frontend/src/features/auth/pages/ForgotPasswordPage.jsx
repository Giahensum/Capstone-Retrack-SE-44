import { useState, useEffect } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useMutation } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { authApi } from '../api';
import { Mail, KeyRound, Lock, Eye, EyeOff, ArrowLeft, ArrowRight, CheckCircle2, Loader2, RefreshCw } from 'lucide-react';
import { BrandMark } from '@/components/brand/BrandLogo';

export default function ForgotPasswordPage() {
  const navigate = useNavigate();
  const [step, setStep] = useState(1); // 1: Email, 2: OTP, 3: New Password, 4: Success
  const [email, setEmail] = useState('');
  const [otp, setOtp] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [showPass, setShowPass] = useState(false);
  const [showConfirmPass, setShowConfirmPass] = useState(false);
  const [countdown, setCountdown] = useState(60);
  const [canResend, setCanResend] = useState(false);

  useEffect(() => {
    let timer;
    if (step === 2 && countdown > 0) {
      timer = setInterval(() => {
        setCountdown((prev) => {
          if (prev <= 1) {
            setCanResend(true);
            return 0;
          }
          return prev - 1;
        });
      }, 1000);
    }
    return () => clearInterval(timer);
  }, [step, countdown]);

  const sendOtpMutation = useMutation({
    mutationFn: () => authApi.forgotPassword(email.trim()),
    onSuccess: (res) => {
      toast.success(res.message || 'Mã OTP đã được gửi đến email của bạn!');
      setStep(2);
      setCountdown(60);
      setCanResend(false);
    },
    onError: (err) => {
      toast.error(err?.response?.data?.message || 'Không thể gửi mã OTP. Vui lòng kiểm tra lại email.');
    },
  });

  const verifyOtpMutation = useMutation({
    mutationFn: () => authApi.verifyOtp(email.trim(), otp.trim()),
    onSuccess: (res) => {
      toast.success(res.message || 'Mã OTP hợp lệ!');
      setStep(3);
    },
    onError: (err) => {
      toast.error(err?.response?.data?.message || 'Mã OTP không chính xác hoặc đã hết hạn.');
    },
  });

  const resetPasswordMutation = useMutation({
    mutationFn: () => authApi.resetPassword(email.trim(), otp.trim(), newPassword),
    onSuccess: (res) => {
      toast.success(res.message || 'Đặt lại mật khẩu thành công!');
      setStep(4);
    },
    onError: (err) => {
      toast.error(err?.response?.data?.message || 'Đặt lại mật khẩu thất bại.');
    },
  });

  const handleSendEmail = (e) => {
    e.preventDefault();
    if (!email.trim()) return toast.error('Vui lòng nhập địa chỉ email');
    sendOtpMutation.mutate();
  };

  const handleVerifyOtp = (e) => {
    e.preventDefault();
    if (!otp.trim()) return toast.error('Vui lòng nhập mã OTP');
    verifyOtpMutation.mutate();
  };

  const handleResetPassword = (e) => {
    e.preventDefault();
    if (!newPassword) return toast.error('Vui lòng nhập mật khẩu mới');
    if (newPassword.length < 6) return toast.error('Mật khẩu phải có ít nhất 6 ký tự');
    if (newPassword !== confirmPassword) return toast.error('Mật khẩu xác nhận không khớp');
    resetPasswordMutation.mutate();
  };

  return (
    <div className="min-h-screen bg-[#f8f9ff] flex flex-col justify-center items-center p-4 lg:p-8">
      {/* Container */}
      <div className="w-full max-w-md">
        {/* Logo */}
        <div className="flex items-center justify-center gap-3 mb-6">
          <BrandMark size={52} />
          <span className="text-2xl font-black text-[#1f2937] tracking-tight">RETRACK</span>
        </div>

        {/* Card */}
        <div className="bg-white rounded-3xl shadow-xl border border-gray-100 overflow-hidden">
          {/* Card Header */}
          <div className="bg-gradient-to-r from-[#446900] to-[#2d4700] p-6 text-white text-center">
            <div className="w-12 h-12 bg-[#a3e635] text-[#446900] rounded-2xl flex items-center justify-center mx-auto mb-3 shadow-md">
              {step === 1 && <Mail size={24} />}
              {step === 2 && <KeyRound size={24} />}
              {step === 3 && <Lock size={24} />}
              {step === 4 && <CheckCircle2 size={24} />}
            </div>
            <h2 className="text-2xl font-black">
              {step === 1 && 'Quên mật khẩu?'}
              {step === 2 && 'Xác thực mã OTP'}
              {step === 3 && 'Tạo mật khẩu mới'}
              {step === 4 && 'Đổi mật khẩu thành công!'}
            </h2>
            <p className="text-xs text-[#a3e635]/90 mt-1 max-w-xs mx-auto">
              {step === 1 && 'Nhập email đã đăng ký để nhận mã OTP khôi phục mật khẩu.'}
              {step === 2 && `Mã xác thực 6 số đã được gửi tới ${email}`}
              {step === 3 && 'Nhập mật khẩu mới an toàn cho tài khoản của bạn.'}
              {step === 4 && 'Bạn có thể đăng nhập ngay bằng mật khẩu mới.'}
            </p>
          </div>

          {/* Form */}
          <div className="p-6 lg:p-8">
            {step === 1 && (
              <form onSubmit={handleSendEmail} className="space-y-4">
                <div>
                  <label className="text-xs font-bold text-gray-700 uppercase tracking-wider mb-1.5 block">
                    Địa chỉ Email
                  </label>
                  <div className="relative">
                    <Mail size={18} className="absolute left-3.5 top-1/2 -translate-y-1/2 text-gray-400" />
                    <input
                      type="email"
                      value={email}
                      onChange={(e) => setEmail(e.target.value)}
                      placeholder="email@example.com"
                      required
                      className="w-full bg-gray-50 border border-gray-200 rounded-xl pl-11 pr-4 py-3 text-sm text-gray-900 placeholder-gray-400 focus:bg-white focus:border-[#446900] focus:ring-2 focus:ring-[#446900]/20 outline-none transition-all"
                    />
                  </div>
                </div>

                <button
                  type="submit"
                  disabled={sendOtpMutation.isPending}
                  className="w-full bg-[#a3e635] hover:bg-[#bef264] text-[#0b1c30] font-bold py-3.5 rounded-xl transition-all shadow-md shadow-[#a3e635]/30 active:scale-[0.98] flex items-center justify-center gap-2 disabled:opacity-60"
                >
                  {sendOtpMutation.isPending ? (
                    <Loader2 size={18} className="animate-spin" />
                  ) : (
                    <>
                      Gửi mã OTP <ArrowRight size={18} />
                    </>
                  )}
                </button>

                <div className="text-center pt-2">
                  <Link to="/login" className="text-xs text-[#446900] font-bold hover:underline inline-flex items-center gap-1">
                    <ArrowLeft size={14} /> Quay lại trang đăng nhập
                  </Link>
                </div>
              </form>
            )}

            {step === 2 && (
              <form onSubmit={handleVerifyOtp} className="space-y-4">
                <div>
                  <div className="flex justify-between items-center mb-1.5">
                    <label className="text-xs font-bold text-gray-700 uppercase tracking-wider">
                      Mã xác thực (6 chữ số)
                    </label>
                    <button
                      type="button"
                      onClick={() => setStep(1)}
                      className="text-xs text-[#446900] hover:underline flex items-center gap-1 font-medium"
                    >
                      <ArrowLeft size={12} /> Đổi email
                    </button>
                  </div>
                  <div className="relative">
                    <KeyRound size={18} className="absolute left-3.5 top-1/2 -translate-y-1/2 text-gray-400" />
                    <input
                      type="text"
                      maxLength={6}
                      value={otp}
                      onChange={(e) => setOtp(e.target.value.replace(/\D/g, ''))}
                      placeholder="123456"
                      required
                      className="w-full bg-gray-50 border border-gray-200 rounded-xl pl-11 pr-4 py-3 text-center tracking-[0.5em] font-mono text-lg font-bold text-gray-900 focus:bg-white focus:border-[#446900] focus:ring-2 focus:ring-[#446900]/20 outline-none transition-all"
                    />
                  </div>
                </div>

                <div className="flex items-center justify-between text-xs text-gray-500 py-1">
                  <span>Không nhận được mã?</span>
                  {canResend ? (
                    <button
                      type="button"
                      onClick={() => sendOtpMutation.mutate()}
                      disabled={sendOtpMutation.isPending}
                      className="text-[#446900] font-bold hover:underline flex items-center gap-1"
                    >
                      <RefreshCw size={12} className={sendOtpMutation.isPending ? 'animate-spin' : ''} />
                      Gửi lại mã OTP
                    </button>
                  ) : (
                    <span className="text-gray-400 font-medium">Gửi lại sau {countdown}s</span>
                  )}
                </div>

                <button
                  type="submit"
                  disabled={verifyOtpMutation.isPending || otp.length !== 6}
                  className="w-full bg-[#a3e635] hover:bg-[#bef264] text-[#0b1c30] font-bold py-3.5 rounded-xl transition-all shadow-md shadow-[#a3e635]/30 active:scale-[0.98] flex items-center justify-center gap-2 disabled:opacity-60"
                >
                  {verifyOtpMutation.isPending ? (
                    <Loader2 size={18} className="animate-spin" />
                  ) : (
                    <>
                      Xác nhận mã OTP <ArrowRight size={18} />
                    </>
                  )}
                </button>
              </form>
            )}

            {step === 3 && (
              <form onSubmit={handleResetPassword} className="space-y-4">
                <div>
                  <label className="text-xs font-bold text-gray-700 uppercase tracking-wider mb-1.5 block">
                    Mật khẩu mới (ít nhất 6 ký tự)
                  </label>
                  <div className="relative">
                    <Lock size={18} className="absolute left-3.5 top-1/2 -translate-y-1/2 text-gray-400" />
                    <input
                      type={showPass ? 'text' : 'password'}
                      value={newPassword}
                      onChange={(e) => setNewPassword(e.target.value)}
                      placeholder="••••••••"
                      required
                      minLength={6}
                      className="w-full bg-gray-50 border border-gray-200 rounded-xl pl-11 pr-11 py-3 text-sm text-gray-900 placeholder-gray-400 focus:bg-white focus:border-[#446900] focus:ring-2 focus:ring-[#446900]/20 outline-none transition-all"
                    />
                    <button
                      type="button"
                      onClick={() => setShowPass(!showPass)}
                      className="absolute right-3.5 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
                    >
                      {showPass ? <EyeOff size={18} /> : <Eye size={18} />}
                    </button>
                  </div>
                </div>

                <div>
                  <label className="text-xs font-bold text-gray-700 uppercase tracking-wider mb-1.5 block">
                    Xác nhận mật khẩu mới
                  </label>
                  <div className="relative">
                    <Lock size={18} className="absolute left-3.5 top-1/2 -translate-y-1/2 text-gray-400" />
                    <input
                      type={showConfirmPass ? 'text' : 'password'}
                      value={confirmPassword}
                      onChange={(e) => setConfirmPassword(e.target.value)}
                      placeholder="••••••••"
                      required
                      minLength={6}
                      className="w-full bg-gray-50 border border-gray-200 rounded-xl pl-11 pr-11 py-3 text-sm text-gray-900 placeholder-gray-400 focus:bg-white focus:border-[#446900] focus:ring-2 focus:ring-[#446900]/20 outline-none transition-all"
                    />
                    <button
                      type="button"
                      onClick={() => setShowConfirmPass(!showConfirmPass)}
                      className="absolute right-3.5 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
                    >
                      {showConfirmPass ? <EyeOff size={18} /> : <Eye size={18} />}
                    </button>
                  </div>
                </div>

                <button
                  type="submit"
                  disabled={resetPasswordMutation.isPending}
                  className="w-full bg-[#a3e635] hover:bg-[#bef264] text-[#0b1c30] font-bold py-3.5 rounded-xl transition-all shadow-md shadow-[#a3e635]/30 active:scale-[0.98] flex items-center justify-center gap-2 disabled:opacity-60"
                >
                  {resetPasswordMutation.isPending ? (
                    <Loader2 size={18} className="animate-spin" />
                  ) : (
                    <>
                      Đổi mật khẩu <CheckCircle2 size={18} />
                    </>
                  )}
                </button>
              </form>
            )}

            {step === 4 && (
              <div className="text-center py-4 space-y-4">
                <div className="w-16 h-16 bg-emerald-100 text-emerald-600 rounded-full flex items-center justify-center mx-auto shadow-inner">
                  <CheckCircle2 size={36} />
                </div>
                <div>
                  <h4 className="text-lg font-bold text-gray-900">Mật khẩu đã được thay đổi!</h4>
                  <p className="text-sm text-gray-600 mt-1">
                    Mật khẩu tài khoản của bạn đã được cập nhật thành công. Vui lòng đăng nhập lại.
                  </p>
                </div>
                <button
                  type="button"
                  onClick={() => navigate('/login')}
                  className="w-full bg-[#446900] hover:bg-[#365300] text-white font-bold py-3.5 rounded-xl transition-all shadow-md active:scale-[0.98]"
                >
                  Đăng nhập ngay
                </button>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
