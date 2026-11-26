# Bàn giao cho TV2 — Đấu giá thanh lý nhiều hạng mục (V2) → nhập kho

Người bàn giao: TV5. Người nhận: Ngô Sỹ Giá (TV2, Chủ kho vựa).
Code đã vào `develop` qua PR #80 (`feature/TV5-auction-items`, merge `1673875`, commit chính `bc241e1`).

## 1. Tóm tắt nghiệp vụ đã chốt

- Một phiên có nhiều hạng mục. Mỗi hạng mục có số lượng, đơn vị (`kg`, `cai`, `bo`), giá khởi điểm, bước giá và **người thắng riêng**. Một kho có thể thắng sắt nhưng thua đồng.
- **Tiền mua = đơn giá trúng × số lượng Seller đã đăng.** Không có bước Seller nhập lại lượng cân bàn giao, không tính lại tiền theo lượng thực nhận.
- Kho tự chịu hao hụt, tạp chất và chi phí phân loại. Lượng thực nhập sau phân loại **chỉ dùng tính giá vốn**, không làm giảm tiền phải trả Seller.
- Không cọc qua RETRACK, không hoa hồng/thu hộ. Hai bên tự thanh toán, hệ thống chỉ ghi nhận các mốc xác nhận.
- Gia hạn: có giá hợp lệ trong 2 phút cuối thì cộng 2 phút, tối đa 5 lần (10 phút), dùng chung cho mọi hạng mục của phiên.
- Bỏ mua: lưu lý do, thỏa thuận tạm dừng, Admin chọn `CONTINUE` hoặc `CANCELLED`. Không phạt, không chuyển cho người giá thứ hai.
- Phiên V1 (trọn lô, `format_version = 1`) chạy tiếp theo luồng cũ; mọi truy vấn V1 đã lọc `FormatVersion == 1`.

## 2. Endpoint

Chủ kho — `api/depot/liquidations/v2`, role `DEPOT_OWNER`, mọi request kèm `?depotId=`:

| Method | Đường dẫn | Mục đích |
|---|---|---|
| GET | `/` | Danh sách phiên (phân trang) |
| GET | `/{id}` | Chi tiết phiên + hạng mục (ẩn danh tính kho dẫn đầu) |
| POST | `/{id}/participation` | Đăng ký khảo sát, chọn hạng mục |
| PATCH | `/{id}/survey-confirmation` | Xác nhận đã tự khảo sát/hiểu hiện trạng (bắt buộc trước khi trả giá) |
| POST | `/{id}/items/{itemId}/bids` | Trả đơn giá cho một hạng mục (`operationId` chống gửi lặp) |
| GET | `/{id}/items/{itemId}/bids` | Lịch sử giá của chính kho |
| GET | `/agreements/{id}` | Thỏa thuận của kho thắng (stage, nextAction, receiptId) |
| PATCH | `/agreements/{id}/report-payment` | Báo đã thanh toán trực tiếp (`reference`) |
| PATCH | `/agreements/{id}/confirm-received` | Xác nhận đã nhận đủ hàng |
| POST | `/agreements/{id}/abandon-report` | Báo bỏ mua |
| GET | `/agreements/{id}/intake-source` | **Contract nhập kho cho TV2** (mục 3) |

Seller — `api/seller/liquidations/v2`: `POST /`, `GET /`, `GET /{id}`, `GET /{id}/items/{itemId}/bids`, `POST /{id}/cancel`, `PATCH /agreements/{id}/confirm-payment`, `PATCH /agreements/{id}/confirm-handover`, `POST /agreements/{id}/abandon-report`.

Admin — `api/admin/liquidations`: `GET /disputes`, `POST /disputes/{id}/resolve` (`resolution` = `CONTINUE` | `CANCELLED`, `note` bắt buộc).

Thông báo: Seller nhận `targetType = LIQUIDATION_V2` → `/seller/liquidations/v2/{lotId}`; Chủ kho nhận `LIQUIDATION_V2` → `/depot/liquidations?kind=v2&campaignId={lotId}`. `category` vẫn là `LIQUIDATION` để bộ lọc cũ hoạt động.

## 3. Contract `intake-source`

`GET /api/depot/liquidations/v2/agreements/{agreementId}/intake-source?depotId=...` → `ApiResponse<LiquidationIntakeSourceDto>`:

```json
{
  "formatVersion": 2,
  "agreementId": "…", "lotId": "…", "depotId": "…",
  "revision": "638…​.638…​.0.0.0",
  "currency": "VND",
  "pricingBasis": "POSTED_QUANTITY",
  "paymentConfirmed": true, "sellerHandedOver": true, "ownerReceived": false,
  "cancelled": false,
  "readyForIntake": true,
  "finalPurchaseAmount": 300000,
  "receiptId": null,
  "items": [
    { "agreementItemId": "…", "auctionItemId": "…", "kind": "MATERIAL", "materialCode": "IRON",
      "name": "Sắt vụn", "unit": "kg", "postedQuantity": 100, "winningUnitPrice": 3000, "finalAmount": 300000 }
  ]
}
```

