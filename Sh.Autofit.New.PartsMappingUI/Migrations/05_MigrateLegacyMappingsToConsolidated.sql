-- =============================================
-- Migration Script: Convert Legacy Mappings to Consolidated Mappings
-- Run this AFTER 03_PopulateConsolidatedVehicleModels.sql
-- =============================================

-- This script:
-- 1. For each legacy mapping (VehicleTypeId-based), finds the corresponding ConsolidatedVehicleModel
-- 2. Creates new consolidated mappings (ConsolidatedModelId-based)
-- 3. Deactivates the old legacy mappings (soft delete)

BEGIN TRANSACTION;

PRINT 'Starting migration of legacy mappings to consolidated mappings...';

-- Step 1: Create consolidated mappings from legacy mappings
-- Group by ConsolidatedModelId + PartItemKey to avoid duplicates
INSERT INTO VehiclePartsMappings (
    ConsolidatedModelId,
    VehicleTypeId,
    PartItemKey,
    MappingLevel,
    MappingSource,
    Priority,
    RequiresModification,
    ModificationNotes,
    VersionNumber,
    IsCurrentVersion,
    IsActive,
    CreatedBy,
    CreatedAt,
    UpdatedBy,
    UpdatedAt
)
SELECT DISTINCT
    cvm.ConsolidatedModelId,
    NULL,  -- No individual VehicleTypeId for consolidated mappings
    vpm.PartItemKey,
    'Consolidated',
    'migration_from_legacy',
    vpm.Priority,
    vpm.RequiresModification,
    vpm.ModificationNotes,
    1,
    1,
    1,
    'migration_script',
    GETUTCDATE(),
    'migration_script',
    GETUTCDATE()
FROM VehiclePartsMappings vpm
INNER JOIN VehicleTypes vt ON vpm.VehicleTypeId = vt.VehicleTypeId
INNER JOIN ConsolidatedVehicleModels cvm ON
    vt.ManufacturerCode = cvm.ManufacturerCode
    AND vt.ModelCode = cvm.ModelCode
    AND ISNULL(vt.ModelName, '') = ISNULL(cvm.ModelName, '')
    AND ISNULL(vt.EngineVolume, 0) = ISNULL(cvm.EngineVolume, 0)
    AND ISNULL(vt.TrimLevel, '') = ISNULL(cvm.TrimLevel, '')
    AND ISNULL(vt.TransmissionType, '') = ISNULL(cvm.TransmissionType, '')
    AND ISNULL(vt.FuelTypeCode, 0) = ISNULL(cvm.FuelTypeCode, 0)
WHERE vpm.VehicleTypeId IS NOT NULL
    AND vpm.IsActive = 1
    AND vpm.IsCurrentVersion = 1
    AND vpm.MappingLevel IS NULL OR vpm.MappingLevel = 'Legacy'
    -- Don't create if consolidated mapping already exists
    AND NOT EXISTS (
        SELECT 1 FROM VehiclePartsMappings existing
        WHERE existing.ConsolidatedModelId = cvm.ConsolidatedModelId
            AND existing.PartItemKey = vpm.PartItemKey
            AND existing.IsActive = 1
            AND existing.MappingLevel = 'Consolidated'
    );

DECLARE @ConsolidatedMappingsCreated INT = @@ROWCOUNT;
PRINT 'Created ' + CAST(@ConsolidatedMappingsCreated AS VARCHAR) + ' consolidated mappings';

-- Step 2: Mark legacy mappings as migrated (update MappingLevel)
UPDATE VehiclePartsMappings
SET MappingLevel = 'Legacy',
    UpdatedBy = 'migration_script',
    UpdatedAt = GETUTCDATE()
WHERE VehicleTypeId IS NOT NULL
    AND IsActive = 1
    AND (MappingLevel IS NULL OR MappingLevel = '');

DECLARE @LegacyMappingsMarked INT = @@ROWCOUNT;
PRINT 'Marked ' + CAST(@LegacyMappingsMarked AS VARCHAR) + ' legacy mappings with MappingLevel = Legacy';

-- Step 3: Optionally deactivate legacy mappings that have been migrated
-- (Uncomment if you want to hide legacy mappings after migration)
/*
UPDATE vpm
SET vpm.IsActive = 0,
    vpm.UpdatedBy = 'migration_script',
    vpm.UpdatedAt = GETUTCDATE()
FROM VehiclePartsMappings vpm
INNER JOIN VehicleTypes vt ON vpm.VehicleTypeId = vt.VehicleTypeId
INNER JOIN ConsolidatedVehicleModels cvm ON
    vt.ManufacturerCode = cvm.ManufacturerCode
    AND vt.ModelCode = cvm.ModelCode
    AND ISNULL(vt.ModelName, '') = ISNULL(cvm.ModelName, '')
    AND ISNULL(vt.EngineVolume, 0) = ISNULL(cvm.EngineVolume, 0)
    AND ISNULL(vt.TrimLevel, '') = ISNULL(cvm.TrimLevel, '')
    AND ISNULL(vt.TransmissionType, '') = ISNULL(cvm.TransmissionType, '')
    AND ISNULL(vt.FuelTypeCode, 0) = ISNULL(cvm.FuelTypeCode, 0)
WHERE vpm.VehicleTypeId IS NOT NULL
    AND vpm.IsActive = 1
    AND vpm.MappingLevel = 'Legacy'
    AND EXISTS (
        SELECT 1 FROM VehiclePartsMappings existing
        WHERE existing.ConsolidatedModelId = cvm.ConsolidatedModelId
            AND existing.PartItemKey = vpm.PartItemKey
            AND existing.IsActive = 1
            AND existing.MappingLevel = 'Consolidated'
    );

DECLARE @LegacyMappingsDeactivated INT = @@ROWCOUNT;
PRINT 'Deactivated ' + CAST(@LegacyMappingsDeactivated AS VARCHAR) + ' legacy mappings (migrated to consolidated)';
*/

-- Verification queries
PRINT '';
PRINT '=== Migration Summary ===';

SELECT 'Total Active Mappings' AS Metric, COUNT(*) AS Count
FROM VehiclePartsMappings WHERE IsActive = 1;

SELECT 'Consolidated Mappings' AS Metric, COUNT(*) AS Count
FROM VehiclePartsMappings WHERE IsActive = 1 AND MappingLevel = 'Consolidated';

SELECT 'Legacy Mappings' AS Metric, COUNT(*) AS Count
FROM VehiclePartsMappings WHERE IsActive = 1 AND MappingLevel = 'Legacy';

SELECT 'Unmigrated Mappings (NULL MappingLevel)' AS Metric, COUNT(*) AS Count
FROM VehiclePartsMappings WHERE IsActive = 1 AND MappingLevel IS NULL;

-- Show sample of migrated mappings
SELECT TOP 10
    cvm.ManufacturerName,
    cvm.ModelName,
    cvm.YearFrom,
    cvm.YearTo,
    vpm.PartItemKey,
    vpm.MappingLevel,
    vpm.MappingSource
FROM VehiclePartsMappings vpm
INNER JOIN ConsolidatedVehicleModels cvm ON vpm.ConsolidatedModelId = cvm.ConsolidatedModelId
WHERE vpm.IsActive = 1 AND vpm.MappingLevel = 'Consolidated'
ORDER BY cvm.ManufacturerName, cvm.ModelName;

COMMIT TRANSACTION;

PRINT '';
PRINT 'Migration completed successfully!';
PRINT 'Note: Legacy mappings are preserved with MappingLevel = ''Legacy'' for backward compatibility.';
PRINT 'You can optionally deactivate them by uncommenting Step 3 in this script.';
