# =============================================================
# seed_payment_test.ps1
# Seed đơn ở trạng thái PAYMENT_SENT để Seller test "Xác nhận nhận tiền"
# Yêu cầu: backend đang chạy tại localhost:5000
# Tài khoản cần: seller@retrack.vn / Seller@123
#               employee@retrack.vn / Employee@123
#               depotowner@retrack.vn / Owner@123
# =============================================================
$ErrorActionPreference = "Stop"
$api = "http://localhost:5000/api"

Write-Host "=== BUOC 1: Dang nhap cac tai khoan ===" -ForegroundColor Cyan

$sellerLogin = Invoke-RestMethod -Uri "$api/auth/login" -Method Post -ContentType "application/json" `
    -Body '{"email":"seller@retrack.vn","password":"Seller@123"}'
$sellerToken = $sellerLogin.data.token
$sellerId = $sellerLogin.data.user.id
Write-Host "  Seller: $($sellerLogin.data.user.fullName)" -ForegroundColor Green

$empLogin = Invoke-RestMethod -Uri "$api/auth/login" -Method Post -ContentType "application/json" `
    -Body '{"email":"employee@retrack.vn","password":"Employee@123"}'
$empToken = $empLogin.data.token
Write-Host "  Employee: $($empLogin.data.user.fullName)" -ForegroundColor Green

$ownerLogin = Invoke-RestMethod -Uri "$api/auth/login" -Method Post -ContentType "application/json" `
    -Body '{"email":"depot@retrack.vn","password":"Depot@123"}'
$ownerToken = $ownerLogin.data.token
Write-Host "  DepotOwner: $($ownerLogin.data.user.fullName)" -ForegroundColor Green

# Lay depotId cua owner
$ownerScope = Invoke-RestMethod -Uri "$api/depot/owned" -Method Get `
    -Headers @{Authorization = "Bearer $ownerToken"}
$depotId = $ownerScope.data[0].id
Write-Host "  DepotId: $depotId" -ForegroundColor Yellow

# =============================================================
Write-Host ""
Write-Host "=== BUOC 2: Seller tao don thu gom ===" -ForegroundColor Cyan

$pickupBody = @{
    description     = "Sat vun + nhom lon (seed test payment)"
    address         = "123 Nguyen Van Linh, Da Nang"
    latitude        = 16.07
    longitude       = 108.15
    targetDepotId   = $depotId
    requestImageUrl = $null
} | ConvertTo-Json

$pickup = Invoke-RestMethod -Uri "$api/sellerpickup" -Method Post -ContentType "application/json" `
    -Headers @{Authorization = "Bearer $sellerToken"} -Body $pickupBody
$pickupId = $pickup.data.id
Write-Host "  Don moi tao: $pickupId (trang thai: $($pickup.data.status))" -ForegroundColor Green

# =============================================================
Write-Host ""
Write-Host "=== BUOC 3: Employee nhan don (PENDING -> SCHEDULED) ===" -ForegroundColor Cyan

Invoke-RestMethod -Uri "$api/employee/pickup/$pickupId/accept" -Method Post `
    -Headers @{Authorization = "Bearer $empToken"} | Out-Null
Write-Host "  Da nhan don -> SCHEDULED" -ForegroundColor Green

# =============================================================
Write-Host ""
Write-Host "=== BUOC 4: Employee nhap ket qua can (SCHEDULED -> WEIGHED) ===" -ForegroundColor Cyan

$weighBody = @{
    items = @(
        @{ materialType = "Sat"; weightKg = 12.5; pricePerKg = 5000 }
        @{ materialType = "Nhom"; weightKg = 3.0; pricePerKg = 18000 }
        @{ materialType = "Giay"; weightKg = 8.0; pricePerKg = 2000 }
    )
    checkinImageUrl = "https://res.cloudinary.com/eiya3d1b/image/upload/v1/retrack/sample.jpg"
} | ConvertTo-Json -Depth 5

Invoke-RestMethod -Uri "$api/depot/pickup-requests/$pickupId/weigh" -Method Patch -ContentType "application/json" `
    -Headers @{Authorization = "Bearer $empToken"} -Body $weighBody | Out-Null
Write-Host "  Sat: 12.5kg x 5000 = 62,500 VND" -ForegroundColor White
Write-Host "  Nhom: 3.0kg x 18000 = 54,000 VND" -ForegroundColor White
Write-Host "  Giay: 8.0kg x 2000 = 16,000 VND" -ForegroundColor White
Write-Host "  => Tong: 132,500 VND -> WEIGHED" -ForegroundColor Green

# =============================================================
Write-Host ""
Write-Host "=== BUOC 5: Seller xac nhan ket qua can (WEIGHED -> SELLER_CONFIRMED) ===" -ForegroundColor Cyan

Invoke-RestMethod -Uri "$api/sellerpickup/$pickupId/confirm" -Method Patch `
    -Headers @{Authorization = "Bearer $sellerToken"} | Out-Null
Write-Host "  Seller da chot gia -> SELLER_CONFIRMED" -ForegroundColor Green

# =============================================================
Write-Host ""
Write-Host "=== BUOC 6: DepotOwner xac nhan da chuyen tien (SELLER_CONFIRMED -> PAYMENT_SENT) ===" -ForegroundColor Cyan

$paymentBody = @{
    paymentProofUrl = "https://res.cloudinary.com/eiya3d1b/image/upload/v1/retrack/sample.jpg"
} | ConvertTo-Json

Invoke-RestMethod -Uri "$api/depot/pickup-requests/$pickupId/payment-sent" -Method Patch -ContentType "application/json" `
    -Headers @{Authorization = "Bearer $ownerToken"} -Body $paymentBody | Out-Null
Write-Host "  Chu kho da chuyen 132,500 VND -> PAYMENT_SENT" -ForegroundColor Green

# =============================================================
Write-Host ""
Write-Host "=== KET QUA ===" -ForegroundColor Yellow
$finalPickup = Invoke-RestMethod -Uri "$api/sellerpickup/$pickupId" -Method Get `
    -Headers @{Authorization = "Bearer $sellerToken"}
$d = $finalPickup.data
Write-Host "  Don ID    : $($d.id)" -ForegroundColor White
Write-Host "  Trang thai: $($d.status)" -ForegroundColor Cyan
Write-Host "  Tong tien : $($d.grossAmount) VND" -ForegroundColor White
Write-Host "  Net amount: $($d.netAmount) VND" -ForegroundColor White
Write-Host ""
Write-Host "=== VIET TIEP: Seller vao web -> 'Don cua toi' -> mo don nay -> bam 'Da nhan tien' ===" -ForegroundColor Yellow
Write-Host "  URL: http://localhost:5173/seller/requests" -ForegroundColor Cyan