- `readyForIntake = true` khi: không bị hủy, không có báo bỏ mua đang chờ, Seller đã xác nhận nhận đủ tiền, Seller đã xác nhận bàn giao, chưa có phiếu nhập hoạt động (`VOIDED`/`REJECTED` không tính).
- `ownerReceived = false` vẫn có thể sẵn sàng: kho xác nhận nhận hàng và ghi kho trong một bước (`confirmBulkReceived = true`).
- `revision` đổi khi có mốc thanh toán/bàn giao/nhận/bỏ mua/xử lý. Dùng để phát hiện dữ liệu cũ, không phải khóa chống race.
- Chỉ kho thắng (đúng `depotId` + chủ kho là `BuyerId`) đọc được; sai kho → 403, không tồn tại → 404.

### Ánh xạ sang phiếu nhập kho của TV2

| Intake | Phiếu nhập (`CreateStockReceiptDto`) |
|---|---|
| `agreementId` | `bulkAgreementId` |
| — | `sourceType = "BULK_PURCHASE"` |
| `finalPurchaseAmount` | `purchaseCost` (server tự lấy `agreement.Amount`; gửi `0` hoặc đúng số này) |
| `kind = MATERIAL` | dòng `kind = "MATERIAL"`, `materialType = materialCode` |
| `kind = USED_GOODS` | dòng `kind = "REUSABLE"` |
| `unit`: `kg` / `cai` / `bo` | `kg` / `cái` / `bộ` (`LiquidationV2Rules.UnitLabel`) |
| `postedQuantity` | **Chỉ để tham khảo.** Số lượng dòng = lượng thực tế sau phân loại do kho nhập |

`agreement.Amount` của V2 đã là tổng các hạng mục kho thắng, nên `DepotStockService.CreateAsync` hiện tại dùng được cho V2 mà không phải sửa (kiểm tra `PaymentConfirmedAt`, `SellerHandedOverAt`, một phiếu hoạt động/thỏa thuận, giá mua = `Amount`, khóa dòng lô `FOR UPDATE`).

Giao diện: `LiquidationV2Agreement.jsx` mở `StockReceiptForm` với `agreement={{ id, title, amount: totalAmount }}` và `confirmBulkReceived={!ownerCollectedAt}`.

### Ví dụ đối chiếu (fixture)

| Nội dung | Giá trị |
|---|---|
| Seller đăng | 100 kg sắt |
| Đơn giá trúng | 3.000 đ/kg |
| `finalPurchaseAmount` (phải trả Seller) | 300.000 đ |
| Kho phân loại xong nhập | 90 kg |
| Chi phí phát sinh (thuê xe, phân loại) | 45.000 đ |
| Tổng vốn | 345.000 đ |
| Giá vốn thực nhập | 3.833,33 đ/kg |

Không triển khai ví dụ “bàn giao 95 kg nên chỉ trả 285.000 đ”. Test tự động: `LiquidationV2PostgresTests.Agreement_flow_feeds_existing_stock_intake_once` (thắng → trả → bàn giao → nhập một lần, lần hai bị 409).

## 4. Trạng thái (stage) của thỏa thuận V2

Suy từ các mốc thời gian, không lưu cột riêng (`LiquidationV2Service.Stage`):

| Stage | Điều kiện | Hành động tiếp theo |
|---|---|---|
| `CANCELLED` | `resolution = CANCELLED` (Admin hủy) | Không nhập kho |
| `DISPUTED` | Có báo bỏ mua, Admin chưa xử lý | Chờ Admin |
| `WAITING_BUYER_PAYMENT` | Kho chưa báo trả tiền | Kho `REPORT_PAYMENT` |
| `WAITING_SELLER_CONFIRM` | Kho đã báo, Seller chưa xác nhận | Seller `CONFIRM_PAYMENT` |
| `WAITING_HANDOVER` | Đã nhận tiền, Seller chưa bàn giao | Seller `CONFIRM_HANDOVER` |
| `WAITING_OWNER_RECEIVE` | Seller đã bàn giao, kho chưa xác nhận | Kho `CONFIRM_RECEIVED` hoặc nhập kho một bước |
| `COMPLETED` | Kho đã xác nhận nhận | `STOCK_INTAKE` nếu chưa có phiếu |

Bất biến đang dựa vào: chỉ báo bỏ mua và Admin hủy được **trước khi Seller bàn giao**. Vì phiếu nhập đòi `SellerHandedOverAt`, thỏa thuận hủy/tranh chấp không vào được kho.

## 5. Lỗi thường gặp

