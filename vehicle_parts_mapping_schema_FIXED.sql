-- =============================================
-- Vehicle Parts Mapping System - FIXED FOR YOUR SH2013
-- Database Name: Sh.Autofit
-- Existing Parts Database: SH2013 (on same server)
-- Version: 1.1 - CORRECTED for ItemKey field
-- Description: Complete schema for mapping vehicle parts to car types
-- FIXED: Uses ItemKey (not KeF) to match your actual Items table
-- =============================================

-- Create the database
USE master;
GO

IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'Sh.Autofit')
BEGIN
    CREATE DATABASE [Sh.Autofit];
    PRINT 'Database Sh.Autofit created successfully';
END
ELSE
BEGIN
    PRINT 'Database Sh.Autofit already exists';
END
GO

USE [Sh.Autofit];
GO

-- Enable snapshot isolation for better concurrency
ALTER DATABASE [Sh.Autofit] SET ALLOW_SNAPSHOT_ISOLATION ON;
ALTER DATABASE [Sh.Autofit] SET READ_COMMITTED_SNAPSHOT ON;
GO

-- =============================================
-- SECTION 1: CORE LOOKUP TABLES
-- =============================================

-- Manufacturers/Makes
CREATE TABLE dbo.Manufacturers (
    ManufacturerId INT IDENTITY(1,1) PRIMARY KEY,
    ManufacturerCode INT NOT NULL UNIQUE,
    ManufacturerName NVARCHAR(200) NOT NULL,
    CountryOfOrigin NVARCHAR(100),
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2(3) NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME2(3) NOT NULL DEFAULT GETDATE(),
    LastSyncedAt DATETIME2(3) NULL,
    
    INDEX IX_Manufacturers_Code (ManufacturerCode),
    INDEX IX_Manufacturers_Name (ManufacturerName)
);

-- Vehicle Types (Models/Variants)
CREATE TABLE dbo.VehicleTypes (
    VehicleTypeId INT IDENTITY(1,1) PRIMARY KEY,
    ManufacturerId INT NOT NULL,
    ModelCode INT NOT NULL,
    ModelName NVARCHAR(200) NOT NULL,
    CommercialName NVARCHAR(100),
    YearFrom INT NOT NULL,
    YearTo INT NULL,
    
    -- Technical specifications
    EngineVolume INT NULL,
    TotalWeight INT NULL,
    EngineModel NVARCHAR(100) NULL,
    FuelTypeCode INT NULL,
    FuelTypeName NVARCHAR(50) NULL,
    TransmissionType NVARCHAR(50) NULL,
    NumberOfDoors INT NULL,
    NumberOfSeats INT NULL,
    Horsepower INT NULL,
    
    -- Classification
    TrimLevel NVARCHAR(100) NULL,
    VehicleCategory NVARCHAR(100) NULL,
    EmissionGroup INT NULL,
    GreenIndex INT NULL,
    SafetyRating DECIMAL(3,2) NULL,
    SafetyLevel INT NULL,
    
    -- Tire specifications
    FrontTireSize NVARCHAR(50) NULL,
    RearTireSize NVARCHAR(50) NULL,
    
    -- Additional data stored as JSON
    AdditionalSpecs NVARCHAR(MAX) NULL,
    
    -- Metadata
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2(3) NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME2(3) NOT NULL DEFAULT GETDATE(),
    LastSyncedAt DATETIME2(3) NULL,
    
    CONSTRAINT FK_VehicleTypes_Manufacturer FOREIGN KEY (ManufacturerId) 
        REFERENCES dbo.Manufacturers(ManufacturerId),
    
    INDEX IX_VehicleTypes_Manufacturer (ManufacturerId),
    INDEX IX_VehicleTypes_ModelCode (ModelCode),
    INDEX IX_VehicleTypes_CommercialName (CommercialName),
    INDEX IX_VehicleTypes_YearFrom (YearFrom),
    INDEX IX_VehicleTypes_Category (VehicleCategory)
);

