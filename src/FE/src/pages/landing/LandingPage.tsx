import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '@/hooks/useAuth'
import { Recycle, Menu, X, ArrowRight, TrendingUp, TrendingDown, Minus, Star, CheckCircle2, Info, ArrowUpRight } from 'lucide-react'

// ── Data ─────────────────────────────────────────────────────────────────────

const PRICE_TABLE = [
  { type: 'Sắt đặc', icon: 'hardware', price: '12.000 - 15.000', unit: 'VNĐ/kg', trend: 'up', trendLabel: 'Tăng nhẹ' },
  { type: 'Đồng cáp', icon: 'hardware', price: '180.000 - 220.000', unit: 'VNĐ/kg', trend: 'stable', trendLabel: 'Ổn định' },
  { type: 'Giấy Carton', icon: 'description', price: '3.500 - 4.500', unit: 'VNĐ/kg', trend: 'down', trendLabel: 'Giảm nhẹ' },
  { type: 'Nhựa PET', icon: 'recycling', price: '8.000 - 12.000', unit: 'VNĐ/kg', trend: 'stable', trendLabel: 'Ổn định' },
]

const PROCESS_STEPS = [
  { num: '01', icon: 'assignment', title: 'Tạo yêu cầu', desc: 'Seller mô tả phế liệu, thêm hình ảnh, địa chỉ và thời gian muốn được thu gom.' },
  { num: '02', icon: 'store', title: 'Chọn kho', desc: 'Chọn một kho vựa phù hợp hoặc Broadcast yêu cầu đến các kho gần bạn.' },
  { num: '03', icon: 'local_shipping', title: 'Thu gom tận nơi', desc: 'Nhân viên nhận đơn, đến địa chỉ Seller và tiếp nhận phế liệu.' },
  { num: '04', icon: 'scale', title: 'Phân loại & Cân', desc: 'Nhân viên phân loại, cân thực tế và nhập đơn giá cho từng loại phế liệu.' },
  { num: '05', icon: 'verified', title: 'Xác nhận giá', desc: 'Seller kiểm tra khối lượng, đơn giá và tổng tiền rồi xác nhận giao dịch.' },
  { num: '06', icon: 'payments', title: 'Thanh toán', desc: 'Chủ kho thực hiện thanh toán. Seller xác nhận đã nhận tiền và giao dịch hoàn tất.' },
]

const TESTIMONIALS = [
  { initials: 'AN', name: 'Chị An', role: 'Người bán (Seller)', stars: 5, bg: 'bg-[#a3e635]', text: 'text-[#446900]', quote: '"Lần đầu tiên dùng web bán ve chai mà thấy chuyên nghiệp vậy. Cân đo rõ ràng, giá cả minh bạch ngay từ đầu. Nhân viên tới tận nhà dọn dẹp sạch sẽ."' },
  { initials: 'BD', name: 'Chú Bình Đặng', role: 'Chủ vựa (Depot)', stars: 5, bg: 'bg-[#a3e635]/20', text: 'text-[#446900]', quote: '"Từ hồi xài phần mềm của RETRACK quản lý sổ sách khỏe re. Khách trên app nổ đơn đều đặn, đỡ mất công chạy rảo như hồi xưa."' },
  { initials: 'EP', name: 'Eco Plastics VN', role: 'Nhà máy (Factory)', stars: 4, bg: 'bg-[#e5e7eb]', text: 'text-[#1f2937]', quote: '"Nguồn nguyên liệu đầu vào ổn định và chất lượng hơn hẳn nhờ hệ thống phân loại chuẩn của RETRACK. Truy xuất nguồn gốc EPR cũng dễ dàng hơn."' },
]

const WHY_DIFFERENT = [
  { icon: 'category', title: 'Loại vật liệu', desc: 'Cùng là sắt nhưng sắt đặc, sắt vụn hay tôn cũ có giá khác nhau. Việc phân loại chính xác tại chỗ sẽ quyết định đơn giá áp dụng.' },
  { icon: 'scale', title: 'Khối lượng', desc: 'Bán khối lượng lớn thường được áp dụng mức giá sỉ tốt hơn so với bán lẻ do tiết kiệm chi phí vận chuyển và thu gom.' },
  { icon: 'verified', title: 'Chất lượng', desc: 'Tỷ lệ tạp chất ảnh hưởng đến khối lượng tịnh. Hàng sạch, đã phân loại sơ bộ có giá cao hơn.' },
  { icon: 'trending_up', title: 'Thị trường', desc: 'Giá phế liệu biến động hàng ngày theo thị trường vật liệu tái chế toàn cầu và nhu cầu của các nhà máy sản xuất trong nước.' },
]

