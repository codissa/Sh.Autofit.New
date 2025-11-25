# Migration Workflow Diagram

## 📊 Complete Migration Process

```
┌─────────────────────────────────────────────────────────────────┐
│                     START: DATABASE MIGRATION                    │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│ STEP 0: PREPARATION                                             │
│ ├─ Read: MIGRATION_README.md (overview)                         │
│ ├─ Read: MIGRATION_INSTRUCTIONS.md (detailed guide)             │
│ └─ Create database backup!                                      │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│ STEP 1: VEHICLE CONSOLIDATION                                   │
│ File: migration_consolidate_vehicles_data.sql                   │
│                                                                  │
│ What it does:                                                   │
│ ├─ Groups similar vehicle variants                             │
│ ├─ Creates ConsolidatedVehicleModels table                     │
│ ├─ Links VehicleTypes → ConsolidatedModelId                    │
│ └─ Example: 5 variants of "Corolla 1.6L" → 1 consolidated     │
│                                                                  │
│ Before: VehicleTypes (standalone)                              │
│ After:  VehicleTypes → ConsolidatedVehicleModels              │
│                                                                  │
│ Status: ✅ Safe (creates new, doesn't delete)                  │
│ Time:   ⏱️ Seconds to minutes                                   │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│ STEP 2: MAPPING MIGRATION                                       │
│ File: migration_migrate_mappings_fixed.sql ⭐ USE THIS ONE      │
│                                                                  │
│ What it does:                                                   │
│ ├─ Analyzes legacy mappings (VehicleTypeId-based)             │
│ ├─ Creates consolidated mappings (ConsolidatedModelId-based)  │
│ ├─ Groups identical mappings: 5 legacy → 1 consolidated       │
│ ├─ Deactivates superseded legacy mappings (IsActive = 0)      │
│ └─ Preserves all data for audit trail                         │
│                                                                  │
│ Before: 10,000 vehicle-specific mappings (Active)             │
│ After:  3,000 consolidated mappings (Active)                   │
│         7,000 legacy mappings (Inactive, preserved)            │
│                                                                  │
│ Fixed: ✅ Resolves unique constraint violation                 │
│        ├─ Sets IsActive = 0 (not IsCurrentVersion = 0)        │
│        ├─ More specific filtering                              │
│        └─ Better conflict prevention                           │
│                                                                  │
│ Status: ✅ Safe (deactivates, doesn't delete)                  │
│ Time:   ⏱️ Seconds to minutes                                   │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│ STEP 3: VERIFICATION                                            │
│ File: migration_verify.sql                                      │
│                                                                  │
│ What it checks:                                                 │
│ ├─ ✓ Vehicle consolidation status                              │
│ ├─ ✓ Mapping migration status                                  │
│ ├─ ✓ Data integrity (no orphans, no corruption)                │
│ ├─ ✓ Sample data verification                                  │
│ ├─ ✓ Migration metadata                                        │
│ └─ ✓ Final verdict: PASS/WARN/FAIL                             │
│                                                                  │
│ Expected Result:                                                │
│ ┌──────────────────────────────────────┐                       │
│ │ ✅ MIGRATION SUCCESSFUL!             │                       │
│ │ All checks passed.                   │                       │
│ │ Ready for deployment.                │                       │
│ └──────────────────────────────────────┘                       │
│                                                                  │
│ Status: ✅ Read-only                                            │
│ Time:   ⏱️ Seconds                                               │
└─────────────────────────────────────────────────────────────────┘
                              ↓
                   ┌──────────┴──────────┐
                   │                     │
              ✅ SUCCESS            ❌ ISSUES FOUND
                   │                     │
                   ↓                     ↓
    ┌──────────────────────┐   ┌──────────────────────┐
    │ STEP 4: DEPLOYMENT   │   │ TROUBLESHOOTING      │
    │                      │   │                      │
    │ ├─ Deploy new app    │   │ ├─ Review errors     │
    │ ├─ Test staging      │   │ ├─ Check MIGRATION_  │
    │ ├─ Deploy production │   │ │   INSTRUCTIONS.md  │
    │ └─ Monitor           │   │ ├─ Re-run if needed  │
    │                      │   │ └─ OR rollback ↓     │
    └──────────────────────┘   └──────────────────────┘
                   │                     │
                   │                     ↓
                   │         ┌──────────────────────────┐
                   │         │ EMERGENCY ROLLBACK       │
                   │         │ File: migration_migrate_ │
                   │         │       mappings_ROLLBACK  │
                   │         │                          │
                   │         │ ⚠️ WARNING: 5 sec delay  │
                   │         │                          │
                   │         │ What it does:            │
                   │         │ ├─ Deletes consolidated  │
                   │         │ ├─ Re-activates legacy   │
                   │         │ └─ Restores pre-migrate  │
                   │         │                          │
                   │         │ Use only if critical!    │
                   │         └──────────────────────────┘
                   │                     │
                   │                     ↓
                   │         ┌──────────────────────────┐
                   │         │ Fix issues & retry       │
                   │         └──────────────────────────┘
                   │
                   ↓
┌─────────────────────────────────────────────────────────────────┐
│ OPTIONAL: BULK COUPLING (After Migration)                       │
│                                                                  │
│ Purpose: Link related vehicle models to share mappings          │
│ Example: Corolla 2020 ↔ Corolla 2021 (same specs)              │
└─────────────────────────────────────────────────────────────────┘
                   ↓
┌─────────────────────────────────────────────────────────────────┐
│ STEP A: ANALYZE SUSPICIOUS OVERLAPS (Optional)                  │
│ File: FindSuspiciousOverlaps.sql                                │
│                                                                  │
│ What it finds:                                                  │
│ ├─ ⚠️ Small overlaps (≤3 years) - might be different gens      │
│ ├─ 📦 Nested ranges - one model contains another               │
│ ├─ 📅 Large gaps (≥5 years) - likely different gens            │
│ └─ 🏷️ Different commercial names - might be variants           │
│                                                                  │
│ Status: ✅ Read-only diagnostic                                 │
│ Time:   ⏱️ Seconds                                               │
└─────────────────────────────────────────────────────────────────┘
                   ↓
┌─────────────────────────────────────────────────────────────────┐
│ STEP B: BULK CREATE COUPLINGS                                   │
│ File: BulkCreateModelCouplings.sql                              │
│ Guide: COUPLING_INSTRUCTIONS.md                                 │
│                                                                  │
│ What it does:                                                   │
│ ├─ STEP 1: Preview matches (same specs, overlapping years)    │
│ ├─ STEP 2: INSERT couplings (transactional)                   │
│ └─ Automatic exclusions:                                       │
│    ├─ ❌ Large year gaps (≥5 years)                            │
│    └─ ❌ Different commercial names                            │
│                                                                  │
│ Matching Criteria:                                              │
│ ├─ Same manufacturer                                           │
│ ├─ Same model name                                             │
│ ├─ Same engine volume                                          │
│ ├─ Same transmission type                                      │
│ ├─ Same trim level (or both null)                              │
│ └─ Overlapping year ranges                                     │
│                                                                  │
│ Status: ✅ Safe (preview first, transactional)                  │
│ Time:   ⏱️ Seconds (much faster than UI!)                       │
└─────────────────────────────────────────────────────────────────┘
                   ↓
┌─────────────────────────────────────────────────────────────────┐
│                      🎉 MIGRATION COMPLETE!                      │
│                                                                  │
│ Your system now uses:                                           │
│ ✅ Consolidated vehicle models                                  │
│ ✅ Simplified part mappings                                     │
│ ✅ Efficient data structure                                     │
│ ✅ Model couplings for sharing mappings                         │
│                                                                  │
│ Benefits:                                                       │
│ ├─ 📉 Fewer duplicate mappings (3:1 compression typical)       │
│ ├─ 🚀 Better performance                                        │
│ ├─ 🔧 Easier maintenance                                        │
│ ├─ 🔍 Preserved audit trail                                     │
│ └─ 🔄 Easy rollback if needed                                   │
└─────────────────────────────────────────────────────────────────┘
```

