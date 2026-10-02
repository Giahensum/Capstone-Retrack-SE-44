import { Link } from 'react-router-dom';
import { Smartphone } from 'lucide-react';
import { LOGO_URL } from '../../../config/branding';

export default function StaffMobilePage() {
  return <main className="min-h-screen bg-[#f8f9ff] flex items-center justify-center p-6">
    <section className="w-full max-w-lg rounded-3xl bg-white border border-[#c2cab0] p-8 text-center text-[#0b1c30]">
      <img src={LOGO_URL} alt="ReTrack" className="w-64 mx-auto" />
      <Smartphone className="mx-auto text-[#446900] mb-5" size={36} />
      <h1 className="text-2xl font-bold mb-4">ReTrack dành cho nhân sự</h1>
      <p className="leading-7 text-slate-600 mb-6">Nhân viên kho và tài xế sử dụng ứng dụng ReTrack trên Android. Đăng nhập trong ứng dụng bằng tài khoản do chủ kho cung cấp.</p>
      <p className="text-sm text-slate-500 mb-8">Liên hệ chủ kho để nhận bản cài đặt hoặc được hỗ trợ tài khoản.</p>
      <Link to="/login" className="inline-block rounded-full bg-[#a3e635] px-6 py-3 font-semibold">Quay lại đăng nhập website</Link>
    </section>
  </main>;
}
