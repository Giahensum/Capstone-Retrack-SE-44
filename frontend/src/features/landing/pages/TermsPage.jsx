import { Link } from 'react-router-dom';
import { PublicShell, PublicSection } from '../components/PublicShell';

// Điều khoản sử dụng và chính sách công khai của RETRACK (bản đồ án).
// Nội dung bám quyết định nhóm: gói Depot, không thu hộ/hoa hồng, đấu giá thanh lý, hoàn tiền gói, quyền riêng tư.
// Không tự điền tên doanh nghiệp, mã số thuế hay thuế suất VAT khi nhóm chưa chốt chủ thể vận hành.
const TOC = [
  ['pham-vi', 'Phạm vi dịch vụ'],
  ['goi', 'Gói quản lý kho vựa'],
  ['tien-hang', 'Tiền hàng và thanh toán giữa các bên'],
  ['dau-gia', 'Đấu giá thanh lý của Seller'],
  ['hoan-tien', 'Hoàn tiền gói'],
  ['chung-tu', 'Chứng từ và thuế'],
  ['quyen-rieng-tu', 'Quyền riêng tư'],
  ['gioi-han', 'Giới hạn trách nhiệm'],
];

export default function TermsPage() {
  return (
    <PublicShell>
      <div className="mb-8 max-w-3xl">
        <p className="text-sm font-bold uppercase tracking-wider text-[#446900]">Điều khoản & chính sách</p>
        <h1 className="mt-2 text-3xl font-black text-[#0b1c30] sm:text-4xl">Điều khoản sử dụng RETRACK</h1>
        <p className="mt-3 text-sm text-[#424936]">
          Đây là điều khoản của sản phẩm đồ án Capstone SE-44. Chủ thể kinh doanh thực tế chưa được xác định,
          vì vậy trang này không nêu tên doanh nghiệp, mã số thuế hay thuế suất. Các nội dung đó sẽ được bổ sung trước khi vận hành thật.
        </p>
      </div>

      <div className="grid gap-6 lg:grid-cols-[220px_minmax(0,1fr)]">
        <nav aria-label="Mục lục" className="h-fit rounded-2xl border border-[#e5e7eb] bg-white p-4 text-sm lg:sticky lg:top-24">
          <p className="mb-2 font-bold text-[#0b1c30]">Mục lục</p>
          <ol className="space-y-1.5">
            {TOC.map(([id, label], i) => (
              <li key={id}><Link to={`/dieu-khoan#${id}`} className="text-[#424936] hover:text-[#446900]">{i + 1}. {label}</Link></li>
            ))}
          </ol>
        </nav>

        <div className="space-y-5">
          <PublicSection id="pham-vi" title="1. Phạm vi dịch vụ">
            <p>RETRACK là phần mềm kết nối người bán phế liệu (Seller), kho vựa (Depot) và nhà máy tái chế (Factory), kèm công cụ quản lý cho kho vựa.</p>
            <ul>
              <li>Seller, Factory, nhân viên kho và tài xế dùng miễn phí theo đúng quyền của tài khoản.</li>
              <li>Kho vựa trả phí gói quản lý theo từng kho. Mua gói không cấp quyền Admin hay quyền của role khác.</li>
              <li>Kho vựa cần được Admin duyệt hồ sơ; mua gói không thay thế việc duyệt hồ sơ.</li>
            </ul>
          </PublicSection>

          <PublicSection id="goi" title="2. Gói quản lý kho vựa">
            <ul>
              <li>Một gói gồm toàn bộ tính năng của kho vựa; chưa chia cấp gói, chưa giới hạn số nhân viên, số phiếu hay số lô.</li>
              <li>Giá niêm yết tại trang <Link to="/goi-dich-vu" className="font-bold text-[#446900] hover:underline">Gói dịch vụ</Link>, tính theo kho và theo tháng lịch. Giá mới chỉ áp dụng cho đơn mua lập sau thời điểm đổi giá.</li>
              <li>Mỗi kho được dùng thử một lần; một mã số thuế chỉ được dùng thử một lần dù đăng ký nhiều kho.</li>
              <li>Gói được kích hoạt khi tiền thực nhận đã được đối soát (Admin duyệt chứng từ hoặc cổng thanh toán xác nhận đúng đơn, đúng số tiền). Gửi ảnh chứng từ chưa đồng nghĩa với đã kích hoạt.</li>
              <li>Không tự gia hạn và không tự trừ tiền. Hết hạn thì kho không tạo được nghiệp vụ mới nhưng vẫn xem lịch sử, đối soát và hoàn tất nghĩa vụ đã phát sinh; dữ liệu không bị xóa.</li>
            </ul>
          </PublicSection>

          <PublicSection id="tien-hang" title="3. Tiền hàng và thanh toán giữa các bên">
            <ul>
              <li>Với giao dịch hàng hóa mới, RETRACK không thu hộ tiền hàng, không giữ tiền cọc và không thu hoa hồng.</li>
              <li>Seller, kho vựa và Factory tự thanh toán trực tiếp (chuyển khoản, tiền mặt hoặc hình thức khác) và ghi nhận trên RETRACK. Bên nhận tiền xác nhận đã nhận thì giao dịch mới hoàn tất.</li>
              <li>Hoàn trả tiền hàng do các bên tự thực hiện. RETRACK không dùng tiền gói để hoàn tiền hàng.</li>
              <li>Phí hoặc khoản thu hộ đã phát sinh từ giai đoạn trước vẫn được giữ nguyên để đối soát, không bị xóa.</li>
            </ul>
          </PublicSection>

          <PublicSection id="dau-gia" title="4. Đấu giá thanh lý của Seller">
            <ul>
              <li>Mỗi phiên có thể gồm nhiều hạng mục; mỗi hạng mục có số lượng, đơn vị, giá khởi điểm, bước giá và người thắng riêng.</li>
              <li>Tiền mua = đơn giá trúng × số lượng Seller đã đăng. Số lượng và đơn vị hiển thị trước khi trả giá và không được sửa sau khi đã có người trả giá.</li>
              <li>Kho vựa khảo sát trước khi trả giá và tự chịu hao hụt, tạp chất, chi phí phân loại sau khi mua. Lượng thực nhập kho chỉ dùng để tính giá vốn, không làm giảm tiền phải trả Seller.</li>
              <li>Có giá hợp lệ trong 2 phút cuối thì phiên được gia hạn thêm 2 phút, tối đa 5 lần (10 phút).</li>
              <li>Hai bên có thể tự thỏa thuận tiền cọc ngoài RETRACK. Khi người thắng bỏ mua, hệ thống lưu lý do và lịch sử để Admin xử lý; RETRACK không tự phạt tiền, không tự tịch thu cọc và không tự chuyển quyền mua cho người trả giá thứ hai.</li>
            </ul>
          </PublicSection>

          <PublicSection id="hoan-tien" title="5. Hoàn tiền gói">
            <p>Chủ kho gửi yêu cầu trong trang Gói dịch vụ của kho; Admin xem xét, duyệt hoặc từ chối kèm lý do.</p>
            <ul>
              <li><b>Chuyển trùng / chuyển thừa:</b> đối soát và hoàn phần trùng hoặc thừa, cần chứng từ hoặc mã giao dịch ngân hàng.</li>
              <li><b>Chuyển thiếu:</b> gói chưa được kích hoạt; hoàn khoản đã nhận và đóng đơn để lập đơn mới đúng giá.</li>
              <li><b>Kỳ gia hạn chưa bắt đầu:</b> được hoàn đủ giá của kỳ đó; kỳ này bị rút khỏi hạn dùng, các kỳ khác giữ nguyên.</li>
              <li><b>Kỳ đang sử dụng:</b> được dùng đến hết hạn; không hoàn tiền vì người dùng ngừng sử dụng.</li>
              <li><b>Lỗi dịch vụ đã được xác minh:</b> Admin xem xét hoàn toàn bộ hoặc một phần, tổng tiền hoàn không vượt số tiền của đơn.</li>
              <li><b>Tiền đến sau khi đơn đã đóng:</b> được đưa vào đối soát, không tự kích hoạt gói trùng.</li>
            </ul>
            <p>Mỗi yêu cầu lưu số tiền, lý do, người duyệt, chứng từ, trạng thái và lịch sử. “Đã duyệt” chưa có nghĩa là tiền đã về tài khoản: yêu cầu chỉ chuyển sang “Đã chuyển tiền hoàn” khi Admin ghi nhận mã giao dịch chuyển trả.</p>
          </PublicSection>

          <PublicSection id="chung-tu" title="6. Chứng từ và thuế">
            <ul>
              <li>Phiếu xác nhận thanh toán gói trên RETRACK là chứng từ nội bộ ghi nhận dịch vụ, <b>không phải hóa đơn điện tử hợp pháp</b>.</li>
              <li>Giá gói là giá thanh toán; RETRACK chưa công bố thuế suất áp dụng khi chủ thể vận hành chưa được xác định.</li>
              <li>Báo cáo giá vốn, lãi lỗ trên RETRACK là công cụ hỗ trợ quản lý, chưa phải sổ sách kế toán thuế. Các bên tự chịu trách nhiệm về nghĩa vụ thuế, phí và thủ tục pháp lý trong hoạt động kinh doanh của mình.</li>
            </ul>
          </PublicSection>

          <PublicSection id="quyen-rieng-tu" title="7. Quyền riêng tư">
            <ul>
              <li>Thông tin chỉ hiển thị cho người có quyền theo vai trò: Seller, kho vựa, Factory và Admin.</li>
              <li>Trước khi kho được phép khảo sát, phiên thanh lý chỉ hiện ảnh, mô tả, khu vực, số lượng, lịch và giá; không công khai số điện thoại hay địa chỉ chi tiết. Sau khi được phép khảo sát, kho nhận thông tin liên hệ cần thiết.</li>
              <li>Khi trả giá, mỗi kho thấy giá cao nhất và giá của mình; danh tính kho đang dẫn đầu được ẩn với các kho khác.</li>
              <li>Chứng từ thanh toán, số tài khoản và lịch sử giao dịch chỉ dùng để đối soát giữa các bên liên quan và Admin.</li>
              <li>Hệ thống chặn một tài khoản tự trả giá cho phiên của chính mình và lưu lịch sử để kiểm tra hành vi bất thường. RETRACK không cam kết ngăn chặn tuyệt đối việc thông đồng giữa nhiều tài khoản.</li>
            </ul>
          </PublicSection>

          <PublicSection id="gioi-han" title="8. Giới hạn trách nhiệm">
            <ul>
              <li>RETRACK ghi nhận thông tin do các bên cung cấp và không thay các bên kiểm định chất lượng, cân đo hay thanh toán tiền hàng.</li>
              <li>Tranh chấp về hàng hóa, tiền hàng hoặc tiền cọc thỏa thuận ngoài hệ thống do các bên tự giải quyết; Admin hỗ trợ dựa trên lịch sử được lưu trên RETRACK.</li>
              <li>Điều khoản có thể được cập nhật; thay đổi về giá không áp dụng ngược cho đơn đã lập.</li>
            </ul>
          </PublicSection>
        </div>
      </div>
    </PublicShell>
  );
}
