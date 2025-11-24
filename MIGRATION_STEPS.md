# Step-by-Step Migration Execution Guide

## What This Migration Does

1. **Normalizes vehicles:** Groups vehicles with same ManufacturerCode + ModelCode + ModelName (+ other attributes) into ONE record with year range
2. **Migrates existing mappings:** Converts old VehicleTypeId-based mappings to ConsolidatedModelId-based mappings
3. **From now on:** All new mappings use the consolidated model, covering all years in the range

## Prerequisites
- [ ] SQL Server Management Studio (SSMS) or Azure Data Studio installed
- [ ] Access to your `Sh.Autofit` database
- [ ] Backup of current database (IMPORTANT!)

---

## Step 1: Backup Your Database ⚠️

**Before making ANY changes, create a backup!**

```sql
-- In SSMS, connect to your server and run:
USE master;
GO

BACKUP DATABASE [Sh.Autofit]
TO DISK = 'C:\Backups\Sh.Autofit_Before_Migration.bak'
WITH FORMAT, COMPRESSION, STATS = 10;
GO
```

✅ **Checkpoint:** Verify backup file exists and is not 0 bytes

---

## Step 2: Run Database Schema Migration

### 2.1 Open the Schema Migration Script
1. Open SSMS or Azure Data Studio
2. Connect to your SQL Server
3. Open file: `migration_consolidated_models.sql`

### 2.2 Execute the Script
```sql
-- The script will:
-- ✓ Create ConsolidatedVehicleModels table
-- ✓ Create ModelCouplings table
-- ✓ Create PartCouplings table
-- ✓ Modify VehiclePartsMappings table
-- ✓ Create stored procedures
-- ✓ Create helper functions

-- Simply click "Execute" or press F5
```

### 2.3 Verify Schema Creation
```sql
-- Run these verification queries:

-- Check new tables exist
SELECT TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME IN ('ConsolidatedVehicleModels', 'ModelCouplings', 'PartCouplings');

-- Should return 3 rows

-- Check stored procedures exist
SELECT ROUTINE_NAME
FROM INFORMATION_SCHEMA.ROUTINES
WHERE ROUTINE_TYPE = 'PROCEDURE'
  AND ROUTINE_NAME LIKE 'sp_GetConsolidated%';

-- Should return 3 rows

-- Check VehiclePartsMappings has new columns
SELECT COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'VehiclePartsMappings'
  AND COLUMN_NAME IN ('ConsolidatedModelId', 'MappingLevel');

-- Should return 2 rows
```

✅ **Checkpoint:** All tables, procedures, and columns created successfully

---

## Step 3: Run Data Migration (Consolidate Vehicles)

### 3.1 Open the Data Migration Script
1. In SSMS/Azure Data Studio
2. Open file: `migration_consolidate_vehicles_data.sql`

### 3.2 Execute the Script
```sql
-- The script will:
-- ✓ Analyze current VehicleTypes
-- ✓ Group by uniqueness key
-- ✓ Create consolidated models
-- ✓ Link original vehicles to consolidated models
-- ✓ Generate statistics

-- Click "Execute" or press F5
```

### 3.3 Review Migration Statistics
The script will output statistics. Look for:
```
Created X consolidated models.
Linked Y VehicleTypes to consolidated models.
SUCCESS: All active VehicleTypes were successfully linked
```

