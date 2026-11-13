$api = "http://localhost:5000/api"
$headersSeller = @{ "Content-Type" = "application/json" }

Write-Host "--- 1. Login Seller (seller@retrack.vn) ---"
$loginRes = Invoke-RestMethod -Uri "$api/auth/login" -Method Post -Body '{"email":"seller@retrack.vn","password":"Seller@123"}' -Headers $headersSeller
$sellerToken = $loginRes.data.token
$headersSeller["Authorization"] = "Bearer $sellerToken"
Write-Host "-> Login successful!" -ForegroundColor Green

Write-Host "
--- 2. Update Seller Profile (Bank Info) ---"
$updateBody = @{
    bankName = "Vietcombank"
    bankAccountNumber = "0123456789"
    bankAccountName = "NGUYEN VAN A"
    bankQrUrl = "https://example.com/qr.png"
} | ConvertTo-Json
$updateRes = Invoke-RestMethod -Uri "$api/seller/profile" -Method Put -Body $updateBody -Headers $headersSeller
Write-Host "-> Update Response: $($updateRes.message)" -ForegroundColor Green

Write-Host "
--- 3. Check Seller Profile (Verify Bank Info) ---"
$profileRes = Invoke-RestMethod -Uri "$api/seller/profile" -Method Get -Headers $headersSeller
Write-Host "-> Bank Name: $($profileRes.data.bankName)"
Write-Host "-> Bank Account Number: $($profileRes.data.bankAccountNumber)"
Write-Host "-> Bank Account Name: $($profileRes.data.bankAccountName)"
Write-Host "-> Bank QR URL: $($profileRes.data.bankQrUrl)"
if ($profileRes.data.bankName -eq "Vietcombank") {
    Write-Host "-> Profile Test Passed! ?" -ForegroundColor Cyan
} else {
    Write-Host "-> Profile Test Failed! ?" -ForegroundColor Red
}

Write-Host "
--- 4. Check Nearby Depots (Prices and Ratings) ---"
$depotsRes = Invoke-RestMethod -Uri "$api/seller/pickups/depots?lat=16.07&lng=108.15" -Method Get -Headers $headersSeller
$firstDepot = $depotsRes.data[0]
Write-Host "-> Fetched $($depotsRes.data.Count) depots."
if ($firstDepot) {
    Write-Host "-> First Depot Name: $($firstDepot.name)"
    Write-Host "-> Avg Rating: $($firstDepot.avgRating)"
    if ($firstDepot.prices) {
        Write-Host "-> Market Prices returned successfully! ?" -ForegroundColor Cyan
        $firstDepot.prices.psobject.properties | ForEach-Object {
            Write-Host "   - $($_.Name): $($_.Value) VNÐ/kg"
        }
    } else {
        Write-Host "-> Missing Market Prices! ?" -ForegroundColor Red
    }
}
