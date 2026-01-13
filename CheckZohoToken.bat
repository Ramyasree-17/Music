@echo off
echo ========================================
echo Testing NEW Zoho Token
echo ========================================
echo.

curl -X GET "https://books.zoho.com/api/v3/organizations?organization_id=60062031469" -H "Authorization: Zoho-oauthtoken 1000.d89235d6848f118b22000b148ba6cadc.4de75cfc708f961ac88345badbc845af"

echo.
echo.
echo ========================================
echo If you see code:0 = Token works!
echo If you see code:57 = Token expired or wrong permissions
echo ========================================
pause























