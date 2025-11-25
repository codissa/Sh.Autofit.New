-- =============================================
-- DATA MIGRATION: Migrate Existing Mappings to Consolidated Models (FIXED)
-- Database: Sh.Autofit
-- Description: Convert legacy VehicleTypeId-based mappings to ConsolidatedModelId-based
-- RUN THIS AFTER: migration_consolidate_vehicles_data.sql
-- =============================================

USE [Sh.Autofit];
GO

PRINT 'Starting mapping migration to consolidated models (FIXED VERSION)...';
PRINT '';
GO

-- =============================================
-- STEP 1: ANALYZE CURRENT MAPPINGS
-- =============================================

PRINT 'Step 1: Analyzing current mappings...';
GO

DECLARE @TotalMappings INT;
DECLARE @LegacyMappings INT;
DECLARE @ConsolidatedMappings INT;

SELECT @TotalMappings = COUNT(*) FROM dbo.VehiclePartsMappings WHERE IsActive = 1 AND IsCurrentVersion = 1;
SELECT @LegacyMappings = COUNT(*) FROM dbo.VehiclePartsMappings WHERE IsActive = 1 AND IsCurrentVersion = 1 AND VehicleTypeId IS NOT NULL;
SELECT @ConsolidatedMappings = COUNT(*) FROM dbo.VehiclePartsMappings WHERE IsActive = 1 AND IsCurrentVersion = 1 AND ConsolidatedModelId IS NOT NULL;

PRINT 'Current Mapping Statistics:';
PRINT '  Total Active Mappings: ' + CAST(@TotalMappings AS NVARCHAR);
PRINT '  Legacy (VehicleTypeId): ' + CAST(@LegacyMappings AS NVARCHAR);
PRINT '  Consolidated: ' + CAST(@ConsolidatedMappings AS NVARCHAR);
PRINT '';
GO

-- =============================================
-- STEP 2: CREATE CONSOLIDATED MAPPINGS FROM LEGACY ONES
-- =============================================

PRINT 'Step 2: Creating consolidated mappings from legacy mappings...';
PRINT 'This groups identical part mappings across vehicle variants into single consolidated mappings.';
PRINT '';
GO

-- For each unique combination of (ConsolidatedModelId + PartItemKey),
-- create ONE consolidated mapping by taking the best priority/score from all variants

;WITH MappingsToConsolidate AS (
    SELECT
        vt.ConsolidatedModelId,
        vpm.PartItemKey,
        MAX(vpm.Priority) AS Priority,
        MAX(vpm.ConfidenceScore) AS ConfidenceScore,
        MIN(vpm.FitsYearFrom) AS FitsYearFrom,
        MAX(vpm.FitsYearTo) AS FitsYearTo,
        MAX(CAST(vpm.RequiresModification AS INT)) AS RequiresModification,
        MAX(vpm.CompatibilityNotes) AS CompatibilityNotes,
        MAX(vpm.InstallationNotes) AS InstallationNotes,
        'Migrated' AS MappingSource,
        MAX(vpm.CreatedBy) AS CreatedBy,
        MIN(vpm.CreatedAt) AS CreatedAt,
        COUNT(*) AS OriginalMappingCount
    FROM dbo.VehiclePartsMappings vpm
    INNER JOIN dbo.VehicleTypes vt ON vpm.VehicleTypeId = vt.VehicleTypeId
    WHERE vpm.IsActive = 1
      AND vpm.IsCurrentVersion = 1
      AND vpm.VehicleTypeId IS NOT NULL
      AND vt.ConsolidatedModelId IS NOT NULL
    GROUP BY vt.ConsolidatedModelId, vpm.PartItemKey
)
INSERT INTO dbo.VehiclePartsMappings (
    ConsolidatedModelId,
    VehicleTypeId,
    PartItemKey,
    MappingSource,
    ConfidenceScore,
    Priority,
    FitsYearFrom,
    FitsYearTo,
    RequiresModification,
    CompatibilityNotes,
    InstallationNotes,
    VersionNumber,
    IsCurrentVersion,
    MappingLevel,
    CreatedAt,
    CreatedBy,
    UpdatedAt,
    UpdatedBy,
    IsActive
)
SELECT
    ConsolidatedModelId,
    NULL AS VehicleTypeId,  -- New consolidated mapping doesn't use VehicleTypeId
    PartItemKey,
    'Migrated' AS MappingSource,
    ConfidenceScore,
    Priority,
    FitsYearFrom,
    FitsYearTo,
    RequiresModification,
    CompatibilityNotes,
    InstallationNotes,
    1 AS VersionNumber,
    1 AS IsCurrentVersion,
    'Consolidated' AS MappingLevel,
    CreatedAt,
    CreatedBy,
    GETDATE() AS UpdatedAt,
    'MIGRATION' AS UpdatedBy,
    1 AS IsActive
