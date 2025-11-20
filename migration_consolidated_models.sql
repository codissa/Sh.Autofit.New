-- =============================================
-- MIGRATION: Consolidated Vehicle Models with Model & Part Coupling
-- Database: Sh.Autofit
-- Version: 2.0
-- Description: Migrate from individual vehicle records to consolidated models
--              with year ranges, model coupling, and part coupling
-- =============================================

USE [Sh.Autofit];
GO

PRINT 'Starting migration to consolidated vehicle models...';
GO

-- =============================================
-- STEP 1: CREATE NEW TABLE - ConsolidatedVehicleModels
-- =============================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ConsolidatedVehicleModels')
BEGIN
    PRINT 'Creating ConsolidatedVehicleModels table...';

    CREATE TABLE dbo.ConsolidatedVehicleModels (
        ConsolidatedModelId INT IDENTITY(1,1) PRIMARY KEY,

        -- Foreign Key to Manufacturer
        ManufacturerId INT NOT NULL,

        -- Natural Key Components (from Government API)
        ManufacturerCode INT NOT NULL,
        ModelCode INT NOT NULL,
        ModelName NVARCHAR(200) NOT NULL,

        -- Uniqueness Attributes
        EngineVolume INT NULL,
        TrimLevel NVARCHAR(100) NULL,
        FinishLevel NVARCHAR(100) NULL,
        TransmissionType NVARCHAR(50) NULL,
        FuelTypeCode INT NULL,
        FuelTypeName NVARCHAR(50) NULL,
        NumberOfDoors INT NULL,
        Horsepower INT NULL,

        -- Year Range (Auto-expanding)
        YearFrom INT NOT NULL,
        YearTo INT NULL,

        -- Additional Common Attributes
        CommercialName NVARCHAR(100) NULL,
        EngineModel NVARCHAR(100) NULL,
        VehicleCategory NVARCHAR(100) NULL,
        EmissionGroup INT NULL,
        GreenIndex INT NULL,
        SafetyRating DECIMAL(3,2) NULL,
        SafetyLevel INT NULL,

        -- Metadata
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIME2(3) NOT NULL DEFAULT GETDATE(),
        UpdatedAt DATETIME2(3) NOT NULL DEFAULT GETDATE(),
        CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'MIGRATION',
        UpdatedBy NVARCHAR(100) NULL,

        -- Foreign Key Constraint
        CONSTRAINT FK_ConsolidatedModels_Manufacturer
            FOREIGN KEY (ManufacturerId) REFERENCES dbo.Manufacturers(ManufacturerId),

        -- Unique Constraint on Natural Key (ensures no duplicates)
        CONSTRAINT UQ_ConsolidatedModels_NaturalKey UNIQUE (
            ManufacturerCode,
            ModelCode,
            ModelName,
            EngineVolume,
            TrimLevel,
            FinishLevel,
            TransmissionType,
            FuelTypeCode,
            NumberOfDoors,
            Horsepower
        ),

        -- Indexes for Performance
        INDEX IX_ConsolidatedModels_Manufacturer (ManufacturerId),
        INDEX IX_ConsolidatedModels_ManufacturerCode (ManufacturerCode),
        INDEX IX_ConsolidatedModels_ModelCode (ModelCode),
        INDEX IX_ConsolidatedModels_ManufacturerModel (ManufacturerCode, ModelCode),
        INDEX IX_ConsolidatedModels_YearRange (YearFrom, YearTo),
        INDEX IX_ConsolidatedModels_Active (IsActive)
    );

    PRINT 'ConsolidatedVehicleModels table created successfully.';
END
ELSE
BEGIN
    PRINT 'ConsolidatedVehicleModels table already exists.';
END
GO

-- =============================================
-- STEP 2: ADD LINK FROM VehicleTypes TO ConsolidatedVehicleModels
-- =============================================

