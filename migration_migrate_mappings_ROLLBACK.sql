-- =============================================
-- ROLLBACK SCRIPT: Undo Mapping Migration
-- Database: Sh.Autofit
-- Description: Re-activates legacy mappings and removes consolidated ones
-- USE WITH CAUTION: Only run this if you need to undo the migration
-- =============================================

USE [Sh.Autofit];
GO

PRINT 'WARNING: This will undo the mapping migration!';
PRINT 'Press Ctrl+C within 5 seconds to cancel...';
PRINT '';
GO

WAITFOR DELAY '00:00:05';
GO

PRINT 'Starting rollback of mapping migration...';
PRINT '';
GO

BEGIN TRANSACTION;

-- =============================================
-- STEP 1: Remove consolidated mappings created by migration
-- =============================================

PRINT 'Step 1: Removing consolidated mappings created by migration...';
GO

DELETE FROM dbo.VehiclePartsMappings
WHERE IsActive = 1
  AND IsCurrentVersion = 1
  AND ConsolidatedModelId IS NOT NULL
  AND VehicleTypeId IS NULL
  AND UpdatedBy = 'MIGRATION'
  AND MappingSource = 'Migrated';

DECLARE @DeletedConsolidated INT = @@ROWCOUNT;
PRINT 'Deleted ' + CAST(@DeletedConsolidated AS NVARCHAR) + ' consolidated mappings.';
PRINT '';
GO

-- =============================================
-- STEP 2: Re-activate legacy mappings
-- =============================================

PRINT 'Step 2: Re-activating legacy mappings...';
GO

UPDATE dbo.VehiclePartsMappings
SET
    IsActive = 1,
    MappingLevel = NULL,  -- Remove 'Legacy' marker
    UpdatedAt = GETDATE(),
    UpdatedBy = 'MIGRATION_ROLLBACK'
WHERE IsActive = 0
  AND IsCurrentVersion = 1
  AND VehicleTypeId IS NOT NULL
  AND ConsolidatedModelId IS NULL
  AND UpdatedBy = 'MIGRATION';

DECLARE @ReactivatedLegacy INT = @@ROWCOUNT;
PRINT 'Re-activated ' + CAST(@ReactivatedLegacy AS NVARCHAR) + ' legacy mappings.';
PRINT '';
GO

-- =============================================
-- STEP 3: Verify rollback
-- =============================================

PRINT 'Step 3: Verifying rollback...';
PRINT '';
GO

DECLARE @ActiveLegacy INT;
DECLARE @ActiveConsolidated INT;

SELECT @ActiveLegacy = COUNT(*)
FROM dbo.VehiclePartsMappings
WHERE IsActive = 1
  AND IsCurrentVersion = 1
  AND VehicleTypeId IS NOT NULL
  AND ConsolidatedModelId IS NULL;

SELECT @ActiveConsolidated = COUNT(*)
FROM dbo.VehiclePartsMappings
WHERE IsActive = 1
  AND IsCurrentVersion = 1
  AND ConsolidatedModelId IS NOT NULL
  AND VehicleTypeId IS NULL
  AND UpdatedBy = 'MIGRATION';

PRINT 'Rollback Verification:';
PRINT '  Active Legacy Mappings: ' + CAST(@ActiveLegacy AS NVARCHAR);
PRINT '  Remaining Migrated Consolidated Mappings: ' + CAST(@ActiveConsolidated AS NVARCHAR);
PRINT '';

IF @ActiveConsolidated = 0
BEGIN
    PRINT '✓ Rollback completed successfully!';
    PRINT '  All migration-created consolidated mappings have been removed.';
    PRINT '  Legacy mappings have been re-activated.';
    PRINT '';
    PRINT 'COMMIT the transaction if everything looks correct.';
    PRINT 'ROLLBACK the transaction if something went wrong.';
END
ELSE
BEGIN
    PRINT '⚠ WARNING: Some consolidated mappings from migration still exist!';
    PRINT '  Review the data before committing.';
END

PRINT '';
PRINT '-- To finalize the rollback, run:';
PRINT '-- COMMIT TRANSACTION;';
PRINT '';
PRINT '-- To undo this rollback operation, run:';
PRINT '-- ROLLBACK TRANSACTION;';
PRINT '';

-- DO NOT AUTO-COMMIT - Let user review and decide
-- COMMIT TRANSACTION;