### 3.4 Verify Data Migration
```sql
-- Check consolidated models were created
SELECT COUNT(*) AS ConsolidatedModelCount
FROM dbo.ConsolidatedVehicleModels;

-- Check original vehicles are linked
SELECT COUNT(*) AS LinkedVehicles
FROM dbo.VehicleTypes
WHERE ConsolidatedModelId IS NOT NULL;

-- Compare counts (should be similar)
SELECT
    COUNT(*) AS OriginalVehicles,
    COUNT(DISTINCT ConsolidatedModelId) AS ConsolidatedModels,
    CAST(COUNT(*) AS FLOAT) / COUNT(DISTINCT ConsolidatedModelId) AS Ratio
FROM dbo.VehicleTypes
WHERE IsActive = 1;

-- View sample consolidated models
SELECT TOP 10
    cm.*,
    m.ManufacturerName,
    COUNT(vt.VehicleTypeId) AS VariantCount
FROM dbo.ConsolidatedVehicleModels cm
INNER JOIN dbo.Manufacturers m ON cm.ManufacturerId = m.ManufacturerId
LEFT JOIN dbo.VehicleTypes vt ON cm.ConsolidatedModelId = vt.ConsolidatedModelId
GROUP BY
    cm.ConsolidatedModelId,
    cm.ManufacturerId,
    cm.ManufacturerCode,
    cm.ModelCode,
    cm.ModelName,
    cm.YearFrom,
    cm.YearTo,
    cm.EngineVolume,
    cm.TransmissionType,
    m.ManufacturerName
ORDER BY VariantCount DESC;
```

✅ **Checkpoint:** Consolidated models created and linked to original vehicles

---

## Step 4: Migrate Existing Mappings

### 4.1 Open the Mappings Migration Script
1. In SSMS/Azure Data Studio
2. Open file: `migration_migrate_mappings.sql`

### 4.2 Execute the Script
```sql
-- The script will:
-- ✓ Analyze existing legacy mappings
-- ✓ Group identical mappings by ConsolidatedModelId + PartItemKey
-- ✓ Create ONE consolidated mapping per unique combination
-- ✓ Mark old legacy mappings as superseded (kept for audit)

-- Click "Execute" or press F5
```

### 4.3 Review Migration Report
The script will output:
```
Created X new consolidated mappings.
Marked Y legacy mappings as superseded.
SUCCESS: All legacy mappings have been consolidated!
```

### 4.4 Verify Mapping Migration
```sql
-- Check consolidated mappings were created
SELECT COUNT(*) AS ConsolidatedMappings
FROM dbo.VehiclePartsMappings
WHERE MappingLevel = 'Consolidated' AND IsActive = 1 AND IsCurrentVersion = 1;

-- Check legacy mappings are superseded
SELECT COUNT(*) AS SupersededLegacyMappings
FROM dbo.VehiclePartsMappings
WHERE MappingLevel = 'Legacy' AND IsCurrentVersion = 0;

-- View sample consolidated mappings
SELECT TOP 10
    vpm.PartItemKey,
    m.ManufacturerName,
    cm.ModelName,
    cm.YearFrom,
    cm.YearTo,
    vpm.MappingSource,
    vpm.Priority
FROM dbo.VehiclePartsMappings vpm
INNER JOIN dbo.ConsolidatedVehicleModels cm ON vpm.ConsolidatedModelId = cm.ConsolidatedModelId
INNER JOIN dbo.Manufacturers m ON cm.ManufacturerId = m.ManufacturerId
WHERE vpm.MappingLevel = 'Consolidated'
  AND vpm.IsActive = 1
  AND vpm.IsCurrentVersion = 1
ORDER BY vpm.MappingId DESC;
```

✅ **Checkpoint:** Existing mappings migrated to consolidated model format

---

## Step 5: Build the .NET Solution

### 5.1 Clean and Rebuild
```bash
# Open Command Prompt or PowerShell in the solution directory
cd "c:\Users\ASUS\source\repos\Sh.Autofit.New"

# Clean the solution
dotnet clean

# Restore NuGet packages
dotnet restore

# Build the solution
dotnet build
```

### 5.2 Check for Build Errors
- **If build succeeds:** ✅ Proceed to Step 6
- **If build fails with EF errors:** The DbContext changes might need adjustment

Common issues:
```bash
# Error: "Model validation failed"
# Solution: The database schema and EF model are out of sync
# This is normal - the entities were updated but database existed before

# You may need to comment out the new entity configurations temporarily
# and gradually uncomment them as we test
```