IF NOT EXISTS (
    SELECT * FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.VehicleTypes')
    AND name = 'ConsolidatedModelId'
)
BEGIN
    PRINT 'Adding ConsolidatedModelId to VehicleTypes for audit trail...';

    ALTER TABLE dbo.VehicleTypes
    ADD ConsolidatedModelId INT NULL;

    ALTER TABLE dbo.VehicleTypes
    ADD CONSTRAINT FK_VehicleTypes_ConsolidatedModel
        FOREIGN KEY (ConsolidatedModelId)
        REFERENCES dbo.ConsolidatedVehicleModels(ConsolidatedModelId);

    CREATE INDEX IX_VehicleTypes_ConsolidatedModel
        ON dbo.VehicleTypes(ConsolidatedModelId);

    PRINT 'ConsolidatedModelId added to VehicleTypes.';
END
ELSE
BEGIN
    PRINT 'ConsolidatedModelId already exists in VehicleTypes.';
END
GO

-- =============================================
-- STEP 3: CREATE NEW TABLE - ModelCouplings
-- =============================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ModelCouplings')
BEGIN
    PRINT 'Creating ModelCouplings table...';

    CREATE TABLE dbo.ModelCouplings (
        ModelCouplingId INT IDENTITY(1,1) PRIMARY KEY,

        -- The two models being coupled (bidirectional)
        ConsolidatedModelId_A INT NOT NULL,
        ConsolidatedModelId_B INT NOT NULL,

        -- Coupling Type and Notes
        CouplingType NVARCHAR(50) NOT NULL DEFAULT 'SameParts', -- 'SameParts', 'Similar', 'Compatible'
        Notes NVARCHAR(500) NULL,

        -- Audit Fields
        CreatedAt DATETIME2(3) NOT NULL DEFAULT GETDATE(),
        CreatedBy NVARCHAR(100) NOT NULL,
        UpdatedAt DATETIME2(3) NOT NULL DEFAULT GETDATE(),
        UpdatedBy NVARCHAR(100) NULL,
        IsActive BIT NOT NULL DEFAULT 1,

        -- Foreign Key Constraints
        CONSTRAINT FK_ModelCouplings_ModelA
            FOREIGN KEY (ConsolidatedModelId_A)
            REFERENCES dbo.ConsolidatedVehicleModels(ConsolidatedModelId),
        CONSTRAINT FK_ModelCouplings_ModelB
            FOREIGN KEY (ConsolidatedModelId_B)
            REFERENCES dbo.ConsolidatedVehicleModels(ConsolidatedModelId),

        -- Prevent self-coupling and ensure A < B for consistency
        CONSTRAINT CK_ModelCouplings_NoSelfCoupling
            CHECK (ConsolidatedModelId_A <> ConsolidatedModelId_B),
        CONSTRAINT CK_ModelCouplings_OrderedIds
            CHECK (ConsolidatedModelId_A < ConsolidatedModelId_B),

        -- Unique Constraint (prevent duplicate couplings)
        CONSTRAINT UQ_ModelCouplings_Pair UNIQUE (ConsolidatedModelId_A, ConsolidatedModelId_B),

        -- Indexes
        INDEX IX_ModelCouplings_ModelA (ConsolidatedModelId_A),
        INDEX IX_ModelCouplings_ModelB (ConsolidatedModelId_B),
        INDEX IX_ModelCouplings_Active (IsActive)
    );

    PRINT 'ModelCouplings table created successfully.';
END
ELSE
BEGIN
    PRINT 'ModelCouplings table already exists.';
END
GO

-- =============================================
-- STEP 4: CREATE NEW TABLE - PartCouplings
-- =============================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PartCouplings')
BEGIN
    PRINT 'Creating PartCouplings table...';

    CREATE TABLE dbo.PartCouplings (
        PartCouplingId INT IDENTITY(1,1) PRIMARY KEY,

        -- The two parts being coupled (bidirectional)
        PartItemKey_A NVARCHAR(20) NOT NULL,
        PartItemKey_B NVARCHAR(20) NOT NULL,

        -- Coupling Type and Notes
        CouplingType NVARCHAR(50) NOT NULL DEFAULT 'Compatible', -- 'Synonym', 'Compatible', 'Superseded', 'Alternative'
        Notes NVARCHAR(500) NULL,

        -- Audit Fields
        CreatedAt DATETIME2(3) NOT NULL DEFAULT GETDATE(),
        CreatedBy NVARCHAR(100) NOT NULL,
        UpdatedAt DATETIME2(3) NOT NULL DEFAULT GETDATE(),
        UpdatedBy NVARCHAR(100) NULL,
        IsActive BIT NOT NULL DEFAULT 1,

        -- Prevent self-coupling and ensure A < B for consistency
        CONSTRAINT CK_PartCouplings_NoSelfCoupling
            CHECK (PartItemKey_A <> PartItemKey_B),
        CONSTRAINT CK_PartCouplings_OrderedKeys
            CHECK (PartItemKey_A < PartItemKey_B),

        -- Unique Constraint (prevent duplicate couplings)
        CONSTRAINT UQ_PartCouplings_Pair UNIQUE (PartItemKey_A, PartItemKey_B),

        -- Indexes
        INDEX IX_PartCouplings_PartA (PartItemKey_A),
        INDEX IX_PartCouplings_PartB (PartItemKey_B),
        INDEX IX_PartCouplings_Active (IsActive)
    );

    PRINT 'PartCouplings table created successfully.';
