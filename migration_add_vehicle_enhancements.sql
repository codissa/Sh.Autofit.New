-- =============================================
-- MIGRATION: Add Vehicle Enhancement Columns
-- Database: Sh.Autofit
-- Description: Add horsepower, drive type, and other vehicle details
-- =============================================

USE [Sh.Autofit];
GO

PRINT '========================================';
PRINT 'Adding Vehicle Enhancement Columns';
PRINT '========================================';
PRINT '';
GO

-- =============================================
-- STEP 1: Add columns to VehicleTypes
-- =============================================

PRINT 'Step 1: Adding columns to VehicleTypes table...';
GO

-- Check if columns don't exist before adding
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.VehicleTypes') AND name = 'DriveType')
BEGIN
    ALTER TABLE dbo.VehicleTypes ADD
        DriveType NVARCHAR(50) NULL,              -- 2WD, 4WD, AWD, FWD, RWD
        GovernmentModelCode NVARCHAR(50) NULL,    -- degem_cd from API (for matching)
        GovernmentManufacturerCode INT NULL,      -- tozeret_cd from API (for matching)
        LastSyncedFromGov DATETIME NULL;          -- Last sync timestamp

    PRINT '  ✓ Added 4 new columns to VehicleTypes';
    PRINT '  ℹ Note: Existing columns will be populated from API:';
    PRINT '    - Horsepower (koah_sus)';
    PRINT '    - NumberOfDoors (mispar_dlatot)';
    PRINT '    - NumberOfSeats (mispar_moshavim)';
    PRINT '    - TotalWeight (mishkal_kolel)';
    PRINT '    - SafetyRating (nikud_betihut)';
    PRINT '    - GreenIndex (madad_yarok)';
    PRINT '    - FinishLevel (ramat_gimur)';
    PRINT '    - TrimLevel (merkav - body type)';
END
ELSE
BEGIN
    PRINT '  ⚠ Columns already exist in VehicleTypes, skipping';
END
GO

-- =============================================
-- STEP 2: Add columns to ConsolidatedVehicleModels
-- =============================================

PRINT '';
PRINT 'Step 2: Adding columns to ConsolidatedVehicleModels table...';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ConsolidatedVehicleModels') AND name = 'DriveType')
BEGIN
    ALTER TABLE dbo.ConsolidatedVehicleModels ADD
        DriveType NVARCHAR(50) NULL,              -- Most common drive type
        NumberOfSeats INT NULL,                   -- Number of seats
        WeightRange NVARCHAR(50) NULL;            -- Weight range (e.g., "1800-2000 kg")

    PRINT '  ✓ Added 3 new columns to ConsolidatedVehicleModels';
    PRINT '  ℹ Note: Existing columns will be aggregated from variants:';
    PRINT '    - Horsepower (average)';
    PRINT '    - NumberOfDoors (most common)';
    PRINT '    - SafetyRating (average)';
    PRINT '    - GreenIndex (average)';
    PRINT '    - FinishLevel (most common)';
    PRINT '    - TrimLevel (most common)';
END
ELSE
BEGIN
    PRINT '  ⚠ Columns already exist in ConsolidatedVehicleModels, skipping';
END
GO

-- =============================================
-- STEP 3: Create indexes for better performance
-- =============================================

PRINT '';
PRINT 'Step 3: Creating indexes for government codes...';
GO

-- Index on GovernmentManufacturerCode and GovernmentModelCode for fast matching
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_VehicleTypes_GovCodes' AND object_id = OBJECT_ID('dbo.VehicleTypes'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_VehicleTypes_GovCodes
    ON dbo.VehicleTypes (GovernmentManufacturerCode, GovernmentModelCode)
    INCLUDE (VehicleTypeId, ModelName, YearFrom, YearTo);

    PRINT '  ✓ Created index IX_VehicleTypes_GovCodes';
END
ELSE
BEGIN
    PRINT '  ⚠ Index IX_VehicleTypes_GovCodes already exists, skipping';
END
GO

-- =============================================
-- STEP 4: Verification
-- =============================================

PRINT '';
PRINT 'Step 4: Verifying changes...';
PRINT '';
GO