-- Vehicle Registrations (from license plate lookups)
CREATE TABLE dbo.VehicleRegistrations (
    RegistrationId INT IDENTITY(1,1) PRIMARY KEY,
    LicensePlate NVARCHAR(20) NOT NULL UNIQUE,
    VehicleTypeId INT NULL,
    ManufacturerId INT NULL,
    
    -- Data from gov API
    RegistrationYear INT NULL,
    Color NVARCHAR(50) NULL,
    CurrentOwner NVARCHAR(200) NULL,
    VIN NVARCHAR(50) NULL,
    
    -- Metadata
    FirstLookupDate DATETIME2(3) NOT NULL DEFAULT GETDATE(),
    LastLookupDate DATETIME2(3) NOT NULL DEFAULT GETDATE(),
    LookupCount INT NOT NULL DEFAULT 1,
    IsActive BIT NOT NULL DEFAULT 1,
    
    CONSTRAINT FK_VehicleReg_VehicleType FOREIGN KEY (VehicleTypeId) 
        REFERENCES dbo.VehicleTypes(VehicleTypeId),
    CONSTRAINT FK_VehicleReg_Manufacturer FOREIGN KEY (ManufacturerId) 
        REFERENCES dbo.Manufacturers(ManufacturerId),
    
    INDEX IX_VehicleReg_LicensePlate (LicensePlate),
    INDEX IX_VehicleReg_VehicleType (VehicleTypeId)
);

-- =============================================
-- SECTION 2: PARTS METADATA (Extension of SH2013.Items)
-- =============================================

-- Additional metadata for parts that doesn't exist in SH2013
CREATE TABLE dbo.PartsMetadata (
    PartMetadataId INT IDENTITY(1,1) PRIMARY KEY,
    ItemKey NVARCHAR(20) NOT NULL UNIQUE, -- FIXED: Now uses ItemKey to match SH2013.dbo.Items.ItemKey
    
    -- Additional fields not in SH2013
    CompatibilityNotes NVARCHAR(500),
    UniversalPart BIT NOT NULL DEFAULT 0,
    ImageUrl NVARCHAR(500),
    DatasheetUrl NVARCHAR(500),
    
    -- Override fields (if we want to override SH2013 data)
    CustomDescription NVARCHAR(1000),
    UseCustomDescription BIT NOT NULL DEFAULT 0,
    
    -- Metadata
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2(3) NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME2(3) NOT NULL DEFAULT GETDATE(),
    CreatedBy NVARCHAR(100),
    UpdatedBy NVARCHAR(100),
    
    INDEX IX_PartsMetadata_ItemKey (ItemKey),
    INDEX IX_PartsMetadata_Universal (UniversalPart)
);

-- =============================================
-- SECTION 3: CROSS-DATABASE VIEW FOR PARTS
-- =============================================

-- View to query parts from SH2013 with metadata from Sh.Autofit
CREATE OR ALTER VIEW dbo.vw_Parts AS
SELECT 
    i.ItemKey AS PartNumber,           -- FIXED: Uses ItemKey
    i.ItemName AS PartName,             -- FIXED: Uses ItemName
    i.Price AS RetailPrice,             -- FIXED: Uses Price
    i.PurchPrice AS CostPrice,          -- FIXED: Uses PurchPrice
    i.Quantity AS StockQuantity,        -- FIXED: Uses Quantity
    
    -- OEM numbers from ExtraNotes (NoteID 2, 5, 6, 7, 8)
    -- Note: ExtraNotes uses KeF field to link to Items.ItemKey
    oem1.Note AS OEMNumber1,
    oem2.Note AS OEMNumber2,
    oem3.Note AS OEMNumber3,
    oem4.Note AS OEMNumber4,
    oem5.Note AS OEMNumber5,
    
    -- Metadata from ExtraNotes
    mfr.Note AS Manufacturer,
    model.Note AS Model,
    category.Note AS Category,
    boxDims.Note AS BoxDimensions,
    
    -- Stock status
    CASE WHEN ISNULL(i.Quantity, 0) > 0 THEN 1 ELSE 0 END AS IsInStock,
    CASE WHEN ISNULL(i.IntrItem, 0) = 0 THEN 1 ELSE 0 END AS IsActive, -- FIXED: IntrItem = 0 means active
    
    -- Metadata from PartsMetadata table (if exists)
    pm.CompatibilityNotes,
    ISNULL(pm.UniversalPart, 0) AS UniversalPart,
    pm.ImageUrl,
    pm.DatasheetUrl,
    pm.CustomDescription,
    ISNULL(pm.UseCustomDescription, 0) AS UseCustomDescription,
    pm.UpdatedAt AS MetadataUpdatedAt
    
