@echo off
echo ============================================
echo WARNING: This will DELETE ALL data from
echo Enterprises and Labels tables!
echo ============================================
echo.
echo Press Ctrl+C to cancel, or
pause

echo.
echo Running SQL script to delete Enterprises and Labels data...
echo.

sqlcmd -S 69.197.148.238,51433 -d Tunewave -U sa -P "kh@tunewave@2025" -i "Database\DeleteEnterprisesAndLabels.sql"

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ✅ Success! Enterprises and Labels data deleted.
) else (
    echo.
    echo ❌ Error running SQL script. Check the error message above.
)

pause




















