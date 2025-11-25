# Database Migration Package

## 📦 What's Included

This package contains everything you need to migrate from the legacy VehicleTypeId-based system to the new ConsolidatedModelId-based system.

---

## 📄 Files

### Core Migration Scripts

1. **[MIGRATION_INSTRUCTIONS.md](MIGRATION_INSTRUCTIONS.md)** ⭐ START HERE
   - Complete step-by-step guide
   - Detailed explanations
   - Troubleshooting tips
   - Best practices

2. **[migration_consolidate_vehicles_data.sql](migration_consolidate_vehicles_data.sql)**
   - **Purpose**: Groups similar vehicle variants into consolidated models
   - **When**: Run FIRST, before mapping migration
   - **Safe**: Read-only analysis + creates new records (doesn't modify existing data)
   - **Time**: Seconds to minutes

3. **[migration_migrate_mappings_fixed.sql](migration_migrate_mappings_fixed.sql)** ✅ FIXED VERSION
   - **Purpose**: Migrates part mappings from VehicleTypeId to ConsolidatedModelId
   - **When**: Run SECOND, after vehicle consolidation
   - **Safe**: Deactivates old mappings (doesn't delete), creates new ones
   - **Time**: Seconds to minutes
   - **Fixed**: Resolves unique constraint violation by setting IsActive=0 instead of updating IsCurrentVersion

4. **[migration_verify.sql](migration_verify.sql)**
   - **Purpose**: Comprehensive verification report
   - **When**: Run AFTER migration to verify success
   - **Safe**: Read-only, doesn't modify any data
   - **Output**: Detailed report with ✓/⚠/❌ status for each check

### Safety & Utilities

5. **[migration_migrate_mappings_ROLLBACK.sql](migration_migrate_mappings_ROLLBACK.sql)** ⚠️ EMERGENCY ONLY
   - **Purpose**: Undo the mapping migration
   - **When**: Only if critical issues found after migration
   - **Warning**: 5-second delay to prevent accidents
   - **Safe**: Uses transactions, can be reviewed before committing

### Bulk Coupling (Optional)

6. **[BulkCreateModelCouplings.sql](BulkCreateModelCouplings.sql)**
   - **Purpose**: Bulk-couple similar vehicle models to share part mappings
   - **When**: Run AFTER migration is complete and verified
   - **Safe**: Preview mode first, then transactional INSERT
   - **Exclusions**: Automatically excludes suspicious patterns (large year gaps, different commercial names)

7. **[FindSuspiciousOverlaps.sql](FindSuspiciousOverlaps.sql)**
   - **Purpose**: Identify potentially problematic coupling candidates
   - **When**: Run BEFORE bulk coupling to review patterns
   - **Safe**: Read-only diagnostic script

8. **[COUPLING_INSTRUCTIONS.md](COUPLING_INSTRUCTIONS.md)**
   - Complete guide for bulk coupling
   - Safety features and exclusion criteria
   - Customization examples

---

## 🚀 Quick Start

### Option 1: Step-by-Step (Recommended for First Time)

Read **[MIGRATION_INSTRUCTIONS.md](MIGRATION_INSTRUCTIONS.md)** and follow each step.

### Option 2: Quick Migration (If You Know What You're Doing)

```sql
-- 1. BACKUP FIRST!
BACKUP DATABASE [Sh.Autofit] TO DISK = 'C:\Backups\Sh.Autofit_PreMigration.bak'

-- 2. Run vehicle consolidation
-- Execute: migration_consolidate_vehicles_data.sql

-- 3. Run mapping migration (FIXED VERSION)
-- Execute: migration_migrate_mappings_fixed.sql

-- 4. Verify success
-- Execute: migration_verify.sql
-- Look for: ✅ MIGRATION SUCCESSFUL!

-- 5. Deploy new app version and test
```

---

## ⚠️ Important Notes

### Which Migration Script to Use?

**USE**: `migration_migrate_mappings_fixed.sql` ✅

**DON'T USE**: `migration_migrate_mappings.sql` (original version with unique constraint bug)

The **fixed version** resolves the unique constraint violation error by:
- Setting `IsActive = 0` instead of updating `IsCurrentVersion`
- More specific filtering to avoid conflicts
- Better error handling and reporting

### Error You Might Have Seen

```
Msg 2601, Level 14, State 1, Line 140
Cannot insert duplicate key row in object 'dbo.VehiclePartsMappings'
with unique index 'UQ_VehiclePartsMappings_Current'
```

**Solution**: Use `migration_migrate_mappings_fixed.sql` instead.

---

## 📊 What the Migration Does

### Before Migration
```
Vehicle-specific mappings:
- Toyota Corolla 2020 1.6L MT Base → Part ABC (Active)
- Toyota Corolla 2021 1.6L MT Base → Part ABC (Active)
- Toyota Corolla 2022 1.6L MT Base → Part ABC (Active)

Result: 3 mappings for the same part
```

### After Migration
```
Consolidated mapping:
- Toyota Corolla 2020-2022 1.6L MT → Part ABC (Active, Consolidated) ✅

Legacy mappings (preserved for audit):
- Toyota Corolla 2020 1.6L MT Base → Part ABC (Inactive, Legacy)
- Toyota Corolla 2021 1.6L MT Base → Part ABC (Inactive, Legacy)
- Toyota Corolla 2022 1.6L MT Base → Part ABC (Inactive, Legacy)

Result: 1 active mapping (3 deactivated but preserved)
Compression: 3:1 ratio
```

**Benefits**:
- ✅ Simpler data model
- ✅ Easier maintenance
- ✅ Better performance
- ✅ Fewer duplicates
- ✅ Still have audit trail

---

## 🔍 Verification

After running the migration, the verification script checks:

1. ✓ **Vehicle Consolidation**: All vehicles linked to consolidated models
2. ✓ **Mapping Migration**: Legacy mappings deactivated, consolidated mappings active
3. ✓ **Data Integrity**: No orphaned or corrupted records
4. ✓ **Sample Data**: Consolidated models have parts and variants
5. ✓ **Metadata**: Migration timestamp recorded
6. ✓ **Couplings**: Model couplings status (if applicable)

**Final Verdict**:
- ✅ MIGRATION SUCCESSFUL! → Ready to deploy
- ⚠ MIGRATION MOSTLY SUCCESSFUL → Review warnings
- ❌ MIGRATION INCOMPLETE OR FAILED → See troubleshooting

---

## 🆘 Support

### If Something Goes Wrong

1. **Check the verification report** (`migration_verify.sql`)
2. **Review troubleshooting section** in MIGRATION_INSTRUCTIONS.md
3. **Consider rollback** if critical issues found
4. **Verify you're using the FIXED version** of the migration script

### Common Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| Duplicate key error | Using old migration script | Use `migration_migrate_mappings_fixed.sql` |
| Orphaned mappings | Vehicle consolidation incomplete | Re-run `migration_consolidate_vehicles_data.sql` |
| Low compression ratio | Genuine unique mappings | This is normal, not a problem |
| Verification fails | Migration didn't complete | Review script output for errors |

---

## 📈 Performance

- **Vehicle Consolidation**: Seconds to minutes
- **Mapping Migration**: Seconds to minutes
- **Verification**: Seconds
- **Rollback**: Seconds

**Downtime**: Not required, but recommended to schedule during maintenance window for safety.

---

## 🎯 Next Steps After Migration

1. ✅ **Deploy new application version** that uses ConsolidatedModelId
2. ✅ **Test thoroughly** in staging first
3. ✅ **Monitor production** for any issues
4. ✅ **Consider bulk coupling** to link related vehicle models
5. ✅ **Celebrate!** 🎉 You've successfully modernized your data model

---

## 📚 Additional Resources

- **Model Coupling**: See [COUPLING_INSTRUCTIONS.md](COUPLING_INSTRUCTIONS.md)
- **Bulk Operations**: Use `BulkCreateModelCouplings.sql` for efficient bulk coupling
- **Diagnostics**: Use `FindSuspiciousOverlaps.sql` to identify problematic patterns

---

## 📝 Change Log

### Version 2.0 (Fixed)
- ✅ Resolved unique constraint violation
- ✅ Changed approach: `IsActive = 0` instead of `IsCurrentVersion = 0`
- ✅ Added comprehensive verification script
- ✅ Added rollback script
- ✅ Improved error handling and reporting

### Version 1.0 (Original)
- ❌ Had unique constraint violation bug
- ❌ Updated `IsCurrentVersion` causing conflicts
- **Status**: Deprecated, use Version 2.0 instead

---

## ✅ Pre-Flight Checklist

Before running the migration:

- [ ] Database backup completed
- [ ] Verified you're on the correct database (Sh.Autofit)
- [ ] No users actively using the system
- [ ] Using the **FIXED** version of migration script
- [ ] Read through MIGRATION_INSTRUCTIONS.md
- [ ] Have time to complete all steps (15-30 minutes)
- [ ] Can rollback if needed

**Ready?** Start with [MIGRATION_INSTRUCTIONS.md](MIGRATION_INSTRUCTIONS.md)!

---

*Last Updated: 2025-11-25*
*Migration Package Version: 2.0 (Fixed)*
