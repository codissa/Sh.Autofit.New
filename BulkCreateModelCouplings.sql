-- =====================================================================
-- Bulk Create Model Couplings Script (SAFE VERSION)
-- =====================================================================
-- This script creates couplings between consolidated vehicle models that:
--   ✅ Same manufacturer
--   ✅ Same model name
--   ✅ Same engine volume
--   ✅ Same transmission type
--   ✅ Same trim level (if not null)
--   ✅ Overlapping year ranges
--
-- EXCLUDED (to avoid suspicious matches):
--   ❌ Large year gaps (≥5 years between start dates)
--   ❌ Different commercial names
-- =====================================================================

-- STEP 1: PREVIEW - See what will be coupled
-- Run this first to review before inserting
-- =====================================================================
SELECT
    m1.ConsolidatedModelId AS ModelA_ID,
    m2.ConsolidatedModelId AS ModelB_ID,
    mfg.ManufacturerShortName AS Manufacturer,
    m1.ModelName,
    m1.YearFrom AS YearFromA,
    m1.YearTo AS YearToA,
    m2.YearFrom AS YearFromB,
    m2.YearTo AS YearToB,
    m1.EngineVolume,
    m1.TransmissionType,
    ISNULL(m1.TrimLevel, 'N/A') AS TrimLevelA,
    ISNULL(m2.TrimLevel, 'N/A') AS TrimLevelB,
    m1.CommercialName AS CommercialNameA,
    m2.CommercialName AS CommercialNameB
FROM ConsolidatedVehicleModels m1
INNER JOIN ConsolidatedVehicleModels m2
    ON m1.ManufacturerId = m2.ManufacturerId
    AND m1.ModelName = m2.ModelName
    AND m1.ConsolidatedModelId < m2.ConsolidatedModelId  -- Avoid duplicates and self-coupling
    AND m1.EngineVolume = m2.EngineVolume
    AND (m1.TransmissionType = m2.TransmissionType OR (m1.TransmissionType IS NULL AND m2.TransmissionType IS NULL))
    AND (
        -- Both have same trim level
        m1.TrimLevel = m2.TrimLevel
        -- OR both are null
        OR (m1.TrimLevel IS NULL AND m2.TrimLevel IS NULL)
    )
    -- Year ranges overlap
    AND NOT (m1.YearTo < m2.YearFrom OR m2.YearTo < m1.YearFrom)
INNER JOIN Manufacturers mfg ON m1.ManufacturerId = mfg.ManufacturerId
WHERE
    -- ✅ EXCLUDE: Large year gap (5+ years between start dates)
    NOT (
        CASE
            WHEN m2.YearFrom > m1.YearFrom THEN m2.YearFrom - m1.YearFrom
            ELSE m1.YearFrom - m2.YearFrom
        END >= 5
    )
    -- ✅ EXCLUDE: Different commercial names
    AND NOT (
        m1.CommercialName IS NOT NULL
        AND m2.CommercialName IS NOT NULL
        AND m1.CommercialName != ''
        AND m2.CommercialName != ''
        AND m1.CommercialName != m2.CommercialName
    )
    -- Check for existing couplings
    AND NOT EXISTS (
        -- Make sure coupling doesn't already exist
        SELECT 1
        FROM ModelCouplings mc
        WHERE mc.IsActive = 1
        AND (
            (mc.ConsolidatedModelId_A = m1.ConsolidatedModelId AND mc.ConsolidatedModelId_B = m2.ConsolidatedModelId)
            OR
            (mc.ConsolidatedModelId_A = m2.ConsolidatedModelId AND mc.ConsolidatedModelId_B = m1.ConsolidatedModelId)
        )
    )
ORDER BY mfg.ManufacturerShortName, m1.ModelName, m1.YearFrom, m2.YearFrom;

-- Show count
SELECT COUNT(*) AS TotalCouplingsToCreate
FROM ConsolidatedVehicleModels m1
INNER JOIN ConsolidatedVehicleModels m2
    ON m1.ManufacturerId = m2.ManufacturerId
    AND m1.ModelName = m2.ModelName
    AND m1.ConsolidatedModelId < m2.ConsolidatedModelId
    AND m1.EngineVolume = m2.EngineVolume
    AND (m1.TransmissionType = m2.TransmissionType OR (m1.TransmissionType IS NULL AND m2.TransmissionType IS NULL))
    AND (m1.TrimLevel = m2.TrimLevel OR (m1.TrimLevel IS NULL AND m2.TrimLevel IS NULL))
    AND NOT (m1.YearTo < m2.YearFrom OR m2.YearTo < m1.YearFrom)
