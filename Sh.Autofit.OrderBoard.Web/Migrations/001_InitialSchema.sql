-- =============================================
-- Sh.Autofit.OrderBoard — Initial Schema
-- Target: Sh.Autofit database (dbo schema)
-- WARNING: Never write to SH2013!
-- =============================================

USE [Sh.Autofit];
GO

-- =============================================
-- 1. dbo.DeliveryMethods (created first — referenced by FKs)
-- =============================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DeliveryMethods' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.DeliveryMethods (
        DeliveryMethodId INT IDENTITY(1,1) PRIMARY KEY,
        Name             NVARCHAR(100) NOT NULL,
        IsActive         BIT NOT NULL DEFAULT 1,
        IsAdHoc          BIT NOT NULL DEFAULT 0,
        CreatedAt        DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ClosedAt         DATETIME2 NULL,
        RulesJson        NVARCHAR(MAX) NULL,
        AutoHideAfterMinutes INT NULL
    );
END
GO

-- =============================================
-- 2. dbo.DeliveryRuns
-- =============================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DeliveryRuns' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.DeliveryRuns (
        DeliveryRunId    INT IDENTITY(1,1) PRIMARY KEY,
        DeliveryMethodId INT NOT NULL
            CONSTRAINT FK_DeliveryRuns_Method FOREIGN KEY REFERENCES dbo.DeliveryMethods(DeliveryMethodId),
        WindowStart      DATETIME2 NOT NULL,
        WindowEnd        DATETIME2 NOT NULL,
        State            VARCHAR(10) NOT NULL DEFAULT 'OPEN'
            CONSTRAINT CK_DeliveryRuns_State CHECK (State IN ('OPEN','CLOSED')),
        ClosedAt         DATETIME2 NULL
    );

    CREATE INDEX IX_DeliveryRuns_MethodId ON dbo.DeliveryRuns(DeliveryMethodId);
    CREATE INDEX IX_DeliveryRuns_State ON dbo.DeliveryRuns(State);
END
GO

-- =============================================
-- 3. dbo.AppOrders
-- =============================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AppOrders' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.AppOrders (
        AppOrderId          INT IDENTITY(1,1) PRIMARY KEY,
        CreatedAt           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        AccountKey          VARCHAR(15) NOT NULL,
        AccountName         VARCHAR(50) NULL,
        City                VARCHAR(50) NULL,
        Address             VARCHAR(100) NULL,
        Phone               VARCHAR(30) NULL,
        DisplayTime         DATETIME2 NULL,
        CurrentStage        VARCHAR(20) NOT NULL DEFAULT 'ORDER_IN_PC'
            CONSTRAINT CK_AppOrders_Stage CHECK (CurrentStage IN ('ORDER_IN_PC','ORDER_PRINTED','DOC_IN_PC','PACKING')),
        StageUpdatedAt      DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        IsManual            BIT NOT NULL DEFAULT 0,
        ManualNote          NVARCHAR(500) NULL,
        Hidden              BIT NOT NULL DEFAULT 0,
        HiddenReason        NVARCHAR(200) NULL,
        HiddenAt            DATETIME2 NULL,
        Pinned              BIT NOT NULL DEFAULT 0,
        DeliveryMethodId    INT NULL
            CONSTRAINT FK_AppOrders_DeliveryMethod FOREIGN KEY REFERENCES dbo.DeliveryMethods(DeliveryMethodId),
        DeliveryRunId       INT NULL
            CONSTRAINT FK_AppOrders_DeliveryRun FOREIGN KEY REFERENCES dbo.DeliveryRuns(DeliveryRunId),
        MergedIntoAppOrderId INT NULL
            CONSTRAINT FK_AppOrders_MergedInto FOREIGN KEY REFERENCES dbo.AppOrders(AppOrderId),
        NeedsResolve        BIT NOT NULL DEFAULT 0
    );

    CREATE INDEX IX_AppOrders_AccountKey ON dbo.AppOrders(AccountKey);
    CREATE INDEX IX_AppOrders_CurrentStage ON dbo.AppOrders(CurrentStage);
    CREATE INDEX IX_AppOrders_Hidden ON dbo.AppOrders(Hidden);
    CREATE INDEX IX_AppOrders_DeliveryRunId ON dbo.AppOrders(DeliveryRunId);