-- Check VehicleTypes columns
DECLARE @VehicleTypesColumns INT;
SELECT @VehicleTypesColumns = COUNT(*)
FROM sys.columns
WHERE object_id = OBJECT_ID('dbo.VehicleTypes')
  AND name IN ('DriveType', 'GovernmentModelCode', 'GovernmentManufacturerCode', 'LastSyncedFromGov');

PRINT 'VehicleTypes: ' + CAST(@VehicleTypesColumns AS NVARCHAR) + '/4 new columns added';

-- Check ConsolidatedVehicleModels columns
DECLARE @ConsolidatedColumns INT;
SELECT @ConsolidatedColumns = COUNT(*)
FROM sys.columns
WHERE object_id = OBJECT_ID('dbo.ConsolidatedVehicleModels')
  AND name IN ('DriveType', 'NumberOfSeats', 'WeightRange');

PRINT 'ConsolidatedVehicleModels: ' + CAST(@ConsolidatedColumns AS NVARCHAR) + '/3 new columns added';

-- Check indexes
DECLARE @IndexCount INT;
SELECT @IndexCount = COUNT(*)
FROM sys.indexes
WHERE name = 'IX_VehicleTypes_GovCodes'
  AND object_id = OBJECT_ID('dbo.VehicleTypes');

PRINT 'Indexes created: ' + CAST(@IndexCount AS NVARCHAR) + '/1';

PRINT '';
IF @VehicleTypesColumns = 4 AND @ConsolidatedColumns = 3 AND @IndexCount = 1
BEGIN
    PRINT '========================================';
    PRINT '✅ MIGRATION COMPLETED SUCCESSFULLY!';
    PRINT '========================================';
    PRINT '';
    PRINT 'Next steps:';
    PRINT '  1. Run the vehicle data sync from the application';
    PRINT '  2. Match vehicles with government API data';
    PRINT '  3. Update consolidated models with aggregated data';
END
ELSE
BEGIN
    PRINT '========================================';
    PRINT '⚠ MIGRATION COMPLETED WITH WARNINGS';
    PRINT '========================================';
    PRINT '';
    PRINT 'Some columns or indexes may already exist.';
    PRINT 'This is normal if running the migration multiple times.';
END

PRINT '';
GO

-- =============================================
-- REFERENCE: Column Mappings
-- =============================================

/*
API Field → Database Column Mapping:

VehicleTypes (NEW COLUMNS):
- DriveType (NEW) ← technologiat_hanaa_nm (parsed: "הנעה רגילה" → "2WD", "4X4" → "4WD")
- GovernmentModelCode (NEW) ← degem_cd (for matching)
- GovernmentManufacturerCode (NEW) ← tozeret_cd (for matching)
- LastSyncedFromGov (NEW) ← timestamp

VehicleTypes (EXISTING COLUMNS TO UPDATE):
- Horsepower ← koah_sus
- NumberOfDoors ← mispar_dlatot
- NumberOfSeats ← mispar_moshavim
- TotalWeight ← mishkal_kolel
- SafetyRating ← nikud_betihut
- GreenIndex ← madad_yarok
- FinishLevel ← ramat_gimur (trim finish level)
- TrimLevel ← merkav (body type like SUV, Sedan, etc.)

ConsolidatedVehicleModels (NEW COLUMNS):
- DriveType (NEW) ← aggregate most common from variants
- NumberOfSeats (NEW) ← aggregate most common from variants
- WeightRange (NEW) ← calculated from min/max TotalWeight

ConsolidatedVehicleModels (EXISTING COLUMNS TO UPDATE):
- Horsepower ← average from variants
- NumberOfDoors ← most common from variants
- SafetyRating ← average from variants
- GreenIndex ← average from variants
- FinishLevel ← most common from variants
- TrimLevel ← most common from variants

Matching Strategy:
Precise three-part key matching:
1. tozeret_cd (manufacturer code) = Manufacturer.ManufacturerCode
2. degem_cd (model code) = VehicleType.ModelCode
3. degem_nm (model name) = VehicleType.ModelName
4. shnat_yitzur (year) falls within YearFrom/YearTo (±1 year tolerance)

Lookup key format: "{ManufacturerCode}_{ModelCode}_{ModelName}"
Example: "8_2341_corolla" matches Toyota Corolla variants

After sync, populates GovernmentManufacturerCode and GovernmentModelCode for future use.
*/
