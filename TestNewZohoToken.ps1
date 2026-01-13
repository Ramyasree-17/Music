# Test Zoho Token Script
# Replace YOUR_NEW_TOKEN with the token you just generated

$token = "YOUR_NEW_TOKEN"
$orgId = "60062031469"

$headers = @{
    "Authorization" = "Zoho-oauthtoken $token"
}

$url = "https://books.zoho.in/api/v3/organizations?organization_id=$orgId"

try {
    $response = Invoke-RestMethod -Uri $url -Method Get -Headers $headers
    Write-Host "✅ SUCCESS! Token is valid." -ForegroundColor Green
    Write-Host "Response:" -ForegroundColor Cyan
    $response | ConvertTo-Json -Depth 5
} catch {
    Write-Host "❌ FAILED! Token is invalid or expired." -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Yellow
    if ($_.ErrorDetails.Message) {
        Write-Host "Details: $($_.ErrorDetails.Message)" -ForegroundColor Yellow
    }
}






















