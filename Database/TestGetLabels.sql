-- Test GET Labels Query for EnterpriseAdmin
-- Run this in SQL Server Management Studio
-- Change @OwnerId to your userId

DECLARE @OwnerId INT = 11;  -- ⚠️ CHANGE THIS to your userId

-- =====================================================================
-- 1. Test query (same as in LabelsController)
-- =====================================================================
SELECT 
    l.LabelId,
    l.LabelName,
    l.EnterpriseId,
    e.EnterpriseName,
    l.PlanTypeId,
    CASE 
        WHEN l.PlanTypeId = 1 THEN 'Starter'
        WHEN l.PlanTypeId = 2 THEN 'Growth'
        ELSE 'Unknown'
    END AS PlanTypeName,
    l.RevenueSharePercent,
    l.Domain,
    l.QCRequired,
    l.Status,
    l.CreatedAt
FROM Labels l
INNER JOIN Enterprises e ON l.EnterpriseId = e.EnterpriseId
WHERE (e.OwnerUserId = @OwnerId 
    OR EXISTS (SELECT 1 FROM EnterpriseUserRoles eur 
               WHERE eur.EnterpriseId = e.EnterpriseId 
               AND eur.UserId = @OwnerId))
    AND l.IsDeleted = 0 
    AND e.IsDeleted = 0
ORDER BY l.LabelName;

-- =====================================================================
-- 2. Check what enterprises the user owns
-- =====================================================================
SELECT 
    e.EnterpriseId,
    e.EnterpriseName,
    e.OwnerUserId,
    CASE WHEN e.OwnerUserId = @OwnerId THEN 'Owner' ELSE 'Not Owner' END AS Ownership
FROM Enterprises e
WHERE e.IsDeleted = 0;

-- =====================================================================
-- 3. Check EnterpriseUserRoles for this user
-- =====================================================================
SELECT 
    eur.EnterpriseId,
    e.EnterpriseName,
    eur.UserId,
    eur.Role
FROM EnterpriseUserRoles eur
INNER JOIN Enterprises e ON eur.EnterpriseId = e.EnterpriseId
WHERE eur.UserId = @OwnerId;

-- =====================================================================
-- 4. Check all labels (for debugging)
-- =====================================================================
SELECT 
    l.LabelId,
    l.LabelName,
    l.EnterpriseId,
    e.EnterpriseName,
    e.OwnerUserId
FROM Labels l
INNER JOIN Enterprises e ON l.EnterpriseId = e.EnterpriseId
WHERE l.IsDeleted = 0 AND e.IsDeleted = 0;