FROM MappingsToConsolidate
WHERE NOT EXISTS (
    -- Don't create if consolidated mapping already exists
    SELECT 1 FROM dbo.VehiclePartsMappings existing
    WHERE existing.ConsolidatedModelId = MappingsToConsolidate.ConsolidatedModelId
      AND existing.PartItemKey = MappingsToConsolidate.PartItemKey
      AND existing.IsActive = 1
      AND existing.IsCurrentVersion = 1
      AND existing.ConsolidatedModelId IS NOT NULL  -- Must be a consolidated mapping
);

DECLARE @NewConsolidatedMappings INT = @@ROWCOUNT;
PRINT 'Created ' + CAST(@NewConsolidatedMappings AS NVARCHAR) + ' new consolidated mappings.';
PRINT '';
GO

-- =============================================
-- STEP 3: DEACTIVATE OLD LEGACY MAPPINGS (FIXED APPROACH)
-- =============================================

PRINT 'Step 3: Deactivating superseded legacy mappings...';
PRINT 'Note: Instead of updating IsCurrentVersion (which causes unique constraint issues),';
PRINT '      we will simply set IsActive = 0 for legacy mappings that have been consolidated.';
PRINT '';
GO

-- Deactivate legacy mappings that have been successfully consolidated
UPDATE vpm
SET
    vpm.IsActive = 0,  -- Deactivate instead of marking as not current
    vpm.MappingLevel = 'Legacy',
    vpm.UpdatedAt = GETDATE(),
    vpm.UpdatedBy = 'MIGRATION'
FROM dbo.VehiclePartsMappings vpm
INNER JOIN dbo.VehicleTypes vt ON vpm.VehicleTypeId = vt.VehicleTypeId
WHERE vpm.IsActive = 1
  AND vpm.IsCurrentVersion = 1
  AND vpm.VehicleTypeId IS NOT NULL
  AND vpm.ConsolidatedModelId IS NULL  -- Only legacy mappings (no ConsolidatedModelId)
  AND vt.ConsolidatedModelId IS NOT NULL
  -- Only deactivate if a consolidated mapping exists
  AND EXISTS (
      SELECT 1 FROM dbo.VehiclePartsMappings consolidated
      WHERE consolidated.ConsolidatedModelId = vt.ConsolidatedModelId
        AND consolidated.PartItemKey = vpm.PartItemKey
        AND consolidated.IsActive = 1
        AND consolidated.IsCurrentVersion = 1
        AND consolidated.ConsolidatedModelId IS NOT NULL
        AND consolidated.VehicleTypeId IS NULL  -- Must be a pure consolidated mapping
  );

DECLARE @DeactivatedMappings INT = @@ROWCOUNT;
PRINT 'Deactivated ' + CAST(@DeactivatedMappings AS NVARCHAR) + ' legacy mappings (they have been consolidated).';
PRINT '';
GO

-- =============================================
-- STEP 4: GENERATE MIGRATION REPORT
-- =============================================

PRINT 'Step 4: Generating migration report...';
PRINT '';
GO

PRINT '=== MIGRATION SUMMARY ===';
PRINT '';

-- Final counts
DECLARE @FinalTotal INT;
DECLARE @FinalActiveLegacy INT;
DECLARE @FinalActiveConsolidated INT;
DECLARE @CurrentLegacy INT;
DECLARE @InactiveLegacy INT;

SELECT @FinalTotal = COUNT(*) FROM dbo.VehiclePartsMappings WHERE IsActive = 1 AND IsCurrentVersion = 1;
SELECT @FinalActiveLegacy = COUNT(*)
FROM dbo.VehiclePartsMappings
WHERE IsActive = 1
  AND IsCurrentVersion = 1
  AND VehicleTypeId IS NOT NULL
  AND ConsolidatedModelId IS NULL;

SELECT @FinalActiveConsolidated = COUNT(*)
FROM dbo.VehiclePartsMappings
WHERE IsActive = 1
  AND IsCurrentVersion = 1
  AND ConsolidatedModelId IS NOT NULL
  AND VehicleTypeId IS NULL;

SELECT @InactiveLegacy = COUNT(*)
FROM dbo.VehiclePartsMappings
WHERE IsActive = 0
  AND VehicleTypeId IS NOT NULL
  AND ConsolidatedModelId IS NULL
  AND UpdatedBy = 'MIGRATION';