END
ELSE
BEGIN
    PRINT 'PartCouplings table already exists.';
END
GO

-- =============================================
-- STEP 5: MODIFY VehiclePartsMappings TABLE
-- =============================================

-- Add ConsolidatedModelId column
IF NOT EXISTS (
    SELECT * FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.VehiclePartsMappings')
    AND name = 'ConsolidatedModelId'
)
BEGIN
    PRINT 'Adding ConsolidatedModelId to VehiclePartsMappings...';

    ALTER TABLE dbo.VehiclePartsMappings
    ADD ConsolidatedModelId INT NULL;

    ALTER TABLE dbo.VehiclePartsMappings
    ADD CONSTRAINT FK_VPMappings_ConsolidatedModel
        FOREIGN KEY (ConsolidatedModelId)
        REFERENCES dbo.ConsolidatedVehicleModels(ConsolidatedModelId);

    CREATE INDEX IX_VPMappings_ConsolidatedModel
        ON dbo.VehiclePartsMappings(ConsolidatedModelId);

    PRINT 'ConsolidatedModelId added to VehiclePartsMappings.';
END
ELSE
BEGIN
    PRINT 'ConsolidatedModelId already exists in VehiclePartsMappings.';
END
GO

-- Make VehicleTypeId nullable (for consolidated mappings)
IF EXISTS (
    SELECT * FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.VehiclePartsMappings')
    AND name = 'VehicleTypeId'
    AND is_nullable = 0
)
BEGIN
    PRINT 'Making VehicleTypeId nullable in VehiclePartsMappings...';

    -- Drop the existing FK constraint
    ALTER TABLE dbo.VehiclePartsMappings
    DROP CONSTRAINT FK_VPMappings_VehicleType;

    -- Drop the existing unique constraint
    ALTER TABLE dbo.VehiclePartsMappings
    DROP CONSTRAINT UQ_VehiclePartsMappings_Current;

    -- Make VehicleTypeId nullable
    ALTER TABLE dbo.VehiclePartsMappings
    ALTER COLUMN VehicleTypeId INT NULL;

    -- Re-add FK constraint
    ALTER TABLE dbo.VehiclePartsMappings
    ADD CONSTRAINT FK_VPMappings_VehicleType
        FOREIGN KEY (VehicleTypeId)
        REFERENCES dbo.VehicleTypes(VehicleTypeId);

    PRINT 'VehicleTypeId is now nullable.';
END
ELSE
BEGIN
    PRINT 'VehicleTypeId is already nullable.';
END
GO

-- Add MappingLevel column
IF NOT EXISTS (
    SELECT * FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.VehiclePartsMappings')
    AND name = 'MappingLevel'
)
BEGIN
    PRINT 'Adding MappingLevel to VehiclePartsMappings...';

    ALTER TABLE dbo.VehiclePartsMappings
    ADD MappingLevel NVARCHAR(20) NOT NULL DEFAULT 'Legacy'; -- 'Legacy', 'Consolidated'

    CREATE INDEX IX_VPMappings_MappingLevel
        ON dbo.VehiclePartsMappings(MappingLevel);

    PRINT 'MappingLevel added to VehiclePartsMappings.';
END
ELSE
BEGIN
    PRINT 'MappingLevel already exists in VehiclePartsMappings.';
END
GO

