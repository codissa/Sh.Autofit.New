# Database Migration Instructions

## Overview
This guide walks you through migrating from legacy VehicleTypeId-based mappings to the new ConsolidatedModelId-based system.

---

## Files

1. **`migration_consolidate_vehicles_data.sql`** - Consolidates vehicle models (run first)
2. **`migration_migrate_mappings_fixed.sql`** - Migrates part mappings (run second)
3. **`migration_verify.sql`** - Comprehensive verification report (run after migration)
4. **`migration_migrate_mappings_ROLLBACK.sql`** - Emergency rollback script (if needed)

---

## Pre-Migration Checklist

✅ **Backup your database** - Always have a backup before major migrations!
```sql
BACKUP DATABASE [Sh.Autofit] TO DISK = 'C:\Backups\Sh.Autofit_PreMigration.bak'
```

✅ **Verify you're on the correct database**
```sql
SELECT DB_NAME() -- Should be 'Sh.Autofit'
```

✅ **Check current state**
```sql
-- How many mappings do you have?
SELECT
    COUNT(*) AS TotalMappings,
    SUM(CASE WHEN VehicleTypeId IS NOT NULL AND ConsolidatedModelId IS NULL THEN 1 ELSE 0 END) AS LegacyMappings,
    SUM(CASE WHEN ConsolidatedModelId IS NOT NULL THEN 1 ELSE 0 END) AS ConsolidatedMappings
FROM dbo.VehiclePartsMappings
WHERE IsActive = 1 AND IsCurrentVersion = 1;
```

✅ **Ensure no one is using the system** - Migrations should run during maintenance windows

---

## Migration Steps

### Step 1: Consolidate Vehicle Models (If Not Already Done)

**File**: `migration_consolidate_vehicles_data.sql`

This script:
- Creates `ConsolidatedVehicleModels` table
- Groups similar vehicle variants into consolidated models
- Links `VehicleTypes` to their consolidated models

**Run**:
```sql
-- Execute the entire script
-- It will show progress and statistics
```

**Expected Output**:
```
Starting vehicle consolidation...
Step 1: Creating consolidated vehicle models...
Created XXX consolidated models
Step 2: Linking vehicle types to consolidated models...
Linked XXX vehicle types
✓ Vehicle consolidation completed!
```

**Verify**:
```sql
-- All vehicle types should be linked
SELECT COUNT(*) FROM dbo.VehicleTypes WHERE ConsolidatedModelId IS NULL;
-- Should return 0 (or very few orphans)
```

---

### Step 2: Migrate Part Mappings

**File**: `migration_migrate_mappings_fixed.sql`

This script:
- Analyzes current mappings
- Creates consolidated mappings by grouping identical part mappings across vehicle variants
- Deactivates legacy mappings that have been successfully consolidated
- Preserves legacy mappings for audit trail

**Run**:
```sql
-- Execute the entire script
-- It will show detailed progress and statistics
```

**Expected Output**:
```
Starting mapping migration to consolidated models (FIXED VERSION)...

Step 1: Analyzing current mappings...
Current Mapping Statistics:
  Total Active Mappings: XXXX
  Legacy (VehicleTypeId): XXXX
  Consolidated: 0

Step 2: Creating consolidated mappings from legacy mappings...
Created XXXX new consolidated mappings.

Step 3: Deactivating superseded legacy mappings...
Deactivated XXXX legacy mappings (they have been consolidated).

Step 4: Generating migration report...
=== MIGRATION SUMMARY ===
Final Mapping Statistics:
  Total Active Current Mappings: XXXX
  Active Legacy (VehicleTypeId only): XX
  Active Consolidated (ConsolidatedModelId): XXXX
  Deactivated Legacy: XXXX

Consolidation Efficiency:
UniqueConsolidatedCombinations: XXXX
OriginalLegacyMappings: XXXX
CompressionRatio: X.XX (e.g., 3.5 means 3.5 legacy mappings became 1 consolidated)

✓ SUCCESS: All legacy mappings have been processed!
```

