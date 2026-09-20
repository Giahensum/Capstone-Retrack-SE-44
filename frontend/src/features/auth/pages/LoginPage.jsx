import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useMutation } from '@tanstack/react-query';
import { useGoogleLogin } from '@react-oauth/google';
import toast from 'react-hot-toast';
import { Input } from '@/components/ui/CommonUI';
import { useAuth } from '@/hooks/useAuth';
import api from '@/lib/axios';
import { Eye, EyeOff, Recycle, ArrowRight } from 'lucide-react';
// ── Role → path mapping ──────────────────────────────────
const ROLE_HOME = {
    SELLER: '/seller',
    DEPOT_OWNER: '/depot',
    DEPOT_EMPLOYEE: '/employee',
    DRIVER: '/driver',
    FACTORY: '/factory',
    ADMIN: '/admin',
};
// ── Seed account hints (chỉ cho dev) ───────────────────
const SEED_ACCOUNTS = [
    { role: 'SELLER', email: 'seller@retrack.vn', pass: 'Seller@123' },
    { role: 'DEPOT_OWNER', email: 'depot@retrack.vn', pass: 'Depot@123' },
    { role: 'EMPLOYEE', email: 'employee@retrack.vn', pass: 'Employee@123' },
    { role: 'DRIVER', email: 'driver@retrack.vn', pass: 'Driver@123' },
    { role: 'FACTORY', email: 'factory@retrack.vn', pass: 'Factory@123' },
    { role: 'ADMIN', email: 'admin@retrack.vn', pass: 'Admin@123' },
];
export default function LoginPage() {
    const navigate = useNavigate();
    const { login } = useAuth();
    const [showPass, setShowPass] = useState(false);
    const [form, setForm] = useState({ email: '', password: '' });
    // ── Helpers ──────────────────────────────────────────
    const handleSuccess = (data) => {
        login(data.token, {
            userId: data.userId,
            email: data.email,
            fullName: data.fullName,
            role: data.role,
        });
        toast.success(`Xin chào, ${data.fullName}!`);
        navigate(ROLE_HOME[data.role] ?? '/');
    };
    // ── Email/Password login ──────────────────────────────
    const loginMutation = useMutation({
        mutationFn: async () => {
            const res = await api.post('/auth/login', form);
            return res.data.data;
        },
        onSuccess: handleSuccess,
        onError: () => toast.error('Email hoặc mật khẩu không đúng'),
    });
    // ── Google Login ─────────────────────────────────────
    const googleLogin = useGoogleLogin({
        flow: 'implicit',
        onSuccess: async (tokenResponse) => {
            try {
                // Dùng access_token để lấy userinfo rồi gửi id_token lên BE
                // Hoặc dùng flow="auth-code" với backend code exchange
                // Ở đây dùng access_token để lấy thông tin
                const googleMutation = await api.post('/auth/google', { idToken: tokenResponse.access_token });
                handleSuccess(googleMutation.data.data);
            }
            catch {
                toast.error('Đăng nhập Google thất bại');
            }
        },
        onError: () => toast.error('Đăng nhập Google bị hủy'),
    });
    const fillAccount = (email, pass) => {
        setForm({ email, password: pass });
    };
    return (<div className="min-h-screen bg-[#f8f9ff] flex">
      {/* Left — Branding */}
      <div className="hidden lg:flex lg:w-[45%] bg-gradient-to-br from-[#446900] to-[#2d4700] relative overflow-hidden flex-col items-center justify-center p-12">
        {/* Decorative circles */}
        <div className="absolute top-0 right-0 w-96 h-96 bg-white/5 rounded-full -translate-y-1/2 translate-x-1/2"/>
        <div className="absolute bottom-0 left-0 w-64 h-64 bg-[#a3e635]/10 rounded-full translate-y-1/2 -translate-x-1/2"/>

        <div className="relative z-10 text-center">
          <div className="w-20 h-20 bg-[#a3e635] rounded-3xl flex items-center justify-center mx-auto mb-8 shadow-2xl">
            <Recycle size={40} className="text-[#446900]"/>
          </div>
          <h1 className="text-4xl font-black text-white mb-4 leading-tight">
            Biến phế liệu<br />thành giá trị
          </h1>
          <p className="text-[#a3e635]/80 text-lg font-medium leading-relaxed max-w-xs mx-auto">
            Nền tảng kết nối tái chế thông minh — minh bạch, nhanh chóng, bền vững.
          </p>

          {/* Stats */}
          <div className="mt-12 grid grid-cols-2 gap-4">
            {[
            { label: 'Đối tác', value: '10,000+' },
            { label: 'Tấn thu gom', value: '1,245' },
            { label: 'Giao dịch', value: '8,492' },
            { label: 'Tỉnh thành', value: '63' },
        ].map((s) => (<div key={s.label} className="bg-white/10 rounded-2xl p-4 text-left">
                <div className="text-2xl font-black text-white">{s.value}</div>
                <div className="text-xs text-[#a3e635]/70 font-medium mt-0.5">{s.label}</div>
              </div>))}
          </div>
        </div>
      </div>

      {/* Right — Form */}
      <div className="flex-1 flex flex-col items-center justify-center p-6 lg:p-12 overflow-y-auto">
        {/* Mobile logo */}
        <div className="flex items-center gap-3 mb-8 lg:hidden">
          <div className="w-10 h-10 bg-[#446900] rounded-xl flex items-center justify-center">
            <Recycle size={20} className="text-[#a3e635]"/>
          </div>
          <span className="text-xl font-black text-[#1f2937]">RETRACK</span>
        </div>

        <div className="w-full max-w-md">
          <div className="mb-8">
            <h2 className="text-3xl font-black text-[#0b1c30] mb-2">Chào mừng trở lại!</h2>
            <p className="text-[#424936] font-medium">
              Chưa có tài khoản?{' '}
              <Link to="/register" className="text-[#446900] font-bold hover:underline">
                Đăng ký miễn phí
              </Link>
            </p>
          </div>

          {/* Google Login */}
          <button onClick={() => googleLogin()} className="w-full flex items-center justify-center gap-3 border-2 border-[#e5e7eb] rounded-2xl py-3.5 px-6 font-semibold text-[#1f2937] hover:bg-[#f3f4f6] hover:border-[#d1d5db] transition-all duration-200 mb-6 bg-white shadow-sm">
            <svg width="20" height="20" viewBox="0 0 24 24">
              <path fill="#4285F4" d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z"/>
              <path fill="#34A853" d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z"/>
              <path fill="#FBBC05" d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z"/>
              <path fill="#EA4335" d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z"/>
            </svg>
            Tiếp tục với Google
          </button>

          {/* Divider */}
          <div className="relative flex items-center my-6">
            <div className="flex-1 border-t border-[#e5e7eb]"/>
            <span className="px-4 text-sm text-[#9ca3af] font-medium bg-[#f8f9ff]">hoặc</span>
            <div className="flex-1 border-t border-[#e5e7eb]"/>
          </div>

          {/* Email form */}
          <form onSubmit={(e) => { e.preventDefault(); loginMutation.mutate(); }} className="space-y-4">
            <Input label="Email" type="email" placeholder="email@example.com" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} required className="!bg-white !border-[#e5e7eb] !text-[#1f2937] focus:!border-[#446900] focus:!ring-[#446900]/20 rounded-xl"/>

            <div className="relative">
              <Input label="Mật khẩu" type={showPass ? 'text' : 'password'} placeholder="••••••••" value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} required className="!bg-white !border-[#e5e7eb] !text-[#1f2937] focus:!border-[#446900] focus:!ring-[#446900]/20 rounded-xl"/>
              <button type="button" onClick={() => setShowPass(!showPass)} className="absolute right-3 bottom-2.5 text-[#9ca3af] hover:text-[#446900] transition-colors">
                {showPass ? <EyeOff size={18}/> : <Eye size={18}/>}
              </button>
            </div>

            <div className="flex justify-end">
              <button type="button" className="text-sm text-[#446900] font-semibold hover:underline">
                Quên mật khẩu?
              </button>
            </div>

            <button type="submit" disabled={loginMutation.isPending} className="w-full bg-[#a3e635] hover:bg-[#bef264] text-[#0b1c30] font-bold py-3.5 rounded-2xl transition-all duration-200 flex items-center justify-center gap-2 shadow-lg shadow-[#a3e635]/30 hover:shadow-[#a3e635]/50 disabled:opacity-60 disabled:cursor-not-allowed text-base">
              {loginMutation.isPending ? (<span className="w-5 h-5 border-2 border-current border-t-transparent rounded-full animate-spin"/>) : (<>Đăng nhập <ArrowRight size={18}/></>)}
            </button>
          </form>

          {/* Dev hint - seed accounts */}
          <details className="mt-8 border border-[#e5e7eb] rounded-2xl overflow-hidden">
            <summary className="px-4 py-3 text-xs font-bold text-[#9ca3af] cursor-pointer hover:bg-[#f3f4f6] select-none uppercase tracking-wide">
              🔧 Tài khoản demo (Development)
            </summary>
            <div className="p-4 space-y-2">
              {SEED_ACCOUNTS.map((a) => (<button key={a.role} onClick={() => fillAccount(a.email, a.pass)} className="w-full text-left flex items-center justify-between px-3 py-2 rounded-xl hover:bg-[#f3f4f6] transition-colors group">
                  <div>
                    <span className="text-xs font-bold text-[#446900] bg-[#a3e635]/20 px-2 py-0.5 rounded-full mr-2">
                      {a.role}
                    </span>
                    <span className="text-xs text-[#6b7280]">{a.email}</span>
                  </div>
                  <span className="text-xs text-[#9ca3af] group-hover:text-[#446900] font-medium">Điền →</span>
                </button>))}
            </div>
          </details>
        </div>
      </div>
    </div>);
}
