-- =============================================
-- DATA MIGRATION: Migrate Existing Mappings to Consolidated Models
-- Database: Sh.Autofit
-- Description: Convert legacy VehicleTypeId-based mappings to ConsolidatedModelId-based
-- RUN THIS AFTER: migration_consolidate_vehicles_data.sql
-- =============================================

USE [Sh.Autofit];
GO

PRINT 'Starting mapping migration to consolidated models...';
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

-- First, identify which legacy mappings can be consolidated
-- (they must have a linked ConsolidatedModelId via VehicleType)

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
);

DECLARE @NewConsolidatedMappings INT = @@ROWCOUNT;
PRINT 'Created ' + CAST(@NewConsolidatedMappings AS NVARCHAR) + ' new consolidated mappings.';
PRINT '';
GO

-- =============================================
-- STEP 3: MARK OLD LEGACY MAPPINGS AS NOT CURRENT
-- =============================================

PRINT 'Step 3: Marking legacy mappings as superseded (not current version)...';
PRINT 'Note: Legacy mappings are preserved but marked as old versions for audit.';
PRINT '';
GO

UPDATE vpm
SET
    vpm.IsCurrentVersion = 0,
    vpm.MappingLevel = 'Legacy',
    vpm.UpdatedAt = GETDATE(),
    vpm.UpdatedBy = 'MIGRATION'
FROM dbo.VehiclePartsMappings vpm
INNER JOIN dbo.VehicleTypes vt ON vpm.VehicleTypeId = vt.VehicleTypeId
WHERE vpm.IsActive = 1
  AND vpm.IsCurrentVersion = 1
  AND vpm.VehicleTypeId IS NOT NULL
  AND vt.ConsolidatedModelId IS NOT NULL
  -- Only mark as superseded if a consolidated mapping exists
  AND EXISTS (
      SELECT 1 FROM dbo.VehiclePartsMappings consolidated
      WHERE consolidated.ConsolidatedModelId = vt.ConsolidatedModelId
        AND consolidated.PartItemKey = vpm.PartItemKey
        AND consolidated.IsActive = 1
        AND consolidated.IsCurrentVersion = 1
        AND consolidated.MappingLevel = 'Consolidated'
  );

DECLARE @SupersededMappings INT = @@ROWCOUNT;
PRINT 'Marked ' + CAST(@SupersededMappings AS NVARCHAR) + ' legacy mappings as superseded.';
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
DECLARE @FinalLegacy INT;
DECLARE @FinalConsolidated INT;
DECLARE @CurrentLegacy INT;

SELECT @FinalTotal = COUNT(*) FROM dbo.VehiclePartsMappings WHERE IsActive = 1;
SELECT @FinalLegacy = COUNT(*) FROM dbo.VehiclePartsMappings WHERE IsActive = 1 AND VehicleTypeId IS NOT NULL;
SELECT @FinalConsolidated = COUNT(*) FROM dbo.VehiclePartsMappings WHERE IsActive = 1 AND ConsolidatedModelId IS NOT NULL;
SELECT @CurrentLegacy = COUNT(*) FROM dbo.VehiclePartsMappings WHERE IsActive = 1 AND IsCurrentVersion = 1 AND VehicleTypeId IS NOT NULL;

PRINT 'Final Mapping Statistics:';
PRINT '  Total Mappings: ' + CAST(@FinalTotal AS NVARCHAR);
PRINT '  Legacy (VehicleTypeId): ' + CAST(@FinalLegacy AS NVARCHAR) + ' (of which ' + CAST(@CurrentLegacy AS NVARCHAR) + ' still current)';
PRINT '  Consolidated: ' + CAST(@FinalConsolidated AS NVARCHAR);
PRINT '';

