# Thiết kế luồng thu – chi qua sàn RETRACK (bản nháp để nhóm duyệt)

> **Lịch sử — thay đổi mô hình 25/11/2026:** thiết kế thu hộ/hoa hồng bên dưới không còn áp dụng cho giao dịch mới khi `BUSINESS_MODEL=DEPOT_SUBSCRIPTION`. Chỉ thu gói Depot; giữ xử lý khoản tiền đã tồn tại. Đọc [mô hình hiện hành và contract tích hợp](goi-quan-ly-depot.md). Không triển khai lại thu hộ mới dựa vào bản nháp này.

> Trạng thái: **nháp**, chưa có code. Phạm vi: demo cho giảng viên, không xét giấy phép trung gian thanh toán.
> Tham khảo: `D:\Fullstack\eco_fashion\eco_backend` (FITLY: NestJS + Prisma) và tài liệu payOS (`https://payos.vn/docs/api/`).

## 1. Mục tiêu

Mọi bên giao dịch đều **trả tiền vào sàn** qua PayOS. Sàn giữ tiền trong thời gian giữ lại (escrow), trừ **hoa hồng**, rồi **chi lại** cho người nhận bằng API chi của payOS. Sàn có lợi nhuận bằng hoa hồng, như sàn thương mại điện tử.

Ba dòng tiền đi qua sàn:

| # | Dòng tiền | Người trả (thu vào sàn) | Người nhận (chi từ sàn) |
|---|---|---|---|
| A | Thu mua phế liệu | Chủ kho | Seller |
| B | Bán lô cho nhà máy (kèm VAT) | Factory | Chủ kho |
| C | Mua lô thanh lý (cọc và phần còn lại) | Chủ kho mua | Seller |

Quyết định đã chốt với chủ đề tài:
- Cả ba dòng đều đi qua sàn.
- Hoa hồng cấu hình **theo từng người nhận**, có **mặc định chung** (theo từng dòng tiền).
- Chi tiền: **nhà cung cấp mô phỏng là mặc định**; payOS thật chỉ dùng để **chuyển thử số tiền nhỏ** khi có kênh chi.

## 2. Nguyên tắc bắt buộc

1. **Một khoản thu, một dòng sổ cái.** Mỗi nguồn (đơn thu mua, lô, thỏa thuận thanh lý) chỉ có một khoản thu còn hiệu lực. Ràng buộc duy nhất trong DB.
2. **Mỗi dòng phải trả chỉ được chi đúng một lần.** Dòng sổ cái gắn vào tối đa một lệnh chi bằng ràng buộc duy nhất; chạy lại không bao giờ chi trùng.
3. **Hoa hồng được chụp lại (snapshot) trên từng dòng** lúc phát sinh. Đổi tỷ lệ về sau không đổi đơn cũ.
4. **Không gọi payOS trong transaction DB.** Ghi trạng thái `PROCESSING` trước, gọi payOS sau, rồi ghi kết quả.
5. **Lỗi mơ hồ (timeout, 5xx) không bao giờ gửi lại bằng khóa mới.** Dùng lại cùng khóa idempotency hoặc đối soát trước.
6. **Mọi thao tác tiền ghi audit** (ai, lúc nào, giá trị cũ và mới).
7. Khóa dòng PostgreSQL theo thứ tự cố định (người dùng, kho, nguồn, dòng sổ cái, kỳ chi) để không deadlock; giữ nguyên giao thức khóa kho hiện có.

## 3. Mô hình dữ liệu đề xuất

Khối SQL mới `RETRACK_MONEY_V1` trong `db/bootstrap/retrack-system.sql` (chạy lại nhiều lần an toàn). Dùng DECIMAL(18,0) cho VND.

### 3.1 `money_collections` – khoản thu vào sàn
| Cột | Ý nghĩa |
|---|---|
| `id`, `flow` | `PICKUP`, `BATCH`, `LIQUIDATION_DEPOSIT`, `LIQUIDATION_BALANCE` |
| `source_type`, `source_id` | nguồn (`PICKUP_REQUEST`, `INVENTORY_BATCH`, `LIQUIDATION_AGREEMENT`) |
| `payer_id`, `payee_id` | người trả và người nhận (user) |
| `amount` | số tiền người trả phải nộp |
| `status` | `PENDING`, `PAID`, `EXPIRED`, `CANCELLED`, `REFUNDING`, `REFUNDED` |
| `payos_order_code` (duy nhất), `payos_checkout_url`, `payos_link_id` | thông tin link PayOS |
| `paid_at`, `received_amount` | thời điểm và số tiền thực nhận |
- Duy nhất: một khoản thu `PENDING` hoặc `PAID` cho mỗi `(flow, source_id)`.

