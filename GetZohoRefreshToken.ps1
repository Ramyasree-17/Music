# PowerShell Script to Get Zoho Refresh Token
# This script exchanges authorization code for refresh token

param(
    [Parameter(Mandatory=$true)]
    [string]$ClientId,
    
    [Parameter(Mandatory=$true)]
    [string]$ClientSecret,
    
    [Parameter(Mandatory=$true)]
    [string]$AuthorizationCode,
    
    [string]$RedirectUri = "http://localhost:8080"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Zoho Refresh Token Generator" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$tokenUrl = "https://accounts.zoho.in/oauth/v2/token"

$body = @{
    grant_type = "authorization_code"
    client_id = $ClientId
    client_secret = $ClientSecret
    redirect_uri = $RedirectUri
    code = $AuthorizationCode
}

Write-Host "Sending request to Zoho..." -ForegroundColor Yellow
Write-Host "Client ID: $ClientId" -ForegroundColor Gray
Write-Host ""

try {
    $response = Invoke-RestMethod -Uri $tokenUrl -Method Post -Body $body -ContentType "application/x-www-form-urlencoded"
    
    Write-Host "✅ SUCCESS!" -ForegroundColor Green
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "TOKENS RECEIVED:" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    
    Write-Host "Access Token:" -ForegroundColor Yellow
    Write-Host $response.access_token -ForegroundColor White
    Write-Host ""
    
    Write-Host "Refresh Token (IMPORTANT - Copy this!):" -ForegroundColor Yellow
    Write-Host $response.refresh_token -ForegroundColor Green
    Write-Host ""
    
    Write-Host "Expires In: $($response.expires_in) seconds" -ForegroundColor Gray
    Write-Host ""
    
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "NEXT STEPS:" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "1. Copy the Refresh Token above" -ForegroundColor White
    Write-Host "2. Update appsettings.json:" -ForegroundColor White
    Write-Host "   - Set RefreshToken = '$($response.refresh_token)'" -ForegroundColor Gray
    Write-Host "   - Set ClientId = '$ClientId'" -ForegroundColor Gray
    Write-Host "   - Set ClientSecret = '$ClientSecret'" -ForegroundColor Gray
    Write-Host "3. Restart your application" -ForegroundColor White
    Write-Host ""
    
} catch {
    Write-Host "❌ ERROR!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Error Message: $($_.Exception.Message)" -ForegroundColor Red
    
    if ($_.ErrorDetails.Message) {
        Write-Host ""
        Write-Host "Error Details:" -ForegroundColor Yellow
        Write-Host $_.ErrorDetails.Message -ForegroundColor Red
        
        try {
            $errorJson = $_.ErrorDetails.Message | ConvertFrom-Json
            if ($errorJson.error) {
                Write-Host ""
                Write-Host "Error Code: $($errorJson.error)" -ForegroundColor Red
                Write-Host "Error Description: $($errorJson.error_description)" -ForegroundColor Red
            }
        } catch {
            # Not JSON, ignore
        }
    }
    
    Write-Host ""
    Write-Host "Common Issues:" -ForegroundColor Yellow
    Write-Host "- Authorization code expired (generate new one)" -ForegroundColor Gray
    Write-Host "- Client ID or Secret incorrect" -ForegroundColor Gray
    Write-Host "- Redirect URI doesn't match" -ForegroundColor Gray
    Write-Host "- Code already used (can only use once)" -ForegroundColor Gray
}






















