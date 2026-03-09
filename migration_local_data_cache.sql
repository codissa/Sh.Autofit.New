-- =============================================
-- MIGRATION: Local Data Cache Tables
-- Database: Sh.Autofit
-- Description: Create tables for locally caching government API data
--              (vehicle quantities, vehicle registrations from 6 resources)
--              and sync tracking log
-- =============================================

USE [Sh.Autofit];
GO

PRINT 'Starting migration: Local Data Cache Tables...';
PRINT '';
GO

-- =============================================
-- STEP 1: CREATE TABLE - LocalVehicleQuantities
-- Stores resource 5e87a7a1 (vehicle quantity aggregates by model/year)
-- =============================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LocalVehicleQuantities')
BEGIN
    PRINT 'Creating LocalVehicleQuantities table...';

    CREATE TABLE dbo.LocalVehicleQuantities (
        Id              INT IDENTITY(1,1) PRIMARY KEY,
        GovRecordId     INT,                                    -- _id
        SugDegem        NVARCHAR(100),                          -- sug_degem
        TozeretCd       INT NOT NULL,                           -- tozeret_cd
        TozeretNm       NVARCHAR(200),                          -- tozeret_nm
        TozeretEretzNm  NVARCHAR(200),                          -- tozeret_eretz_nm
        Tozar           NVARCHAR(200),                          -- tozar
        DegemCd         INT NOT NULL,                           -- degem_cd
        DegemNm         NVARCHAR(200),                          -- degem_nm
        ShnatYitzur     INT,                                    -- shnat_yitzur
        MisparRechavimPailim    INT NOT NULL DEFAULT 0,         -- mispar_rechavim_pailim
        MisparRechavimLePailim  INT NOT NULL DEFAULT 0,         -- mispar_rechavim_le_pailim
        KinuyMishari    NVARCHAR(200),                          -- kinuy_mishari
        SyncedAt        DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );

    CREATE INDEX IX_LocalVehicleQuantities_Lookup
        ON dbo.LocalVehicleQuantities (TozeretCd, DegemCd, DegemNm);

    PRINT 'LocalVehicleQuantities table created successfully.';
END
ELSE
BEGIN
    PRINT 'LocalVehicleQuantities table already exists.';
END
GO

