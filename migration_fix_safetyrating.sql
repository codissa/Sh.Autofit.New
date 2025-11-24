-- =============================================
-- FIX: Update SafetyRating column type
-- Run this if you already ran migration_consolidated_models.sql
-- and got the "Arithmetic overflow" error
-- =============================================

USE [Sh.Autofit];
GO

-- Check if table exists and fix the column type
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'ConsolidatedVehicleModels')
BEGIN
    PRINT 'Fixing SafetyRating column type...';

    -- Alter column from DECIMAL(3,2) to DECIMAL(10,2)
    ALTER TABLE dbo.ConsolidatedVehicleModels
    ALTER COLUMN SafetyRating DECIMAL(10,2) NULL;

    PRINT 'SafetyRating column updated to DECIMAL(10,2)';
END
ELSE
BEGIN
    PRINT 'ConsolidatedVehicleModels table does not exist. Run migration_consolidated_models.sql first.';
END
GO

PRINT '';
PRINT 'Fix complete. Now re-run migration_consolidate_vehicles_data.sql';
GO