PRINT 'Final Mapping Statistics:';
PRINT '  Total Active Current Mappings: ' + CAST(@FinalTotal AS NVARCHAR);
PRINT '  Active Legacy (VehicleTypeId only): ' + CAST(@FinalActiveLegacy AS NVARCHAR);
PRINT '  Active Consolidated (ConsolidatedModelId): ' + CAST(@FinalActiveConsolidated AS NVARCHAR);
PRINT '  Deactivated Legacy: ' + CAST(@InactiveLegacy AS NVARCHAR);
PRINT '';

-- Consolidation statistics
PRINT 'Consolidation Efficiency:';
SELECT
    COUNT(DISTINCT CONCAT(vt.ConsolidatedModelId, '-', vpm.PartItemKey)) AS UniqueConsolidatedCombinations,
    COUNT(*) AS OriginalLegacyMappings,
    CAST(COUNT(*) AS FLOAT) / NULLIF(COUNT(DISTINCT CONCAT(vt.ConsolidatedModelId, '-', vpm.PartItemKey)), 0) AS CompressionRatio
FROM dbo.VehiclePartsMappings vpm
INNER JOIN dbo.VehicleTypes vt ON vpm.VehicleTypeId = vt.VehicleTypeId
WHERE vpm.IsActive = 0
  AND vpm.UpdatedBy = 'MIGRATION'
  AND vt.ConsolidatedModelId IS NOT NULL;

-- Orphaned mappings (legacy mappings without consolidated model link)
DECLARE @OrphanedMappings INT;
SELECT @OrphanedMappings = COUNT(*)
FROM dbo.VehiclePartsMappings vpm
INNER JOIN dbo.VehicleTypes vt ON vpm.VehicleTypeId = vt.VehicleTypeId
WHERE vpm.IsActive = 1
  AND vpm.IsCurrentVersion = 1
  AND vpm.VehicleTypeId IS NOT NULL
  AND vpm.ConsolidatedModelId IS NULL
  AND vt.ConsolidatedModelId IS NULL;

IF @OrphanedMappings > 0
BEGIN
    PRINT '';
    PRINT 'WARNING: ' + CAST(@OrphanedMappings AS NVARCHAR) + ' legacy mappings could not be consolidated';
    PRINT '(Their VehicleTypes are not linked to ConsolidatedVehicleModels)';
    PRINT 'These mappings remain active as Legacy and still work.';
END
ELSE
BEGIN
    PRINT '';
    PRINT 'SUCCESS: All legacy mappings have been processed!';
END

-- Show top consolidated mappings
PRINT '';
PRINT 'Top 10 parts with most consolidated variants:';
SELECT TOP 10
    vpm.PartItemKey,
    m.ManufacturerName,
    cm.ModelName,
    cm.YearFrom,
    cm.YearTo,
    COUNT(DISTINCT vt.VehicleTypeId) AS ConsolidatedVariantCount
FROM dbo.VehiclePartsMappings vpm
INNER JOIN dbo.ConsolidatedVehicleModels cm ON vpm.ConsolidatedModelId = cm.ConsolidatedModelId
INNER JOIN dbo.Manufacturers m ON cm.ManufacturerId = m.ManufacturerId
LEFT JOIN dbo.VehicleTypes vt ON vt.ConsolidatedModelId = cm.ConsolidatedModelId
WHERE vpm.IsActive = 1
  AND vpm.IsCurrentVersion = 1
  AND vpm.ConsolidatedModelId IS NOT NULL
  AND vpm.VehicleTypeId IS NULL
GROUP BY vpm.PartItemKey, m.ManufacturerName, cm.ModelName, cm.YearFrom, cm.YearTo
ORDER BY COUNT(DISTINCT vt.VehicleTypeId) DESC;

PRINT '';
PRINT '========================================';
PRINT 'Mapping migration completed successfully!';
PRINT '========================================';
PRINT '';
PRINT 'What happened:';
PRINT '  1. Analyzed legacy (VehicleTypeId) mappings';
PRINT '  2. Grouped identical mappings by ConsolidatedModelId + PartItemKey';
PRINT '  3. Created new consolidated mappings (one per unique combination)';
PRINT '  4. Deactivated old legacy mappings that were successfully consolidated';
PRINT '';
PRINT 'From now on:';
PRINT '  - Use ConsolidatedModelId for all new mappings';
PRINT '  - Queries should filter by: IsActive = 1 AND ConsolidatedModelId IS NOT NULL';
PRINT '  - Legacy mappings are deactivated (IsActive = 0) but preserved for audit';
PRINT '';
GO
