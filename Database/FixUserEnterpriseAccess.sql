-- Fix User Enterprise Access
-- This will make userId 11 the owner of enterpriseId 2, OR add role in EnterpriseUserRoles

-- Option 1: Make userId 11 the owner of enterpriseId 2
-- Uncomment the line below if you want userId 11 to OWN enterpriseId 2
-- UPDATE Enterprises SET OwnerUserId = 11 WHERE EnterpriseId = 2;

-- Option 2: Add userId 11 to EnterpriseUserRoles for enterpriseId 2 (Recommended)
-- This allows user to access labels without being the owner
IF NOT EXISTS (SELECT 1 FROM EnterpriseUserRoles WHERE EnterpriseId = 2 AND UserId = 11)
BEGIN
    INSERT INTO EnterpriseUserRoles (EnterpriseId, UserId, Role, CreatedAt)
    VALUES (2, 11, 'EnterpriseAdmin', SYSUTCDATETIME());
    PRINT 'User 11 added to EnterpriseUserRoles for Enterprise 2';
END
ELSE
BEGIN
    PRINT 'User 11 already has role in Enterprise 2';
END
GO

-- Verify the change
SELECT 
    e.EnterpriseId,
    e.EnterpriseName,
    e.OwnerUserId,
    CASE WHEN e.OwnerUserId = 11 THEN 'Owner' ELSE 'Not Owner' END AS Ownership,
    eur.UserId AS HasRole,
    eur.Role AS UserRole
FROM Enterprises e
LEFT JOIN EnterpriseUserRoles eur ON e.EnterpriseId = eur.EnterpriseId AND eur.UserId = 11
WHERE e.EnterpriseId = 2;
GO

-- Test the labels query again
DECLARE @OwnerId INT = 11;
SELECT 
    l.LabelId,
    l.LabelName,
    l.EnterpriseId,
    e.EnterpriseName,
    l.Status
FROM Labels l
INNER JOIN Enterprises e ON l.EnterpriseId = e.EnterpriseId
WHERE (e.OwnerUserId = @OwnerId 
    OR EXISTS (SELECT 1 FROM EnterpriseUserRoles eur 
               WHERE eur.EnterpriseId = e.EnterpriseId 
               AND eur.UserId = @OwnerId))
    AND l.IsDeleted = 0 
    AND e.IsDeleted = 0;
GO


