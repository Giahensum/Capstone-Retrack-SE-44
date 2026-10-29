import { formatCurrency } from '@/lib/utils';
import { amountInWords } from '@/utils/numberToWords';

const TZ = 'Asia/Ho_Chi_Minh';
const dateParts = (value) => {
  const d = new Date(value);
  const get = (opt) => new Intl.DateTimeFormat('vi-VN', { timeZone: TZ, ...opt }).format(d);
  return { day: get({ day: '2-digit' }), month: get({ month: '2-digit' }), year: get({ year: 'numeric' }) };
};
const dateTime = (value) => new Date(value).toLocaleString('vi-VN', { timeZone: TZ, hour12: false });
const dateOnly = (value) => new Date(value).toLocaleDateString('vi-VN', { timeZone: TZ });
const dots = (n = 28) => '.'.repeat(n);

// Gộp dữ liệu Seller/Chủ kho về cùng một dạng để lập hợp đồng.
export function toContract(agreement, lot) {
  const created = new Date(agreement.createdAt);
  const yyyymmdd = `${created.getFullYear()}${String(created.getMonth() + 1).padStart(2, '0')}${String(created.getDate()).padStart(2, '0')}`;
  return {
    id: agreement.id,
    number: `HĐĐC-${yyyymmdd}-${agreement.id.slice(0, 6).toUpperCase()}`,
    createdAt: agreement.createdAt,
    title: agreement.title,
    scopeDetails: lot.scopeDetails,
    collectionTerms: lot.collectionTerms,
    lotAddress: lot.address,
    amount: agreement.amount,
    depositPercent: agreement.depositPercent,
    depositAmount: agreement.depositAmount,
    remainingAmount: agreement.remainingAmount,
    collectionDeadline: agreement.collectionDeadline,
    sellerSignedAt: agreement.sellerSignedAt,
    buyerSignedAt: agreement.buyerSignedAt,
    parties: agreement.parties,
  };
}

function Signature({ role, name, signedAt }) {
  return (
    <div className="contract-avoid text-center">
      <p className="font-bold uppercase">{role}</p>
      <p className="text-[13px] italic">(Ký, ghi rõ họ tên)</p>
      <div className="mx-auto mt-3 flex min-h-[92px] w-full max-w-[250px] items-center justify-center">
        {signedAt ? (
          <div className="rounded border-2 border-[#1f6c3a] px-3 py-2 text-left text-[12px] leading-snug text-[#1f6c3a]">
            <p className="font-bold uppercase">Đã ký điện tử</p>
            <p>{name}</p>
            <p>Lúc {dateTime(signedAt)}</p>
            <p>Qua nền tảng RETRACK</p>
          </div>
        ) : <span className="text-[13px] text-[#9ca3af]">(chưa ký)</span>}
      </div>
      <p className="mt-2 font-semibold">{name}</p>
    </div>
  );
}