-- Consolidation statistics
SELECT
    'Consolidation Ratio' AS Metric,
    COUNT(DISTINCT CONCAT(vt.ConsolidatedModelId, '-', vpm.PartItemKey)) AS UniqueConsolidatedMappings,
    COUNT(*) AS OriginalMappings,
    CAST(COUNT(*) AS FLOAT) / NULLIF(COUNT(DISTINCT CONCAT(vt.ConsolidatedModelId, '-', vpm.PartItemKey)), 0) AS CompressionRatio
FROM dbo.VehiclePartsMappings vpm
INNER JOIN dbo.VehicleTypes vt ON vpm.VehicleTypeId = vt.VehicleTypeId
WHERE vpm.IsActive = 1
  AND vpm.MappingLevel = 'Legacy'
  AND vt.ConsolidatedModelId IS NOT NULL;

-- Top consolidated mappings (most variants consolidated)
PRINT '';
PRINT 'Top 10 parts with most consolidated variants:';

SELECT TOP 10
    vpm.PartItemKey,
    m.ManufacturerName,
    cm.ModelName,
    cm.YearFrom,
    cm.YearTo,
    COUNT(legacy.MappingId) AS OriginalVariantMappings
FROM dbo.VehiclePartsMappings vpm
INNER JOIN dbo.ConsolidatedVehicleModels cm ON vpm.ConsolidatedModelId = cm.ConsolidatedModelId
INNER JOIN dbo.Manufacturers m ON cm.ManufacturerId = m.ManufacturerId
LEFT JOIN dbo.VehiclePartsMappings legacy
    ON legacy.PartItemKey = vpm.PartItemKey
    AND legacy.MappingLevel = 'Legacy'
    AND legacy.VehicleTypeId IN (
        SELECT VehicleTypeId FROM dbo.VehicleTypes WHERE ConsolidatedModelId = cm.ConsolidatedModelId
    )
WHERE vpm.IsActive = 1
  AND vpm.IsCurrentVersion = 1
  AND vpm.MappingLevel = 'Consolidated'
GROUP BY vpm.PartItemKey, m.ManufacturerName, cm.ModelName, cm.YearFrom, cm.YearTo
ORDER BY COUNT(legacy.MappingId) DESC;

-- Orphaned mappings (legacy mappings without consolidated counterpart)
DECLARE @OrphanedMappings INT;
SELECT @OrphanedMappings = COUNT(*)
FROM dbo.VehiclePartsMappings vpm
INNER JOIN dbo.VehicleTypes vt ON vpm.VehicleTypeId = vt.VehicleTypeId
WHERE vpm.IsActive = 1
  AND vpm.IsCurrentVersion = 1
  AND vpm.VehicleTypeId IS NOT NULL
  AND vt.ConsolidatedModelId IS NULL;

IF @OrphanedMappings > 0
BEGIN
    PRINT '';
    PRINT 'WARNING: ' + CAST(@OrphanedMappings AS NVARCHAR) + ' legacy mappings could not be consolidated';
    PRINT '(Their VehicleTypes are not linked to ConsolidatedVehicleModels)';
    PRINT 'These mappings remain as Legacy and still work.';
END
ELSE
BEGIN
    PRINT '';
    PRINT 'SUCCESS: All legacy mappings have been consolidated!';
END

PRINT '';
PRINT '========================================';
PRINT 'Mapping migration completed!';
PRINT '========================================';
PRINT '';
PRINT 'What happened:';
PRINT '  1. Analyzed legacy (VehicleTypeId) mappings';
PRINT '  2. Grouped identical mappings by ConsolidatedModelId + PartItemKey';
PRINT '  3. Created new consolidated mappings (one per unique combination)';
PRINT '  4. Marked old legacy mappings as superseded (kept for audit)';
PRINT '';
PRINT 'From now on:';
PRINT '  - New mappings should use ConsolidatedModelId';
PRINT '  - Queries should look for MappingLevel = ''Consolidated''';
PRINT '  - Legacy mappings are preserved but not active';
PRINT '';
GO
