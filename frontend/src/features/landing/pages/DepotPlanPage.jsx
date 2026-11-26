import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { CheckCircle2, Info, Loader2 } from 'lucide-react';
import api from '@/lib/axios';
import { PublicShell, PublicSection } from '../components/PublicShell';

// Trang công khai giới thiệu gói quản lý Depot. Giá và số ngày dùng thử lấy từ server (Admin có thể đổi giá cho đơn mới).
const FEATURES = [
  'Quản lý kho, phiếu nhập/xuất, tồn kho và giá vốn',
  'Quản lý đồ cũ theo cái/bộ và báo cáo lãi lỗ dự kiến',
  'Nhân sự, điều phối thu gom và theo dõi đơn của Seller',
  'Tham gia đấu giá thanh lý của Seller (trả giá theo từng hạng mục)',
  'Kết nối và bán hàng cho Factory, theo dõi quyết toán',
  'Ghi nhận thanh toán với Seller/Factory và lịch sử đối soát',
];
const FREE_ROLES = [
  ['Seller (người bán)', 'Đăng bán phế liệu, đăng phiên thanh lý, xác nhận đã nhận tiền.'],
  ['Factory (nhà máy)', 'Đặt mua, nhận hàng, quyết toán với kho.'],
  ['Nhân viên kho, tài xế', 'Dùng theo quyền kho cấp, không mua gói riêng.'],
];
const money = (v) => `${Number(v).toLocaleString('vi-VN')}đ`;