-- Add check constraint: either VehicleTypeId OR ConsolidatedModelId must be set
IF NOT EXISTS (
    SELECT * FROM sys.check_constraints
    WHERE name = 'CK_VPMappings_OneIdRequired'
)
BEGIN
    PRINT 'Adding check constraint for VehicleTypeId/ConsolidatedModelId...';

    ALTER TABLE dbo.VehiclePartsMappings
    ADD CONSTRAINT CK_VPMappings_OneIdRequired
        CHECK (
            (VehicleTypeId IS NOT NULL AND ConsolidatedModelId IS NULL) OR
            (VehicleTypeId IS NULL AND ConsolidatedModelId IS NOT NULL)
        );

    PRINT 'Check constraint added.';
END
ELSE
BEGIN
    PRINT 'Check constraint already exists.';
END
GO

-- Re-create unique constraint with new logic
IF NOT EXISTS (
    SELECT * FROM sys.indexes
    WHERE name = 'UQ_VehiclePartsMappings_Current'
)
BEGIN
    PRINT 'Recreating unique constraint for VehiclePartsMappings...';

    -- For legacy mappings (VehicleTypeId-based)
    CREATE UNIQUE INDEX UQ_VehiclePartsMappings_Current
    ON dbo.VehiclePartsMappings(VehicleTypeId, PartItemKey, IsCurrentVersion, IsActive)
    WHERE VehicleTypeId IS NOT NULL;

    -- For consolidated mappings (ConsolidatedModelId-based)
    CREATE UNIQUE INDEX UQ_VehiclePartsMappings_Consolidated
    ON dbo.VehiclePartsMappings(ConsolidatedModelId, PartItemKey, IsCurrentVersion, IsActive)
    WHERE ConsolidatedModelId IS NOT NULL;

    PRINT 'Unique constraints recreated.';
END
ELSE
BEGIN
    PRINT 'Unique constraint already exists.';
END
GO

-- =============================================
-- STEP 6: CREATE HELPER FUNCTION - Get Coupled Models
-- =============================================

CREATE OR ALTER FUNCTION dbo.fn_GetCoupledModels(@ConsolidatedModelId INT)
RETURNS TABLE
AS
RETURN
(
    -- Get all models coupled to the input model (bidirectional)
    SELECT DISTINCT ConsolidatedModelId
    FROM (
        -- Models where input is Model A
        SELECT ConsolidatedModelId_B AS ConsolidatedModelId
        FROM dbo.ModelCouplings
        WHERE ConsolidatedModelId_A = @ConsolidatedModelId
          AND IsActive = 1

        UNION

        -- Models where input is Model B
        SELECT ConsolidatedModelId_A AS ConsolidatedModelId
        FROM dbo.ModelCouplings
        WHERE ConsolidatedModelId_B = @ConsolidatedModelId
          AND IsActive = 1

        UNION

        -- Include the input model itself
        SELECT @ConsolidatedModelId AS ConsolidatedModelId
    ) AS CoupledModels
);
GO

PRINT 'Created function: fn_GetCoupledModels';
GO

-- =============================================
-- STEP 7: CREATE HELPER FUNCTION - Get Coupled Parts
-- =============================================

CREATE OR ALTER FUNCTION dbo.fn_GetCoupledParts(@PartItemKey NVARCHAR(20))
RETURNS TABLE
AS
RETURN
(
    -- Get all parts coupled to the input part (bidirectional)
    SELECT DISTINCT PartItemKey
    FROM (
        -- Parts where input is Part A
        SELECT PartItemKey_B AS PartItemKey
        FROM dbo.PartCouplings
        WHERE PartItemKey_A = @PartItemKey
          AND IsActive = 1

        UNION

        -- Parts where input is Part B
        SELECT PartItemKey_A AS PartItemKey
        FROM dbo.PartCouplings
        WHERE PartItemKey_B = @PartItemKey
          AND IsActive = 1

        UNION

        -- Include the input part itself
        SELECT @PartItemKey AS PartItemKey
    ) AS CoupledParts
);
GO

PRINT 'Created function: fn_GetCoupledParts';
GO

-- =============================================
-- STEP 8: CREATE STORED PROCEDURE - Get Consolidated Models by Lookup
-- =============================================