WHERE
    -- EXCLUDE: Large year gap
    NOT (
        CASE
            WHEN m2.YearFrom > m1.YearFrom THEN m2.YearFrom - m1.YearFrom
            ELSE m1.YearFrom - m2.YearFrom
        END >= 5
    )
    -- EXCLUDE: Different commercial names
    AND NOT (
        m1.CommercialName IS NOT NULL
        AND m2.CommercialName IS NOT NULL
        AND m1.CommercialName != ''
        AND m2.CommercialName != ''
        AND m1.CommercialName != m2.CommercialName
    )
    AND NOT EXISTS (
        SELECT 1 FROM ModelCouplings mc WHERE mc.IsActive = 1
        AND ((mc.ConsolidatedModelId_A = m1.ConsolidatedModelId AND mc.ConsolidatedModelId_B = m2.ConsolidatedModelId)
             OR (mc.ConsolidatedModelId_A = m2.ConsolidatedModelId AND mc.ConsolidatedModelId_B = m1.ConsolidatedModelId))
    );

GO

-- =====================================================================
-- STEP 2: INSERT - Actually create the couplings
-- ⚠️ ONLY RUN THIS AFTER REVIEWING THE PREVIEW ABOVE!
-- =====================================================================

-- Uncomment the section below to execute the inserts:

/*
BEGIN TRANSACTION;

INSERT INTO ModelCouplings (
    ConsolidatedModelId_A,
    ConsolidatedModelId_B,
    CouplingType,
    Notes,
    CreatedAt,
    CreatedBy,
    UpdatedAt,
    UpdatedBy,
    IsActive
)
SELECT
    m1.ConsolidatedModelId AS ConsolidatedModelId_A,
    m2.ConsolidatedModelId AS ConsolidatedModelId_B,
    'FullModel' AS CouplingType,
    CONCAT(
        'Auto-coupled: ',
        mfg.ManufacturerShortName, ' ',
        m1.ModelName, ' ',
        m1.EngineVolume, 'cc ',
        m1.TransmissionType,
        CASE WHEN m1.TrimLevel IS NOT NULL THEN CONCAT(' ', m1.TrimLevel) ELSE '' END
    ) AS Notes,
    GETDATE() AS CreatedAt,
    'BulkCouplingScript' AS CreatedBy,
    GETDATE() AS UpdatedAt,
    'BulkCouplingScript' AS UpdatedBy,
    1 AS IsActive
FROM ConsolidatedVehicleModels m1
INNER JOIN ConsolidatedVehicleModels m2
    ON m1.ManufacturerId = m2.ManufacturerId
    AND m1.ModelName = m2.ModelName
    AND m1.ConsolidatedModelId < m2.ConsolidatedModelId  -- Ensure A < B to avoid duplicates
    AND m1.EngineVolume = m2.EngineVolume
    AND (m1.TransmissionType = m2.TransmissionType OR (m1.TransmissionType IS NULL AND m2.TransmissionType IS NULL))
    AND (m1.TrimLevel = m2.TrimLevel OR (m1.TrimLevel IS NULL AND m2.TrimLevel IS NULL))
    -- Year ranges overlap
    AND NOT (m1.YearTo < m2.YearFrom OR m2.YearTo < m1.YearFrom)
INNER JOIN Manufacturers mfg ON m1.ManufacturerId = mfg.ManufacturerId
WHERE
    -- ✅ EXCLUDE: Large year gap (5+ years between start dates)
    NOT (
        CASE
            WHEN m2.YearFrom > m1.YearFrom THEN m2.YearFrom - m1.YearFrom
            ELSE m1.YearFrom - m2.YearFrom
        END >= 5
    )
    -- ✅ EXCLUDE: Different commercial names
    AND NOT (
        m1.CommercialName IS NOT NULL
        AND m2.CommercialName IS NOT NULL
        AND m1.CommercialName != ''
        AND m2.CommercialName != ''
        AND m1.CommercialName != m2.CommercialName
    )
    -- Check for existing couplings
    AND NOT EXISTS (
        -- Make sure coupling doesn't already exist
        SELECT 1
        FROM ModelCouplings mc
        WHERE mc.IsActive = 1
        AND (
            (mc.ConsolidatedModelId_A = m1.ConsolidatedModelId AND mc.ConsolidatedModelId_B = m2.ConsolidatedModelId)
            OR
            (mc.ConsolidatedModelId_A = m2.ConsolidatedModelId AND mc.ConsolidatedModelId_B = m1.ConsolidatedModelId)
        )
    );

-- Show results
SELECT @@ROWCOUNT AS CouplingsCreated;

-- Review what was created
SELECT
    mc.ModelCouplingId,
    mfg1.ManufacturerShortName AS MfgA,
    m1.ModelName AS ModelA,
    m1.YearFrom AS YearFromA,
    m1.YearTo AS YearToA,
    m1.EngineVolume AS EngineA,
    m1.TransmissionType AS TransA,
    m1.TrimLevel AS TrimA,
    '<=>' AS [Link],
    mfg2.ManufacturerShortName AS MfgB,
    m2.ModelName AS ModelB,
    m2.YearFrom AS YearFromB,
    m2.YearTo AS YearToB,
    m2.EngineVolume AS EngineB,
    m2.TransmissionType AS TransB,
    m2.TrimLevel AS TrimB,
    mc.Notes,
    mc.CreatedAt
FROM ModelCouplings mc
INNER JOIN ConsolidatedVehicleModels m1 ON mc.ConsolidatedModelId_A = m1.ConsolidatedModelId
INNER JOIN ConsolidatedVehicleModels m2 ON mc.ConsolidatedModelId_B = m2.ConsolidatedModelId
INNER JOIN Manufacturers mfg1 ON m1.ManufacturerId = mfg1.ManufacturerId
INNER JOIN Manufacturers mfg2 ON m2.ManufacturerId = mfg2.ManufacturerId
WHERE mc.CreatedBy = 'BulkCouplingScript'
AND mc.IsActive = 1
ORDER BY mc.CreatedAt DESC;

-- If everything looks good, commit the transaction
COMMIT TRANSACTION;

-- If something went wrong, uncomment the line below to rollback:
-- ROLLBACK TRANSACTION;
*/

