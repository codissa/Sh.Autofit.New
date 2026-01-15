-- Migration 07: Optimize Plate Search Caching
-- Add special flags and consolidated model ID to VehicleRegistrations for better performance
-- Created: 2025-12-11
-- Purpose: Eliminate redundant API calls and improve cache efficiency

-- Add new columns to VehicleRegistrations table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'VehicleRegistrations') AND name = 'IsOffRoad')
BEGIN
    ALTER TABLE VehicleRegistrations
    ADD IsOffRoad BIT NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'VehicleRegistrations') AND name = 'IsPersonalImport')
BEGIN
    ALTER TABLE VehicleRegistrations
    ADD IsPersonalImport BIT NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'VehicleRegistrations') AND name = 'SourceResourceId')
BEGIN
    ALTER TABLE VehicleRegistrations
    ADD SourceResourceId NVARCHAR(100) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'VehicleRegistrations') AND name = 'ConsolidatedModelId')
BEGIN
    ALTER TABLE VehicleRegistrations
    ADD ConsolidatedModelId INT NULL;
END
GO

-- Add foreign key constraint for ConsolidatedModelId
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_VehicleRegistrations_ConsolidatedModel')
BEGIN
    ALTER TABLE VehicleRegistrations
    ADD CONSTRAINT FK_VehicleRegistrations_ConsolidatedModel
    FOREIGN KEY (ConsolidatedModelId)
    REFERENCES ConsolidatedVehicleModels(ConsolidatedModelId);
END
GO

-- Create index for faster consolidated model lookups
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_VehicleRegistrations_ConsolidatedModelId')
BEGIN
    SET QUOTED_IDENTIFIER ON;
    CREATE NONCLUSTERED INDEX IX_VehicleRegistrations_ConsolidatedModelId
    ON VehicleRegistrations(ConsolidatedModelId)
    WHERE ConsolidatedModelId IS NOT NULL;
END
GO

PRINT 'Migration 07: Optimize Plate Search Caching completed successfully';
GO
