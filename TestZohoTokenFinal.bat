@echo off
echo ========================================
echo Testing Zoho Token
echo ========================================
echo.

curl -X GET "https://books.zoho.in/api/v3/organizations?organization_id=60062031469" -H "Authorization: Zoho-oauthtoken 1000.9a632786b52ec206ccedf43fbfd72e9b.a7d1b31c8f611221b286217db82acf33"

echo.
echo.
echo ========================================
echo If you see code:0 = Token works! SUCCESS!
echo If you see code:57 = Token expired or wrong permissions
echo ========================================
pause