export default function DepotPlanPage() {
  const plan = useQuery({
    queryKey: ['public-depot-plan'],
    staleTime: 60 * 1000,
    queryFn: async () => (await api.get('/public/depot-plan')).data.data,
  });

  return (
    <PublicShell>
      <div className="mb-8 max-w-3xl">
        <p className="text-sm font-bold uppercase tracking-wider text-[#446900]">Gói quản lý kho vựa</p>
        <h1 className="mt-2 text-3xl font-black text-[#0b1c30] sm:text-4xl">Một gói đầy đủ cho mỗi kho vựa</h1>
        <p className="mt-3 text-[#424936]">
          RETRACK chỉ thu tiền gói phần mềm quản lý của kho vựa. Tiền hàng giữa Seller, kho vựa và Factory do các bên tự thanh toán trực tiếp;
          RETRACK không thu hộ và không lấy hoa hồng trên giao dịch hàng hóa mới.
        </p>
      </div>

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.3fr)]">
        <div className="rounded-2xl border-2 border-[#446900] bg-white p-6 shadow-sm">
          <h2 className="text-lg font-extrabold text-[#0b1c30]">Gói quản lý Depot</h2>
          {plan.isLoading && (
            <p role="status" className="mt-4 flex items-center gap-2 text-sm text-[#424936]"><Loader2 className="size-4 animate-spin" /> Đang tải giá gói…</p>
          )}
          {plan.isError && (
            <div role="alert" className="mt-4 rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-700">
              Không tải được giá gói. <button type="button" className="font-bold underline" onClick={() => plan.refetch()}>Thử lại</button>
            </div>
          )}
          {plan.data && (
            <>
              <p className="mt-4">
                <span className="text-4xl font-black text-[#446900]">{money(plan.data.monthlyPrice)}</span>
                <span className="ml-1 text-sm font-semibold text-[#424936]">/ kho / tháng</span>
              </p>
              <p className="mt-1 text-xs text-[#424936]">Giá thanh toán của gói. Giá áp dụng cho đơn mua mới; đơn đã lập giữ nguyên số tiền.</p>
              <ul className="mt-5 space-y-2 text-sm text-[#1f2937]">
                <li className="flex gap-2"><CheckCircle2 className="mt-0.5 size-4 shrink-0 text-[#446900]" />Dùng thử miễn phí {plan.data.trialDays} ngày, một lần cho mỗi kho (mỗi mã số thuế chỉ được dùng thử một lần).</li>
                <li className="flex gap-2"><CheckCircle2 className="mt-0.5 size-4 shrink-0 text-[#446900]" />Tính phí theo từng kho: chủ kho có nhiều kho thì mỗi kho có hạn dùng riêng.</li>
                <li className="flex gap-2"><CheckCircle2 className="mt-0.5 size-4 shrink-0 text-[#446900]" />Mỗi lần mua là một tháng lịch, cộng nối tiếp sau hạn hiện tại. Không tự gia hạn, không tự trừ tiền.</li>
                <li className="flex gap-2"><CheckCircle2 className="mt-0.5 size-4 shrink-0 text-[#446900]" />Chưa chia cấp gói, chưa giới hạn số nhân viên, số phiếu hay số lô.</li>
              </ul>
              {!plan.data.subscriptionOnly && (
                <p className="mt-4 flex gap-2 rounded-xl border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
                  <Info className="mt-0.5 size-4 shrink-0" /> Gói quản lý hiện chưa mở bán trên hệ thống. Thông tin trên để tham khảo.
                </p>
              )}
              <Link to="/register" className="mt-6 inline-flex w-full justify-center rounded-full bg-[#a3e635] px-5 py-3 text-sm font-bold text-[#0b1c30] hover:bg-[#bef264]">
                Đăng ký tài khoản chủ kho
              </Link>
              <p className="mt-2 text-center text-xs text-[#424936]">Kho cần được Admin duyệt hồ sơ; mua gói không thay việc duyệt hồ sơ.</p>
            </>
          )}
        </div>

        <div className="space-y-6">
          <PublicSection id="bao-gom" title="Gói bao gồm toàn bộ tính năng của kho vựa">
            <ul>{FEATURES.map((f) => <li key={f}>{f}</li>)}</ul>
            <p>Mua gói không cấp quyền Admin hay quyền của role khác; mỗi tài khoản vẫn dùng đúng quyền của mình.</p>
          </PublicSection>
          <PublicSection id="mien-phi" title="Miễn phí cho các bên còn lại">
            <ul>{FREE_ROLES.map(([r, d]) => <li key={r}><b className="text-[#1f2937]">{r}:</b> {d}</li>)}</ul>
          </PublicSection>
        </div>
      </div>

      <div className="mt-6 grid gap-6 lg:grid-cols-2">
        <PublicSection id="het-han" title="Khi gói hết hạn">
          <ul>
            <li>Kho không tạo được nghiệp vụ mới như nhận đơn thu gom mới, tạo phiếu nhập/xuất mới.</li>
            <li>Vẫn xem được lịch sử, đối soát và hoàn tất các đơn đã nhận, lô đã mua trước đó.</li>
            <li>Dữ liệu không bị xóa; gia hạn xong dùng tiếp như cũ.</li>
          </ul>
        </PublicSection>
        <PublicSection id="hoan-tien" title="Hoàn tiền gói">
          <p>Yêu cầu hoàn tiền gửi trong trang Gói dịch vụ của kho, Admin xem xét và duyệt:</p>
          <ul>
            <li>Chuyển trùng/thừa: hoàn phần trùng hoặc thừa sau đối soát.</li>
            <li>Kỳ gia hạn chưa bắt đầu: được hoàn kỳ chưa dùng. Kỳ đang sử dụng được dùng đến hết hạn, không hoàn vì ngừng sử dụng.</li>
            <li>Lỗi dịch vụ đã xác minh: Admin xem xét hoàn toàn bộ hoặc một phần.</li>
          </ul>
          <p><Link to="/dieu-khoan#hoan-tien" className="font-bold text-[#446900] hover:underline">Xem đầy đủ chính sách hoàn tiền</Link></p>
        </PublicSection>
      </div>

      <p className="mt-6 rounded-xl border border-[#e5e7eb] bg-white p-4 text-xs leading-5 text-[#424936]">
        Phiếu xác nhận thanh toán gói trên RETRACK là chứng từ nội bộ ghi nhận dịch vụ, <b>không phải hóa đơn điện tử hợp pháp</b>.
        Giá trên là giá thanh toán của gói; RETRACK không cam kết thay các bên thực hiện nghĩa vụ thuế, phí hay thủ tục pháp lý liên quan tới hoạt động kinh doanh của mình.
        Xem <Link to="/dieu-khoan" className="font-bold text-[#446900] hover:underline">Điều khoản sử dụng</Link>.
      </p>
    </PublicShell>
  );
}