---

## 📁 File Reference

### Documentation
- **MIGRATION_README.md** - Start here! Package overview
- **MIGRATION_INSTRUCTIONS.md** - Detailed step-by-step guide
- **COUPLING_INSTRUCTIONS.md** - Bulk coupling guide
- **MIGRATION_WORKFLOW.md** - This file (visual workflow)

### Core Migration Scripts (Run in Order)
1. **migration_consolidate_vehicles_data.sql** - Vehicle consolidation
2. **migration_migrate_mappings_fixed.sql** - Mapping migration (FIXED)
3. **migration_verify.sql** - Verification report

### Safety & Utilities
- **migration_migrate_mappings_ROLLBACK.sql** - Emergency rollback
- **FindSuspiciousOverlaps.sql** - Diagnostic (for coupling)
- **BulkCreateModelCouplings.sql** - Bulk coupling (optional)

### ❌ Deprecated (Do Not Use)
- ~~migration_migrate_mappings.sql~~ - Old version with bug

---

## 🎯 Quick Decision Tree

```
Do you need to migrate from VehicleTypeId to ConsolidatedModelId?
│
├─ YES
│  │
│  ├─ Read MIGRATION_README.md
│  ├─ Read MIGRATION_INSTRUCTIONS.md
│  ├─ Backup database
│  ├─ Run migration_consolidate_vehicles_data.sql
│  ├─ Run migration_migrate_mappings_fixed.sql
│  ├─ Run migration_verify.sql
│  │
│  └─ Did verification pass?
│     │
│     ├─ YES ✅
│     │  ├─ Deploy new app version
│     │  └─ Optionally: Bulk couple models (see below)
│     │
│     └─ NO ❌
│        ├─ Review troubleshooting section
│        ├─ Re-run if fixable
│        └─ Rollback if critical
│
└─ NO (Already migrated)
   │
   └─ Want to bulk-couple models?
      │
      ├─ YES
      │  ├─ Read COUPLING_INSTRUCTIONS.md
      │  ├─ Optionally: Run FindSuspiciousOverlaps.sql
      │  └─ Run BulkCreateModelCouplings.sql
      │
      └─ NO
         └─ You're all set! 🎉
```

