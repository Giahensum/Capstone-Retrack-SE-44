$ErrorActionPreference = "Stop"
$apiUrl = "http://localhost:5000/api"

Write-Host "`n========================================================" -ForegroundColor Cyan
Write-Host "   TEST 1: BROADCAST PICKUP 20 KM RADIUS VERIFICATION" -ForegroundColor Cyan
Write-Host "========================================================`n" -ForegroundColor Cyan

# 1. Login as Seller
$sellerLogin = Invoke-RestMethod -Uri "$apiUrl/auth/login" -Method Post -ContentType "application/json" -Body '{"email":"seller@retrack.vn","password":"Seller@123"}'
$sellerToken = $sellerLogin.data.token
Write-Host "[OK] Seller logged in successfully." -ForegroundColor Green

# 2. Seller creates Near Broadcast Request (~14.7 km from depot)
Write-Host "Creating NEAR broadcast request (Danang ~14.7km)..."
$nearReq = Invoke-RestMethod -Uri "$apiUrl/seller/pickups" -Method Post -ContentType "application/json" -Headers @{Authorization="Bearer $sellerToken"} -Body '{"description":"Test Near 14.7km","latitude":16.07,"longitude":108.15,"address":"Hai Chau, Da Nang","targetDepotId":null}'
$nearId = $nearReq.data.id
Write-Host "[OK] Created Near Request ID: $nearId" -ForegroundColor Green

# 3. Seller creates FAR Broadcast Request (> 20 km, e.g. HCMC ~600km)
Write-Host "Creating FAR broadcast request (HCMC > 600km)..."
$farReq = Invoke-RestMethod -Uri "$apiUrl/seller/pickups" -Method Post -ContentType "application/json" -Headers @{Authorization="Bearer $sellerToken"} -Body '{"description":"Test Far 600km","latitude":10.77,"longitude":106.69,"address":"District 1, HCMC","targetDepotId":null}'
$farId = $farReq.data.id
Write-Host "[OK] Created Far Request ID: $farId" -ForegroundColor Green

# 4. Login as Employee
$empLogin = Invoke-RestMethod -Uri "$apiUrl/auth/login" -Method Post -ContentType "application/json" -Body '{"email":"employee@retrack.vn","password":"Employee@123"}'
$empToken = $empLogin.data.token
Write-Host "[OK] Employee logged in successfully." -ForegroundColor Green

# 5. Check Pickup Pool
Write-Host "Checking Pickup Pool for Employee..."
$pool = Invoke-RestMethod -Uri "$apiUrl/employee/pickup-pool" -Method Get -Headers @{Authorization="Bearer $empToken"}
$poolIds = $pool.data | ForEach-Object { $_.id }

$hasNear = $poolIds -contains $nearId
$hasFar = $poolIds -contains $farId

if ($hasNear) {
    Write-Host "[PASSED] Employee sees NEAR request ($nearId) in pool!" -ForegroundColor Green
} else {
    Write-Host "[FAILED] Employee CANNOT see NEAR request ($nearId) in pool!" -ForegroundColor Red
    exit 1
}

if (-not $hasFar) {
    Write-Host "[PASSED] Employee DOES NOT see FAR request ($farId) in pool (correctly filtered out by 20km radius)!" -ForegroundColor Green
} else {
    Write-Host "[FAILED] Employee sees FAR request ($farId) in pool (radius filter failed)!" -ForegroundColor Red
    exit 1
}

# 6. Test accepting FAR request (should be rejected)
Write-Host "Testing employee attempt to accept FAR request ($farId)..."
try {
    Invoke-RestMethod -Uri "$apiUrl/employee/pickup/$farId/accept" -Method Post -Headers @{Authorization="Bearer $empToken"}
    Write-Host "[FAILED] Employee was able to accept order outside 20km!" -ForegroundColor Red
    exit 1
} catch {
    Write-Host "[PASSED] Server correctly rejected acceptance of far order ($($_.Exception.Message))" -ForegroundColor Green
}

# 7. Test accepting NEAR request (should succeed)
Write-Host "Testing employee accepting NEAR request ($nearId)..."
$acceptNear = Invoke-RestMethod -Uri "$apiUrl/employee/pickup/$nearId/accept" -Method Post -Headers @{Authorization="Bearer $empToken"}
if ($acceptNear.success) {
    Write-Host "[PASSED] Near request accepted successfully. Message: $($acceptNear.message)" -ForegroundColor Green
} else {
    Write-Host "[FAILED] Could not accept near request." -ForegroundColor Red
    exit 1
}

