import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useMutation } from '@tanstack/react-query';
import { useGoogleLogin } from '@react-oauth/google';
import toast from 'react-hot-toast';
import { Input } from '@/components/ui/CommonUI';
import { useAuth } from '@/hooks/useAuth';
import api from '@/lib/axios';
import { Eye, EyeOff, Recycle, ArrowRight, CheckCircle2 } from 'lucide-react';
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
    { value: 'DRIVER', label: 'Tài xế', desc: 'Vận chuyển phế liệu từ kho đến nhà máy' },
    { value: 'FACTORY', label: 'Nhà máy tái chế', desc: 'Cơ sở tái chế quy mô lớn' },
];
export default function RegisterPage() {
    const navigate = useNavigate();
    const { login } = useAuth();
    const [showPass, setShowPass] = useState(false);
    const [step, setStep] = useState(1);
    const [form, setForm] = useState({
        email: '',
        password: '',
        fullName: '',
        phone: '',
        role: 'SELLER',
    });
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
        mutationFn: async () => {
            const res = await api.post('/auth/register', form);
            return res.data.data;
        },
        onSuccess: handleSuccess,
        onError: (err) => {
            const msg = err
                ?.response?.data?.message ?? 'Đăng ký thất bại';
            toast.error(msg);
        },
    });
    const googleLogin = useGoogleLogin({
        flow: 'implicit',
        onSuccess: async (tokenResponse) => {
            try {
                const res = await api.post('/auth/google', { idToken: tokenResponse.access_token });
                handleSuccess(res.data.data);
            }
            catch {
                toast.error('Đăng nhập Google thất bại');
            }
        },
        onError: () => toast.error('Đăng nhập Google bị hủy'),
    });
    return (<div className="min-h-screen bg-[#f8f9ff] flex">
      {/* Left panel */}
      <div className="hidden lg:flex lg:w-[45%] bg-gradient-to-br from-[#1f6c3a] to-[#0d3d20] relative overflow-hidden flex-col items-center justify-center p-12">
        <div className="absolute top-0 right-0 w-96 h-96 bg-white/5 rounded-full -translate-y-1/2 translate-x-1/2"/>
        <div className="absolute bottom-0 left-0 w-64 h-64 bg-[#a4f1b2]/10 rounded-full translate-y-1/2 -translate-x-1/2"/>

        <div className="relative z-10 text-white">
          <div className="w-20 h-20 bg-[#a4f1b2] rounded-3xl flex items-center justify-center mx-auto mb-8 shadow-2xl">
            <Recycle size={40} className="text-[#1f6c3a]"/>
          </div>
          <h2 className="text-3xl font-black text-center mb-6">Tham gia RETRACK</h2>
          <div className="space-y-4">
            {[
            'Bán phế liệu giá tốt, thu gom tận nơi',
            'Minh bạch cân đo, giá cả rõ ràng',
            'Thanh toán nhanh, hỗ trợ 24/7',
            'Góp phần bảo vệ môi trường',
        ].map((item) => (<div key={item} className="flex items-center gap-3">
                <CheckCircle2 size={20} className="text-[#a4f1b2] flex-shrink-0"/>
                <span className="text-white/90 font-medium">{item}</span>
              </div>))}
          </div>
        </div>
      </div>

      {/* Right panel */}
      <div className="flex-1 flex flex-col items-center justify-center p-6 lg:p-12 overflow-y-auto">
        <div className="flex items-center gap-3 mb-8 lg:hidden">
          <div className="w-10 h-10 bg-[#446900] rounded-xl flex items-center justify-center">
            <Recycle size={20} className="text-[#a3e635]"/>
          </div>
          <span className="text-xl font-black text-[#1f2937]">RETRACK</span>
        </div>

        <div className="w-full max-w-md">
          <div className="mb-6">
            <h2 className="text-3xl font-black text-[#0b1c30] mb-2">Tạo tài khoản</h2>
            <p className="text-[#424936] font-medium">
              Đã có tài khoản?{' '}
              <Link to="/login" className="text-[#446900] font-bold hover:underline">
                Đăng nhập ngay
              </Link>
            </p>
          </div>

          {/* Step indicator */}
          <div className="flex items-center gap-3 mb-8">
            {[1, 2].map((s) => (<div key={s} className="flex items-center gap-2">
                <div className={`w-8 h-8 rounded-full flex items-center justify-center text-sm font-bold transition-colors ${step >= s
                ? 'bg-[#a3e635] text-[#0b1c30]'
                : 'bg-[#e5e7eb] text-[#9ca3af]'}`}>
                  {step > s ? '✓' : s}
                </div>
                <span className={`text-sm font-semibold ${step >= s ? 'text-[#446900]' : 'text-[#9ca3af]'}`}>
                  {s === 1 ? 'Loại tài khoản' : 'Thông tin'}
                </span>
                {s < 2 && <div className="h-px w-8 bg-[#e5e7eb]"/>}
              </div>))}
          </div>

          {step === 1 ? (
        /* Step 1: Choose role */
        <div className="space-y-4">
              <p className="text-sm font-semibold text-[#424936] mb-4">Bạn muốn tham gia với tư cách:</p>
              {ROLES.map((r) => (<button key={r.value} onClick={() => setForm({ ...form, role: r.value })} className={`w-full text-left p-4 rounded-2xl border-2 transition-all duration-200 ${form.role === r.value
                    ? 'border-[#446900] bg-[#a3e635]/10'
                    : 'border-[#e5e7eb] bg-white hover:border-[#d1d5db]'}`}>
                  <div className="flex items-center gap-3">
                    <div className={`w-5 h-5 rounded-full border-2 flex items-center justify-center flex-shrink-0 ${form.role === r.value ? 'border-[#446900]' : 'border-[#d1d5db]'}`}>
                      {form.role === r.value && (<div className="w-2.5 h-2.5 rounded-full bg-[#446900]"/>)}
                    </div>
                    <div>
                      <div className="font-bold text-[#0b1c30] text-sm">{r.label}</div>
                      <div className="text-xs text-[#6b7280] mt-0.5">{r.desc}</div>
                    </div>
                  </div>
                </button>))}

              <button onClick={() => setStep(2)} className="w-full bg-[#a3e635] hover:bg-[#bef264] text-[#0b1c30] font-bold py-3.5 rounded-2xl transition-all duration-200 flex items-center justify-center gap-2 shadow-lg shadow-[#a3e635]/30 mt-6 text-base">
                Tiếp theo <ArrowRight size={18}/>
              </button>

              {/* Google */}
              <div className="relative flex items-center my-4">
                <div className="flex-1 border-t border-[#e5e7eb]"/>
                <span className="px-3 text-xs text-[#9ca3af] font-medium">hoặc</span>
                <div className="flex-1 border-t border-[#e5e7eb]"/>
              </div>
              <button onClick={() => googleLogin()} className="w-full flex items-center justify-center gap-3 border-2 border-[#e5e7eb] rounded-2xl py-3 px-6 font-semibold text-[#1f2937] hover:bg-[#f3f4f6] transition-all bg-white text-sm">
                <svg width="18" height="18" viewBox="0 0 24 24">
                  <path fill="#4285F4" d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z"/>
                  <path fill="#34A853" d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z"/>
                  <path fill="#FBBC05" d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z"/>
                  <path fill="#EA4335" d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z"/>
                </svg>
                Đăng ký với Google (SELLER)
              </button>
            </div>) : (
        /* Step 2: Fill info */
        <form onSubmit={(e) => { e.preventDefault(); registerMutation.mutate(); }} className="space-y-4">
              <div className="flex items-center gap-2 mb-2">
                <button type="button" onClick={() => setStep(1)} className="text-sm text-[#446900] font-semibold hover:underline">
                  ← Quay lại
                </button>
                <span className="text-sm text-[#9ca3af]">·</span>
                <span className="text-sm font-bold text-[#446900] bg-[#a3e635]/20 px-2 py-0.5 rounded-full">
                  {ROLES.find(r => r.value === form.role)?.label}
                </span>
              </div>

              <Input label="Họ và tên" placeholder="Nguyễn Văn A" value={form.fullName} onChange={(e) => setForm({ ...form, fullName: e.target.value })} required className="!bg-white !border-[#e5e7eb] !text-[#1f2937] focus:!border-[#446900] rounded-xl"/>
              <Input label="Email" type="email" placeholder="email@example.com" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} required className="!bg-white !border-[#e5e7eb] !text-[#1f2937] focus:!border-[#446900] rounded-xl"/>
              <Input label="Số điện thoại" type="tel" placeholder="0912345678" value={form.phone} onChange={(e) => setForm({ ...form, phone: e.target.value })} required className="!bg-white !border-[#e5e7eb] !text-[#1f2937] focus:!border-[#446900] rounded-xl"/>
              <div className="relative">
                <Input label="Mật khẩu" type={showPass ? 'text' : 'password'} placeholder="Tối thiểu 6 ký tự" value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} required minLength={6} className="!bg-white !border-[#e5e7eb] !text-[#1f2937] focus:!border-[#446900] rounded-xl"/>
                <button type="button" onClick={() => setShowPass(!showPass)} className="absolute right-3 bottom-2.5 text-[#9ca3af] hover:text-[#446900]">
                  {showPass ? <EyeOff size={18}/> : <Eye size={18}/>}
                </button>
              </div>

              <p className="text-xs text-[#6b7280] leading-relaxed">
                Bằng cách đăng ký, bạn đồng ý với{' '}
                <a href="#" className="text-[#446900] font-semibold hover:underline">Điều khoản sử dụng</a>{' '}
                và{' '}
                <a href="#" className="text-[#446900] font-semibold hover:underline">Chính sách bảo mật</a> của RETRACK.
              </p>

              <button type="submit" disabled={registerMutation.isPending} className="w-full bg-[#a3e635] hover:bg-[#bef264] text-[#0b1c30] font-bold py-3.5 rounded-2xl transition-all duration-200 flex items-center justify-center gap-2 shadow-lg shadow-[#a3e635]/30 disabled:opacity-60 disabled:cursor-not-allowed text-base mt-2">
                {registerMutation.isPending ? (<span className="w-5 h-5 border-2 border-current border-t-transparent rounded-full animate-spin"/>) : (<>Tạo tài khoản <ArrowRight size={18}/></>)}
              </button>
            </form>)}
        </div>
      </div>
    </div>);
}
