@echo off
echo Running SQL script to add Zoho Books columns...
echo.

sqlcmd -S 69.197.148.238,51433 -d Tunewave -U sa -P "kh@tunewave@2025" -i "Database\AddZohoBooksColumns.sql"

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ✅ Success! Zoho Books columns added to database.
) else (
    echo.
    echo ❌ Error running SQL script. Check the error message above.
)

pause






















