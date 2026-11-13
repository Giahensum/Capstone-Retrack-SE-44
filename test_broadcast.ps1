$ErrorActionPreference = "Stop"
$apiUrl = "http://localhost:5000/api"

# 1. Login as Seller
$sellerLogin = Invoke-RestMethod -Uri "$apiUrl/auth/login" -Method Post -ContentType "application/json" -Body '{"email":"seller@retrack.vn","password":"Seller@123"}'
$sellerToken = $sellerLogin.data.token

# 2. Seller Creates a Broadcast Request (targetDepotId = null)
Write-Host "Seller is creating a broadcast request..."
$createReq = Invoke-RestMethod -Uri "$apiUrl/seller/pickups" -Method Post -ContentType "application/json" -Headers @{Authorization="Bearer $sellerToken"} -Body '{"description":"5kg giấy carton","latitude":16.07,"longitude":108.15,"address":"Test Broadcast Addr","targetDepotId":null,"imageUrls":[]}'
$requestId = $createReq.data.id
Write-Host "-> Created Broadcast Request ID: $requestId"

# 3. Login as Employee
$empLogin = Invoke-RestMethod -Uri "$apiUrl/auth/login" -Method Post -ContentType "application/json" -Body '{"email":"employee@retrack.vn","password":"Employee@123"}'
$empToken = $empLogin.data.token

# 4. Employee checks the Pickup Pool
Write-Host "Employee is checking the pool..."
$pool = Invoke-RestMethod -Uri "$apiUrl/employee/pickup-pool" -Method Get -Headers @{Authorization="Bearer $empToken"}

$found = $false
foreach ($item in $pool.data) {
    if ($item.id -eq $requestId) {
        $found = $true
        Write-Host "-> SUCCESS: Employee sees the broadcast request in the pool!"
    }
}

if (-not $found) {
    Write-Host "-> FAILED: Employee does not see the broadcast request!"
} else {
    # 5. Employee accepts the request
    Write-Host "Employee is accepting the request..."
    $accept = Invoke-RestMethod -Uri "$apiUrl/employee/pickup/$requestId/accept" -Method Post -Headers @{Authorization="Bearer $empToken"}
    Write-Host "-> SUCCESS: Request accepted. New Status: $($accept.data.status)"
}
