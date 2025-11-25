-- =============================================
-- Migration Verification Script
-- Database: Sh.Autofit
-- Description: Comprehensive checks to verify migration completed successfully
-- =============================================

USE [Sh.Autofit];
GO

PRINT '========================================';
PRINT 'MIGRATION VERIFICATION REPORT';
PRINT '========================================';
PRINT '';
GO

-- =============================================
-- CHECK 1: Vehicle Consolidation Status
-- =============================================

PRINT '1. VEHICLE CONSOLIDATION STATUS';
PRINT '================================';
PRINT '';

DECLARE @TotalVehicleTypes INT;
DECLARE @LinkedVehicleTypes INT;
DECLARE @OrphanedVehicleTypes INT;

SELECT @TotalVehicleTypes = COUNT(*) FROM dbo.VehicleTypes;
SELECT @LinkedVehicleTypes = COUNT(*) FROM dbo.VehicleTypes WHERE ConsolidatedModelId IS NOT NULL;
SELECT @OrphanedVehicleTypes = COUNT(*) FROM dbo.VehicleTypes WHERE ConsolidatedModelId IS NULL;

PRINT 'Vehicle Types:';
PRINT '  Total: ' + CAST(@TotalVehicleTypes AS NVARCHAR);
PRINT '  Linked to Consolidated Models: ' + CAST(@LinkedVehicleTypes AS NVARCHAR);
PRINT '  Orphaned (Not Linked): ' + CAST(@OrphanedVehicleTypes AS NVARCHAR);

IF @OrphanedVehicleTypes = 0
    PRINT '  ✓ All vehicle types are linked!';
ELSE IF @OrphanedVehicleTypes < (@TotalVehicleTypes * 0.05) -- Less than 5%
    PRINT '  ⚠ Some orphaned vehicles exist, but this is acceptable (<5%)';
ELSE
    PRINT '  ❌ Too many orphaned vehicles! Consider re-running vehicle consolidation.';

PRINT '';
GO

-- =============================================
-- CHECK 2: Mapping Migration Status
-- =============================================

PRINT '2. MAPPING MIGRATION STATUS';
PRINT '===========================';
PRINT '';

DECLARE @TotalActiveMappings INT;
DECLARE @ConsolidatedMappings INT;
DECLARE @LegacyActiveMappings INT;
DECLARE @DeactivatedLegacyMappings INT;

SELECT @TotalActiveMappings = COUNT(*)
FROM dbo.VehiclePartsMappings
WHERE IsActive = 1 AND IsCurrentVersion = 1;

SELECT @ConsolidatedMappings = COUNT(*)
FROM dbo.VehiclePartsMappings
WHERE IsActive = 1
  AND IsCurrentVersion = 1
  AND ConsolidatedModelId IS NOT NULL
  AND VehicleTypeId IS NULL;

SELECT @LegacyActiveMappings = COUNT(*)
FROM dbo.VehiclePartsMappings
WHERE IsActive = 1
  AND IsCurrentVersion = 1
  AND VehicleTypeId IS NOT NULL
  AND ConsolidatedModelId IS NULL;

SELECT @DeactivatedLegacyMappings = COUNT(*)
FROM dbo.VehiclePartsMappings
WHERE IsActive = 0
  AND VehicleTypeId IS NOT NULL
  AND ConsolidatedModelId IS NULL
  AND UpdatedBy = 'MIGRATION';

PRINT 'Active Mappings:';
PRINT '  Total Active: ' + CAST(@TotalActiveMappings AS NVARCHAR);
PRINT '  Consolidated (ConsolidatedModelId): ' + CAST(@ConsolidatedMappings AS NVARCHAR);
PRINT '  Legacy Still Active (VehicleTypeId): ' + CAST(@LegacyActiveMappings AS NVARCHAR);
PRINT '';
PRINT 'Deactivated by Migration:';
PRINT '  Legacy Mappings Deactivated: ' + CAST(@DeactivatedLegacyMappings AS NVARCHAR);

IF @ConsolidatedMappings > 0 AND @DeactivatedLegacyMappings > 0
BEGIN
    DECLARE @CompressionRatio FLOAT = CAST(@DeactivatedLegacyMappings AS FLOAT) / NULLIF(@ConsolidatedMappings, 0);
    PRINT '  Compression Ratio: ' + CAST(ROUND(@CompressionRatio, 2) AS NVARCHAR) + ':1';
    PRINT '  (e.g., 3.5:1 means 3.5 legacy mappings became 1 consolidated mapping)';
END

PRINT '';

IF @ConsolidatedMappings > 0
    PRINT '  ✓ Consolidated mappings exist!';