Write-Host "`n========================================================" -ForegroundColor Cyan
Write-Host "   TEST 2: FORGOT PASSWORD & EMAIL OTP VERIFICATION" -ForegroundColor Cyan
Write-Host "========================================================`n" -ForegroundColor Cyan

# 1. Request OTP for non-existent email
Write-Host "Requesting OTP for non-existent email..."
try {
    Invoke-RestMethod -Uri "$apiUrl/auth/forgot-password" -Method Post -ContentType "application/json" -Body '{"email":"nobody@example.com"}'
    Write-Host "[FAILED] Server did not return 404 for unknown email" -ForegroundColor Red
    exit 1
} catch {
    Write-Host "[PASSED] Server returned 404 for unknown email: $($_.Exception.Message)" -ForegroundColor Green
}

# 2. Request OTP for existing email
Write-Host "Requesting OTP for seller@retrack.vn..."
$forgotRes = Invoke-RestMethod -Uri "$apiUrl/auth/forgot-password" -Method Post -ContentType "application/json" -Body '{"email":"seller@retrack.vn"}'
Write-Host "[OK] $($forgotRes.message)" -ForegroundColor Green

# 3. Test wrong OTP
Write-Host "Testing wrong OTP (999999)..."
try {
    Invoke-RestMethod -Uri "$apiUrl/auth/verify-otp" -Method Post -ContentType "application/json" -Body '{"email":"seller@retrack.vn","otp":"999999"}'
    Write-Host "[FAILED] Wrong OTP was accepted" -ForegroundColor Red
    exit 1
} catch {
    Write-Host "[PASSED] Wrong OTP correctly rejected" -ForegroundColor Green
}

# 4. Query database for the generated OTP
$realOtp = (dotnet run --project DbSeeder/DbSeeder.csproj -- get-otp seller@retrack.vn | Select-Object -Last 1).Trim()
Write-Host "Retrieved OTP from database: $realOtp" -ForegroundColor Yellow

# 5. Verify with correct OTP
Write-Host "Verifying with correct OTP ($realOtp)..."
$verifyRes = Invoke-RestMethod -Uri "$apiUrl/auth/verify-otp" -Method Post -ContentType "application/json" -Body "{`"email`":`"seller@retrack.vn`",`"otp`":`"$realOtp`"}"
Write-Host "[PASSED] $($verifyRes.message)" -ForegroundColor Green

# 6. Reset password to new password
Write-Host "Resetting password to 'Seller@New456'..."
$resetRes = Invoke-RestMethod -Uri "$apiUrl/auth/reset-password" -Method Post -ContentType "application/json" -Body "{`"email`":`"seller@retrack.vn`",`"otp`":`"$realOtp`",`"newPassword`":`"Seller@New456`"}"
Write-Host "[PASSED] $($resetRes.message)" -ForegroundColor Green

# 7. Try login with old password (must fail)
Write-Host "Testing login with old password 'Seller@123' (must fail)..."
try {
    Invoke-RestMethod -Uri "$apiUrl/auth/login" -Method Post -ContentType "application/json" -Body '{"email":"seller@retrack.vn","password":"Seller@123"}'
    Write-Host "[FAILED] Login with old password succeeded!" -ForegroundColor Red
    exit 1
} catch {
    Write-Host "[PASSED] Old password rejected (401 Unauthorized)" -ForegroundColor Green
}

# 8. Try login with new password (must succeed)
Write-Host "Testing login with new password 'Seller@New456'..."
$newLogin = Invoke-RestMethod -Uri "$apiUrl/auth/login" -Method Post -ContentType "application/json" -Body '{"email":"seller@retrack.vn","password":"Seller@New456"}'
if ($newLogin.success -and $newLogin.data.token) {
    Write-Host "[PASSED] Login with new password succeeded! Token obtained." -ForegroundColor Green
} else {
    Write-Host "[FAILED] Login with new password failed" -ForegroundColor Red
    exit 1
}

# 9. Clean up: restore password to 'Seller@123'
Write-Host "Restoring password to default 'Seller@123'..."
dotnet run --project DbSeeder/DbSeeder.csproj -- restore-seller-password | Out-Null

# Verify login with restored password
$restoredLogin = Invoke-RestMethod -Uri "$apiUrl/auth/login" -Method Post -ContentType "application/json" -Body '{"email":"seller@retrack.vn","password":"Seller@123"}'
if ($restoredLogin.success) {
    Write-Host "[PASSED] Default password restored successfully!" -ForegroundColor Green
}

Write-Host "`n========================================================" -ForegroundColor Cyan
Write-Host "   ALL TESTS PASSED WITH 100% SUCCESS!" -ForegroundColor Green
Write-Host "========================================================`n" -ForegroundColor Cyan
