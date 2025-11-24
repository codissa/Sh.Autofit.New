-- =============================================
-- DATA MIGRATION: Consolidate Existing Vehicles
-- Database: Sh.Autofit
-- Description: Migrate existing VehicleTypes to ConsolidatedVehicleModels
--              Group by uniqueness key and calculate year ranges
-- UNIQUENESS KEY: ManufacturerCode + ModelCode + ModelName + EngineVolume + TrimLevel + TransmissionType + FuelTypeCode
-- =============================================

USE [Sh.Autofit];
GO

PRINT 'Starting data migration to consolidate vehicles...';
PRINT 'Uniqueness Key: ManufacturerCode + ModelCode + ModelName + EngineVolume + TrimLevel + TransmissionType + FuelTypeCode';
PRINT '';
PRINT 'This script will:';
PRINT '  1. Analyze existing VehicleTypes';
PRINT '  2. Group by uniqueness attributes (7 fields)';
PRINT '  3. Create consolidated models with year ranges';
PRINT '  4. Link original VehicleTypes to consolidated models';
PRINT '  5. Generate statistics';
PRINT '';
GO

-- =============================================
-- STEP 1: ANALYZE CURRENT DATA
-- =============================================

PRINT 'Step 1: Analyzing current VehicleTypes data...';
GO

DECLARE @TotalVehicles INT;
DECLARE @TotalManufacturers INT;

SELECT @TotalVehicles = COUNT(*) FROM dbo.VehicleTypes WHERE IsActive = 1;
SELECT @TotalManufacturers = COUNT(DISTINCT ManufacturerId) FROM dbo.VehicleTypes WHERE IsActive = 1;

PRINT 'Current Statistics:';
PRINT '  Total Active Vehicles: ' + CAST(@TotalVehicles AS NVARCHAR);
PRINT '  Total Manufacturers: ' + CAST(@TotalManufacturers AS NVARCHAR);
PRINT '';
GO

-- =============================================
-- STEP 2: CREATE CONSOLIDATED MODELS
-- =============================================

PRINT 'Step 2: Creating consolidated models from existing VehicleTypes...';
PRINT 'Grouping by: ManufacturerCode + ModelCode + ModelName + EngineVolume + TrimLevel + TransmissionType + FuelTypeCode';
GO

-- Insert consolidated models by grouping VehicleTypes
INSERT INTO dbo.ConsolidatedVehicleModels (
    ManufacturerId,
    ManufacturerCode,
    ModelCode,
    ModelName,
    EngineVolume,
    TrimLevel,
    TransmissionType,
    FuelTypeCode,
    FuelTypeName,
    YearFrom,
    YearTo,
    -- Additional metadata (not part of uniqueness)
    FinishLevel,
    NumberOfDoors,
    Horsepower,
    CommercialName,
    EngineModel,
    VehicleCategory,
    EmissionGroup,
    GreenIndex,
    SafetyRating,
    SafetyLevel,
    IsActive,
    CreatedBy,
    CreatedAt,
    UpdatedAt
)
SELECT
    -- Primary attributes
    ManufacturerId,
    (SELECT TOP 1 ManufacturerCode FROM dbo.Manufacturers m WHERE m.ManufacturerId = vt.ManufacturerId) AS ManufacturerCode,
    ModelCode,
    ModelName,

    -- Uniqueness attributes (part of GROUP BY)
    EngineVolume,
    TrimLevel,
    TransmissionType,
    FuelTypeCode,
    MAX(FuelTypeName) AS FuelTypeName,

    -- Year range (aggregated)
    MIN(YearFrom) AS YearFrom,
    MAX(CASE WHEN YearTo IS NOT NULL THEN YearTo ELSE YearFrom END) AS YearTo,

    -- Additional metadata (take most recent values)
    MAX(FinishLevel) AS FinishLevel,
    MAX(NumberOfDoors) AS NumberOfDoors,
    MAX(Horsepower) AS Horsepower,
    MAX(CommercialName) AS CommercialName,
    MAX(EngineModel) AS EngineModel,
    MAX(VehicleCategory) AS VehicleCategory,
    MAX(EmissionGroup) AS EmissionGroup,
    MAX(GreenIndex) AS GreenIndex,
    -- SafetyRating - convert MONEY to DECIMAL(10,2)
    CAST(MAX(SafetyRating) AS DECIMAL(10,2)) AS SafetyRating,
    MAX(SafetyLevel) AS SafetyLevel,

    1 AS IsActive,
    'MIGRATION' AS CreatedBy,
    MIN(CreatedAt) AS CreatedAt,
    MAX(UpdatedAt) AS UpdatedAt

FROM dbo.VehicleTypes vt
WHERE vt.IsActive = 1
GROUP BY
    ManufacturerId,
    ModelCode,
    ModelName,
    EngineVolume,
    TrimLevel,
    TransmissionType,
    FuelTypeCode;