ELSE
    PRINT '  ❌ No consolidated mappings found! Migration may not have run.';

IF @LegacyActiveMappings = 0
    PRINT '  ✓ All legacy mappings have been consolidated or deactivated!';
ELSE IF @LegacyActiveMappings < (@TotalActiveMappings * 0.05)
    PRINT '  ⚠ Some legacy mappings remain active, but this is acceptable (<5%)';
ELSE
    PRINT '  ❌ Too many legacy mappings still active! Migration may be incomplete.';

PRINT '';
GO

-- =============================================
-- CHECK 3: Data Integrity Checks
-- =============================================

PRINT '3. DATA INTEGRITY CHECKS';
PRINT '========================';
PRINT '';

-- Check for mappings with both ConsolidatedModelId and VehicleTypeId (should not exist)
DECLARE @DualMappings INT;
SELECT @DualMappings = COUNT(*)
FROM dbo.VehiclePartsMappings
WHERE IsActive = 1
  AND ConsolidatedModelId IS NOT NULL
  AND VehicleTypeId IS NOT NULL;

PRINT 'Dual Mappings (should be 0):';
PRINT '  Mappings with both ConsolidatedModelId AND VehicleTypeId: ' + CAST(@DualMappings AS NVARCHAR);

IF @DualMappings = 0
    PRINT '  ✓ No dual mappings found!';
ELSE
    PRINT '  ❌ Dual mappings exist! This indicates data corruption.';

PRINT '';

-- Check for orphaned consolidated mappings (ConsolidatedModelId doesn't exist)
DECLARE @OrphanedConsolidatedMappings INT;
SELECT @OrphanedConsolidatedMappings = COUNT(*)
FROM dbo.VehiclePartsMappings vpm
WHERE vpm.IsActive = 1
  AND vpm.ConsolidatedModelId IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM dbo.ConsolidatedVehicleModels cm
      WHERE cm.ConsolidatedModelId = vpm.ConsolidatedModelId
  );

PRINT 'Orphaned Consolidated Mappings (should be 0):';
PRINT '  Mappings pointing to non-existent ConsolidatedModelId: ' + CAST(@OrphanedConsolidatedMappings AS NVARCHAR);

IF @OrphanedConsolidatedMappings = 0
    PRINT '  ✓ All consolidated mappings point to valid models!';
ELSE
    PRINT '  ❌ Orphaned consolidated mappings exist! Data integrity issue.';

PRINT '';

-- Check for orphaned legacy mappings (VehicleTypeId doesn't exist)
DECLARE @OrphanedLegacyMappings INT;
SELECT @OrphanedLegacyMappings = COUNT(*)
FROM dbo.VehiclePartsMappings vpm
WHERE vpm.IsActive = 1
  AND vpm.VehicleTypeId IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM dbo.VehicleTypes vt
      WHERE vt.VehicleTypeId = vpm.VehicleTypeId
  );

PRINT 'Orphaned Legacy Mappings (should be 0):';
PRINT '  Mappings pointing to non-existent VehicleTypeId: ' + CAST(@OrphanedLegacyMappings AS NVARCHAR);

IF @OrphanedLegacyMappings = 0
    PRINT '  ✓ All legacy mappings point to valid vehicle types!';
ELSE
    PRINT '  ❌ Orphaned legacy mappings exist! Data integrity issue.';

PRINT '';
GO

-- =============================================
-- CHECK 4: Sample Data Verification
-- =============================================

PRINT '4. SAMPLE DATA VERIFICATION';
PRINT '===========================';
PRINT '';

PRINT 'Top 5 Consolidated Models by Part Count:';
SELECT TOP 5
    cm.ConsolidatedModelId,
    m.ManufacturerShortName AS Manufacturer,
    cm.ModelName,
    cm.YearFrom,
    cm.YearTo,
    cm.EngineVolume,
    cm.TransmissionType,
    COUNT(DISTINCT vpm.PartItemKey) AS UniquePartCount,
    COUNT(DISTINCT vt.VehicleTypeId) AS ConsolidatedVariantCount
FROM dbo.ConsolidatedVehicleModels cm
INNER JOIN dbo.Manufacturers m ON cm.ManufacturerId = m.ManufacturerId
LEFT JOIN dbo.VehicleTypes vt ON vt.ConsolidatedModelId = cm.ConsolidatedModelId
LEFT JOIN dbo.VehiclePartsMappings vpm ON vpm.ConsolidatedModelId = cm.ConsolidatedModelId
    AND vpm.IsActive = 1
    AND vpm.IsCurrentVersion = 1
    AND vpm.VehicleTypeId IS NULL
