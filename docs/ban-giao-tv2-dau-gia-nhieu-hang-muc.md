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

## 6. Việc TV2 cần kiểm tra/đề xuất

1. (Khuyến nghị, phòng thủ thêm) Trong `DepotStockService.CreateAsync`, từ chối khi `source.Resolution == "CANCELLED"` hoặc `AbandonReportedAt != null && Resolution == null`. Hiện an toàn nhờ bất biến ở mục 4, nhưng sẽ hỏng nếu sau này cho báo bỏ mua sau bàn giao.
2. Có thể gọi `intake-source` để điền sẵn dòng (tên, đơn vị, `materialType`) và hiển thị `postedQuantity` làm gợi ý, nhưng không khóa số lượng theo nó.
3. Báo giá vốn: `purchaseCost` là tổng tiền mua cả thỏa thuận; phân bổ cho từng dòng theo cơ chế `DepotCosting.Allocate` hiện có.
4. Hạn gói: đăng ký hạng mục, xác nhận khảo sát và trả giá V2 cần gói còn hạn (402 `SUBSCRIPTION_REQUIRED`). Gửi lại cùng `operationId` vẫn trả kết quả cũ. Báo trả tiền, nhận hàng và nhập kho từ thỏa thuận đã thắng vẫn làm được khi hết hạn (đúng quy tắc chung).

## 7. Kiểm tra lại

```
dotnet build backend/Retrack.sln --no-restore
# RETRACK_TEST_POSTGRES và RETRACK_TEST_CONNECTION trỏ tới Retrack_TV2_test
dotnet test backend/Retrack.sln --no-restore --filter "FullyQualifiedName~LiquidationV2"
```

DB cũ: áp khối `RETRACK_LIQUIDATION_V2` … `RETRACK_LIQUIDATION_V2_END` trong `db/bootstrap/retrack-system.sql` (idempotent, trước `-- RETRACK_SCHEMA_END`), không chạy seed.