| HTTP | Khi nào |
|---|---|
| 400 | Dữ liệu sai (đơn vị, mô tả `cai`/`bo` < 10 ký tự, bước giá…) |
| 403 | Không phải kho thắng / không phải chủ kho đó |
| 404 | Không tìm thấy phiên/thỏa thuận |
| 409 | Sai trạng thái (chưa nhận tiền, chưa bàn giao, đã có phiếu nhập, có báo bỏ mua, `operationId` dùng lại cho nội dung khác, giá mua khác `Amount`) |

Ngoài ra, khi chọn V2 trong `MoneyCollectionService`, khoản thu hộ số dư bị chặn (V2 không có tiền qua sàn).

## 6. Kết quả tích hợp phía TV2 — 26/11/2026

1. API kho tự từ chối thỏa thuận đã hủy hoặc có báo bỏ mua chưa xử lý. Kiểm tra nằm sau khóa nguồn và trước khi tạo phiếu; không chỉ dựa vào nút giao diện hoặc bất biến trước bàn giao.
2. Popup gọi intake-source khi mở nhập kho, điền sẵn tên/loại/đơn vị các hạng mục thắng. Lượng Seller đăng chỉ là gợi ý; ô lượng thực nhập để trống để Owner điền sau phân loại. Hạng mục hỗn hợp chưa có mã phải chọn vật liệu, không tự coi là sắt. Có thể thêm/bỏ dòng theo kết quả phân loại thực tế.
3. Tiền mua tự lấy finalPurchaseAmount. Một đầu ra không hỏi giá tham khảo; nhiều đầu ra giữ trọng số DepotCosting.Allocate. Không cộng lại tiền mua theo lượng thực nhập.
4. Lưu/đóng phiếu làm mới cache V2 của kho để thỏa thuận và danh sách chuyển đúng trạng thái, có đường tới phiếu đã ghi.
5. Đính chính mục hạn gói của bàn giao ban đầu: RegisterAsync/ConfirmSurveyAsync/BidAsync đã gọi RequireTradingAsync; DepotService được DI với IDepotSubscriptionService nên runtime đã kiểm gói. Test V2 hiện dùng cùng dependency này, chứng minh hết hạn chặn tham gia/giá mới, retry đúng thao tác và nhận lô đã mua vẫn được hoàn tất. Không thêm guard trùng ở service V2.
6. Test PostgreSQL bổ sung kiểm nguồn hủy/tranh chấp dù dữ liệu cờ bàn giao bất nhất, không sinh phiếu/tác động nhận hàng; kiểm tiền mua300000đ + chi45000đ với90kg thực nhập cho vốn345000đ.

## 7. Kiểm tra lại

```
dotnet build backend/Retrack.sln --no-restore
# RETRACK_TEST_POSTGRES và RETRACK_TEST_CONNECTION trỏ tới Retrack_TV2_test
dotnet test backend/Retrack.sln --no-restore --filter "FullyQualifiedName~LiquidationV2"
```

DB cũ: áp khối `RETRACK_LIQUIDATION_V2` … `RETRACK_LIQUIDATION_V2_END` trong `db/bootstrap/retrack-system.sql` (idempotent, trước `-- RETRACK_SCHEMA_END`), không chạy seed.

## 8. Kiểm chứng tích hợp TV2 — 26/11/2026

- Backend: 632 ca API.Tests +330 ca Retrack.Tests Passed trên PostgreSQL test;22 ca V2 trong đó kiểm cả gói, gia hạn, thắng riêng, tiền theo lượng đăng, retry/race và nhập kho.
- Frontend:43 unit Passed; lint/build Passed, còn cảnh báo scaffold/chunk có sẵn.
- Browser fixture riêng: mua100kg sắt giá300000đ, nhập90kg+45000đ chi phí → vốn345000đ; mua sắt và5 ghế giá600000đ+200000đ → vốn800000đ phân bổ đúng. Ghi sổ và reload đều giữ dữ liệu, không hiện nút nhập lại; form chặn lượng trống và số cái lẻ.
- Responsive:320/768/1024/1440px không tràn ngang toàn trang, hành động ghi sổ ở trong viewport.
- File SQL full hiện hành đã chạy hai lần trên schema test riêng: đủ6 role,52 user,8 depot, không thiếu cột model. Không đổi cấu trúc SQL chỉ để thêm một bản sao cho task.
- DB local đã backup, áp hai khối V2/refund không seed, đủ719 cột model; số dòng54 bảng cũ giữ nguyên. Đây là kiểm schema/số dòng, không phải kiểm toán mọi dữ liệu hoặc chứng nhận production.
- Chưa nghiệm thu ở lượt này: ngân hàng/PayOS thật và webhook công khai; GPS/camera mobile thật; toàn bộ UI các role trên môi trường deploy. Browser fixture dùng đăng nhập email, Google có client ID mẫu nên không dùng làm bằng chứng Google OAuth.