### 3.2 `payable_lines` – sổ cái phải chi cho người nhận
| Cột | Ý nghĩa |
|---|---|
| `id`, `collection_id`, `payee_id`, `flow`, `source_id` | gắn với khoản thu |
| `kind` | `SALE` hoặc `REFUND` (hoàn tiền là dòng âm) |
| `gross` | tiền người nhận được ghi nhận |
| `commission_rate` | tỷ lệ đã chụp lại (%) |
| `commission` | hoa hồng đã tính, làm tròn nửa lên tới 1 đồng |
| `state` | `HELD` (đang giữ), `AVAILABLE` (được chi), `IN_PAYOUT`, `PAID`, `REVERSED` |
| `available_at` | thời điểm hết giữ (kích hoạt + thời gian giữ) |
| `payout_id` | kỳ chi đang chứa dòng này (null nếu chưa chi) |
- **Duy nhất `(source_type, source_id, kind)`**: một nguồn chỉ có một dòng bán trong toàn hệ thống.

### 3.3 `payout_runs` – kỳ chi trả cho một người nhận
Giống `ShopPayout` của FITLY, đổi sang người nhận:
`code`, `payee_id`, `period_start`, `period_end`, `gross`, `commission`, `refund_amount`, `carried_debt`, `net`, `status`, `provider` (`SIMULATED` hoặc `PAYOS`), `provider_ref`, `gateway_status`, `fail_reason`, `bank_snapshot` (JSON), `approved_by`, `approved_at`, `created_by` (NULL nếu do hệ thống tự chạy), `paid_at`, `version`.
- Trạng thái: `PENDING`, `AWAITING_APPROVAL`, `PROCESSING`, `PAID`, `FAILED`, `UNKNOWN`, `HELD`, `DEFERRED`, `CARRIED`, `SETTLED`.
- Duy nhất `(payee_id, period_start, period_end)`, duy nhất `code`, duy nhất `provider_ref` khi không rỗng.
- Bảng nối: `payable_lines.payout_id` (mỗi dòng thuộc tối đa một kỳ).

### 3.4 Cấu hình
- Dùng `system_configs` hiện có (hoặc bảng mới nếu thiếu), các khóa:
  - `money.mode`: `PLATFORM` hoặc `LEGACY` (cờ chuyển luồng, mặc định `LEGACY` cho tới khi nhóm bật).
  - `commission.default.PICKUP`, `commission.default.BATCH`, `commission.default.LIQUIDATION` (%).
  - `payout.auto_enabled`, `payout.schedule` (`DAILY` hoặc `WEEKLY`), `payout.hold_minutes` (cho demo có thể đặt vài phút), `payout.min_amount`, `payout.auto_max_amount` (trên mức này cần Admin duyệt), `payout.provider` (`SIMULATED` hoặc `PAYOS`), `payout.test_enabled`, `payout.test_max_amount`.
- `payee_commissions(user_id, flow, rate, updated_by)`: tỷ lệ riêng theo người nhận, ghi đè mặc định.
- `payee_payout_settings(user_id, auto_enabled)`: công tắc tự chi từng người.
- Cột mới trên `users`: `bank_bin` (mã BIN 6 số), `bank_status` (`PENDING_VERIFICATION`, `VERIFIED`), `bank_verified_at`. Tên ngân hàng, số tài khoản và tên chủ tài khoản đã có.

## 4. Hoa hồng

- Tỷ lệ lấy theo thứ tự: `payee_commissions` của người nhận và dòng tiền, nếu không có thì mặc định chung của dòng tiền. Ghi vào `payable_lines.commission_rate` ngay khi tạo dòng.
- **Cơ sở tính:**
  - A (thu mua): tiền hàng người nhận được, không gồm phí vận chuyển nếu có.
  - B (bán lô): tiền hàng. **VAT không tính hoa hồng** nhưng vẫn chi cho chủ kho (chủ kho nộp thuế); cần nhóm chốt cách hiển thị.
  - C (thanh lý): giá trúng cả lô (cọc cộng phần còn lại).