FROM SH2013.dbo.Items i
-- Note: ExtraNotes.KeF links to Items.ItemKey
LEFT JOIN SH2013.dbo.ExtraNotes oem1 ON i.ItemKey = oem1.KeF AND oem1.NoteID = 2 AND oem1.ItemFlag = 1
LEFT JOIN SH2013.dbo.ExtraNotes oem2 ON i.ItemKey = oem2.KeF AND oem2.NoteID = 5 AND oem2.ItemFlag = 1
LEFT JOIN SH2013.dbo.ExtraNotes oem3 ON i.ItemKey = oem3.KeF AND oem3.NoteID = 6 AND oem3.ItemFlag = 1
LEFT JOIN SH2013.dbo.ExtraNotes oem4 ON i.ItemKey = oem4.KeF AND oem4.NoteID = 7 AND oem4.ItemFlag = 1
LEFT JOIN SH2013.dbo.ExtraNotes oem5 ON i.ItemKey = oem5.KeF AND oem5.NoteID = 8 AND oem5.ItemFlag = 1
LEFT JOIN SH2013.dbo.ExtraNotes mfr ON i.ItemKey = mfr.KeF AND mfr.NoteID = 10 AND mfr.ItemFlag = 1
LEFT JOIN SH2013.dbo.ExtraNotes model ON i.ItemKey = model.KeF AND model.NoteID = 11 AND model.ItemFlag = 1
LEFT JOIN SH2013.dbo.ExtraNotes category ON i.ItemKey = category.KeF AND category.NoteID = 12 AND category.ItemFlag = 1
LEFT JOIN SH2013.dbo.ExtraNotes boxDims ON i.ItemKey = boxDims.KeF AND boxDims.NoteID = 9 AND boxDims.ItemFlag = 1
LEFT JOIN dbo.PartsMetadata pm ON i.ItemKey = pm.ItemKey
WHERE ISNULL(i.TreeType, 0) = 0; -- Only items, not folders/categories
GO

-- =============================================
-- SECTION 4: HELPER FUNCTIONS
-- =============================================

-- Function to get primary OEM number
CREATE OR ALTER FUNCTION dbo.fn_GetPrimaryOEM(@ItemKey NVARCHAR(20))
RETURNS NVARCHAR(100)
AS
BEGIN
    DECLARE @OEM NVARCHAR(100);
    
    SELECT TOP 1 @OEM = Note 
    FROM SH2013.dbo.ExtraNotes
    WHERE KeF = @ItemKey 
      AND NoteID IN (2, 5, 6, 7, 8)
      AND ItemFlag = 1
      AND Note IS NOT NULL
      AND LTRIM(RTRIM(Note)) <> ''
    ORDER BY NoteID; -- Prioritize NoteID 2 (primary OEM)
    
    RETURN @OEM;
END;
GO

-- =============================================
-- SECTION 5: VEHICLE-PART MAPPINGS
-- =============================================

-- Main mapping table (current version only)
CREATE TABLE dbo.VehiclePartsMappings (
    MappingId INT IDENTITY(1,1) PRIMARY KEY,
    VehicleTypeId INT NOT NULL,
    PartItemKey NVARCHAR(20) NOT NULL, -- FIXED: Now PartItemKey instead of PartKeF
    
    -- Mapping metadata
    MappingSource NVARCHAR(50) NOT NULL DEFAULT 'Manual',
    ConfidenceScore DECIMAL(5,2) NULL,
    Priority INT NOT NULL DEFAULT 0,
    
    -- Compatibility details
    FitsYearFrom INT NULL,
    FitsYearTo INT NULL,
    RequiresModification BIT NOT NULL DEFAULT 0,
    CompatibilityNotes NVARCHAR(500),
    InstallationNotes NVARCHAR(1000),
    
    -- Version tracking
    VersionNumber INT NOT NULL DEFAULT 1,
    IsCurrentVersion BIT NOT NULL DEFAULT 1,
    
    -- Audit fields
    CreatedAt DATETIME2(3) NOT NULL DEFAULT GETDATE(),
    CreatedBy NVARCHAR(100) NOT NULL,
    UpdatedAt DATETIME2(3) NOT NULL DEFAULT GETDATE(),
    UpdatedBy NVARCHAR(100),
    
    -- Soft delete
    IsActive BIT NOT NULL DEFAULT 1,
    DeactivatedAt DATETIME2(3) NULL,
    DeactivatedBy NVARCHAR(100),
    DeactivationReason NVARCHAR(500),
    
    CONSTRAINT FK_VPMappings_VehicleType FOREIGN KEY (VehicleTypeId) 
        REFERENCES dbo.VehicleTypes(VehicleTypeId),
    
    -- Ensure only one active current version per vehicle-part combination
    CONSTRAINT UQ_VehiclePartsMappings_Current UNIQUE (VehicleTypeId, PartItemKey, IsCurrentVersion, IsActive),
    
    INDEX IX_VPMappings_VehicleType (VehicleTypeId),
    INDEX IX_VPMappings_PartItemKey (PartItemKey),
    INDEX IX_VPMappings_Active (IsActive),
    INDEX IX_VPMappings_CurrentVersion (IsCurrentVersion),
    INDEX IX_VPMappings_Source (MappingSource)
);

