# Bulk Model Coupling Instructions

## Overview
This guide explains how to safely bulk-couple vehicle models using the provided SQL scripts.

## Files
1. **`BulkCreateModelCouplings.sql`** - Main script to create couplings (SAFE VERSION)
2. **`FindSuspiciousOverlaps.sql`** - Diagnostic script to identify problematic matches

---

## Step-by-Step Process

### Step 1: Run Suspicious Overlaps Analysis (Optional but Recommended)
Open `FindSuspiciousOverlaps.sql` and run it to see what patterns exist in your data:

```sql
-- Run each scenario separately to understand your data
-- This helps you decide if you need additional exclusions
```

**What it finds:**
- ⚠️ Small overlaps (≤3 years) - might be different generations
- 📦 Nested ranges - one model contains another
- 📅 Large gaps (≥5 years) - likely different generations
- 🏷️ Different commercial names - might be variants

### Step 2: Preview Couplings
Open `BulkCreateModelCouplings.sql` and run **STEP 1** (lines 17-108):

```sql
-- This shows you exactly what will be coupled
-- Review the results carefully!
```

**Check the output:**
- Does everything look logical?
- Are there any surprising matches?
- Note the total count

### Step 3: Execute Bulk Coupling
If the preview looks good:

1. **Uncomment** the INSERT section (remove `/*` and `*/` around STEP 2)
2. **Run** the entire section
3. **Review** the created couplings
4. **COMMIT** if everything looks good
5. **ROLLBACK** if something is wrong

```sql
-- Inside the transaction block, you can:
COMMIT TRANSACTION;     -- If everything is correct
-- OR
ROLLBACK TRANSACTION;   -- If you need to undo
```

---

## Safety Features

### ✅ Matching Criteria
Models are coupled when they have:
- Same manufacturer
- Same model name
- Same engine volume
- Same transmission type
- Same trim level (or both null)
- Overlapping year ranges

### ❌ Exclusions (Built-in Safety)
The script **automatically excludes**:
- **Large year gaps**: ≥5 years between start dates
- **Different commercial names**: Models with different commercial names

### 🔒 Additional Safety
- Uses transactions (can rollback)
- Checks for existing couplings (no duplicates)
- Ensures A < B ordering (no bidirectional duplicates)
- Shows preview before execution
- Logs all created couplings with `CreatedBy = 'BulkCouplingScript'`

---

## Customizing Exclusions

### To Add More Exclusions
Add additional `AND NOT (...)` conditions in the WHERE clause:

```sql
-- Example: Exclude specific model IDs
AND NOT (m1.ConsolidatedModelId IN (123, 456) OR m2.ConsolidatedModelId IN (123, 456))

-- Example: Only couple models with small year gaps (≤2 years)
AND (
    CASE
        WHEN m2.YearFrom > m1.YearFrom THEN m2.YearFrom - m1.YearFrom
        ELSE m1.YearFrom - m2.YearFrom
    END <= 2
)

-- Example: Exclude specific manufacturers
AND mfg.ManufacturerShortName NOT IN ('Toyota', 'Honda')
```

### To Remove Exclusions
Comment out or remove the exclusion conditions you don't want.

---

## After Bulk Coupling

### View All Auto-Created Couplings
```sql
SELECT
    mc.ModelCouplingId,
    m1.ModelName,
    m1.YearFrom AS YearFromA,
    m1.YearTo AS YearToA,
    m2.YearFrom AS YearFromB,
    m2.YearTo AS YearToB,
    mc.Notes,
    mc.CreatedAt
FROM ModelCouplings mc
INNER JOIN ConsolidatedVehicleModels m1 ON mc.ConsolidatedModelId_A = m1.ConsolidatedModelId
INNER JOIN ConsolidatedVehicleModels m2 ON mc.ConsolidatedModelId_B = m2.ConsolidatedModelId
WHERE mc.CreatedBy = 'BulkCouplingScript'
AND mc.IsActive = 1
ORDER BY mc.CreatedAt DESC;
```

### Delete All Auto-Created Couplings (if needed)
```sql
-- BE CAREFUL! This will delete all couplings created by the script
UPDATE ModelCouplings
SET IsActive = 0,
    UpdatedAt = GETDATE(),
    UpdatedBy = 'Rollback'
WHERE CreatedBy = 'BulkCouplingScript';
```

---

## Troubleshooting

### Q: Too many couplings created?
**A:** Review the exclusion criteria. You might want to be more strict:
- Reduce the year gap threshold from 5 to 3
- Require exact commercial name matches

### Q: Too few couplings created?
**A:** The exclusions might be too strict:
- Increase the year gap threshold
- Allow different commercial names for specific manufacturers
- Use the "relaxed criteria" version at the bottom of the script

### Q: Wrong models coupled together?
**A:**
1. Use `FindSuspiciousOverlaps.sql` to identify the pattern
2. Add specific exclusions for those model IDs
3. Rollback and re-run with updated criteria

---

## Performance
- Expected speed: **Very fast** (seconds for thousands of couplings)
- Uses single batch INSERT operation
- No UI overhead
- Transactional (atomic operation)

---

## Best Practices

1. ✅ **Always run the preview first**
2. ✅ **Review suspicious overlaps before bulk coupling**
3. ✅ **Start with strict criteria, then relax if needed**
4. ✅ **Keep backups before bulk operations**
5. ✅ **Test on a few manufacturers first** (add manufacturer filter)
6. ✅ **Document any manual exclusions you add**

---

## Support
If you encounter issues:
1. Check the suspicious overlaps analysis
2. Review the preview results
3. Add specific exclusions as needed
4. Test with smaller batches first
