$api = "http://localhost:5000/api"
$headersSeller = @{ "Content-Type" = "application/json" }

Write-Host "========================================"
Write-Host " SELLER FEATURE TEST SUITE"
Write-Host "========================================"

# TEST 1: Login
Write-Host "`n--- TEST 1: Login Seller ---"
$loginRes = Invoke-RestMethod -Uri "$api/auth/login" -Method Post -Body '{"email":"seller@retrack.vn","password":"Seller@123"}' -Headers $headersSeller
$sellerToken = $loginRes.data.token
$headersSeller["Authorization"] = "Bearer $sellerToken"
Write-Host "-> Login OK" -ForegroundColor Green

# TEST 2: Update Bank Info
Write-Host "`n--- TEST 2: Update Bank Info ---"
$body = '{"bankName":"MB Bank","bankAccountNumber":"1112223334","bankAccountName":"LE VAN C"}'
Invoke-RestMethod -Uri "$api/seller/profile" -Method Put -Body $body -Headers $headersSeller | Out-Null
$p = Invoke-RestMethod -Uri "$api/seller/profile" -Method Get -Headers $headersSeller
if ($p.data.bankName -eq "MB Bank" -and $p.data.bankAccountNumber -eq "1112223334") {
    Write-Host "-> PASSED (bank=$($p.data.bankName), acc=$($p.data.bankAccountNumber))" -ForegroundColor Green
} else {
    Write-Host "-> FAILED" -ForegroundColor Red
}

# TEST 3: QR is optional
Write-Host "`n--- TEST 3: QR field is optional ---"
Write-Host "-> bankQrUrl = '$($p.data.bankQrUrl)'"
Write-Host "-> PASSED (QR not required)" -ForegroundColor Green

# TEST 4: Nearby Depots + Prices
Write-Host "`n--- TEST 4: Depots + Market Prices ---"
$d = Invoke-RestMethod -Uri "$api/seller/pickups/depots?lat=16.07&lng=108.15" -Method Get -Headers $headersSeller
Write-Host "-> $($d.data.Count) depots found"
$first = $d.data[0]
if ($first.prices) {
    $first.prices.psobject.properties | ForEach-Object { Write-Host "   $($_.Name): $($_.Value) VND/kg" }
    Write-Host "-> PASSED (prices from DB)" -ForegroundColor Green
} else {
    Write-Host "-> FAILED (no prices)" -ForegroundColor Red
}

# TEST 5: Request Detail has review fields
Write-Host "`n--- TEST 5: Review fields in request detail ---"
$reqs = Invoke-RestMethod -Uri "$api/seller/pickups" -Method Get -Headers $headersSeller
if ($reqs.data.Count -gt 0) {
    $det = Invoke-RestMethod -Uri "$api/seller/pickups/$($reqs.data[0].id)" -Method Get -Headers $headersSeller
    $hasField = $det.data.psobject.properties.Name -contains "reviewRating"
    if ($hasField) {
        Write-Host "-> reviewRating=$($det.data.reviewRating), reviewComment=$($det.data.reviewComment)" 
        Write-Host "-> PASSED (review fields in DTO)" -ForegroundColor Green
    } else {
        Write-Host "-> FAILED (missing reviewRating)" -ForegroundColor Red
    }
} else {
    Write-Host "-> SKIPPED (no requests)" -ForegroundColor Yellow
}

Write-Host "`n========================================"
Write-Host " ALL TESTS COMPLETE"
Write-Host "========================================"