END
GO

-- =============================================
-- 4. dbo.AppOrderLinks
-- =============================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AppOrderLinks' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.AppOrderLinks (
        LinkId        INT IDENTITY(1,1) PRIMARY KEY,
        AppOrderId    INT NOT NULL
            CONSTRAINT FK_AppOrderLinks_Order FOREIGN KEY REFERENCES dbo.AppOrders(AppOrderId) ON DELETE CASCADE,
        SourceDb      VARCHAR(50) NOT NULL DEFAULT 'SH2013',
        StockId       INT NOT NULL,
        DocumentId    INT NOT NULL,
        DocNumber     INT NULL,
        Status        SMALLINT NULL,
        Reference     INT NULL,
        FirstSeenAt   DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        LastSeenAt    DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        IsPresent     BIT NOT NULL DEFAULT 1,
        DisappearedAt DATETIME2 NULL,
        MissCount     INT NOT NULL DEFAULT 0,

        CONSTRAINT UQ_AppOrderLinks_StockId UNIQUE (StockId)
    );

    CREATE INDEX IX_AppOrderLinks_AppOrderId ON dbo.AppOrderLinks(AppOrderId);
    CREATE INDEX IX_AppOrderLinks_DocNumber ON dbo.AppOrderLinks(DocNumber);
    CREATE INDEX IX_AppOrderLinks_Reference ON dbo.AppOrderLinks(Reference);
    CREATE INDEX IX_AppOrderLinks_IsPresent ON dbo.AppOrderLinks(IsPresent);
END
GO

-- =============================================
-- 5. dbo.StageEvents (audit)
-- =============================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'StageEvents' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.StageEvents (
        EventId      BIGINT IDENTITY(1,1) PRIMARY KEY,
        AppOrderId   INT NULL,
        At           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        Actor        NVARCHAR(100) NOT NULL,
        Action       NVARCHAR(50) NOT NULL,
        FromStage    VARCHAR(20) NULL,
        ToStage      VARCHAR(20) NULL,
        Payload      NVARCHAR(MAX) NULL
    );

    CREATE INDEX IX_StageEvents_AppOrderId ON dbo.StageEvents(AppOrderId);
    CREATE INDEX IX_StageEvents_At ON dbo.StageEvents(At DESC);
END
GO

-- =============================================
-- 6. dbo.DeliveryMethodCustomerRules
-- =============================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DeliveryMethodCustomerRules' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.DeliveryMethodCustomerRules (
        Id               INT IDENTITY(1,1) PRIMARY KEY,
        AccountKey       VARCHAR(15) NOT NULL,
        DeliveryMethodId INT NOT NULL
            CONSTRAINT FK_DMCR_Method FOREIGN KEY REFERENCES dbo.DeliveryMethods(DeliveryMethodId),
        WindowStart      TIME NULL,
        WindowEnd        TIME NULL,
        DaysOfWeek       VARCHAR(20) NULL,
        IsActive         BIT NOT NULL DEFAULT 1
    );

    CREATE INDEX IX_DMCR_AccountKey ON dbo.DeliveryMethodCustomerRules(AccountKey);
    CREATE INDEX IX_DMCR_MethodId ON dbo.DeliveryMethodCustomerRules(DeliveryMethodId);
END
GO

-- =============================================
-- 7. dbo.OrderBoardSettings
-- =============================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OrderBoardSettings' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.OrderBoardSettings (
        [Key]   NVARCHAR(100) PRIMARY KEY,
        [Value] NVARCHAR(MAX) NOT NULL
    );

    INSERT INTO dbo.OrderBoardSettings ([Key], [Value]) VALUES
        ('PollingIntervalSeconds', '30'),
        ('DisappearanceThresholdPolls', '3'),
        ('TodayRangeHours', '48'),
        ('SlaMinutes', '60'),
        ('LastMaxStockId', '0');
END
GO

PRINT 'OrderBoard schema created successfully.';
GO
