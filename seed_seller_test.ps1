$ErrorActionPreference = "Stop"
$apiUrl = "http://localhost:5000/api"

$sellerLogin = Invoke-RestMethod -Uri "$apiUrl/auth/login" -Method Post -ContentType "application/json" -Body '{"email":"seller@retrack.vn","password":"Seller@123"}'
$sellerToken = $sellerLogin.data.token

$empLogin = Invoke-RestMethod -Uri "$apiUrl/auth/login" -Method Post -ContentType "application/json" -Body '{"email":"employee@retrack.vn","password":"Employee@123"}'
$empToken = $empLogin.data.token

Write-Host "Creating Request 1 (Target: WEIGHED)..."
$req1 = Invoke-RestMethod -Uri "$apiUrl/seller/pickups" -Method Post -ContentType "application/json" -Headers @{Authorization="Bearer $sellerToken"} -Body '{"description":"Phe lieu 1","latitude":16.07,"longitude":108.15,"address":"Nha Test 1","targetDepotId":null,"imageUrls":[]}'
$req1Id = $req1.data.id

Write-Host "Creating Request 2 (Target: PAYMENT_SENT)..."
$req2 = Invoke-RestMethod -Uri "$apiUrl/seller/pickups" -Method Post -ContentType "application/json" -Headers @{Authorization="Bearer $sellerToken"} -Body '{"description":"Phe lieu 2","latitude":16.07,"longitude":108.15,"address":"Nha Test 2","targetDepotId":null,"imageUrls":[]}'
$req2Id = $req2.data.id

Write-Host "Employee accepting requests..."
Invoke-RestMethod -Uri "$apiUrl/employee/pickup/$req1Id/accept" -Method Post -Headers @{Authorization="Bearer $empToken"} | Out-Null
Invoke-RestMethod -Uri "$apiUrl/employee/pickup/$req2Id/accept" -Method Post -Headers @{Authorization="Bearer $empToken"} | Out-Null

$materials = Invoke-RestMethod -Uri "$apiUrl/employee/material-prices" -Method Get -Headers @{Authorization="Bearer $empToken"}
$matId = $materials.data[0].materialId

Write-Host "Employee checking in..."
# Multipart-form upload for checkin requires a file, wait!
# EmployeeCheckInRequest has Photo as IFormFile. We might need a dummy file or just pass it correctly if it's not required.
# Actually, the previous CheckIn might have failed if Photo is required?