- **Công thức:** `commission = làm tròn nửa lên(gross × tỷ lệ ÷ 100)` trên từng dòng, rồi cộng các dòng.
- **Số chi:** `net = tổng gross − tổng hoa hồng − tổng hoàn tiền − nợ mang sang` (đúng yêu cầu "chi = thực tế − phí % hoa hồng").
- **Hoàn tiền một phần:** chia theo tỷ lệ tích lũy để tổng hoa hồng hoàn lại khớp chính xác, không lệch làm tròn (như FITLY).
- **Thay phí nền tảng hiện tại:** cơ chế `BUYER_SEPARATE_V1` và hóa đơn phí hằng tháng chỉ áp dụng khi `money.mode = LEGACY`. Ở chế độ `PLATFORM`, hoa hồng khấu trừ thẳng vào tiền chi; đơn cũ giữ nguyên mức phí đã chốt.

## 5. Dòng thu: người mua trả vào sàn

Dùng lại hạ tầng payOS thu hiện có (`PayOsClient`, webhook `/api/payos/webhook`, worker đối soát), mở rộng để tra cứu theo `money_collections.payos_order_code` thay vì chỉ `platform_invoices`.

| Dòng | Điểm tạo khoản thu | Điểm kích hoạt "được chi" (bắt đầu tính giữ) |
|---|---|---|
| A thu mua | Chủ kho ở trạng thái `AWAITING_PAYMENT` bấm thanh toán PayOS; thu đủ thì đơn sang bước chờ Seller xác nhận | Đơn sang `DONE` (Seller xác nhận, hàng đã nhập kho) |
| B bán lô | Sau khi chủ kho chấp nhận giá quyết toán, Factory thanh toán PayOS | Factory nghiệm thu QC đạt và quyết toán hoàn tất |
| C thanh lý | Chủ kho trả cọc rồi phần còn lại qua PayOS (hai khoản thu của cùng thỏa thuận) | Chủ kho xác nhận thu gom đủ (`OwnerCollectedAt`) |

- `available_at = thời điểm kích hoạt + payout.hold_minutes`. Đơn bị từ chối QC, hủy hoặc đang tranh chấp thì dòng ở `HELD` và không chi.
- **Xử lý bất thường** (theo FITLY): trả thiếu thì ghi `mismatch` và không coi là đã trả; trả dư thì tạo khoản hoàn phần dư; trả trùng khi đã thanh toán thì hoàn phần trùng; tiền về muộn sau khi hết hạn thì hoàn lại.
- **Hoàn tiền cho người mua** (ví dụ lô trả hàng): dùng cùng API chi của payOS tới tài khoản ngân hàng của người mua, hoặc đánh dấu `REFUNDING` để Admin xử lý tay trong bản demo.
- Giữ nguyên các kiểm tra trạng thái hiện có (tồn kho chỉ nhận đơn `DONE`, giao thức khóa dòng depot).

## 6. Dòng chi: sàn trả cho người nhận

### 6.1 Nhà cung cấp chi (`IPayoutProvider`)
- Giao diện: `PayoutAsync(...)`, `GetPayoutAsync(...)`, `GetBalanceAsync()`, `IsConfigured`.
- **`SimulatedPayoutProvider`** (mặc định): luôn thành công sau một độ trễ cấu hình được, có chế độ giả lỗi để test; số dư giả lập.
- **`PayOsPayoutProvider`**:
  - `POST /v1/payouts` (headers: `x-client-id`, `x-api-key`, `x-idempotency-key`, `x-signature`), body `referenceId`, `amount` (số nguyên), `description` (**tối đa 20 ký tự**), `toBin`, `toAccountNumber`.
  - Kiểm tra số dư qua `GET /v1/payouts-account/balance` trước khi gửi.
  - Chữ ký lệnh chi: **chưa được kiểm chứng** (xem mục 9). Thử theo FITLY: sắp các khóa theo alphabet, mỗi cặp `encodeURIComponent(khóa)=encodeURIComponent(giá trị)` nối bằng `&`, ký HMAC-SHA256 bằng checksum key của **kênh chi**.
  - Trạng thái lệnh chi chỉ biết bằng polling (`GET /v1/payouts/{id}` hoặc tìm theo `referenceId`): `PROCESSING` rồi `SUCCEEDED`.
- **Chuyển thử số nhỏ:** endpoint riêng cho Admin, chỉ chạy khi `payout.test_enabled`, số tiền trong khoảng (ví dụ 1.000 đến `payout.test_max_amount`), số dư đủ, và không tạo kỳ chi.

