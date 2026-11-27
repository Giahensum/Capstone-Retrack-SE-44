import { useEffect } from 'react';
import { Link, NavLink, useLocation } from 'react-router-dom';
import { BrandMark } from '@/components/brand/BrandLogo';
import { useAuth } from '@/hooks/useAuth';

// Khung chung cho các trang công khai (giới thiệu gói, điều khoản): không cần đăng nhập, dùng màu thương hiệu RETRACK.
const LINKS = [
  { to: '/goi-dich-vu', label: 'Gói cho kho vựa' },
  { to: '/dieu-khoan', label: 'Điều khoản & chính sách' },
];

export function PublicShell({ children }) {
  const { user } = useAuth();
  const { hash, pathname } = useLocation();
  // Cuộn tới mục khi mở bằng liên kết có #, ví dụ /dieu-khoan#quyen-rieng-tu.
  useEffect(() => {
    if (!hash) { window.scrollTo(0, 0); return undefined; }
    // Chờ trình duyệt dựng xong bố cục rồi mới cuộn, nếu không vị trí mục bị tính sai.
    const timer = window.setTimeout(() => document.getElementById(decodeURIComponent(hash.slice(1)))?.scrollIntoView({ block: 'start' }), 50);
    return () => window.clearTimeout(timer);
  }, [hash, pathname]);

  return (
    <div className="min-h-screen bg-[#f8f9ff] text-[#1f2937]" style={{ fontFamily: 'Inter, sans-serif' }}>
      <header className="sticky top-0 z-40 border-b border-[#c2cab0]/40 bg-white/90 backdrop-blur-md">
        <div className="mx-auto flex h-16 max-w-[1120px] items-center justify-between gap-3 px-4 sm:px-6">
          <Link to="/" className="flex items-center gap-2 hover:opacity-80">
            <BrandMark size={36} />
            <span className="text-lg font-black tracking-tight text-[#446900]">RETRACK</span>
          </Link>
          <nav aria-label="Trang công khai" className="hidden items-center gap-6 md:flex">
            {LINKS.map((l) => (
              <NavLink key={l.to} to={l.to}
                className={({ isActive }) => `text-sm font-semibold ${isActive ? 'text-[#446900]' : 'text-[#424936] hover:text-[#446900]'}`}>
                {l.label}
              </NavLink>
            ))}
          </nav>
          {user ? (
            <Link to="/" className="rounded-full bg-[#a3e635] px-4 py-2 text-sm font-bold text-[#0b1c30] hover:bg-[#bef264]">Về trang chủ</Link>
          ) : (
            <div className="flex items-center gap-2">
              <Link to="/login" className="px-3 py-2 text-sm font-semibold text-[#424936] hover:text-[#446900]">Đăng nhập</Link>
              <Link to="/register" className="rounded-full bg-[#a3e635] px-4 py-2 text-sm font-bold text-[#0b1c30] hover:bg-[#bef264]">Đăng ký</Link>
            </div>
          )}
        </div>
        <nav aria-label="Trang công khai (di động)" className="flex gap-4 overflow-x-auto border-t border-[#e5e7eb] px-4 py-2 md:hidden">
          {LINKS.map((l) => (
            <NavLink key={l.to} to={l.to}
              className={({ isActive }) => `whitespace-nowrap text-sm font-semibold ${isActive ? 'text-[#446900]' : 'text-[#424936]'}`}>
              {l.label}
            </NavLink>
          ))}
        </nav>
      </header>
      <main className="mx-auto max-w-[1120px] px-4 py-10 sm:px-6">{children}</main>
      <footer className="bg-[#0b1c30] py-8 text-white/60">
        <div className="mx-auto flex max-w-[1120px] flex-col gap-3 px-4 text-sm sm:flex-row sm:items-center sm:justify-between sm:px-6">
          <p>© RETRACK — Capstone SE-44. Sản phẩm đồ án, chưa phải đơn vị kinh doanh đã đăng ký.</p>
          <div className="flex gap-5">
            <Link to="/goi-dich-vu" className="hover:text-white">Gói dịch vụ</Link>
            <Link to="/dieu-khoan" className="hover:text-white">Điều khoản</Link>
            <Link to="/dieu-khoan#quyen-rieng-tu" className="hover:text-white">Quyền riêng tư</Link>
          </div>
        </div>
      </footer>
    </div>
  );
}

/** Một mục nội dung có tiêu đề và neo để liên kết trực tiếp. */
export function PublicSection({ id, title, children }) {
  return (
    <section id={id} className="scroll-mt-24 rounded-2xl border border-[#e5e7eb] bg-white p-5 shadow-sm sm:p-7">
      <h2 className="mb-3 text-lg font-extrabold text-[#0b1c30]">{title}</h2>
      <div className="space-y-2 text-sm leading-6 text-[#424936] [&_li]:ml-5 [&_li]:list-disc">{children}</div>
    </section>
  );
}