-- Mapping history (all versions)
CREATE TABLE dbo.VehiclePartsMappingsHistory (
    MappingHistoryId INT IDENTITY(1,1) PRIMARY KEY,
    MappingId INT NOT NULL,
    VehicleTypeId INT NOT NULL,
    PartItemKey NVARCHAR(20) NOT NULL, -- FIXED
    
    -- Snapshot of mapping data
    MappingSource NVARCHAR(50) NOT NULL,
    ConfidenceScore DECIMAL(5,2) NULL,
    Priority INT NOT NULL,
    FitsYearFrom INT NULL,
    FitsYearTo INT NULL,
    RequiresModification BIT NOT NULL,
    CompatibilityNotes NVARCHAR(500),
    InstallationNotes NVARCHAR(1000),
    
    -- Version info
    VersionNumber INT NOT NULL,
    ChangeType NVARCHAR(50) NOT NULL,
    ChangeReason NVARCHAR(500),
    
    -- Audit
    ChangedAt DATETIME2(3) NOT NULL DEFAULT GETDATE(),
    ChangedBy NVARCHAR(100) NOT NULL,
    
    CONSTRAINT FK_VPMappingsHistory_Mapping FOREIGN KEY (MappingId) 
        REFERENCES dbo.VehiclePartsMappings(MappingId),
    
    INDEX IX_VPMappingsHistory_Mapping (MappingId),
    INDEX IX_VPMappingsHistory_PartItemKey (PartItemKey),
    INDEX IX_VPMappingsHistory_ChangedAt (ChangedAt)
);

-- =============================================
-- SECTION 6: USER MANAGEMENT (SIMPLIFIED - NO LOGIN REQUIRED)
-- =============================================

CREATE TABLE dbo.Users (
    UserId INT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(100) NOT NULL UNIQUE,
    FullName NVARCHAR(200) NOT NULL,
    Email NVARCHAR(200),
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2(3) NOT NULL DEFAULT GETDATE(),
    LastLoginAt DATETIME2(3) NULL,
    
    INDEX IX_Users_Username (Username)
);

-- User activity log
CREATE TABLE dbo.UserActivityLog (
    ActivityId INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NULL,
    Username NVARCHAR(100) NOT NULL,
    ActivityType NVARCHAR(50) NOT NULL,
    EntityType NVARCHAR(50),
    EntityId INT NULL,
    Description NVARCHAR(500),
    IpAddress NVARCHAR(50),
    UserAgent NVARCHAR(500),
    CreatedAt DATETIME2(3) NOT NULL DEFAULT GETDATE(),
    
    CONSTRAINT FK_UserActivity_User FOREIGN KEY (UserId) 
        REFERENCES dbo.Users(UserId),
    
    INDEX IX_UserActivity_User (UserId),
    INDEX IX_UserActivity_Type (ActivityType),
    INDEX IX_UserActivity_CreatedAt (CreatedAt)
);

-- =============================================
-- SECTION 7: SYSTEM TABLES
-- =============================================

