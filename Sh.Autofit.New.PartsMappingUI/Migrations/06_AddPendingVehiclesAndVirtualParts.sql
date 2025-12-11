-- =====================================================================
-- Migration 06: Add PendingVehicleReviews, VirtualParts, and VirtualPartMigrationLog tables
-- Purpose: Support auto-discovery of government vehicles and virtual part creation
-- Date: 2025-12-08
-- =====================================================================

USE [ShAutofit]
GO

-- =====================================================================
-- Table 1: PendingVehicleReviews
-- Purpose: Queue for newly discovered vehicles from government API
-- =====================================================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[PendingVehicleReviews]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[PendingVehicleReviews] (
        [PendingVehicleId] INT PRIMARY KEY IDENTITY(1,1),

        -- Government API data
        [ManufacturerCode] INT NOT NULL,
        [ManufacturerName] NVARCHAR(100) NOT NULL,
        [ModelCode] INT NOT NULL,
        [ModelName] NVARCHAR(100) NOT NULL,
        [CommercialName] NVARCHAR(100) NULL,
        [ManufacturingYear] INT NOT NULL,
        [EngineVolume] INT NULL,
        [FuelType] NVARCHAR(50) NULL,
        [TransmissionType] NVARCHAR(50) NULL,
        [TrimLevel] NVARCHAR(100) NULL,
        [FinishLevel] NVARCHAR(100) NULL,
        [Horsepower] INT NULL,
        [DriveType] NVARCHAR(50) NULL,
        [NumberOfDoors] INT NULL,
        [NumberOfSeats] INT NULL,
        [TotalWeight] INT NULL,
        [SafetyRating] DECIMAL(3,1) NULL,
        [GreenIndex] INT NULL,

        -- Review status
        [ReviewStatus] NVARCHAR(20) NOT NULL DEFAULT 'Pending', -- Pending, Approved, Rejected
        [ReviewedBy] NVARCHAR(100) NULL,
        [ReviewedAt] DATETIME NULL,
        [ReviewNotes] NVARCHAR(500) NULL,

        -- Discovery metadata
        [DiscoveredAt] DATETIME NOT NULL DEFAULT GETDATE(),
        [DiscoverySource] NVARCHAR(50) NOT NULL, -- 'AutoDiscovery', 'ManualSync'

        -- Batch processing
        [BatchId] UNIQUEIDENTIFIER NULL,
        [ProcessedAt] DATETIME NULL,

        [IsActive] BIT NOT NULL DEFAULT 1,
        [CreatedAt] DATETIME NOT NULL DEFAULT GETDATE()
    );

    CREATE INDEX [IX_PendingVehicles_ReviewStatus] ON [dbo].[PendingVehicleReviews]([ReviewStatus], [IsActive]);
    CREATE INDEX [IX_PendingVehicles_Batch] ON [dbo].[PendingVehicleReviews]([BatchId]);
    CREATE INDEX [IX_PendingVehicles_ManufacturerModel] ON [dbo].[PendingVehicleReviews]([ManufacturerCode], [ModelCode], [ModelName], [ManufacturingYear]);

    PRINT 'Created table: PendingVehicleReviews';
END
ELSE
BEGIN
    PRINT 'Table PendingVehicleReviews already exists';
END
GO

-- =====================================================================
-- Table 2: VirtualParts
-- Purpose: Store user-created placeholder parts with OEM numbers
-- =====================================================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[VirtualParts]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[VirtualParts] (
        [VirtualPartId] INT PRIMARY KEY IDENTITY(1,1),

        -- Part identification (PartNumber = first OEM entered)
        [PartNumber] NVARCHAR(50) NOT NULL UNIQUE,
        [PartName] NVARCHAR(200) NOT NULL, -- Description

        -- OEM Numbers
        [OemNumber1] NVARCHAR(50) NOT NULL, -- Required (becomes PartNumber)
        [OemNumber2] NVARCHAR(50) NULL,
        [OemNumber3] NVARCHAR(50) NULL,
        [OemNumber4] NVARCHAR(50) NULL,
        [OemNumber5] NVARCHAR(500) NULL, -- Can contain multiple OEMs separated by "/"

        -- Optional fields
        [Category] NVARCHAR(100) NULL, -- From existing categories
        [Notes] NVARCHAR(1000) NOT NULL, -- Required

        -- Creation context
        [CreatedForVehicleTypeId] INT NULL, -- FK to VehicleTypes (nullable)
        [CreatedForConsolidatedModelId] INT NULL, -- FK to ConsolidatedVehicleModels (nullable)

        -- Metadata
        [CreatedBy] NVARCHAR(100) NOT NULL,
        [CreatedAt] DATETIME NOT NULL DEFAULT GETDATE(),
        [UpdatedAt] DATETIME NOT NULL DEFAULT GETDATE(),
        [IsActive] BIT NOT NULL DEFAULT 1,

        CONSTRAINT [FK_VirtualParts_VehicleTypes] FOREIGN KEY ([CreatedForVehicleTypeId])
            REFERENCES [dbo].[VehicleTypes]([VehicleTypeId]),
        CONSTRAINT [FK_VirtualParts_ConsolidatedModels] FOREIGN KEY ([CreatedForConsolidatedModelId])
            REFERENCES [dbo].[ConsolidatedVehicleModels]([ConsolidatedModelId])
    );

    CREATE INDEX [IX_VirtualParts_OEM1] ON [dbo].[VirtualParts]([OemNumber1]) WHERE [IsActive] = 1;
    CREATE INDEX [IX_VirtualParts_Active] ON [dbo].[VirtualParts]([IsActive]);
    CREATE INDEX [IX_VirtualParts_PartNumber] ON [dbo].[VirtualParts]([PartNumber]) WHERE [IsActive] = 1;

    PRINT 'Created table: VirtualParts';
END
ELSE
BEGIN
    PRINT 'Table VirtualParts already exists';
END
GO

-- =====================================================================
-- Table 3: VirtualPartMigrationLog
-- Purpose: Audit trail for virtual part → real part migrations
-- =====================================================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[VirtualPartMigrationLog]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[VirtualPartMigrationLog] (
        [MigrationId] INT PRIMARY KEY IDENTITY(1,1),

        [VirtualPartNumber] NVARCHAR(50) NOT NULL,
        [RealPartNumber] NVARCHAR(50) NOT NULL,

        [MatchedOemNumbers] NVARCHAR(500) NULL, -- Which OEMs matched (JSON or comma-separated)

        [MappingsTransferred] INT NOT NULL, -- Count of mappings moved

        [MigratedBy] NVARCHAR(100) NOT NULL,
        [MigratedAt] DATETIME NOT NULL DEFAULT GETDATE(),

        [VirtualPartData] NVARCHAR(MAX) NULL -- JSON snapshot of virtual part before deletion
    );

    CREATE INDEX [IX_VirtualPartMigrationLog_VirtualPart] ON [dbo].[VirtualPartMigrationLog]([VirtualPartNumber]);
    CREATE INDEX [IX_VirtualPartMigrationLog_RealPart] ON [dbo].[VirtualPartMigrationLog]([RealPartNumber]);

    PRINT 'Created table: VirtualPartMigrationLog';
END
ELSE
BEGIN
    PRINT 'Table VirtualPartMigrationLog already exists';
END
GO

PRINT 'Migration 06 completed successfully';
GO