CREATE OR ALTER PROCEDURE dbo.sp_GetConsolidatedModelsForLookup
    @ManufacturerCode INT,
    @ModelCode INT,
    @Year INT = NULL,
    @IncludeInactive BIT = 0
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        cm.*,
        m.ManufacturerName,
        m.CountryOfOrigin,
        (SELECT COUNT(*)
         FROM dbo.VehiclePartsMappings vpm
         WHERE vpm.ConsolidatedModelId = cm.ConsolidatedModelId
           AND vpm.IsActive = 1
           AND vpm.IsCurrentVersion = 1) AS MappingCount
    FROM dbo.ConsolidatedVehicleModels cm
    INNER JOIN dbo.Manufacturers m ON cm.ManufacturerId = m.ManufacturerId
    WHERE cm.ManufacturerCode = @ManufacturerCode
      AND cm.ModelCode = @ModelCode
      AND (@Year IS NULL OR (@Year >= cm.YearFrom AND (@Year <= cm.YearTo OR cm.YearTo IS NULL)))
      AND (@IncludeInactive = 1 OR cm.IsActive = 1)
    ORDER BY cm.YearFrom DESC, cm.EngineVolume, cm.Horsepower;
END;
GO

PRINT 'Created procedure: sp_GetConsolidatedModelsForLookup';
GO

-- =============================================
-- STEP 9: CREATE STORED PROCEDURE - Get Parts for Consolidated Model (with couplings)
-- =============================================

CREATE OR ALTER PROCEDURE dbo.sp_GetPartsForConsolidatedModel
    @ConsolidatedModelId INT,
    @IncludeInactive BIT = 0,
    @IncludeCoupledModels BIT = 1,
    @IncludeCoupledParts BIT = 1
AS
BEGIN
    SET NOCOUNT ON;

    -- Get parts for this model and all coupled models
    SELECT DISTINCT
        vpm.MappingId,
        vpm.PartItemKey,
        vp.*,
        vpm.Priority,
        vpm.FitsYearFrom,
        vpm.FitsYearTo,
        vpm.RequiresModification,
        vpm.CompatibilityNotes,
        vpm.InstallationNotes,
        vpm.ConfidenceScore,
        vpm.MappingSource,
        vpm.MappingLevel,
        vpm.UpdatedAt AS MappingUpdatedAt,
        CASE
            WHEN vpm.ConsolidatedModelId = @ConsolidatedModelId THEN 'Direct'
            WHEN vpm.ConsolidatedModelId IN (SELECT ConsolidatedModelId FROM dbo.fn_GetCoupledModels(@ConsolidatedModelId)) THEN 'CoupledModel'
            WHEN vpm.PartItemKey IN (SELECT PartItemKey FROM dbo.fn_GetCoupledParts(vpm.PartItemKey)) THEN 'CoupledPart'
            ELSE 'Unknown'
        END AS MappingType
    FROM dbo.VehiclePartsMappings vpm
    INNER JOIN dbo.vw_Parts vp ON vpm.PartItemKey = vp.PartNumber
    WHERE vpm.IsCurrentVersion = 1
      AND (@IncludeInactive = 1 OR vpm.IsActive = 1)
      AND vp.IsActive = 1
      AND (
          -- Direct mappings to this model
          vpm.ConsolidatedModelId = @ConsolidatedModelId

          -- Mappings to coupled models
          OR (@IncludeCoupledModels = 1 AND vpm.ConsolidatedModelId IN (
              SELECT ConsolidatedModelId FROM dbo.fn_GetCoupledModels(@ConsolidatedModelId)
          ))
      )
    ORDER BY vpm.Priority DESC, vp.PartName;
END;
GO

PRINT 'Created procedure: sp_GetPartsForConsolidatedModel';
GO

-- =============================================
-- STEP 10: CREATE STORED PROCEDURE - Get Vehicles for Part (with couplings)
-- =============================================

CREATE OR ALTER PROCEDURE dbo.sp_GetConsolidatedModelsForPart
    @PartItemKey NVARCHAR(20),
    @IncludeInactive BIT = 0,
    @IncludeCoupledModels BIT = 1,
    @IncludeCoupledParts BIT = 1