### 6.2 Thuật toán một lần chạy
Đầu vào: `payeeIds[]` (một hoặc nhiều), `periodStart`, `periodEnd`, `mode` (`PREVIEW` hoặc `EXECUTE`), `autoOnly`.
1. Kiểm tra `periodStart < periodEnd` và `periodEnd` không ở tương lai.
2. Với từng người nhận, kiểm tra khoảng ngày không chồng lên kỳ đã có (chạy lại đúng khoảng cũ là hợp lệ và idempotent).
3. Lấy các `payable_lines` ở `AVAILABLE`, chưa có `payout_id`, có `available_at <= periodEnd`, và `created/kích hoạt` trong khoảng. Loại dòng đã thuộc kỳ khác.
4. Tính `gross`, `commission`, `refund`, `carried_debt`, `net`.
5. **`PREVIEW`**: trả bảng kết quả (từng người nhận, từng dòng, tổng) và không ghi gì.
6. **`EXECUTE`**: mỗi người nhận một transaction riêng (lỗi một người không chặn người khác): khóa kỳ gần nhất của người nhận, tạo `payout_runs`, đánh `payout_id` lên các dòng, ghi audit.
7. Quyết định trạng thái theo thứ tự: `net < 0` thì `CARRIED`; `net = 0` thì `SETTLED`; `net < min_amount` thì `DEFERRED`; tài khoản ngân hàng chưa xác minh hoặc mới đổi thì `HELD`; `net > auto_max_amount` thì `AWAITING_APPROVAL`; còn lại `PROCESSING`.
8. Sau khi commit, với kỳ `PROCESSING`: kiểm tra số dư, gọi provider (ngoài transaction), ghi kết quả: thành công nhận thì `PROCESSING` kèm `provider_ref`; từ chối rõ ràng thì `FAILED`; timeout hoặc 5xx thì `UNKNOWN`.

### 6.3 Hai cách chạy theo yêu cầu
- **Tự động (Admin cấu hình):** worker nền chạy theo `payout.schedule` (hằng ngày hoặc hằng tuần) khi `payout.auto_enabled`. Kỳ = từ mốc `period_end` gần nhất tới đầu ngày hiện tại (giờ Việt Nam). Bỏ qua người nhận có `auto_enabled = false`; các dòng của họ không mất, chờ lần chạy thủ công.
- **Thủ công:** Admin chọn **một hoặc nhiều người nhận**, ngày bắt đầu và ngày kết thúc, bấm **Xem trước** (xem từng người nhận được bao nhiêu sau hoa hồng), rồi **Chạy đối soát**. Chạy thủ công bỏ qua công tắc tự động từng người.

### 6.4 Sau khi chi
- Worker nền mỗi vài phút: hỏi provider các kỳ `PROCESSING` hoặc `UNKNOWN` (cũ hơn 1 phút), cập nhật `PAID` hoặc `FAILED`. Provider không biết mã tham chiếu quá 10 phút thì coi là chưa nhận và cho phép gửi lại **cùng khóa**.
- Admin có các thao tác: duyệt kỳ `AWAITING_APPROVAL`, thử lại kỳ `FAILED`, đối soát một kỳ, đánh dấu đã chi tay (bắt buộc xác nhận khi kỳ ở `PROCESSING` hoặc `UNKNOWN`).
- Thông báo cho người nhận khi được chi hoặc khi bị giữ.

## 7. API đề xuất

**Admin** (`/api/admin/money/...`, vai trò ADMIN):
- `GET /payouts`, `GET /payouts/{id}` (kèm các dòng)
- `POST /payouts/preview`, `POST /payouts/run` (một hoặc nhiều người nhận)
- `POST /payouts/{id}/approve | retry | reconcile | mark-paid | release`
- `GET /settings`, `PUT /settings` (cấu hình hoa hồng mặc định, tự động, thời gian giữ, hạn mức)
- `GET /commissions`, `PUT /commissions/{userId}` (hoa hồng riêng)
- `GET /balance` (số dư tài khoản chi), `POST /test-transfer`
- `GET /collections` (khoản thu), `GET /ledger` (sổ cái phải chi)

**Người trả:** `POST /api/money/collections/{source}/payos-link`, `POST .../payos-sync`.
**Người nhận (Seller, Chủ kho, Factory):** `GET /api/money/my/earnings` (số dư đang giữ, sắp được chi, đã chi), `GET /api/money/my/payouts`.

## 8. Giao diện

