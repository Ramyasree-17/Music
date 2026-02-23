-- =============================================
-- Add Constraint to Prevent Invalid JSON in FooterLinksJson
-- =============================================
-- This constraint ensures FooterLinksJson is either NULL or valid JSON

PRINT '=== Adding Constraint to Branding.FooterLinksJson ===';

-- Check if constraint already exists
IF NOT EXISTS (
    SELECT * FROM sys.check_constraints 
    WHERE name = 'CK_Branding_FooterLinksJson_IsJson'
)
BEGIN
    ALTER TABLE Branding
    ADD CONSTRAINT CK_Branding_FooterLinksJson_IsJson
    CHECK (FooterLinksJson IS NULL OR ISJSON(FooterLinksJson) = 1);

    PRINT 'Constraint CK_Branding_FooterLinksJson_IsJson added successfully';
END
ELSE
BEGIN
    PRINT 'Constraint CK_Branding_FooterLinksJson_IsJson already exists';
END

GO

-- =============================================
-- Test the constraint
-- =============================================
PRINT '=== Testing Constraint ===';

-- This should fail (invalid JSON)
BEGIN TRY
    UPDATE Branding
    SET FooterLinksJson = 'invalid json string'
    WHERE Id = 999999; -- Non-existent ID to avoid actual update
    
    PRINT 'WARNING: Constraint test failed - invalid JSON was accepted';
END TRY
BEGIN CATCH
    PRINT 'Constraint working correctly - invalid JSON rejected';
    PRINT 'Error: ' + ERROR_MESSAGE();
END CATCH

GO



