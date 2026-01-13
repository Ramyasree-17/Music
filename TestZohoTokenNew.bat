@echo off
echo ========================================
echo Testing New Zoho Token
echo ========================================
echo.

curl -X GET "https://books.zoho.in/api/v3/organizations?organization_id=60062031469" -H "Authorization: Zoho-oauthtoken 1000.5de0ad4b52fceeca9da0fdb751176dac.634495cd507d350abd0a70c37b518924"

echo.
echo.
echo ========================================
echo If you see code:0 = Token works! SUCCESS!
echo If you see code:57 = Token expired or wrong permissions
echo If you see code:14 = Token invalid
echo ========================================
pause