GO

-- =====================================================================
-- OPTIONAL: More relaxed criteria (if the above is too strict)
-- =====================================================================
-- This version ignores trim level differences and only requires:
--   - Same manufacturer, model name, engine volume, transmission
--   - Overlapping years
-- Uncomment to see preview:

/*
SELECT
    m1.ConsolidatedModelId AS ModelA_ID,
    m2.ConsolidatedModelId AS ModelB_ID,
    mfg.ManufacturerShortName AS Manufacturer,
    m1.ModelName,
    m1.YearFrom AS YearFromA,
    m1.YearTo AS YearToA,
    m2.YearFrom AS YearFromB,
    m2.YearTo AS YearToB,
    m1.EngineVolume,
    m1.TransmissionType,
    ISNULL(m1.TrimLevel, 'N/A') AS TrimLevelA,
    ISNULL(m2.TrimLevel, 'N/A') AS TrimLevelB
FROM ConsolidatedVehicleModels m1
INNER JOIN ConsolidatedVehicleModels m2
    ON m1.ManufacturerId = m2.ManufacturerId
    AND m1.ModelName = m2.ModelName
    AND m1.ConsolidatedModelId < m2.ConsolidatedModelId
    AND m1.EngineVolume = m2.EngineVolume
    AND (m1.TransmissionType = m2.TransmissionType OR (m1.TransmissionType IS NULL AND m2.TransmissionType IS NULL))
    AND NOT (m1.YearTo < m2.YearFrom OR m2.YearTo < m1.YearFrom)
INNER JOIN Manufacturers mfg ON m1.ManufacturerId = mfg.ManufacturerId
WHERE NOT EXISTS (
    SELECT 1 FROM ModelCouplings mc WHERE mc.IsActive = 1
    AND ((mc.ConsolidatedModelId_A = m1.ConsolidatedModelId AND mc.ConsolidatedModelId_B = m2.ConsolidatedModelId)
         OR (mc.ConsolidatedModelId_A = m2.ConsolidatedModelId AND mc.ConsolidatedModelId_B = m1.ConsolidatedModelId))
)
ORDER BY mfg.ManufacturerShortName, m1.ModelName, m1.YearFrom;
*/
