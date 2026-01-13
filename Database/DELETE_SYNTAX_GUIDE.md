# DELETE Data Syntax Guide

## Problem: Foreign Key Constraint Error

When you try to delete from `Enterprises` table, you get this error:
```
The DELETE statement conflicted with the REFERENCE constraint "FK_Labels_Enterprise". 
The conflict occurred in database "Tunewave", table "dbo.Labels", column 'EnterpriseId'.
```

This happens because `Labels` table has a foreign key pointing to `Enterprises` table.

---

## Solution 1: Delete in Correct Order (RECOMMENDED)

Delete child tables first, then parent tables:

```sql
-- Step 1: Delete child tables first
DELETE FROM UserLabelRoles;        -- Depends on Labels
DELETE FROM Labels;                -- Depends on Enterprises
DELETE FROM EnterpriseUserRoles;   -- Depends on Enterprises

-- Step 2: Delete parent table
DELETE FROM Enterprises;
```

**File:** `DeleteEnterprisesAndLabels.sql` (Method 1)

---

## Solution 2: Temporarily Disable Constraints

Disable the foreign key constraint, delete, then re-enable:

```sql
-- Disable constraint
ALTER TABLE Labels NOCHECK CONSTRAINT FK_Labels_Enterprise;
GO

-- Delete data (order doesn't matter now)
DELETE FROM Labels;
DELETE FROM Enterprises;
GO

-- Re-enable constraint
ALTER TABLE Labels WITH CHECK CHECK CONSTRAINT FK_Labels_Enterprise;
GO
```

**File:** `DeleteEnterprisesAndLabels.sql` (Method 2 - commented)

---

## Solution 3: Delete Specific Enterprise

If you want to delete a specific enterprise and its labels:

```sql
DECLARE @EnterpriseId INT = 1;  -- Change to your EnterpriseId

-- Delete Labels for this Enterprise first
DELETE FROM Labels WHERE EnterpriseId = @EnterpriseId;

-- Delete EnterpriseUserRoles
DELETE FROM EnterpriseUserRoles WHERE EnterpriseId = @EnterpriseId;

-- Delete the Enterprise
DELETE FROM Enterprises WHERE EnterpriseId = @EnterpriseId;
```

**File:** `DeleteEnterprisesAndLabels.sql` (Method 3 - commented)

---

## Solution 4: Delete ALL Data from ALL Tables

Disable ALL foreign key constraints, delete everything, then re-enable:

```sql
-- Disable ALL constraints
EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';
GO

-- Delete all data (order doesn't matter)
DELETE FROM Labels;
DELETE FROM Enterprises;
DELETE FROM Users;
-- ... (all other tables)
GO

-- Re-enable ALL constraints
EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';
GO
```

**File:** `DeleteAllData_Quick.sql`

---

## Quick Reference: Table Dependencies

### Delete Order for Enterprises and Labels:
1. `UserLabelRoles` (depends on Labels)
2. `Labels` (depends on Enterprises)
3. `EnterpriseUserRoles` (depends on Enterprises)
4. `Enterprises` (parent table)

### Full Delete Order (if deleting everything):
1. All child/dependent tables (Tracks, Releases, Artists, etc.)
2. `UserLabelRoles`
3. `Labels`
4. `EnterpriseUserRoles`
5. `Enterprises`
6. `Users`
7. Reference tables (optional)

---

## How to Run

### Option 1: Using Batch Files
- **Delete Enterprises & Labels only:** Run `RunDeleteEnterprisesAndLabels.bat`
- **Delete ALL data:** Run `RunDeleteAllData.bat`

### Option 2: Using SQL Server Management Studio
1. Open the SQL script file
2. Connect to your database
3. Execute the script

### Option 3: Using sqlcmd (Command Line)
```bash
sqlcmd -S 69.197.148.238,51433 -d Tunewave -U sa -P "kh@tunewave@2025" -i "Database\DeleteEnterprisesAndLabels.sql"
```

---

## Important Notes

⚠️ **WARNING:** These scripts will permanently delete data! Always:
- Backup your database first
- Test on a development/test database first
- Verify you're connected to the correct database

✅ **Best Practice:** Use Solution 1 (delete in correct order) as it's safer and doesn't require disabling constraints.




















