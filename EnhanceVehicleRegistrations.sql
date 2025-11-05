-- =============================================
-- Enhance VehicleRegistrations table for Task 5
-- Adds fields for better analytics and unmatched tracking
-- =============================================

USE [Sh.Autofit];
GO

-- Add new columns for Gov API raw data and match tracking
ALTER TABLE dbo.VehicleRegistrations
ADD
    -- Raw data from Government API (for unmatched tracking)
    GovManufacturerName NVARCHAR(200) NULL,
    GovModelName NVARCHAR(200) NULL,
    GovEngineVolume INT NULL,
    GovFuelType NVARCHAR(50) NULL,
    GovYear INT NULL,

    -- Match status tracking
    MatchStatus NVARCHAR(50) NOT NULL DEFAULT 'Pending',
    -- Values: 'Matched', 'NotInOurDB', 'NotFoundInGovAPI', 'AutoCreated'

    MatchReason NVARCHAR(500) NULL,
    -- Why it matched or why it didn't

    -- API call metadata
    ApiResourceUsed NVARCHAR(100) NULL,
    -- Which API resource ID returned the data (primary, fallback1, fallback2)

    ApiResponseJson NVARCHAR(MAX) NULL;
    -- Store full API response for debugging (optional)
GO

-- Add indexes for analytics queries
CREATE INDEX IX_VehicleReg_MatchStatus ON dbo.VehicleRegistrations(MatchStatus);
CREATE INDEX IX_VehicleReg_LastLookupDate ON dbo.VehicleRegistrations(LastLookupDate DESC);
CREATE INDEX IX_VehicleReg_GovModel ON dbo.VehicleRegistrations(GovManufacturerName, GovModelName);
GO

PRINT 'VehicleRegistrations table enhanced successfully';