✅ **Checkpoint:** Solution builds without errors

---

## Step 5: Test Basic Queries (Optional but Recommended)

### 5.1 Test Consolidated Model Lookup
```sql
-- Test the lookup stored procedure
EXEC dbo.sp_GetConsolidatedModelsForLookup
    @ManufacturerCode = 7, -- Replace with actual code
    @ModelCode = 506,      -- Replace with actual code
    @Year = 2020;
```

### 5.2 Test Coupling Functions
```sql
-- Test part coupling function
-- First, create a test coupling
INSERT INTO dbo.PartCouplings (PartItemKeyA, PartItemKeyB, CouplingType, CreatedBy, IsActive)
VALUES ('PART001', 'PART002', 'Compatible', 'TEST', 1);

-- Test the function
SELECT * FROM dbo.fn_GetCoupledParts('PART001');
-- Should return both PART001 and PART002

-- Clean up test data
DELETE FROM dbo.PartCouplings WHERE CreatedBy = 'TEST';
```

✅ **Checkpoint:** Database queries work correctly

---

## Step 6: Update Stored Procedure Mappings (Code)

This is where you continue with Phase 4. You need to:

### 6.1 Find and Update ShAutofitContextProcedures.cs

**Location:** `Sh.Autofit.New.Entities\Models\ShAutofitContextProcedures.cs`

**Task:** Add method mappings for the new stored procedures

Would you like me to:
- **Option A:** Continue and implement Phase 4 (Data Access Layer) now?
- **Option B:** Stop here and let you test Steps 1-5 first?

---

## Current Progress Summary

| Step | Description | Status |
|------|-------------|--------|
| 1 | Backup database | ⏸️ **YOU DO THIS** |
| 2 | Run schema migration SQL (`migration_consolidated_models.sql`) | ⏸️ **YOU DO THIS** |
| 3 | Run vehicle consolidation SQL (`migration_consolidate_vehicles_data.sql`) | ⏸️ **YOU DO THIS** |
| 4 | Run mappings migration SQL (`migration_migrate_mappings.sql`) | ⏸️ **YOU DO THIS** |
| 5 | Build .NET solution | ⏸️ **YOU DO THIS** |
| 6 | Test database queries | ⏸️ **YOU DO THIS** (Optional) |
| 7 | Update C# code (Phase 4) | ⏳ **I CAN HELP** |
| 8 | Update ViewModels (Phase 5) | ⏳ **I CAN HELP** |
| 9 | Update UI Views (Phase 6) | ⏳ **I CAN HELP** |

---

## ⚠️ Important Notes

### What NOT to do:
- ❌ Don't run migration scripts on production without testing
- ❌ Don't skip the backup step
- ❌ Don't delete old VehicleTypes data (it's kept for audit)

### What to expect:
- ✅ Old mappings continue to work (backward compatible)
- ✅ You can create new consolidated mappings alongside old ones
- ✅ Original VehicleTypes table is unchanged (only adds ConsolidatedModelId column)

### Rollback Plan (if needed):
```sql
-- If something goes wrong, restore from backup:
USE master;
GO

ALTER DATABASE [Sh.Autofit] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
GO

RESTORE DATABASE [Sh.Autofit]
FROM DISK = 'C:\Backups\Sh.Autofit_Before_Migration.bak'
WITH REPLACE;
GO

ALTER DATABASE [Sh.Autofit] SET MULTI_USER;
GO
```

---

## What Happens Next?

After completing Steps 1-5, you have:
- ✅ New database structure ready
- ✅ Existing data consolidated
- ✅ Old system still working

**Then we proceed to update the application code (Steps 6-8)** to actually USE the new consolidated models in the UI.

---

## Need Help?

**Stuck on a step?** Let me know which step and what error you're seeing.

**Ready to continue?** Tell me when you've completed Steps 1-5 and I'll proceed with Phase 4 (updating the C# code).

**Want me to continue now?** I can implement Phase 4-6 while you prepare to run the database migrations.
