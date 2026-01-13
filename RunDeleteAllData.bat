@echo off
echo ============================================
echo ⚠️  WARNING: This will DELETE ALL DATA
echo from ALL TABLES in the database!
echo ============================================
echo.
echo Press Ctrl+C to cancel, or
pause

echo.
echo Running SQL script to delete all data...
echo.

sqlcmd -S 69.197.148.238,51433 -d Tunewave -U sa -P "kh@tunewave@2025" -i "Database\DeleteAllData_Quick.sql"

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ✅ Success! All data deleted from database.
) else (
    echo.
    echo ❌ Error running SQL script. Check the error message above.
)

pause




