const FOOTER_LINKS = {
  'RETRACK': ['Về chúng tôi', 'Blog', 'Tuyển dụng', 'Liên hệ'],
  'Dịch vụ': ['Bán phế liệu', 'Kho vựa', 'Vận chuyển', 'Nhà máy'],
  'Hỗ trợ': ['Trung tâm trợ giúp', 'Báo cáo sự cố', 'Chính sách', 'Điều khoản'],
}

// ── Components ────────────────────────────────────────────────────────────────

function TrendIcon({ trend }: { trend: string }) {
  if (trend === 'up') return <TrendingUp size={16} className="text-[#446900]" />
  if (trend === 'down') return <TrendingDown size={16} className="text-[#ba1a1a]" />
  return <Minus size={16} className="text-[#6b7280]" />
}

function Stars({ count }: { count: number }) {
  return (
    <div className="flex gap-0.5">
      {[1, 2, 3, 4, 5].map((i) => (
        <Star
          key={i}
          size={16}
          className={i <= count ? 'fill-[#eab308] text-[#eab308]' : 'text-[#e5e7eb]'}
        />
      ))}
    </div>
  )
}

// ── Main Page ─────────────────────────────────────────────────────────────────

export default function LandingPage() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const [menuOpen, setMenuOpen] = useState(false)
  const [priceFilter, setPriceFilter] = useState('Tất cả')

  const handleLogout = () => {
    logout()
    navigate('/login')
  }

  const priceFilters = ['Tất cả', 'Kim loại', 'Giấy', 'Nhựa', 'Điện tử', 'Khác']

  return (
    <div className="min-h-screen bg-[#f8f9ff] text-[#1f2937] font-[Inter]" style={{ fontFamily: 'Inter, sans-serif' }}>

      {/* ── HEADER ──────────────────────────────────────────────────── */}
      <header className="fixed top-0 w-full z-50 bg-white/90 backdrop-blur-md shadow-sm border-b border-[#c2cab0]/30 transition-all duration-300">
        <div className="flex justify-between items-center px-10 py-0 max-w-[1280px] mx-auto h-20">
          {/* Logo */}
          <Link to="/seller" className="flex items-center gap-2 hover:opacity-80 transition-opacity">
            <div className="w-10 h-10 bg-[#446900] rounded-xl flex items-center justify-center">
              <Recycle size={22} className="text-[#a3e635]" />
            </div>
            <span className="font-black text-[#446900] text-xl tracking-tight">RETRACK</span>
          </Link>

          {/* Nav */}
          <nav className="hidden xl:flex items-center gap-8">
            {['Trang chủ', 'Cách hoạt động', 'Bán phế liệu', 'Kho vựa', 'Marketplace', 'Bảng giá'].map((item, i) => (
              <a
                key={item}
                href={i === 0 ? '#' : `#section-${i}`}
                className={`text-sm font-semibold transition-colors ${
                  i === 0 ? 'text-[#446900]' : 'text-[#424936] hover:text-[#446900]'
                }`}
              >
                {item}
              </a>
            ))}
          </nav>

          {/* Auth buttons */}
          <div className="flex items-center gap-3">
            <button className="xl:hidden p-2 text-[#424936]" onClick={() => setMenuOpen(!menuOpen)}>
              {menuOpen ? <X size={22} /> : <Menu size={22} />}
            </button>
            {user ? (
              <>
                <span className="hidden md:block text-sm font-semibold text-[#424936]">
                  Xin chào, <span className="text-[#446900]">{user.fullName.split(' ').slice(-1)}</span>
                </span>
                <button
                  onClick={handleLogout}
                  className="text-sm text-[#424936] font-semibold px-4 py-2 hover:text-[#446900] transition-colors"
                >
                  Đăng xuất
                </button>
              </>
            ) : (
              <>
                <Link to="/login" className="text-sm text-[#424936] font-semibold px-4 py-2 hover:text-[#446900] transition-colors">
                  Đăng nhập
                </Link>
                <Link to="/register" className="border border-[#c2cab0] text-[#1f2937] text-sm font-bold px-5 py-2 rounded-full hover:bg-[#f3f4f6] transition-colors">
                  Đăng ký
                </Link>
              </>
            )}
            <Link
              to={user ? '/seller/request' : '/register'}
              className="bg-[#a3e635] text-[#0b1c30] text-sm font-bold px-5 py-2.5 rounded-full hover:bg-[#bef264] transition-colors shadow-sm flex items-center gap-2"
            >
              Bán phế liệu ngay
            </Link>
          </div>
        </div>

        {/* Mobile nav */}
        {menuOpen && (
          <div className="xl:hidden bg-white border-t border-[#e5e7eb] px-6 py-4 space-y-3">
            {['Trang chủ', 'Cách hoạt động', 'Bán phế liệu', 'Kho vựa', 'Marketplace', 'Bảng giá'].map((item) => (
              <a key={item} href="#" className="block text-sm font-semibold text-[#424936] py-2 hover:text-[#446900]">
                {item}
              </a>
            ))}
          </div>
        )}
      </header>

      <div className="pt-20">

        {/* ── HERO ──────────────────────────────────────────────────── */}
        <section className="relative max-w-full mx-auto pb-24 overflow-hidden pt-20">
          {/* Background */}
          <div className="absolute inset-0 z-0">
            <div className="absolute inset-0 bg-gradient-to-r from-[#f8f9ff] via-[#f8f9ff]/80 to-transparent z-10 w-full md:w-2/3" />
            <img
              src="https://images.unsplash.com/photo-1532996122724-e3c354a0b15b?w=1200&q=80"
              alt="Recycling background"
              className="absolute inset-0 w-full h-full object-cover object-right"
            />
          </div>

          <div className="max-w-[1280px] mx-auto px-10 relative z-20">
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-12 items-center min-h-[600px]">

              {/* Hero text */}
              <div className="flex flex-col gap-6 z-10 pt-12">
                <h1 className="text-[56px] leading-[1.1] text-[#0b1c30] font-black tracking-tight">
                  Biến phế liệu thành giá trị – Kết nối tái chế thông minh
                </h1>
                <p className="text-xl text-[#424936] max-w-xl leading-relaxed font-medium">
                  Nền tảng công nghệ kết nối trực tiếp người bán với mạng lưới trạm thu mua và nhà máy tái chế.
                  Nhanh chóng, minh bạch giá cả và hướng tới tương lai bền vững.
                </p>
                <div className="flex flex-wrap gap-4 mt-6">
                  <Link
                    to={user ? '/seller/request' : '/register'}
                    className="bg-[#a3e635] text-[#0b1c30] font-bold px-8 py-4 rounded-full hover:bg-[#bef264] transition-all duration-200 shadow-md hover:shadow-lg flex items-center gap-2 text-base"
                  >
                    Bán phế liệu ngay
                    <ArrowRight size={18} />
                  </Link>
                  <button className="bg-white/80 backdrop-blur-md text-[#0b1c30] font-semibold px-8 py-4 rounded-full hover:bg-white transition-all border border-[#c2cab0] flex items-center gap-2 text-base shadow-sm">
                    Khám phá cách hoạt động
                  </button>
                </div>

                {/* Social proof */}
                <div className="mt-8 flex items-center gap-4">
                  <div className="flex -space-x-3">
                    {['👤', '🏪', '🏭'].map((emoji, i) => (
                      <div key={i} className={`w-10 h-10 rounded-full flex items-center justify-center border-2 border-[#f8f9ff] shadow-sm ${
                        ['bg-[#a4f1b2]', 'bg-[#a3e635]', 'bg-[#d1d3d5]'][i]
                      }`} style={{ zIndex: 3 - i }}>
                        <span className="text-sm">{emoji}</span>
                      </div>
                    ))}
                  </div>
                  <p className="text-sm font-medium text-[#424936] bg-white/50 backdrop-blur-sm px-3 py-1 rounded-full border border-[#c2cab0]/30">
                    Kết nối <span className="text-[#446900] font-bold">10,000+</span> đối tác trên toàn quốc
                  </p>
                </div>
              </div>

              {/* Dashboard card */}
              <div className="relative w-full hidden lg:flex items-center justify-center p-8">
                <div className="relative w-full max-w-lg bg-white/40 backdrop-blur-xl rounded-[2rem] border border-white/40 p-8 shadow-2xl flex flex-col gap-6">
                  <div className="absolute -inset-1 bg-gradient-to-tr from-[#a3e635]/30 to-white/10 rounded-[2.1rem] blur-sm -z-10" />

                  <div className="flex justify-between items-center pb-6">
                    <h3 className="font-bold text-[#0b1c30] text-xl">Tổng quan hệ thống</h3>
                    <div className="flex items-center gap-2 bg-[#a3e635]/80 px-4 py-2 rounded-full border border-[#a3e635]">
                      <span className="w-2 h-2 rounded-full bg-[#446900] animate-pulse" />
                      <span className="text-[#446900] font-bold text-xs uppercase tracking-wider">Live</span>
                    </div>
                  </div>

                  <div className="grid grid-cols-2 gap-4">
                    {[
                      { icon: '♻️', label: 'Tổng phế liệu thu gom', value: '1,245.8', unit: 'Tấn' },
                      { icon: '🤝', label: 'Số giao dịch hoàn tất', value: '8,492', unit: '' },
                    ].map((card) => (
                      <div key={card.label} className="bg-white/90 backdrop-blur-md p-6 rounded-2xl border border-white/50 shadow-sm flex flex-col gap-3 hover:scale-105 hover:shadow-xl hover:border-[#a3e635] transition-all duration-300 cursor-pointer">
                        <div className="w-10 h-10 bg-[#a3e635]/30 rounded-full flex items-center justify-center text-xl">
                          {card.icon}
                        </div>
                        <div>
                          <p className="text-[#424936] font-semibold text-[10px] uppercase tracking-wider mb-1">{card.label}</p>
                          <h4 className="text-[#0b1c30] font-black text-2xl">
                            {card.value} {card.unit && <span className="text-sm font-semibold text-[#424936]">{card.unit}</span>}
                          </h4>
                        </div>
                      </div>
                    ))}
                  </div>

                  <div className="bg-white/90 backdrop-blur-md p-6 rounded-2xl border border-white/50 shadow-sm flex items-center justify-between hover:scale-105 hover:shadow-xl hover:border-[#a3e635] transition-all duration-300 cursor-pointer">
                    <div className="flex items-center gap-4">
                      <div className="w-12 h-12 bg-[#eff4ff] rounded-full flex items-center justify-center text-2xl">💰</div>
                      <div>
                        <h5 className="font-bold text-[#0b1c30] text-base">Minh bạch giá</h5>
                        <p className="text-[#424936] text-xs font-medium">Cập nhật realtime từ thị trường</p>
                      </div>
                    </div>
                    <div className="w-8 h-8 bg-[#a3e635]/20 rounded-full flex items-center justify-center">
                      <ArrowUpRight size={16} className="text-[#446900]" />
                    </div>
                  </div>
                </div>
              </div>

            </div>
          </div>
        </section>

        {/* ── RETRACK LÀ GÌ ─────────────────────────────────────────── */}
        <section className="bg-[#f8f9ff] py-24 border-t border-[#c2cab0]/30 relative z-10">
          <div className="max-w-[1280px] mx-auto px-10">
            <div className="max-w-3xl mb-16">
              <h2 className="text-4xl font-black text-[#0b1c30] mb-6">RETRACK Là Gì?</h2>
              <p className="text-lg text-[#424936] leading-relaxed font-medium">
                RETRACK là một nền tảng SaaS & Marketplace toàn diện dành cho hệ sinh thái tái chế. Chúng tôi cung cấp
                công cụ quản lý chuyên sâu cho các trạm thu mua (Depot), đồng thời tạo ra một sàn giao dịch trực tiếp
                giữa người bán lẻ (Seller) và các nhà máy tái chế (Factory). Thông qua việc chuẩn hóa quy trình, minh
                bạch dữ liệu và tự động hóa vận hành, RETRACK hướng tới việc xây dựng một chuỗi cung ứng vật liệu tái
                chế bền vững, hiệu quả và có thể truy xuất nguồn gốc (EPR).
              </p>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
              {/* SELLER */}
              <div className="bg-[#eff4ff] p-8 rounded-[1.5rem] border border-[#c2cab0]/50 shadow-md hover:shadow-xl transition-all duration-300">
                <div className="mb-6 overflow-hidden rounded-2xl shadow-sm h-48 bg-[#a3e635]/10 flex items-center justify-center">
                  <span className="text-8xl">🛒</span>
                </div>
                <div className="w-14 h-14 bg-[#a3e635]/40 rounded-2xl flex items-center justify-center mb-6">
                  <span className="text-2xl">👤</span>
                </div>
                <h3 className="text-xl font-bold text-[#0b1c30] mb-3">Người Bán (SELLER)</h3>
                <p className="text-[#424936] mb-6 leading-relaxed font-medium">
                  Hộ gia đình, cá nhân hoặc doanh nghiệp có phế liệu cần thanh lý. Bán nhanh chóng, giá tốt, thu gom tận nơi.
                </p>
                <ul className="space-y-3">
                  {['Báo giá minh bạch', 'Theo dõi đơn hàng', 'Tích điểm đổi quà'].map((item) => (
                    <li key={item} className="flex items-center gap-3">
                      <CheckCircle2 size={18} className="text-[#a3e635] flex-shrink-0" />
                      <span className="text-sm font-semibold text-[#0b1c30]">{item}</span>
                    </li>
                  ))}
                </ul>
              </div>

              {/* DEPOT — highlighted */}
              <div className="bg-[#eff4ff] p-8 rounded-[1.5rem] border-2 border-[#446900]/20 shadow-md hover:shadow-xl transition-all duration-300 relative overflow-hidden">
                <div className="absolute top-0 right-0 bg-[#446900] text-white text-xs font-bold px-4 py-1.5 rounded-bl-[1rem]">CORE</div>
                <div className="mb-6 overflow-hidden rounded-2xl shadow-sm h-48 bg-[#a3e635]/10 flex items-center justify-center">
                  <span className="text-8xl">🏪</span>
                </div>
                <div className="w-14 h-14 bg-[#a3e635] rounded-2xl flex items-center justify-center mb-6">
                  <span className="text-2xl">🏬</span>
                </div>
                <h3 className="text-xl font-bold text-[#0b1c30] mb-3">Kho Vựa (DEPOT)</h3>
                <p className="text-[#424936] mb-6 leading-relaxed font-medium">
                  Các trạm thu mua trung gian. Tối ưu hóa vận hành, quản lý nhân viên và kết nối nhà máy qua phần mềm SaaS.
                </p>
                <ul className="space-y-3">
                  {['Quản lý kho thông minh', 'Phân công tự động', 'Báo cáo doanh thu'].map((item) => (
                    <li key={item} className="flex items-center gap-3">
                      <CheckCircle2 size={18} className="text-[#446900] flex-shrink-0" />
                      <span className="text-sm font-semibold text-[#0b1c30]">{item}</span>
                    </li>
                  ))}
                </ul>
              </div>

              {/* FACTORY */}
              <div className="bg-[#eff4ff] p-8 rounded-[1.5rem] border border-[#c2cab0]/50 shadow-md hover:shadow-xl transition-all duration-300">
                <div className="mb-6 overflow-hidden rounded-2xl shadow-sm h-48 bg-[#a3e635]/10 flex items-center justify-center">
                  <span className="text-8xl">🏭</span>
                </div>
                <div className="w-14 h-14 bg-[#a4f1b2]/40 rounded-2xl flex items-center justify-center mb-6">
                  <span className="text-2xl">🏗️</span>
                </div>
                <h3 className="text-xl font-bold text-[#0b1c30] mb-3">Nhà Máy (FACTORY)</h3>
                <p className="text-[#424936] mb-6 leading-relaxed font-medium">
                  Cơ sở tái chế quy mô lớn. Nguồn cung ổn định, đảm bảo chất lượng, hỗ trợ truy xuất nguồn gốc EPR.
                </p>
                <ul className="space-y-3">
                  {['Marketplace lô hàng lớn', 'Chứng nhận EPR', 'Kiểm soát chất lượng (QC)'].map((item) => (
                    <li key={item} className="flex items-center gap-3">
                      <CheckCircle2 size={18} className="text-[#a3e635] flex-shrink-0" />
                      <span className="text-sm font-semibold text-[#0b1c30]">{item}</span>
                    </li>
                  ))}
                </ul>
              </div>
            </div>
          </div>
        </section>

        {/* ── BẢNG GIÁ ─────────────────────────────────────────────── */}
        <section className="bg-[#f8f9ff] max-w-[1280px] mx-auto px-10 py-24" id="section-5">
          <div className="flex flex-col md:flex-row justify-between items-end mb-12 gap-6">
            <div className="max-w-2xl">
              <h2 className="text-4xl font-black text-[#0b1c30] mb-4">Giá phế liệu hôm nay</h2>
              <p className="text-[#424936] text-lg font-medium">
                Tham khảo giá thu mua phế liệu mới nhất trên RETRACK. Giá thực tế có thể thay đổi tùy chất lượng, khối lượng, khu vực.
              </p>
            </div>
            <div className="flex flex-col items-end">
              <span className="text-sm text-[#727a64] mb-4 font-medium">Cập nhật lần cuối: Hôm nay, 10:30</span>
              <button className="bg-[#a3e635] text-[#0b1c30] font-bold px-6 py-3 rounded-full hover:bg-[#bef264] transition-colors flex items-center gap-2 shadow-sm">
                Xem toàn bộ bảng giá
                <ArrowRight size={16} />
              </button>
            </div>
          </div>

          {/* Filter tabs */}
          <div className="flex flex-wrap gap-3 mb-8">
            {priceFilters.map((f) => (
              <button
                key={f}
                onClick={() => setPriceFilter(f)}
                className={`text-sm font-bold px-6 py-2.5 rounded-full transition-colors border shadow-sm ${
                  priceFilter === f
                    ? 'bg-[#446900] text-white border-[#446900]'
                    : 'bg-white text-[#424936] border-[#c2cab0] hover:bg-[#f3f4f6]'
                }`}
              >
                {f}
              </button>
            ))}
          </div>

          {/* Table */}
          <div className="overflow-x-auto rounded-[1.5rem] border border-[#c2cab0]/60 shadow-sm bg-white">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="bg-[#eff4ff] border-b border-[#c2cab0]/60">
                  {['Loại phế liệu', 'Giá tham khảo', 'Đơn vị', 'Xu hướng', 'Chi tiết'].map((h, i) => (
                    <th key={h} className={`p-6 font-bold text-[#0b1c30] uppercase tracking-wider text-sm ${i === 4 ? 'text-right' : ''}`}>
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-[#c2cab0]/40">
                {PRICE_TABLE.map((row) => (
                  <tr key={row.type} className="hover:bg-[#eff4ff] transition-colors">
                    <td className="p-6">
                      <div className="flex items-center gap-4">
                        <div className="w-10 h-10 rounded-full bg-[#a3e635]/20 flex items-center justify-center text-[#446900] text-lg">♻️</div>
                        <span className="font-semibold text-[#0b1c30]">{row.type}</span>
                      </div>
                    </td>
                    <td className="p-6 font-bold text-[#0b1c30]">{row.price}</td>
                    <td className="p-6 text-[#424936] font-medium">{row.unit}</td>
                    <td className="p-6">
                      <div className={`flex items-center gap-1.5 font-semibold text-sm ${
                        row.trend === 'up' ? 'text-[#446900]' : row.trend === 'down' ? 'text-[#ba1a1a]' : 'text-[#424936]'
                      }`}>
                        <TrendIcon trend={row.trend} />
                        {row.trendLabel}
                      </div>
                    </td>
                    <td className="p-6 text-right">
                      <a href="#" className="text-[#446900] font-bold hover:underline text-sm">Xem chi tiết</a>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="mt-6 text-sm text-[#727a64] flex items-start gap-3 bg-[#eff4ff] p-5 rounded-xl border border-[#c2cab0]/40">
            <Info size={18} className="text-[#446900] flex-shrink-0 mt-0.5" />
            <p className="font-medium text-[#424936]">
              Giá trên chỉ mang tính tham khảo. Giá cuối cùng được xác định sau khi nhân viên phân loại, cân thực tế và xác nhận với Seller.
            </p>
          </div>
        </section>

        {/* ── QUY TRÌNH ─────────────────────────────────────────────── */}
        <section className="bg-[#dce9ff] py-24" id="section-1">
          <div className="max-w-[1280px] mx-auto px-10">
            <div className="text-center max-w-3xl mx-auto mb-16">
              <h2 className="text-4xl font-black text-[#0b1c30] mb-6">Quy trình bán phế liệu tại RETRACK</h2>
              <p className="text-lg text-[#424936] leading-relaxed font-medium">
                Chỉ vài bước đơn giản để bán phế liệu, được thu gom tận nơi và nhận thanh toán minh bạch.
              </p>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-3 lg:grid-cols-6 gap-6 relative z-10">
              {PROCESS_STEPS.map((step, i) => (
                <div
                  key={step.num}
                  className="group bg-white p-6 rounded-[1rem] shadow-sm border border-[#c2cab0]/50 flex flex-col gap-4 hover:-translate-y-1 hover:shadow-lg transition-all duration-300 h-full"
                >
                  <div className="w-14 h-14 bg-[#eff4ff] rounded-full flex items-center justify-center border-4 border-[#dce9ff] group-hover:bg-[#a3e635] group-hover:border-[#a3e635] transition-colors mx-auto md:mx-0">
                    <span className="text-xl">{['📝', '🏪', '🚚', '⚖️', '✅', '💳'][i]}</span>
                  </div>
                  <div className="text-center md:text-left">
                    <div className="text-xs font-black text-[#446900] mb-1 tracking-widest">{step.num}</div>
                    <h3 className="text-sm font-bold text-[#0b1c30] mb-2 uppercase">{step.title}</h3>
                    <p className="text-xs text-[#424936] font-medium leading-relaxed">{step.desc}</p>
                  </div>
                </div>
              ))}
            </div>

            {/* Highlight panel */}
            <div className="mt-16 bg-white p-8 rounded-[1.5rem] border border-[#c2cab0]/50 shadow-sm flex flex-col lg:flex-row items-center gap-8 justify-between max-w-5xl mx-auto">
              <div className="flex items-center gap-4 flex-shrink-0">
                <div className="w-12 h-12 rounded-full bg-[#a3e635]/20 flex items-center justify-center text-2xl">🛡️</div>
                <h3 className="text-xl font-black text-[#0b1c30]">Minh bạch ở mọi bước</h3>
              </div>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4 w-full">
                {['Seller được xem kết quả cân', 'Seller xác nhận trước khi thanh toán',
                  'Chủ kho là người thực hiện thanh toán', 'Toàn bộ giao dịch được lưu trên hệ thống'].map((item) => (
                  <div key={item} className="flex items-start gap-3">
                    <CheckCircle2 size={18} className="text-[#a3e635] flex-shrink-0 mt-0.5" />
                    <span className="text-sm text-[#424936] font-medium">{item}</span>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </section>

        {/* ── ƯỚC TÍNH & TẠI SAO KHÁC GIÁ ─────────────────────────── */}
        <section className="max-w-[1280px] mx-auto px-10 py-24">
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-12">
            {/* Estimate */}
            <div>
              <h2 className="text-4xl font-black text-[#0b1c30] mb-8">Ước tính giá trị phế liệu của bạn</h2>
              <div className="bg-white p-8 rounded-[1.5rem] border border-[#c2cab0]/60 shadow-md">
                <div className="space-y-4 mb-8">
                  {[
                    { type: 'Sắt vụn', weight: '10 kg', value: '120.000 đ' },
                    { type: 'Đồng cáp', weight: '2 kg', value: '400.000 đ' },
                    { type: 'Carton', weight: '15 kg', value: '60.000 đ' },
                  ].map((item, i) => (
                    <div key={i} className="grid grid-cols-3 gap-4 pb-4 border-b border-[#c2cab0]/40">
                      <div className="font-semibold text-[#0b1c30]">{item.type}</div>
                      <div className="font-semibold text-[#0b1c30]">{item.weight}</div>
                      <div className="text-right font-bold text-lg text-[#0b1c30]">{item.value}</div>
                    </div>
                  ))}
                </div>
                <div className="bg-[#dce9ff] p-6 rounded-[1rem] flex justify-between items-center mb-8 border border-[#c2cab0]/40">
                  <div>
                    <span className="block text-sm text-[#424936] font-bold uppercase tracking-wider mb-1">Tổng thu nhập dự kiến</span>
                    <span className="text-xs text-[#727a64] font-medium">Chưa bao gồm thưởng</span>
                  </div>
                  <div className="text-3xl font-black text-[#446900]">580.000 VNĐ</div>
                </div>
                <Link
                  to={user ? '/seller/request' : '/register'}
                  className="w-full bg-[#a3e635] text-[#0b1c30] font-bold px-6 py-4 rounded-full hover:bg-[#bef264] transition-colors flex justify-center items-center gap-2 text-lg shadow-sm"
                >
                  Đặt lịch thu gom ngay
                  <ArrowRight size={20} />
                </Link>
              </div>
            </div>

            {/* Why different */}
            <div>
              <h2 className="text-4xl font-black text-[#0b1c30] mb-8">Tại sao giá phế liệu thực tế có thể khác giá tham khảo?</h2>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-6">
                {WHY_DIFFERENT.map((item) => (
                  <div key={item.title} className="bg-white p-6 rounded-[1.5rem] border border-[#c2cab0]/60 shadow-sm hover:shadow-md transition-shadow">
                    <div className="w-12 h-12 bg-[#446900]/10 rounded-full flex items-center justify-center mb-4 text-2xl">
                      {item.icon === 'category' ? '📦' : item.icon === 'scale' ? '⚖️' : item.icon === 'verified' ? '✅' : '📈'}
                    </div>
                    <h4 className="font-bold text-[#0b1c30] mb-2 uppercase tracking-wide text-sm">{item.title}</h4>
                    <p className="text-sm text-[#424936] leading-relaxed font-medium">{item.desc}</p>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </section>

        {/* ── TESTIMONIALS ─────────────────────────────────────────── */}
        <section className="bg-[#dce9ff] py-24 border-t border-[#c2cab0]/30">
          <div className="max-w-[1280px] mx-auto px-10">
            <h2 className="text-4xl font-black text-[#0b1c30] mb-12 text-center">Khách hàng nói gì về RETRACK?</h2>
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-8 mb-16">
              {TESTIMONIALS.map((t) => (
                <div key={t.name} className="bg-white p-8 rounded-[1.5rem] shadow-sm hover:shadow-md transition-shadow border border-[#c2cab0]/50 flex flex-col">
                  <div className="flex items-center gap-4 mb-6">
                    <div className={`w-14 h-14 ${t.bg} rounded-full flex items-center justify-center font-black ${t.text} text-lg`}>
                      {t.initials}
                    </div>
                    <div>
                      <h4 className="font-bold text-[#0b1c30] text-lg">{t.name}</h4>
                      <p className="text-sm text-[#424936] font-medium">{t.role}</p>
                    </div>
                  </div>
                  <Stars count={t.stars} />
                  <p className="text-[#424936] text-base leading-relaxed flex-grow italic mt-4">{t.quote}</p>
                  <div className="mt-6 pt-4 border-t border-[#c2cab0]/40 flex items-center gap-2 text-sm text-[#446900] font-bold">
                    <CheckCircle2 size={16} />
                    Verified transaction
                  </div>
                </div>
              ))}
            </div>

            {/* Trust brands */}
            <div className="mt-8">
              <p className="text-center text-sm font-semibold text-[#727a64] mb-8 uppercase tracking-wider">Nền tảng công nghệ hàng đầu</p>
              <div className="flex flex-wrap justify-center gap-8 opacity-60">
                {['🌍 Google Maps', '☁️ Cloudinary', '🔐 JWT Auth', '🗄️ PostgreSQL', '♻️ EPR Certified'].map((brand) => (
                  <div key={brand} className="text-sm font-bold text-[#424936] flex items-center gap-2">
                    {brand}
                  </div>
                ))}
              </div>
            </div>
          </div>
        </section>

        {/* ── CTA FINAL ─────────────────────────────────────────────── */}
        <section className="bg-[#446900] py-20">
          <div className="max-w-[1280px] mx-auto px-10 text-center">
            <h2 className="text-4xl font-black text-white mb-4">
              Không chỉ là website bán phế liệu…
            </h2>
            <p className="text-[#a3e635] text-xl font-medium mb-10 max-w-3xl mx-auto leading-relaxed">
              RETRACK là hệ sinh thái tái chế toàn diện, nơi mọi bên đều hưởng lợi — Seller bán được giá tốt,
              Depot quản lý hiệu quả, Factory có nguồn nguyên liệu ổn định.
            </p>
            <div className="flex flex-wrap justify-center gap-4">
              <Link
                to={user ? '/seller/request' : '/register'}
                className="bg-[#a3e635] text-[#0b1c30] font-bold px-10 py-4 rounded-full hover:bg-[#bef264] transition-all text-lg shadow-lg shadow-[#a3e635]/30 flex items-center gap-2"
              >
                Bắt đầu ngay hôm nay
                <ArrowRight size={20} />
              </Link>
              <a
                href="#section-1"
                className="bg-white/10 text-white font-bold px-10 py-4 rounded-full hover:bg-white/20 transition-all text-lg border border-white/30"
              >
                Tìm hiểu thêm
              </a>
            </div>

            {/* Benefits */}
            <div className="mt-16 grid grid-cols-1 md:grid-cols-3 gap-8 text-left max-w-4xl mx-auto">
              {[
                { icon: '🚀', title: 'Dành cho Seller', items: ['Đặt lịch thu gom nhanh chóng', 'Theo dõi đơn hàng realtime', 'Lịch sử giao dịch đầy đủ'] },
                { icon: '🏪', title: 'Dành cho Depot', items: ['Quản lý nhân viên, tài xế', 'Kết nối trực tiếp nhà máy', 'Báo cáo doanh thu chi tiết'] },
                { icon: '🏭', title: 'Dành cho Factory', items: ['Marketplace lô hàng lớn', 'QC chất lượng tại chỗ', 'Hỗ trợ chứng nhận EPR'] },
              ].map((block) => (
                <div key={block.title} className="bg-white/10 rounded-2xl p-6 border border-white/20">
                  <div className="text-3xl mb-3">{block.icon}</div>
                  <h3 className="font-bold text-white mb-4">{block.title}</h3>
                  <ul className="space-y-2">
                    {block.items.map((item) => (
                      <li key={item} className="flex items-center gap-2 text-sm text-white/80 font-medium">
                        <CheckCircle2 size={14} className="text-[#a3e635] flex-shrink-0" />
                        {item}
                      </li>
                    ))}
                  </ul>
                </div>
              ))}
            </div>
          </div>
        </section>

        {/* ── FOOTER ────────────────────────────────────────────────── */}
        <footer className="bg-[#0b1c30] text-white py-16">
          <div className="max-w-[1280px] mx-auto px-10">
            <div className="grid grid-cols-1 md:grid-cols-4 gap-10 mb-12">
              {/* Brand */}
              <div>
                <div className="flex items-center gap-2 mb-4">
                  <div className="w-8 h-8 bg-[#a3e635] rounded-lg flex items-center justify-center">
                    <Recycle size={18} className="text-[#446900]" />
                  </div>
                  <span className="font-black text-xl">RETRACK</span>
                </div>
                <p className="text-white/60 text-sm leading-relaxed font-medium mb-6">
                  Nền tảng tái chế thông minh kết nối toàn bộ chuỗi cung ứng vật liệu tái chế Việt Nam.
                </p>
                <div className="flex gap-3">
                  {['📘', '🐦', '📷', '💼'].map((icon, i) => (
                    <button key={i} className="w-9 h-9 bg-white/10 rounded-lg flex items-center justify-center hover:bg-white/20 transition-colors text-sm">
                      {icon}
                    </button>
                  ))}
                </div>
              </div>

              {/* Links */}
              {Object.entries(FOOTER_LINKS).map(([title, links]) => (
                <div key={title}>
                  <h4 className="font-bold text-white mb-4 uppercase tracking-wider text-sm">{title}</h4>
                  <ul className="space-y-3">
                    {links.map((link) => (
                      <li key={link}>
                        <a href="#" className="text-white/60 hover:text-white text-sm font-medium transition-colors">
                          {link}
                        </a>
                      </li>
                    ))}
                  </ul>
                </div>
              ))}
            </div>

            <div className="border-t border-white/10 pt-8 flex flex-col md:flex-row justify-between items-center gap-4">
              <p className="text-white/40 text-sm font-medium">© 2024 RETRACK. All rights reserved. — Capstone SE-44</p>
              <div className="flex gap-6">
                {['Điều khoản', 'Bảo mật', 'Cookie'].map((item) => (
                  <a key={item} href="#" className="text-white/40 hover:text-white/80 text-sm font-medium transition-colors">
                    {item}
                  </a>
                ))}
              </div>
            </div>
          </div>
        </footer>

      </div>
    </div>
  )
}