-- API Sync Log (for government API calls)
CREATE TABLE dbo.ApiSyncLog (
    SyncLogId INT IDENTITY(1,1) PRIMARY KEY,
    ApiName NVARCHAR(100) NOT NULL,
    SyncType NVARCHAR(50) NOT NULL,
    RecordsFetched INT NULL,
    RecordsInserted INT NULL,
    RecordsUpdated INT NULL,
    StartedAt DATETIME2(3) NOT NULL DEFAULT GETDATE(),
    CompletedAt DATETIME2(3) NULL,
    Status NVARCHAR(50) NOT NULL,
    ErrorMessage NVARCHAR(MAX),
    
    INDEX IX_ApiSync_ApiName (ApiName),
    INDEX IX_ApiSync_Status (Status),
    INDEX IX_ApiSync_StartedAt (StartedAt)
);

-- System Settings
CREATE TABLE dbo.SystemSettings (
    SettingId INT IDENTITY(1,1) PRIMARY KEY,
    SettingKey NVARCHAR(100) NOT NULL UNIQUE,
    SettingValue NVARCHAR(MAX),
    Description NVARCHAR(500),
    IsEditable BIT NOT NULL DEFAULT 1,
    UpdatedAt DATETIME2(3) NOT NULL DEFAULT GETDATE(),
    UpdatedBy NVARCHAR(100),
    
    INDEX IX_SystemSettings_Key (SettingKey)
);

-- =============================================
-- SECTION 8: ANALYTICS & REPORTING TABLES
-- =============================================

-- Mapping statistics (materialized for performance)
CREATE TABLE dbo.MappingStatistics (
    StatId INT IDENTITY(1,1) PRIMARY KEY,
    StatDate DATE NOT NULL,
    
    -- Overall counts
    TotalMappings INT NOT NULL,
    ActiveMappings INT NOT NULL,
    MappingsCreatedToday INT NOT NULL,
    MappingsUpdatedToday INT NOT NULL,
    
    -- By source
    ManualMappings INT NOT NULL,
    AIGeneratedMappings INT NOT NULL,
    ImportedMappings INT NOT NULL,
    
    -- Coverage
    VehiclesWithMappings INT NOT NULL,
    VehiclesWithoutMappings INT NOT NULL,
    PartsWithMappings INT NOT NULL,
    PartsWithoutMappings INT NOT NULL,
    
    -- User activity
    ActiveUsers INT NOT NULL,
    
    CreatedAt DATETIME2(3) NOT NULL DEFAULT GETDATE(),
    
    CONSTRAINT UQ_MappingStats_Date UNIQUE (StatDate),
    INDEX IX_MappingStats_Date (StatDate DESC)
);

-- Popular searches (for auto-complete and suggestions)
CREATE TABLE dbo.PopularSearches (
    SearchId INT IDENTITY(1,1) PRIMARY KEY,
    SearchType NVARCHAR(50) NOT NULL,
    SearchTerm NVARCHAR(200) NOT NULL,
    SearchCount INT NOT NULL DEFAULT 1,
    LastSearchedAt DATETIME2(3) NOT NULL DEFAULT GETDATE(),
    
    CONSTRAINT UQ_PopularSearches UNIQUE (SearchType, SearchTerm),
    INDEX IX_PopularSearches_Type_Count (SearchType, SearchCount DESC)
);

-- =============================================
-- SECTION 9: STORED PROCEDURES
-- =============================================

-- Get parts compatible with a vehicle
CREATE OR ALTER PROCEDURE dbo.sp_GetPartsForVehicle
    @VehicleTypeId INT,
    @IncludeInactive BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
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
        vpm.UpdatedAt AS MappingUpdatedAt
    FROM dbo.VehiclePartsMappings vpm
    INNER JOIN dbo.vw_Parts vp ON vpm.PartItemKey = vp.PartNumber
    WHERE vpm.VehicleTypeId = @VehicleTypeId
      AND vpm.IsCurrentVersion = 1
      AND (@IncludeInactive = 1 OR vpm.IsActive = 1)
      AND vp.IsActive = 1
    ORDER BY vpm.Priority DESC, vp.PartName;
END;
GO

-- Get vehicles compatible with a part
CREATE OR ALTER PROCEDURE dbo.sp_GetVehiclesForPart
    @PartItemKey NVARCHAR(20),
    @IncludeInactive BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        vpm.MappingId,
        vt.*,
        m.ManufacturerName,
        vpm.Priority,
        vpm.FitsYearFrom,
        vpm.FitsYearTo,
        vpm.RequiresModification,
        vpm.CompatibilityNotes,
        vpm.MappingSource,
        vpm.UpdatedAt AS MappingUpdatedAt
    FROM dbo.VehiclePartsMappings vpm
    INNER JOIN dbo.VehicleTypes vt ON vpm.VehicleTypeId = vt.VehicleTypeId
    INNER JOIN dbo.Manufacturers m ON vt.ManufacturerId = m.ManufacturerId
    WHERE vpm.PartItemKey = @PartItemKey
      AND vpm.IsCurrentVersion = 1
      AND (@IncludeInactive = 1 OR vpm.IsActive = 1)
      AND vt.IsActive = 1
    ORDER BY m.ManufacturerName, vt.ModelName, vt.YearFrom;