---

## 📊 Data Flow Diagram

### Before Migration
```
┌──────────────────┐
│  VehicleTypes    │
│  - VehicleTypeId │ ──┐
│  - MakeName      │   │
│  - ModelName     │   │   ┌─────────────────────────────┐
│  - Year          │   │   │ VehiclePartsMappings        │
│  - EngineVolume  │   └──→│ - VehicleTypeId (FK) ✓      │
│  - Transmission  │       │ - ConsolidatedModelId ✗     │
│  - TrimLevel     │       │ - PartItemKey               │
└──────────────────┘       │ - IsActive                  │
                           │ - IsCurrentVersion          │
                           └─────────────────────────────┘

Problem: Many VehicleTypes → Many duplicate mappings
Example: 5 variants × 200 parts = 1,000 mappings for same model
```

### After Migration
```
┌──────────────────┐
│  VehicleTypes    │       ┌─────────────────────────────┐
│  - VehicleTypeId │       │ ConsolidatedVehicleModels   │
│  - MakeName      │   ┌──→│ - ConsolidatedModelId       │
│  - ModelName     │   │   │ - ManufacturerId            │
│  - Year          │   │   │ - ModelName                 │
│  - EngineVolume  │   │   │ - YearFrom, YearTo          │
│  - Transmission  │   │   │ - EngineVolume              │
│  - TrimLevel     │   │   │ - TransmissionType          │
│  - Consolidated  │───┘   └─────────────────────────────┘
│    ModelId (FK)  │                    │
└──────────────────┘                    │
                                        │
                         ┌──────────────┴───────────────────┐
                         │ VehiclePartsMappings             │
                         │ - VehicleTypeId ✗ (deactivated) │
                         │ - ConsolidatedModelId (FK) ✓    │
                         │ - PartItemKey                    │
                         │ - IsActive                       │
                         │ - IsCurrentVersion               │
                         └──────────────────────────────────┘

Solution: Many VehicleTypes → One ConsolidatedModel → Fewer mappings
Example: 5 variants → 1 consolidated × 200 parts = 200 mappings
Result: 80% reduction (1,000 → 200 mappings)
```

---

## ⏱️ Time Estimates

| Step | Task | Time | Can Skip? |
|------|------|------|-----------|
| 0 | Read documentation | 15 min | ⚠️ No |
| 1 | Backup database | 1-5 min | ⚠️ No |
| 2 | Vehicle consolidation | 1-5 min | ⚠️ No |
| 3 | Mapping migration | 1-5 min | ⚠️ No |
| 4 | Verification | 30 sec | ⚠️ No |
| 5 | Deploy & test | Variable | ⚠️ No |
| 6 | Bulk coupling (optional) | 1-2 min | ✅ Yes |

**Total Core Migration**: 15-30 minutes
**Total with Optional**: 20-35 minutes

---

## 🆘 Support & Troubleshooting

### Common Errors

| Error | File | Solution |
|-------|------|----------|
| Duplicate key: UQ_VehiclePartsMappings_Current | migration_migrate_mappings.sql | Use `migration_migrate_mappings_fixed.sql` instead |
| Orphaned vehicles | migration_consolidate_vehicles_data.sql | Re-run vehicle consolidation |
| Verification fails | migration_verify.sql | Review detailed report, check troubleshooting guide |
| Low compression ratio | migration_migrate_mappings_fixed.sql | Normal if vehicles genuinely have unique mappings |

### Where to Get Help

1. **MIGRATION_INSTRUCTIONS.md** - Detailed troubleshooting section
2. **migration_verify.sql** - Identifies specific issues
3. **COUPLING_INSTRUCTIONS.md** - Coupling-specific help

---

*Visual workflow diagram for database migration*
*Version: 2.0 (Fixed)*
*Last Updated: 2025-11-25*
