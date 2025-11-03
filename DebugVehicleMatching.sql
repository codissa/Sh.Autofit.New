-- Vehicle Matching Debug Query
-- This SQL matches the logic in VehicleMatchingService.FindPossibleMatchesAsync

-- ============================================
-- PARAMETERS - Replace these with your actual values from government API
-- ============================================
DECLARE @GovManufacturerName NVARCHAR(100) = 'טויוטה יפן'  -- Replace with EXACT manufacturer name from government API
DECLARE @GovModelName NVARCHAR(100) = 'COROLLA'  -- Replace with actual model name from government
DECLARE @GovYear INT = 2015  -- Replace with actual year from government

-- ============================================
-- FULL QUERY (All filters combined)
-- ============================================
SELECT
    vt.VehicleTypeID,
    vt.ManufacturerID,
    m.ManufacturerName,
    m.ManufacturerShortName,
    vt.ModelCode,
    vt.ModelName,
    vt.YearFrom,
    vt.YearTo,
    vt.VehicleCategory,
    vt.CommercialName,
    vt.FuelTypeName,
    vt.EngineModel
FROM VehicleType vt
INNER JOIN Manufacturer m ON vt.ManufacturerID = m.ManufacturerID
WHERE vt.IsActive = 1
    -- Step 1: Match manufacturer name (exact match)
    AND (
        m.ManufacturerName = @GovManufacturerName
        OR m.ManufacturerShortName = @GovManufacturerName
    )
    -- Step 2: Match model name or model code (bidirectional contains)
    AND (
        (vt.ModelName IS NOT NULL AND vt.ModelName LIKE '%' + @GovModelName + '%')
        OR (vt.ModelCode IS NOT NULL AND vt.ModelCode LIKE '%' + @GovModelName + '%')
        OR (vt.ModelName IS NOT NULL AND @GovModelName LIKE '%' + vt.ModelName + '%')
        OR (vt.ModelCode IS NOT NULL AND @GovModelName LIKE '%' + vt.ModelCode + '%')
    )
    -- Step 3: Match year (within range)
    AND (vt.YearFrom IS NULL OR vt.YearFrom <= @GovYear)
    AND (vt.YearTo IS NULL OR vt.YearTo >= @GovYear);

-- ============================================
-- STEP-BY-STEP DEBUG QUERIES
-- ============================================

-- Step 1: Check manufacturer matching only (EXACT match)
SELECT
    m.ManufacturerID,
    m.ManufacturerName,
    m.ManufacturerShortName,
    COUNT(*) as VehicleCount
FROM VehicleType vt
INNER JOIN Manufacturer m ON vt.ManufacturerID = m.ManufacturerID
WHERE vt.IsActive = 1
    AND (
        m.ManufacturerName = @GovManufacturerName
        OR m.ManufacturerShortName = @GovManufacturerName
    )
GROUP BY m.ManufacturerID, m.ManufacturerName, m.ManufacturerShortName;

-- Step 2: Add model name/code matching
SELECT
    vt.VehicleTypeID,
    m.ManufacturerName,
    m.ManufacturerShortName,
    vt.ModelCode,
    vt.ModelName,
    vt.YearFrom,
    vt.YearTo
FROM VehicleType vt
INNER JOIN Manufacturer m ON vt.ManufacturerID = m.ManufacturerID
WHERE vt.IsActive = 1
    AND (
        m.ManufacturerName = @GovManufacturerName
        OR m.ManufacturerShortName = @GovManufacturerName
    )
    AND (
        (vt.ModelName IS NOT NULL AND vt.ModelName LIKE '%' + @GovModelName + '%')
        OR (vt.ModelCode IS NOT NULL AND vt.ModelCode LIKE '%' + @GovModelName + '%')
        OR (vt.ModelName IS NOT NULL AND @GovModelName LIKE '%' + vt.ModelName + '%')
        OR (vt.ModelCode IS NOT NULL AND @GovModelName LIKE '%' + vt.ModelCode + '%')
    );

-- Step 3: Add year matching
SELECT
    vt.VehicleTypeID,
    m.ManufacturerName,
    m.ManufacturerShortName,
    vt.ModelCode,
    vt.ModelName,
    vt.YearFrom,
    vt.YearTo,
    vt.CommercialName
FROM VehicleType vt
INNER JOIN Manufacturer m ON vt.ManufacturerID = m.ManufacturerID
WHERE vt.IsActive = 1
    AND (
        m.ManufacturerName = @GovManufacturerName
        OR m.ManufacturerShortName = @GovManufacturerName
    )
    AND (
        (vt.ModelName IS NOT NULL AND vt.ModelName LIKE '%' + @GovModelName + '%')
        OR (vt.ModelCode IS NOT NULL AND vt.ModelCode LIKE '%' + @GovModelName + '%')
        OR (vt.ModelName IS NOT NULL AND @GovModelName LIKE '%' + vt.ModelName + '%')
        OR (vt.ModelCode IS NOT NULL AND @GovModelName LIKE '%' + vt.ModelCode + '%')
    )
    AND (vt.YearFrom IS NULL OR vt.YearFrom <= @GovYear)
    AND (vt.YearTo IS NULL OR vt.YearTo >= @GovYear);

-- ============================================
-- DIAGNOSTIC QUERIES
-- ============================================

-- Check what manufacturers exist in the database
SELECT DISTINCT
    ManufacturerName,
    ManufacturerShortName
FROM Manufacturer
ORDER BY ManufacturerName;

-- Check what models exist for a specific manufacturer
SELECT
    vt.ModelCode,
    vt.ModelName,
    COUNT(*) as Count
FROM VehicleType vt
INNER JOIN Manufacturer m ON vt.ManufacturerID = m.ManufacturerID
WHERE vt.IsActive = 1
    AND (
        m.ManufacturerName = @GovManufacturerName
        OR m.ManufacturerShortName = @GovManufacturerName
    )
GROUP BY vt.ModelCode, vt.ModelName
ORDER BY vt.ModelName;

-- Check year ranges for a specific manufacturer and model
SELECT
    vt.VehicleTypeID,
    m.ManufacturerName,
    vt.ModelName,
    vt.ModelCode,
    vt.YearFrom,
    vt.YearTo,
    vt.CommercialName,
    vt.IsActive
FROM VehicleType vt
INNER JOIN Manufacturer m ON vt.ManufacturerID = m.ManufacturerID
WHERE (
    m.ManufacturerName = @GovManufacturerName
    OR m.ManufacturerShortName = @GovManufacturerName
)
ORDER BY vt.ModelName, vt.YearFrom;