END;
GO

-- Create or update mapping with history
CREATE OR ALTER PROCEDURE dbo.sp_UpsertMapping
    @VehicleTypeId INT,
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
    WHERE VehicleTypeId = @VehicleTypeId 
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
        VehicleTypeId, PartItemKey, MappingSource, ConfidenceScore, Priority,
        FitsYearFrom, FitsYearTo, RequiresModification, CompatibilityNotes,
        InstallationNotes, VersionNumber, IsCurrentVersion, CreatedBy, UpdatedBy
    )
    VALUES (
        @VehicleTypeId, @PartItemKey, @MappingSource, @ConfidenceScore, @Priority,
        @FitsYearFrom, @FitsYearTo, @RequiresModification, @CompatibilityNotes,
        @InstallationNotes, @NewVersionNumber, 1, @Username, @Username
    );
    
    SET @MappingId = SCOPE_IDENTITY();
    
    COMMIT TRANSACTION;
    
    SELECT @MappingId AS MappingId, @NewVersionNumber AS VersionNumber;
END;
GO

-- Bulk insert mappings (for importing)
CREATE OR ALTER PROCEDURE dbo.sp_BulkInsertMappings
    @Mappings NVARCHAR(MAX), -- JSON array of mappings
    @Username NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Parse JSON and insert mappings
    INSERT INTO dbo.VehiclePartsMappings (
        VehicleTypeId, PartItemKey, MappingSource, Priority,
        RequiresModification, CompatibilityNotes, CreatedBy, UpdatedBy
    )
    SELECT 
        VehicleTypeId,
        PartItemKey,
        'Import' AS MappingSource,
        ISNULL(Priority, 0) AS Priority,
        ISNULL(RequiresModification, 0) AS RequiresModification,
        CompatibilityNotes,
        @Username,
        @Username
    FROM OPENJSON(@Mappings)
    WITH (
        VehicleTypeId INT,
        PartItemKey NVARCHAR(20),
        Priority INT,
        RequiresModification BIT,
        CompatibilityNotes NVARCHAR(500)
    )
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.VehiclePartsMappings vpm
        WHERE vpm.VehicleTypeId = JSON_VALUE(value, '$.VehicleTypeId')
          AND vpm.PartItemKey = JSON_VALUE(value, '$.PartItemKey')
          AND vpm.IsCurrentVersion = 1
    );
    
    SELECT @@ROWCOUNT AS InsertedCount;
END;
GO

-- =============================================
-- SECTION 10: INITIAL DATA
-- =============================================

-- Insert default user
INSERT INTO dbo.Users (Username, FullName, Email)
VALUES ('admin', 'System Administrator', 'admin@example.com');

-- Insert default settings
INSERT INTO dbo.SystemSettings (SettingKey, SettingValue, Description)
VALUES 
    ('GovApiBaseUrl', 'https://data.gov.il/api/3/action', 'Israeli Government API base URL'),
    ('VehicleTypesResourceId', '142afde2-6228-49f9-8a29-9b6c3a0cbe40', 'Resource ID for vehicle types'),
    ('VehicleLookupResourceId', '053cea08-09bc-40ec-8f7a-156f0677aff3', 'Resource ID for license plate lookup'),
    ('AutoSyncEnabled', 'true', 'Enable automatic sync with gov API'),
    ('AutoSyncIntervalDays', '7', 'Days between automatic syncs');

PRINT 'Database schema created successfully!';
PRINT 'IMPORTANT: This schema uses ItemKey to match your SH2013.Items table';
PRINT 'Next steps:';
PRINT '1. Verify vw_Parts view works: SELECT TOP 10 * FROM dbo.vw_Parts';
PRINT '2. Add sample mappings to test';
PRINT '3. Create your ASP.NET Core project and copy DatabaseEntities.cs';
GO
