# Chữ ký request kênh chi payOS (x-signature) - CHƯA XÁC MINH

Tài liệu payOS về API chi (`POST /v1/payouts`) yêu cầu header `x-signature` nhưng **không nêu chuỗi cần ký**.
Cách đang cài đặt là phỏng đoán tốt nhất (dựa trên một dự án khác, bản thân dự án đó cũng chưa được xác minh).
**Chưa từng được kiểm chứng với payOS thật.**

## Thuật toán mặc định (`SignatureMode = Canonical`)

1. Lấy các khóa của thân request, sắp xếp A-Z (so sánh ordinal) ở mọi cấp.
2. Mỗi cặp: `encodeURIComponent(key)=encodeURIComponent(value)`, nối bằng `&`.
   - `null`/thiếu giá trị thành chuỗi rỗng.
   - object/array lồng nhau thành JSON gọn, khóa sắp sâu, giữ thứ tự mảng.
   - `encodeURIComponent` đúng kiểu JavaScript: giữ `A-Z a-z 0-9 - _ . ! ~ * ' ( )`, còn lại mã hóa UTF-8 `%XX` chữ hoa (khác `Uri.EscapeDataString`).
3. `x-signature = HMAC-SHA256(chuỗi trên, PayOSPayout__ChecksumKey)`, hex chữ thường.

Ví dụ: `{b:'x y', a:1, c:null, d:undefined}` -> `a=1&b=x%20y&c=&d=`.

Chế độ dự phòng `RawJson`: HMAC trên đúng chuỗi JSON thân request gửi đi.

## Cách xác minh bằng một lệnh chuyển thật nhỏ

1. Điền `PayOSPayout__ClientId/ApiKey/ChecksumKey` (khóa kênh chi, không phải kênh thu), nạp số dư nhỏ.
2. Gọi `CreatePayoutAsync` một lần với số tiền nhỏ (ví dụ 2.000 đ) tới tài khoản của chính bạn, `idempotencyKey` và `referenceId` mới.
3. Đọc kết quả: `Accepted` (code `00`) là chữ ký đúng. Sau đó dùng `GetPayoutAsync`/`FindByReferenceAsync` để xem trạng thái thật.
4. Nếu payOS trả 401 hoặc lỗi chữ ký (kết quả `DefiniteFailure`, `Code`/`Desc` nêu lỗi):
   - Thử đặt `PayOSPayout__SignatureMode=RawJson` rồi khởi động lại (không cần deploy code).
   - Nếu vẫn lỗi, chỉ sửa **một hàm**: `PayOsPayoutSigner.BuildSigningString` trong `PayOsPayoutClient.cs`, rồi cập nhật vector kiểm thử trong `PayOsPayoutClientTests.cs`.

Lưu ý: `Accepted` chỉ nghĩa là payOS đã nhận lệnh. Tiền đã đi hay chưa phải dựa vào tra cứu (PROCESSING -> SUCCEEDED).
Không có webhook cho lệnh chi trong tài liệu payOS, nên cần polling. Kết quả `Unknown` (5xx/timeout/lỗi mạng) tuyệt đối không gửi lại bằng idempotency-key mới; hãy tra cứu theo `referenceId`.