**Verify**:
```sql
-- Check the final state
SELECT
    COUNT(*) AS TotalActive,
    SUM(CASE WHEN ConsolidatedModelId IS NOT NULL AND VehicleTypeId IS NULL THEN 1 ELSE 0 END) AS ConsolidatedMappings,
    SUM(CASE WHEN VehicleTypeId IS NOT NULL AND ConsolidatedModelId IS NULL THEN 1 ELSE 0 END) AS RemainingLegacy
FROM dbo.VehiclePartsMappings
WHERE IsActive = 1 AND IsCurrentVersion = 1;

-- ConsolidatedMappings should be the majority
-- RemainingLegacy should be 0 or very few (orphaned vehicles only)
```

---

### Step 3: Run Verification Report

**File**: `migration_verify.sql`

This script provides a comprehensive report on:
- Vehicle consolidation status
- Mapping migration status
- Data integrity checks
- Sample data verification
- Migration metadata
- Model coupling status
- Final verdict (pass/fail)

**Run**:
```sql
-- Execute the entire script
-- Review all sections carefully
```

**Expected Output**:
```
========================================
MIGRATION VERIFICATION REPORT
========================================

1. VEHICLE CONSOLIDATION STATUS
================================
Vehicle Types:
  Total: XXXX
  Linked to Consolidated Models: XXXX
  Orphaned (Not Linked): 0
  ✓ All vehicle types are linked!

2. MAPPING MIGRATION STATUS
===========================
Active Mappings:
  Total Active: XXXX
  Consolidated (ConsolidatedModelId): XXXX
  Legacy Still Active (VehicleTypeId): 0

Deactivated by Migration:
  Legacy Mappings Deactivated: XXXX
  Compression Ratio: X.XX:1
  ✓ Consolidated mappings exist!
  ✓ All legacy mappings have been consolidated or deactivated!

3. DATA INTEGRITY CHECKS
========================
Dual Mappings (should be 0):
  Mappings with both ConsolidatedModelId AND VehicleTypeId: 0
  ✓ No dual mappings found!

Orphaned Consolidated Mappings (should be 0):
  Mappings pointing to non-existent ConsolidatedModelId: 0
  ✓ All consolidated mappings point to valid models!

Orphaned Legacy Mappings (should be 0):
  Mappings pointing to non-existent VehicleTypeId: 0
  ✓ All legacy mappings point to valid vehicle types!

...

========================================
FINAL VERDICT
========================================

✅ MIGRATION SUCCESSFUL!

All checks passed. Your database is ready for the new application version.
========================================
```

**What to Look For**:
- All sections should show ✓ (success) or ⚠ (acceptable warning)
- No ❌ (critical errors)
- Final verdict should be "MIGRATION SUCCESSFUL!"

---

## Understanding the Migration

### What Happens to Legacy Mappings?

**Before Migration**:
```
Toyota Corolla 2020 1.6L Manual Base → Part ABC123 (Active, Current)
Toyota Corolla 2021 1.6L Manual Base → Part ABC123 (Active, Current)
Toyota Corolla 2022 1.6L Manual Base → Part ABC123 (Active, Current)
```

**After Migration**:
```
Toyota Corolla 2020-2022 1.6L Manual → Part ABC123 (Active, Current, Consolidated)
Toyota Corolla 2020 1.6L Manual Base → Part ABC123 (Inactive, Current, Legacy) ← Deactivated
Toyota Corolla 2021 1.6L Manual Base → Part ABC123 (Inactive, Current, Legacy) ← Deactivated
Toyota Corolla 2022 1.6L Manual Base → Part ABC123 (Inactive, Current, Legacy) ← Deactivated
```

### Why Keep Deactivated Mappings?

- **Audit Trail**: You can see what mappings existed before
- **Rollback Capability**: Easy to undo if needed
- **Historical Data**: Preserved for reports and analysis
- **No Data Loss**: Nothing is deleted, just marked inactive

### The Fixed Approach

The original migration script failed because it tried to update `IsCurrentVersion = 0`, which violated the unique constraint:
```
UQ_VehiclePartsMappings_Current (ConsolidatedModelId, VehicleTypeId, PartItemKey, IsCurrentVersion)
```

The **fixed script** instead sets `IsActive = 0`, which:
- ✅ Doesn't trigger unique constraint violations
- ✅ Still marks mappings as superseded
- ✅ Preserves all data for audit
- ✅ Can be easily rolled back

---

## Post-Migration

### Application Deployment

After successful migration:

1. **Deploy the new application version** that uses `ConsolidatedModelId`
2. **Test thoroughly** in a staging environment first
3. **Monitor for issues** after production deployment

