@echo off
echo ========================================
echo Testing Zoho Token
echo ========================================
echo.

curl -X GET "https://books.zoho.in/api/v3/organizations?organization_id=60062031469" -H "Authorization: Zoho-oauthtoken 1000.4MI2D0R97EC67Y3FNVIBDZ8IPC775E"

echo.
echo.
echo ========================================
echo If you see code:0 = Token works!
echo If you see code:57 = Token expired or wrong permissions
echo ========================================
pause