DECLARE @ConsolidatedCount INT = @@ROWCOUNT;
PRINT 'Created ' + CAST(@ConsolidatedCount AS NVARCHAR) + ' consolidated models.';
PRINT '';
GO

-- =============================================
-- STEP 3: LINK VEHICLETYPES TO CONSOLIDATED MODELS
-- =============================================

PRINT 'Step 3: Linking original VehicleTypes to ConsolidatedVehicleModels...';
GO

UPDATE vt
SET vt.ConsolidatedModelId = cm.ConsolidatedModelId
FROM dbo.VehicleTypes vt
INNER JOIN dbo.ConsolidatedVehicleModels cm
    ON vt.ManufacturerId = cm.ManufacturerId
    AND vt.ModelCode = cm.ModelCode
    AND vt.ModelName = cm.ModelName
    AND ISNULL(vt.EngineVolume, -1) = ISNULL(cm.EngineVolume, -1)
    AND ISNULL(vt.TrimLevel, '') = ISNULL(cm.TrimLevel, '')
    AND ISNULL(vt.TransmissionType, '') = ISNULL(cm.TransmissionType, '')
    AND ISNULL(vt.FuelTypeCode, -1) = ISNULL(cm.FuelTypeCode, -1)
WHERE vt.IsActive = 1;

DECLARE @LinkedCount INT = @@ROWCOUNT;
PRINT 'Linked ' + CAST(@LinkedCount AS NVARCHAR) + ' VehicleTypes to consolidated models.';
PRINT '';
GO

-- =============================================
-- STEP 4: GENERATE MIGRATION STATISTICS
-- =============================================

PRINT 'Step 4: Generating migration statistics...';
PRINT '';
GO

-- Consolidation ratio
SELECT
    COUNT(*) AS OriginalVehicleCount,
    COUNT(DISTINCT ConsolidatedModelId) AS ConsolidatedModelCount,
    CAST(COUNT(*) AS FLOAT) / COUNT(DISTINCT ConsolidatedModelId) AS ConsolidationRatio
FROM dbo.VehicleTypes
WHERE IsActive = 1 AND ConsolidatedModelId IS NOT NULL;

-- Year range statistics
SELECT
    AVG(ISNULL(YearTo, YearFrom) - YearFrom + 1) AS AvgYearSpan,
    MAX(ISNULL(YearTo, YearFrom) - YearFrom + 1) AS MaxYearSpan,
    MIN(ISNULL(YearTo, YearFrom) - YearFrom + 1) AS MinYearSpan
FROM dbo.ConsolidatedVehicleModels;

-- Top consolidated models (most variants)
PRINT 'Top 10 models with most variants:';
SELECT TOP 10
    m.ManufacturerName,
    cm.ModelName,
    cm.EngineVolume,
    cm.TransmissionType,
    cm.YearFrom,
    cm.YearTo,
    COUNT(vt.VehicleTypeId) AS VariantCount
FROM dbo.ConsolidatedVehicleModels cm
INNER JOIN dbo.Manufacturers m ON cm.ManufacturerId = m.ManufacturerId
LEFT JOIN dbo.VehicleTypes vt ON cm.ConsolidatedModelId = vt.ConsolidatedModelId
GROUP BY
    m.ManufacturerName,
    cm.ModelName,
    cm.EngineVolume,
    cm.TransmissionType,
    cm.YearFrom,
    cm.YearTo,
    cm.ConsolidatedModelId
ORDER BY COUNT(vt.VehicleTypeId) DESC;

-- Orphaned vehicles (not linked to consolidated model)
DECLARE @OrphanedCount INT;
SELECT @OrphanedCount = COUNT(*)
FROM dbo.VehicleTypes
WHERE IsActive = 1 AND ConsolidatedModelId IS NULL;

IF @OrphanedCount > 0
BEGIN
    PRINT '';
    PRINT 'WARNING: ' + CAST(@OrphanedCount AS NVARCHAR) + ' VehicleTypes were not linked to consolidated models.';
    PRINT 'Please investigate these records:';

    SELECT TOP 10 *
    FROM dbo.VehicleTypes
    WHERE IsActive = 1 AND ConsolidatedModelId IS NULL;
END
ELSE
BEGIN
    PRINT '';
    PRINT 'SUCCESS: All active VehicleTypes were successfully linked to consolidated models.';
END

PRINT '';
PRINT '========================================';
PRINT 'Data migration completed!';
PRINT '========================================';
PRINT '';
PRINT 'Summary:';
PRINT '  - Vehicles grouped by: ManufacturerCode + ModelCode + ModelName + EngineVolume + TrimLevel + TransmissionType + FuelTypeCode';
PRINT '  - Year ranges calculated from MIN(YearFrom) to MAX(YearTo)';
PRINT '  - Original VehicleTypes linked to consolidated models for audit';
PRINT '';
PRINT 'Next steps:';
PRINT '  1. Run migration_migrate_mappings.sql to migrate existing mappings';
PRINT '  2. Update Entity Framework models';
PRINT '  3. Update application code to use consolidated models';
PRINT '';
GO