-- =============================================
-- STEP 2: CREATE TABLE - LocalVehicleRegistrations
-- Superset table for all 6 registration resources:
--   Primary (053cea08), InactiveWithCode (f6efe89a),
--   OffRoadCancelled (851ecab1), PersonalImport (03adc637),
--   InactiveNoCode (6f6acd03), HeavyAndNoCode (cd3acc5c)
-- Each resource has different fields - missing fields are NULL
-- =============================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LocalVehicleRegistrations')
BEGIN
    PRINT 'Creating LocalVehicleRegistrations table...';

    CREATE TABLE dbo.LocalVehicleRegistrations (
        Id                  BIGINT IDENTITY(1,1) PRIMARY KEY,
        SourceResource      NVARCHAR(30) NOT NULL,              -- 'Primary','InactiveWithCode','OffRoadCancelled','PersonalImport','InactiveNoCode','HeavyAndNoCode'
        GovRecordId         INT,                                -- _id (for incremental sync)

        -- Core fields (all/most resources)
        MisparRechev        NVARCHAR(20) NOT NULL,              -- mispar_rechev (license plate)
        TozeretCd           INT,                                -- tozeret_cd
        TozeretNm           NVARCHAR(200),                      -- tozeret_nm
        DegemNm             NVARCHAR(200),                      -- degem_nm
        ShnatYitzur         INT,                                -- shnat_yitzur
        DegemManoa          NVARCHAR(200),                      -- degem_manoa
        SugDelekNm          NVARCHAR(200),                      -- sug_delek_nm

        -- PRIMARY + InactiveWithCode fields
        DegemCd             INT,                                -- degem_cd
        SugDegem            NVARCHAR(100),                      -- sug_degem
        RamatGimur          NVARCHAR(200),                      -- ramat_gimur
        RamatEivzurBetihuty INT,                                -- ramat_eivzur_betihuty
        KvutzatZihum        INT,                                -- kvutzat_zihum
        MivchanAcharonDt    NVARCHAR(50),                       -- mivchan_acharon_dt
        TokefDt             NVARCHAR(50),                       -- tokef_dt
        Baalut              NVARCHAR(100),                      -- baalut
        Misgeret            NVARCHAR(100),                      -- misgeret
        TzevaCd             INT,                                -- tzeva_cd
        TzevaRechev         NVARCHAR(100),                      -- tzeva_rechev
        ZmigKidmi           NVARCHAR(100),                      -- zmig_kidmi
        ZmigAhori           NVARCHAR(100),                      -- zmig_ahori
        HoraatRishum        INT,                                -- horaat_rishum
        MoedAliyaLakvish    NVARCHAR(50),                       -- moed_aliya_lakvish
        KinuyMishari        NVARCHAR(200),                      -- kinuy_mishari

        -- Off-Road Cancelled (851ecab1) + Personal Import (03adc637) shared
        SugRechevCd         INT,                                -- sug_rechev_cd
        SugRechevNm         NVARCHAR(200),                      -- sug_rechev_nm

        -- Off-Road Cancelled (851ecab1) specific
        BitulDt             NVARCHAR(50),                       -- bitul_dt
        TozarManoa          NVARCHAR(200),                      -- tozar_manoa
        MisparManoa         NVARCHAR(100),                      -- mispar_manoa

        -- Shared across multiple non-primary resources
        MishkalKolel        INT,                                -- mishkal_kolel (OffRoadCancelled, PersonalImport, InactiveNoCode, HeavyAndNoCode)
        NefachManoa         INT,                                -- nefach_manoa (PersonalImport, InactiveNoCode, HeavyAndNoCode)
        TozeretEretzNm      NVARCHAR(200),                      -- tozeret_eretz_nm (PersonalImport, InactiveNoCode, HeavyAndNoCode)

        -- Personal Import (03adc637) specific
        Shilda              NVARCHAR(100),                      -- shilda (VIN)
        SugYevu             NVARCHAR(200),                      -- sug_yevu

        -- Inactive WITHOUT Model Code (6f6acd03) + Heavy >3.5t (cd3acc5c) shared
        MisparShilda        NVARCHAR(100),                      -- mispar_shilda (VIN variant)
        TkinaEU             NVARCHAR(100),                      -- tkina_EU
        SugDelekCd          INT,                                -- sug_delek_cd
        MishkalAzmi         INT,                                -- mishkal_azmi
        HanaaCd             NVARCHAR(50),                       -- hanaa_cd
        HanaaNm             NVARCHAR(200),                      -- hanaa_nm
        MishkalMitanHarama  INT,                                -- mishkal_mitan_harama

        -- Heavy >3.5t (cd3acc5c) specific
        MisparMekomotLeyadNahag INT,                            -- mispar_mekomot_leyd_nahag
        MisparMekomot       INT,                                -- mispar_mekomot
        KvutzatSugRechev    NVARCHAR(200),                      -- kvutzat_sug_rechev
        GriraNm             NVARCHAR(200),                      -- grira_nm

        SyncedAt            DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );

    CREATE INDEX IX_LocalVehicleRegistrations_Plate
        ON dbo.LocalVehicleRegistrations (MisparRechev);
    CREATE INDEX IX_LocalVehicleRegistrations_Source
        ON dbo.LocalVehicleRegistrations (SourceResource);
    CREATE INDEX IX_LocalVehicleRegistrations_Incremental
        ON dbo.LocalVehicleRegistrations (SourceResource, GovRecordId);

    PRINT 'LocalVehicleRegistrations table created successfully.';
END
ELSE
BEGIN
    PRINT 'LocalVehicleRegistrations table already exists.';
END
GO

-- =============================================
-- STEP 3: CREATE TABLE - DataSyncLog
-- Tracks sync operations for UI display and incremental sync watermarks
-- =============================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DataSyncLog')
BEGIN
    PRINT 'Creating DataSyncLog table...';

    CREATE TABLE dbo.DataSyncLog (
        Id                INT IDENTITY(1,1) PRIMARY KEY,
        DatasetName       NVARCHAR(100) NOT NULL,               -- 'VehicleQuantities', 'Reg_Primary', 'Reg_InactiveWithCode', etc.
        ResourceId        NVARCHAR(100),
        StartedAt         DATETIME2 NOT NULL,
        CompletedAt       DATETIME2,
        TotalApiRecords   INT DEFAULT 0,                        -- Total records reported by API
        RecordsDownloaded INT DEFAULT 0,                        -- Records actually downloaded this sync
        LocalRecordCount  INT DEFAULT 0,                        -- Total local records after sync
        Status            NVARCHAR(50) NOT NULL,                -- 'Running', 'Completed', 'Failed', 'Cancelled'
        ErrorMessage      NVARCHAR(MAX)
    );

    CREATE INDEX IX_DataSyncLog_Dataset
        ON dbo.DataSyncLog (DatasetName, CompletedAt DESC);

    PRINT 'DataSyncLog table created successfully.';
END
ELSE
BEGIN
    PRINT 'DataSyncLog table already exists.';
END
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
PRINT '  - LocalVehicleQuantities (vehicle quantity aggregates)';
PRINT '  - LocalVehicleRegistrations (plate lookups from 6 gov resources)';
PRINT '  - DataSyncLog (sync operation tracking)';
PRINT '';
PRINT 'Next steps:';
PRINT '  1. Update Entity Framework models';
PRINT '  2. Run sync from the application UI';
PRINT '';
GO