GROUP BY
    cm.ConsolidatedModelId,
    m.ManufacturerShortName,
    cm.ModelName,
    cm.YearFrom,
    cm.YearTo,
    cm.EngineVolume,
    cm.TransmissionType
ORDER BY COUNT(DISTINCT vpm.PartItemKey) DESC;

PRINT '';
GO

-- =============================================
-- CHECK 5: Migration Metadata
-- =============================================

PRINT '5. MIGRATION METADATA';
PRINT '=====================';
PRINT '';

-- Check when migration ran
DECLARE @MigrationDate DATETIME;
SELECT @MigrationDate = MAX(UpdatedAt)
FROM dbo.VehiclePartsMappings
WHERE UpdatedBy = 'MIGRATION'
  AND IsActive = 0;

IF @MigrationDate IS NOT NULL
BEGIN
    PRINT 'Migration Last Run:';
    PRINT '  Date: ' + CONVERT(NVARCHAR, @MigrationDate, 120);
    PRINT '  Days Ago: ' + CAST(DATEDIFF(DAY, @MigrationDate, GETDATE()) AS NVARCHAR);
    PRINT '';
END
ELSE
BEGIN
    PRINT '  ⚠ No migration metadata found. Migration may not have run.';
    PRINT '';
END

GO

-- =============================================
-- CHECK 6: Coupling Status
-- =============================================

PRINT '6. MODEL COUPLING STATUS';
PRINT '========================';
PRINT '';

DECLARE @TotalCouplings INT;
DECLARE @ActiveCouplings INT;

SELECT @TotalCouplings = COUNT(*) FROM dbo.ModelCouplings;
SELECT @ActiveCouplings = COUNT(*) FROM dbo.ModelCouplings WHERE IsActive = 1;

PRINT 'Model Couplings:';
PRINT '  Total: ' + CAST(@TotalCouplings AS NVARCHAR);
PRINT '  Active: ' + CAST(@ActiveCouplings AS NVARCHAR);

IF @ActiveCouplings > 0
    PRINT '  ✓ Model couplings exist!';
ELSE
    PRINT '  ℹ No model couplings found (this is normal if bulk coupling hasn''t been run yet)';

PRINT '';
GO

-- =============================================
-- FINAL SUMMARY
-- =============================================

PRINT '========================================';
PRINT 'FINAL VERDICT';
PRINT '========================================';
PRINT '';

DECLARE @VehicleScore INT = 0;
DECLARE @MappingScore INT = 0;
DECLARE @IntegrityScore INT = 0;

-- Vehicle score
IF (SELECT COUNT(*) FROM dbo.VehicleTypes WHERE ConsolidatedModelId IS NULL) = 0
    SET @VehicleScore = 2;
ELSE IF (SELECT COUNT(*) FROM dbo.VehicleTypes WHERE ConsolidatedModelId IS NULL) < (SELECT COUNT(*) * 0.05 FROM dbo.VehicleTypes)
    SET @VehicleScore = 1;

-- Mapping score
IF (SELECT COUNT(*) FROM dbo.VehiclePartsMappings WHERE IsActive = 1 AND ConsolidatedModelId IS NOT NULL) > 0
    SET @MappingScore = 2;

-- Integrity score
IF (SELECT COUNT(*) FROM dbo.VehiclePartsMappings WHERE IsActive = 1 AND ConsolidatedModelId IS NOT NULL AND VehicleTypeId IS NOT NULL) = 0
    SET @IntegrityScore = 2;
ELSE
    SET @IntegrityScore = 0;

DECLARE @TotalScore INT = @VehicleScore + @MappingScore + @IntegrityScore;

IF @TotalScore = 6
BEGIN
    PRINT '✅ MIGRATION SUCCESSFUL!';
    PRINT '';
    PRINT 'All checks passed. Your database is ready for the new application version.';
END
ELSE IF @TotalScore >= 4
BEGIN
    PRINT '⚠ MIGRATION MOSTLY SUCCESSFUL';
    PRINT '';
    PRINT 'Most checks passed, but there are minor issues. Review the report above.';
END
ELSE
BEGIN
    PRINT '❌ MIGRATION INCOMPLETE OR FAILED';
    PRINT '';
    PRINT 'Critical issues detected. Review the report above and consider:';
    PRINT '  1. Re-running the migration scripts';
    PRINT '  2. Rolling back and investigating issues';
    PRINT '  3. Checking the migration script output for errors';
END

PRINT '';
PRINT '========================================';
PRINT 'END OF REPORT';
PRINT '========================================';
GO