### Query Changes

**Old Query (Legacy)**:
```csharp
var parts = await context.VehiclePartsMappings
    .Where(m => m.VehicleTypeId == vehicleTypeId && m.IsActive && m.IsCurrentVersion)
    .ToListAsync();
```

**New Query (Consolidated)**:
```csharp
var consolidatedModelId = await context.VehicleTypes
    .Where(vt => vt.VehicleTypeId == vehicleTypeId)
    .Select(vt => vt.ConsolidatedModelId)
    .FirstOrDefaultAsync();

var parts = await context.VehiclePartsMappings
    .Where(m => m.ConsolidatedModelId == consolidatedModelId && m.IsActive && m.IsCurrentVersion)
    .ToListAsync();
```

---

## Rollback (Emergency Only)

**⚠️ WARNING**: Only use this if you need to undo the migration!

**File**: `migration_migrate_mappings_ROLLBACK.sql`

This script:
- Deletes consolidated mappings created by the migration
- Re-activates legacy mappings
- Restores the system to pre-migration state

**When to Rollback**:
- Critical bug discovered in the new system
- Data integrity issues found
- Need to revert to old application version

**Run**:
```sql
-- Execute the rollback script
-- It has a 5-second delay to prevent accidents
-- Review the output before committing
```

**After Rollback**:
```sql
-- You can re-run the migration after fixing issues
```

---

## Troubleshooting

### Issue: "Orphaned mappings could not be consolidated"

**Cause**: Some `VehicleTypes` don't have a `ConsolidatedModelId`

**Solution**:
1. Run Step 1 (vehicle consolidation) again
2. Or manually link orphaned vehicles to consolidated models:
```sql
-- Find orphaned vehicles
SELECT * FROM dbo.VehicleTypes WHERE ConsolidatedModelId IS NULL;

-- Link them to appropriate consolidated models
UPDATE dbo.VehicleTypes
SET ConsolidatedModelId = <appropriate_id>
WHERE VehicleTypeId = <orphan_id>;
```

### Issue: Migration seems to have low compression ratio

**Cause**: Your vehicles might genuinely have unique mappings

**Solution**: This is normal if:
- Different vehicle variants truly use different parts
- You have many special editions with unique part requirements

**Not a problem**: The system will work correctly either way!

### Issue: "Cannot insert duplicate key" error

**Cause**: You might be running the old migration script

**Solution**: Make sure you're running `migration_migrate_mappings_fixed.sql`, not the original version

---

## Performance Notes

- **Vehicle Consolidation**: Takes seconds to minutes depending on database size
- **Mapping Migration**: Takes seconds to minutes depending on number of mappings
- **No Downtime Required**: Scripts run quickly, but schedule during maintenance for safety
- **Rollback**: Takes seconds

---

## Support

If you encounter issues:

1. ✅ Check the script output for detailed error messages
2. ✅ Run the verification queries to understand the current state
3. ✅ Ensure you're running the **FIXED** version of the migration script
4. ✅ Check that vehicle consolidation (Step 1) completed successfully
5. ✅ Verify you have a database backup before proceeding

---

## Summary

✅ **Backup database**
✅ **Run `migration_consolidate_vehicles_data.sql`**
✅ **Verify vehicle consolidation**
✅ **Run `migration_migrate_mappings_fixed.sql`**
✅ **Run `migration_verify.sql` and check for success**
✅ **Deploy new application version**
✅ **Test thoroughly**

🎉 **You're done!** Your system now uses the efficient consolidated model approach!

---

## Quick Reference

### Migration Commands (in order):

```sql
-- 1. Backup first!
BACKUP DATABASE [Sh.Autofit] TO DISK = 'C:\Backups\Sh.Autofit_PreMigration.bak'

-- 2. Consolidate vehicles
-- Run: migration_consolidate_vehicles_data.sql

-- 3. Migrate mappings (FIXED VERSION)
-- Run: migration_migrate_mappings_fixed.sql

-- 4. Verify everything
-- Run: migration_verify.sql

-- 5. If verification passes, deploy new app version
-- 6. If verification fails, see troubleshooting section
-- 7. If critical issues, consider rollback:
-- Run: migration_migrate_mappings_ROLLBACK.sql
```