- **Admin:** trang **Thu chi** gồm: cấu hình (hoa hồng, tự động, giữ tiền, hạn mức), **Chạy đối soát** (chọn người nhận, ngày, xem trước, chạy), danh sách kỳ chi và chi tiết từng dòng, số dư tài khoản chi, công cụ chuyển thử.
- **Người nhận:** mục **Thu nhập / Ví** với số dư đang giữ, số sắp được chi, lịch sử chi, thông tin tài khoản ngân hàng và trạng thái xác minh.
- **Người trả:** các nút **Thanh toán PayOS** thay cho "Xác nhận chuyển khoản" ở từng dòng tiền (khi `money.mode = PLATFORM`).

## 9. Rủi ro và điểm chưa chắc

1. **Chữ ký lệnh chi payOS chưa được kiểm chứng.** Tài liệu chỉ nêu "chữ ký xác thực request"; FITLY chỉ suy ra từ trang kiểm tra webhook. Phải thử bằng một lần chuyển nhỏ trước khi bật tự động.
2. **Tài liệu payOS không nói rõ cách nạp tiền vào tài khoản chi, hạn mức mỗi lần và mỗi ngày, và phí chi.** Cần hỏi payOS (support@payos.vn, 1900 8144), hoặc xác minh thực tế khi tạo kênh chi.
3. **Không có webhook cho lệnh chi** trong tài liệu; chỉ polling.
4. **Điều khoản payOS** có mục cấm khai thác nền tảng thay mặt bên thứ ba khi chưa được chấp thuận; với demo dùng mô phỏng làm mặc định thì không bị ảnh hưởng.
5. **Giới hạn mô tả lệnh chi 20 ký tự**, FITLY từng bị từ chối vì mô tả dài hơn.
6. **Chuyển đổi luồng cũ sang mới** là thay đổi lớn: các test và dữ liệu hiện có dùng luồng chuyển khoản thủ công. Dùng cờ `money.mode` để hai chế độ cùng tồn tại; chỉ bật `PLATFORM` khi nhóm đã kiểm thử.
7. **Tiền cọc thanh lý khi hủy hoặc từ chối thực hiện:** quy tắc mất cọc theo Điều 7 của hợp đồng cần quy định xử lý (cho phép Admin điều chỉnh sổ cái thủ công).
8. **Tài khoản ngân hàng người nhận:** cần mã BIN (payOS bắt buộc `toBin`); phải thêm cột và giao diện nhập, kèm xác minh của Admin như FITLY.
9. **VAT khi bán lô:** chưa chốt VAT có được chi trọn cho chủ kho hay không.

## 10. Kế hoạch triển khai theo giai đoạn

| GĐ | Nội dung | Kết quả kiểm chứng |
|---|---|---|
| 0 | Hỏi payOS về nạp tiền chi, hạn mức, phí, chữ ký; quyết định VAT | Tài liệu trả lời |
| 1 | Khối SQL `RETRACK_MONEY_V1`, model, hoa hồng, sổ cái, `IPayoutProvider` mô phỏng | Test hoa hồng làm tròn, ràng buộc duy nhất, không trùng dòng |
| 2 | Dòng thu A, B, C qua PayOS vào sàn, tạo dòng sổ cái và giữ tiền | Test webhook thiếu, dư, trùng, muộn; test trạng thái theo từng dòng |
| 3 | Engine chi: xem trước, chạy thủ công nhiều người, tự động theo lịch, đối soát | Test không chi trùng, chạy lại, chồng khoảng, lỗi mơ hồ |
| 4 | Giao diện Admin Thu chi và trang thu nhập của người nhận | Kiểm thử trình duyệt trên môi trường test |
| 5 | `PayOsPayoutProvider`, chuyển thử nhỏ, cập nhật Report 3, 4, 5, 6 và Excel test | Một lệnh chi thật số nhỏ, hoặc ghi rõ chưa chạy |

Mỗi giai đoạn tách nhánh riêng từ `develop`, có PR và khối SQL review trước khi chạy trên DB dùng chung.

## 11. Cần nhóm chốt

1. VAT của dòng B có chi trọn cho chủ kho không?
2. Giá trị mặc định hoa hồng cho A, B, C là bao nhiêu (ví dụ 3%, 2%, 5%)?
3. Thời gian giữ mặc định của từng dòng (ví dụ 24 giờ; demo đặt 1 đến 5 phút)?
4. Người trả có được hủy khoản thu đã thanh toán không, và hoàn tiền xử lý tự động hay Admin duyệt tay?
5. Ai được xác minh tài khoản ngân hàng của người nhận (Admin duyệt hay tự xác minh theo khớp tên)?