AS
BEGIN
    SET NOCOUNT ON;

    -- Get all coupled parts first
    DECLARE @CoupledParts TABLE (PartItemKey NVARCHAR(20));

    IF @IncludeCoupledParts = 1
    BEGIN
        INSERT INTO @CoupledParts
        SELECT PartItemKey FROM dbo.fn_GetCoupledParts(@PartItemKey);
    END
    ELSE
    BEGIN
        INSERT INTO @CoupledParts VALUES (@PartItemKey);
    END

    -- Get consolidated models mapped to these parts and their coupled models
    SELECT DISTINCT
        vpm.MappingId,
        cm.*,
        m.ManufacturerName,
        vpm.Priority,
        vpm.FitsYearFrom,
        vpm.FitsYearTo,
        vpm.RequiresModification,
        vpm.CompatibilityNotes,
        vpm.MappingSource,
        vpm.MappingLevel,
        vpm.UpdatedAt AS MappingUpdatedAt,
        CASE
            WHEN vpm.PartItemKey = @PartItemKey AND vpm.ConsolidatedModelId = cm.ConsolidatedModelId THEN 'Direct'
            WHEN vpm.PartItemKey <> @PartItemKey THEN 'CoupledPart'
            WHEN vpm.ConsolidatedModelId IN (SELECT ConsolidatedModelId FROM dbo.fn_GetCoupledModels(cm.ConsolidatedModelId)) THEN 'CoupledModel'
            ELSE 'Unknown'
        END AS MappingType
    FROM dbo.VehiclePartsMappings vpm
    INNER JOIN dbo.ConsolidatedVehicleModels cm ON vpm.ConsolidatedModelId = cm.ConsolidatedModelId
    INNER JOIN dbo.Manufacturers m ON cm.ManufacturerId = m.ManufacturerId
    WHERE vpm.PartItemKey IN (SELECT PartItemKey FROM @CoupledParts)
      AND vpm.IsCurrentVersion = 1
      AND (@IncludeInactive = 1 OR vpm.IsActive = 1)
      AND cm.IsActive = 1
    ORDER BY m.ManufacturerName, cm.ModelName, cm.YearFrom;
END;
GO

PRINT 'Created procedure: sp_GetConsolidatedModelsForPart';
GO

-- =============================================
-- STEP 11: CREATE STORED PROCEDURE - Upsert Consolidated Mapping
-- =============================================

CREATE OR ALTER PROCEDURE dbo.sp_UpsertConsolidatedMapping
    @ConsolidatedModelId INT,
    @PartItemKey NVARCHAR(20),
    @MappingSource NVARCHAR(50),
    @ConfidenceScore DECIMAL(5,2) = NULL,
    @Priority INT = 0,
    @FitsYearFrom INT = NULL,
    @FitsYearTo INT = NULL,
    @RequiresModification BIT = 0,
    @CompatibilityNotes NVARCHAR(500) = NULL,
    @InstallationNotes NVARCHAR(1000) = NULL,
    @Username NVARCHAR(100),
    @ChangeReason NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    DECLARE @MappingId INT;
    DECLARE @NewVersionNumber INT;

    -- Check if mapping exists
    SELECT @MappingId = MappingId, @NewVersionNumber = VersionNumber
    FROM dbo.VehiclePartsMappings
    WHERE ConsolidatedModelId = @ConsolidatedModelId
      AND PartItemKey = @PartItemKey
      AND IsCurrentVersion = 1;

    IF @MappingId IS NOT NULL
    BEGIN
        -- Mark current version as old
        UPDATE dbo.VehiclePartsMappings
        SET IsCurrentVersion = 0
        WHERE MappingId = @MappingId;

        -- Increment version
        SET @NewVersionNumber = @NewVersionNumber + 1;

        -- Insert history record
        INSERT INTO dbo.VehiclePartsMappingsHistory (
            MappingId, VehicleTypeId, PartItemKey, MappingSource, ConfidenceScore,
            Priority, FitsYearFrom, FitsYearTo, RequiresModification, CompatibilityNotes,
            InstallationNotes, VersionNumber, ChangeType, ChangeReason, ChangedBy
        )
        SELECT
            MappingId, VehicleTypeId, PartItemKey, MappingSource, ConfidenceScore,
            Priority, FitsYearFrom, FitsYearTo, RequiresModification, CompatibilityNotes,
            InstallationNotes, VersionNumber, 'Updated', @ChangeReason, @Username
        FROM dbo.VehiclePartsMappings
        WHERE MappingId = @MappingId;
    END
    ELSE
    BEGIN
        SET @NewVersionNumber = 1;
    END

    -- Insert new version
    INSERT INTO dbo.VehiclePartsMappings (
        ConsolidatedModelId, PartItemKey, MappingSource, ConfidenceScore, Priority,
        FitsYearFrom, FitsYearTo, RequiresModification, CompatibilityNotes,
        InstallationNotes, VersionNumber, IsCurrentVersion, MappingLevel, CreatedBy, UpdatedBy
    )
    VALUES (
        @ConsolidatedModelId, @PartItemKey, @MappingSource, @ConfidenceScore, @Priority,
        @FitsYearFrom, @FitsYearTo, @RequiresModification, @CompatibilityNotes,
        @InstallationNotes, @NewVersionNumber, 1, 'Consolidated', @Username, @Username
    );

    SET @MappingId = SCOPE_IDENTITY();

    COMMIT TRANSACTION;

    SELECT @MappingId AS MappingId, @NewVersionNumber AS VersionNumber;
