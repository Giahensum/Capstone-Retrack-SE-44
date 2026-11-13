$api = "http://localhost:5000/api"
$headersSeller = @{ "Content-Type" = "application/json" }
$headersEmp = @{ "Content-Type" = "application/json" }

Write-Host "1. Login Seller (seller@retrack.vn)..."
$loginRes = Invoke-RestMethod -Uri "$api/auth/login" -Method Post -Body '{"email":"seller@retrack.vn","password":"Seller@123"}' -Headers $headersSeller
$sellerToken = $loginRes.data.token
$headersSeller["Authorization"] = "Bearer $sellerToken"

Write-Host "2. Login Employee (employee@retrack.vn)..."
$loginRes = Invoke-RestMethod -Uri "$api/auth/login" -Method Post -Body '{"email":"employee@retrack.vn","password":"Employee@123"}' -Headers $headersEmp
$empToken = $loginRes.data.token
$headersEmp["Authorization"] = "Bearer $empToken"

Write-Host "3. Seller: Create Broadcast Request (null depot)..."
$createBody = @{
    description = "Mobile App E2E Test"
    address = "123 Duong test E2E"
    latitude = 16.07
    longitude = 108.15
    targetDepotId = $null
} | ConvertTo-Json
$createRes = Invoke-RestMethod -Uri "$api/seller/pickups" -Method Post -Body $createBody -Headers $headersSeller
$pickupId = $createRes.data.id
Write-Host " -> Created Pickup ID: $pickupId"

Write-Host "4. Employee: Check Pickup Pool..."
$pool = Invoke-RestMethod -Uri "$api/employee/pickup-pool" -Method Get -Headers $headersEmp
$found = $pool.data | Where-Object { $_.id -eq $pickupId }
if ($found) {
    Write-Host " -> Success: Employee found the new pickup in the pool!" -ForegroundColor Green
} else {
    Write-Host " -> Error: Employee DID NOT find the new pickup in the pool!" -ForegroundColor Red
    exit
}

Write-Host "5. Employee: Accept Pickup..."
try {
    $acceptRes = Invoke-RestMethod -Uri "$api/employee/pickup/$pickupId/accept" -Method Post -Headers $headersEmp
    if ($acceptRes.success) {
        Write-Host " -> Success: Employee accepted the pickup!" -ForegroundColor Green
    }
} catch {
    Write-Host " -> Error: Employee could not accept. Note: Sometimes Employee already has an active task and can't accept another!" -ForegroundColor Yellow
    Write-Host $_.Exception.Message
}

Write-Host "6. Seller: Check Request Status..."
$req = Invoke-RestMethod -Uri "$api/seller/pickups/$pickupId" -Method Get -Headers $headersSeller
Write-Host " -> Seller sees status as: $($req.data.status)"
if ($req.data.status -eq "SCHEDULED") {
    Write-Host " -> Mobile Flow works perfectly! ??" -ForegroundColor Cyan
} else {
    Write-Host " -> Flow partially worked, request is currently $($req.data.status)" -ForegroundColor Yellow
}

