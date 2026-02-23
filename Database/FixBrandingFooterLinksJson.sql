-- =============================================
-- Fix Branding FooterLinksJson Bad Values
-- =============================================
-- This script fixes all rows where FooterLinksJson = 'string'
-- and replaces them with valid JSON

PRINT '=== Step 1: Fix FooterLinksJson Bad Values ===';

UPDATE Branding
SET FooterLinksJson = '[
  { "title": "Privacy Policy", "url": "/privacy" },
  { "title": "Terms", "url": "/terms" }
]'
WHERE FooterLinksJson = 'string' OR FooterLinksJson IS NULL;

PRINT 'FooterLinksJson values fixed successfully';

-- =============================================
-- Step 2: Verify the fix
-- =============================================
PRINT '=== Step 2: Verify Fixed Values ===';

SELECT 
    Id,
    SiteName,
    FooterLinksJson,
    CASE 
        WHEN FooterLinksJson IS NULL THEN 'NULL'
        WHEN FooterLinksJson = 'string' THEN 'STILL BAD'
        WHEN ISJSON(FooterLinksJson) = 1 THEN 'VALID JSON'
        ELSE 'INVALID JSON'
    END AS Status
FROM Branding
WHERE FooterLinksJson IS NOT NULL
ORDER BY Id;

PRINT 'Verification complete';

GO