// Văn bản hợp đồng khổ A4. `bare` = không có lề (dùng cho bản in/PDF, lề do trang giấy quyết định).
export default function ContractDocument({ contract: c, bare = false }) {
  const p = c.parties;
  const date = dateParts(c.createdAt);
  return (
    <article className={`contract-sheet ${bare ? '' : 'px-[18mm] py-[16mm]'}`} aria-label="Hợp đồng đặt cọc">
      <div className="contract-avoid text-center">
        <p className="font-bold uppercase">Cộng hòa xã hội chủ nghĩa Việt Nam</p>
        <p className="font-bold">Độc lập – Tự do – Hạnh phúc</p>
        <p className="leading-none">———————</p>
      </div>

      <h1 className="contract-avoid mt-5 text-center text-[19px] font-bold uppercase leading-snug">Hợp đồng đặt cọc mua bán trọn lô tài sản thanh lý</h1>
      <p className="mt-1 text-center">Số: {c.number}</p>

      <div className="mt-4 space-y-1 italic">
        <p className="contract-avoid">– Căn cứ Bộ luật Dân sự năm 2015;</p>
        <p className="contract-avoid">– Căn cứ kết quả phiên trả giá lô tài sản “{c.title}” trên nền tảng RETRACK kết thúc ngày {dateOnly(c.createdAt)};</p>
        <p className="contract-avoid">– Căn cứ nhu cầu và khả năng của hai bên.</p>
      </div>

      <p className="mt-4">Hôm nay, ngày {date.day} tháng {date.month} năm {date.year}, hai bên gồm:</p>

      <div className="contract-avoid mt-2">
        <p className="contract-avoid"><span className="font-bold uppercase">Bên bán</span> <strong>(sau đây gọi là Bên A)</strong></p>
        <p className="contract-avoid">Ông/Bà: <strong>{p.sellerName}</strong></p>
        <p className="contract-avoid">Số CCCD/CMND hoặc Mã số thuế: {dots()}</p>
        <p className="contract-avoid">Địa chỉ thường trú/trụ sở: {dots(40)}</p>
        <p className="contract-avoid">Điện thoại: {p.sellerPhone}</p>
      </div>

      <div className="contract-avoid mt-3">
        <p className="contract-avoid"><span className="font-bold uppercase">Bên mua</span> <strong>(sau đây gọi là Bên B)</strong></p>
        <p className="contract-avoid">Đơn vị/Kho: <strong>{p.depotName}</strong></p>
        <p className="contract-avoid">Địa chỉ: {p.depotAddress}</p>
        <p className="contract-avoid">Mã số thuế: {p.depotTaxCode || dots(24)}</p>
        <p className="contract-avoid">Người đại diện: <strong>{p.buyerRepresentative}</strong></p>
        <p className="contract-avoid">Điện thoại: {p.buyerPhone}</p>
      </div>

      <p className="mt-4">Hai bên thống nhất ký kết Hợp đồng đặt cọc mua bán trọn lô tài sản thanh lý (sau đây gọi là “Hợp đồng”) với các điều khoản sau:</p>

      <section className="mt-3 text-justify">
        <h2 className="contract-avoid font-bold">Điều 1. Đối tượng của Hợp đồng</h2>
        <p className="contract-avoid">1.1. Bên A đồng ý bán và Bên B đồng ý mua trọn lô tài sản thanh lý “<strong>{c.title}</strong>” (sau đây gọi là “Lô hàng”) theo giá trúng của phiên trả giá nêu tại Điều 2.</p>
        <p className="contract-avoid">1.2. Phạm vi tài sản theo mô tả của Bên A: {c.scopeDetails}</p>
        <p className="contract-avoid">1.3. Địa điểm tài sản: {c.lotAddress}</p>
        <p className="contract-avoid">1.4. Bên B xác nhận đã tự khảo sát, hiểu rõ hiện trạng, phạm vi và điều kiện mua cả lô. Số lượng, khối lượng nêu trong mô tả chỉ mang tính ước lượng; giá trúng là giá trọn lô và không điều chỉnh theo khối lượng cân được sau khi nhận hàng.</p>

        <h2 className="contract-avoid pt-1 font-bold">Điều 2. Giá mua bán và tiền đặt cọc</h2>
        <p className="contract-avoid">2.1. Tổng giá trị Hợp đồng (giá trúng trọn lô): <strong>{formatCurrency(c.amount)}</strong> (Bằng chữ: <em>{amountInWords(c.amount)}</em>).</p>
        <p className="contract-avoid">2.2. Tiền đặt cọc: <strong>{c.depositPercent}%</strong> giá trúng, tương đương <strong>{formatCurrency(c.depositAmount)}</strong> (Bằng chữ: <em>{amountInWords(c.depositAmount)}</em>). Bên B nộp cho Bên A sau khi hai bên đã ký Hợp đồng này.</p>
        <p className="contract-avoid">2.3. Phần còn lại: <strong>{formatCurrency(c.remainingAmount)}</strong> (Bằng chữ: <em>{amountInWords(c.remainingAmount)}</em>). Bên B thanh toán sau khi Bên A xác nhận đã nhận tiền đặt cọc và trước khi Bên A bàn giao Lô hàng.</p>
        <p className="contract-avoid">2.4. Tiền đặt cọc được trừ vào giá mua khi Hợp đồng được thực hiện.</p>

        <h2 className="contract-avoid pt-1 font-bold">Điều 3. Phương thức thanh toán</h2>
        <p className="contract-avoid">3.1. Tiền đặt cọc và tiền mua hàng do Bên B chuyển trực tiếp cho Bên A (chuyển khoản hoặc phương thức khác do hai bên thỏa thuận), thực hiện ngoài nền tảng RETRACK.</p>
        <p className="contract-avoid">3.2. Mỗi lần chuyển tiền, Bên B ghi rõ nội dung/tham chiếu giao dịch và báo trên nền tảng; Bên A đối chiếu và xác nhận khi đã nhận đủ tiền.</p>

        <h2 className="contract-avoid pt-1 font-bold">Điều 4. Giao nhận và thu gom Lô hàng</h2>
        <p className="contract-avoid">4.1. Bên A bàn giao Lô hàng cho Bên B sau khi đã nhận đủ tiền theo Điều 2.</p>
        <p className="contract-avoid">4.2. Bên B thu gom hết Lô hàng chậm nhất vào ngày <strong>{dateOnly(c.collectionDeadline)}</strong>.</p>
        <p className="contract-avoid">4.3. Điều kiện thu gom và thanh toán do Bên A công bố: {c.collectionTerms}</p>
        <p className="contract-avoid">4.4. Việc bàn giao của Bên A và việc nhận đủ hàng của Bên B được mỗi bên xác nhận trên nền tảng RETRACK.</p>

        <h2 className="contract-avoid pt-1 font-bold">Điều 5. Quyền và nghĩa vụ của Bên A</h2>
        <p className="contract-avoid">5.1. Cung cấp thông tin trung thực về Lô hàng; giữ nguyên hiện trạng Lô hàng cho đến khi bàn giao.</p>
        <p className="contract-avoid">5.2. Tạo điều kiện để Bên B vào thu gom Lô hàng trong thời hạn Hợp đồng; xác nhận kịp thời việc đã nhận tiền và đã bàn giao.</p>
        <p className="contract-avoid">5.3. Nhận tiền đặt cọc và tiền mua hàng theo Điều 2.</p>

        <h2 className="contract-avoid pt-1 font-bold">Điều 6. Quyền và nghĩa vụ của Bên B</h2>
        <p className="contract-avoid">6.1. Thanh toán đúng số tiền và thời hạn theo Điều 2; thu gom Lô hàng đúng thời hạn theo Điều 4.</p>
        <p className="contract-avoid">6.2. Tự chịu trách nhiệm tuân thủ quy định pháp luật về an toàn lao động, giao thông và bảo vệ môi trường khi tháo dỡ, vận chuyển và xử lý Lô hàng.</p>
        <p className="contract-avoid">6.3. Nhận Lô hàng đúng phạm vi và hiện trạng đã khảo sát.</p>

        <h2 className="contract-avoid pt-1 font-bold">Điều 7. Xử lý khi vi phạm và tiền đặt cọc</h2>
        <p className="contract-avoid">Trừ khi hai bên có thỏa thuận khác bằng văn bản, việc xử lý tiền đặt cọc áp dụng theo Điều 328 Bộ luật Dân sự năm 2015: nếu Hợp đồng được thực hiện thì tiền đặt cọc được trả lại hoặc trừ vào nghĩa vụ thanh toán; nếu Bên B từ chối thực hiện Hợp đồng thì tiền đặt cọc thuộc về Bên A; nếu Bên A từ chối thực hiện Hợp đồng thì phải trả lại tiền đặt cọc và một khoản tiền tương đương giá trị tiền đặt cọc cho Bên B.</p>

        <h2 className="contract-avoid pt-1 font-bold">Điều 8. Vai trò của nền tảng RETRACK</h2>
        <p className="contract-avoid">Nền tảng RETRACK chỉ ghi nhận kết quả phiên trả giá và xác nhận điện tử của hai bên; không phải là một bên của Hợp đồng, không giữ hộ tiền, không bảo lãnh và không xác minh giao dịch ngân hàng của hai bên.</p>

        <h2 className="contract-avoid pt-1 font-bold">Điều 9. Giải quyết tranh chấp</h2>
        <p className="contract-avoid">Mọi tranh chấp phát sinh từ Hợp đồng được hai bên giải quyết trên tinh thần thương lượng, hòa giải. Nếu không đạt được thỏa thuận, tranh chấp được giải quyết tại Tòa án nhân dân có thẩm quyền theo quy định của pháp luật Việt Nam.</p>

        <h2 className="contract-avoid pt-1 font-bold">Điều 10. Hiệu lực và hình thức Hợp đồng</h2>
        <p className="contract-avoid">10.1. Hợp đồng có hiệu lực kể từ thời điểm cả hai bên xác nhận ký điện tử trên nền tảng RETRACK (thời điểm ghi tại phần chữ ký).</p>
        <p className="contract-avoid">10.2. Hợp đồng được lập dưới dạng thông điệp dữ liệu điện tử; bản in từ nền tảng có giá trị như bản thể hiện của Hợp đồng điện tử. Hai bên có thể ký tay bổ sung trên bản in.</p>
      </section>

      <div className="contract-avoid mt-6 grid grid-cols-2 gap-6">
        <Signature role="Đại diện Bên A" name={p.sellerName} signedAt={c.sellerSignedAt} />
        <Signature role="Đại diện Bên B" name={p.buyerRepresentative} signedAt={c.buyerSignedAt} />
      </div>

      <p className="contract-avoid mt-6 border-t border-[#d4d4d8] pt-2 text-center text-[11px] text-[#737373]">
        Hợp đồng số {c.number} • Mã thỏa thuận {c.id} • Lập từ dữ liệu trên nền tảng RETRACK
      </p>
    </article>
  );
}