END;
GO

PRINT 'Created procedure: sp_UpsertConsolidatedMapping';
GO

-- =============================================
-- STEP 12: CREATE STORED PROCEDURE - Auto-Expand Year Range
-- =============================================

CREATE OR ALTER PROCEDURE dbo.sp_AutoExpandYearRange
    @ConsolidatedModelId INT,
    @NewYear INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @CurrentYearFrom INT;
    DECLARE @CurrentYearTo INT;

    SELECT @CurrentYearFrom = YearFrom, @CurrentYearTo = YearTo
    FROM dbo.ConsolidatedVehicleModels
    WHERE ConsolidatedModelId = @ConsolidatedModelId;

    IF @NewYear < @CurrentYearFrom OR @NewYear > ISNULL(@CurrentYearTo, @NewYear)
    BEGIN
        UPDATE dbo.ConsolidatedVehicleModels
        SET YearFrom = CASE WHEN @NewYear < YearFrom THEN @NewYear ELSE YearFrom END,
            YearTo = CASE WHEN @NewYear > ISNULL(YearTo, @NewYear) THEN @NewYear ELSE YearTo END,
            UpdatedAt = GETDATE(),
            UpdatedBy = 'AUTO_EXPAND'
        WHERE ConsolidatedModelId = @ConsolidatedModelId;

        PRINT 'Year range expanded for ConsolidatedModelId=' + CAST(@ConsolidatedModelId AS NVARCHAR) + ' to include year ' + CAST(@NewYear AS NVARCHAR);
    END
END;
GO

PRINT 'Created procedure: sp_AutoExpandYearRange';
GO

-- =============================================
-- MIGRATION COMPLETE
-- =============================================

PRINT '';
PRINT '========================================';
PRINT 'Migration completed successfully!';
PRINT '========================================';
PRINT '';
PRINT 'New tables created:';
PRINT '  - ConsolidatedVehicleModels';
PRINT '  - ModelCouplings';
PRINT '  - PartCouplings';
PRINT '';
PRINT 'Modified tables:';
PRINT '  - VehicleTypes (added ConsolidatedModelId)';
PRINT '  - VehiclePartsMappings (added ConsolidatedModelId, MappingLevel; made VehicleTypeId nullable)';
PRINT '';
PRINT 'New stored procedures:';
PRINT '  - sp_GetConsolidatedModelsForLookup';
PRINT '  - sp_GetPartsForConsolidatedModel';
PRINT '  - sp_GetConsolidatedModelsForPart';
PRINT '  - sp_UpsertConsolidatedMapping';
PRINT '  - sp_AutoExpandYearRange';
PRINT '';
PRINT 'New functions:';
PRINT '  - fn_GetCoupledModels';
PRINT '  - fn_GetCoupledParts';
PRINT '';
PRINT 'Next steps:';
PRINT '  1. Run data migration script to consolidate existing vehicles';
PRINT '  2. Update Entity Framework models';
PRINT '  3. Update application code to use consolidated models';
PRINT '  4. Test coupling functionality';
PRINT '';
GO
