-- =====================================================================
-- Find Suspicious Year Overlaps in Potential Couplings
-- =====================================================================
-- This script identifies model pairs that match on specs but have
-- suspicious year overlaps that might indicate different generations
-- =====================================================================

-- SCENARIO 1: Small Overlap (might be different generations)
-- Models that match specs but only overlap by a few years
-- =====================================================================
SELECT
    mfg.ManufacturerShortName AS Manufacturer,
    m1.ModelName,
    m1.ConsolidatedModelId AS ModelA_ID,
    m2.ConsolidatedModelId AS ModelB_ID,
    m1.YearFrom AS YearFromA,
    m1.YearTo AS YearToA,
    m2.YearFrom AS YearFromB,
    m2.YearTo AS YearToB,
    -- Calculate overlap
    CASE
        WHEN m1.YearFrom > m2.YearFrom THEN m1.YearFrom
        ELSE m2.YearFrom
    END AS OverlapStart,
    CASE
        WHEN m1.YearTo < m2.YearTo THEN m1.YearTo
        ELSE m2.YearTo
    END AS OverlapEnd,
    -- Years of overlap
    (CASE WHEN m1.YearTo < m2.YearTo THEN m1.YearTo ELSE m2.YearTo END) -
    (CASE WHEN m1.YearFrom > m2.YearFrom THEN m1.YearFrom ELSE m2.YearFrom END) + 1 AS YearsOverlap,
    -- Total span
    (m1.YearTo - m1.YearFrom + 1) AS YearsSpanA,
    (m2.YearTo - m2.YearFrom + 1) AS YearsSpanB,
    m1.EngineVolume,
    m1.TransmissionType,
    ISNULL(m1.TrimLevel, 'N/A') AS TrimLevelA,
    ISNULL(m2.TrimLevel, 'N/A') AS TrimLevelB,
    m1.CommercialName AS CommercialA,
    m2.CommercialName AS CommercialB,
    '⚠️ SUSPICIOUS' AS Warning
FROM ConsolidatedVehicleModels m1
INNER JOIN ConsolidatedVehicleModels m2
    ON m1.ManufacturerId = m2.ManufacturerId
    AND m1.ModelName = m2.ModelName
    AND m1.ConsolidatedModelId < m2.ConsolidatedModelId
    AND m1.EngineVolume = m2.EngineVolume
    AND (m1.TransmissionType = m2.TransmissionType OR (m1.TransmissionType IS NULL AND m2.TransmissionType IS NULL))
    AND (m1.TrimLevel = m2.TrimLevel OR (m1.TrimLevel IS NULL AND m2.TrimLevel IS NULL))
    -- Year ranges overlap
    AND NOT (m1.YearTo < m2.YearFrom OR m2.YearTo < m1.YearFrom)
INNER JOIN Manufacturers mfg ON m1.ManufacturerId = mfg.ManufacturerId
WHERE
    -- Small overlap (3 years or less) - SUSPICIOUS
    (CASE WHEN m1.YearTo < m2.YearTo THEN m1.YearTo ELSE m2.YearTo END) -
    (CASE WHEN m1.YearFrom > m2.YearFrom THEN m1.YearFrom ELSE m2.YearFrom END) + 1 <= 3
ORDER BY
    mfg.ManufacturerShortName,
    m1.ModelName,
    m1.YearFrom;

GO

-- =====================================================================
-- SCENARIO 2: One range completely contains another (nested ranges)
-- Might indicate a special edition within a model range
-- =====================================================================
SELECT
    mfg.ManufacturerShortName AS Manufacturer,
    m1.ModelName,
    m1.ConsolidatedModelId AS ModelA_ID,
    m2.ConsolidatedModelId AS ModelB_ID,
    m1.YearFrom AS YearFromA,
    m1.YearTo AS YearToA,
    m2.YearFrom AS YearFromB,
    m2.YearTo AS YearToB,
    (m1.YearTo - m1.YearFrom + 1) AS YearsSpanA,
    (m2.YearTo - m2.YearFrom + 1) AS YearsSpanB,
    m1.EngineVolume,
    m1.TransmissionType,
    ISNULL(m1.TrimLevel, 'N/A') AS TrimLevelA,
    ISNULL(m2.TrimLevel, 'N/A') AS TrimLevelB,
    m1.CommercialName AS CommercialA,
    m2.CommercialName AS CommercialB,
    CASE
        WHEN m1.YearFrom <= m2.YearFrom AND m1.YearTo >= m2.YearTo THEN 'A contains B'
        WHEN m2.YearFrom <= m1.YearFrom AND m2.YearTo >= m1.YearTo THEN 'B contains A'
    END AS Relationship,
    '📦 NESTED' AS Warning
FROM ConsolidatedVehicleModels m1
INNER JOIN ConsolidatedVehicleModels m2
    ON m1.ManufacturerId = m2.ManufacturerId
    AND m1.ModelName = m2.ModelName
    AND m1.ConsolidatedModelId < m2.ConsolidatedModelId
    AND m1.EngineVolume = m2.EngineVolume
    AND (m1.TransmissionType = m2.TransmissionType OR (m1.TransmissionType IS NULL AND m2.TransmissionType IS NULL))
    AND (m1.TrimLevel = m2.TrimLevel OR (m1.TrimLevel IS NULL AND m2.TrimLevel IS NULL))
    AND NOT (m1.YearTo < m2.YearFrom OR m2.YearTo < m1.YearFrom)
INNER JOIN Manufacturers mfg ON m1.ManufacturerId = mfg.ManufacturerId
WHERE
    -- One completely contains the other
    (m1.YearFrom <= m2.YearFrom AND m1.YearTo >= m2.YearTo)
    OR
    (m2.YearFrom <= m1.YearFrom AND m2.YearTo >= m1.YearTo)
ORDER BY
    mfg.ManufacturerShortName,
    m1.ModelName,
    m1.YearFrom;

GO

-- =====================================================================
-- SCENARIO 3: Large gap between ranges but still overlapping
-- Might indicate a model that was sold across different periods
-- =====================================================================
SELECT
    mfg.ManufacturerShortName AS Manufacturer,
    m1.ModelName,
    m1.ConsolidatedModelId AS ModelA_ID,
    m2.ConsolidatedModelId AS ModelB_ID,
    m1.YearFrom AS YearFromA,
    m1.YearTo AS YearToA,
    m2.YearFrom AS YearFromB,
    m2.YearTo AS YearToB,
    -- Gap before overlap
    CASE
        WHEN m2.YearFrom > m1.YearFrom THEN m2.YearFrom - m1.YearFrom
        ELSE m1.YearFrom - m2.YearFrom
    END AS YearGap,
    m1.EngineVolume,
    m1.TransmissionType,
    ISNULL(m1.TrimLevel, 'N/A') AS TrimLevelA,
    ISNULL(m2.TrimLevel, 'N/A') AS TrimLevelB,
    m1.CommercialName AS CommercialA,
    m2.CommercialName AS CommercialB,
    '📅 LARGE GAP' AS Warning
FROM ConsolidatedVehicleModels m1
INNER JOIN ConsolidatedVehicleModels m2
    ON m1.ManufacturerId = m2.ManufacturerId
    AND m1.ModelName = m2.ModelName
    AND m1.ConsolidatedModelId < m2.ConsolidatedModelId
    AND m1.EngineVolume = m2.EngineVolume
    AND (m1.TransmissionType = m2.TransmissionType OR (m1.TransmissionType IS NULL AND m2.TransmissionType IS NULL))
    AND (m1.TrimLevel = m2.TrimLevel OR (m1.TrimLevel IS NULL AND m2.TrimLevel IS NULL))
    AND NOT (m1.YearTo < m2.YearFrom OR m2.YearTo < m1.YearFrom)
INNER JOIN Manufacturers mfg ON m1.ManufacturerId = mfg.ManufacturerId
WHERE
    -- Large gap (5+ years) before overlap starts
    CASE
        WHEN m2.YearFrom > m1.YearFrom THEN m2.YearFrom - m1.YearFrom
        ELSE m1.YearFrom - m2.YearFrom
    END >= 5
ORDER BY
    YearGap DESC,
    mfg.ManufacturerShortName,
    m1.ModelName;

GO

-- =====================================================================
-- SCENARIO 4: Commercial names differ significantly
-- Same specs but different commercial names might indicate variants
-- =====================================================================
SELECT
    mfg.ManufacturerShortName AS Manufacturer,
    m1.ModelName,
    m1.ConsolidatedModelId AS ModelA_ID,
    m2.ConsolidatedModelId AS ModelB_ID,
    m1.YearFrom AS YearFromA,
    m1.YearTo AS YearToA,
    m2.YearFrom AS YearFromB,
    m2.YearTo AS YearToB,
    m1.CommercialName AS CommercialA,
    m2.CommercialName AS CommercialB,
    m1.EngineVolume,
    m1.TransmissionType,
    ISNULL(m1.TrimLevel, 'N/A') AS TrimLevelA,
    ISNULL(m2.TrimLevel, 'N/A') AS TrimLevelB,
    '🏷️ DIFFERENT NAMES' AS Warning
FROM ConsolidatedVehicleModels m1
INNER JOIN ConsolidatedVehicleModels m2
    ON m1.ManufacturerId = m2.ManufacturerId
    AND m1.ModelName = m2.ModelName
    AND m1.ConsolidatedModelId < m2.ConsolidatedModelId
    AND m1.EngineVolume = m2.EngineVolume
    AND (m1.TransmissionType = m2.TransmissionType OR (m1.TransmissionType IS NULL AND m2.TransmissionType IS NULL))
    AND (m1.TrimLevel = m2.TrimLevel OR (m1.TrimLevel IS NULL AND m2.TrimLevel IS NULL))
    AND NOT (m1.YearTo < m2.YearFrom OR m2.YearTo < m1.YearFrom)
INNER JOIN Manufacturers mfg ON m1.ManufacturerId = mfg.ManufacturerId
WHERE
    -- Both have commercial names
    m1.CommercialName IS NOT NULL
    AND m2.CommercialName IS NOT NULL
    AND m1.CommercialName != ''
    AND m2.CommercialName != ''
    -- And they're different
    AND m1.CommercialName != m2.CommercialName
ORDER BY
    mfg.ManufacturerShortName,
    m1.ModelName,
    m1.YearFrom;

GO

-- =====================================================================
-- SUMMARY: All suspicious cases combined
-- =====================================================================
SELECT
    'Small Overlap (≤3 years)' AS SuspicionType,
    COUNT(*) AS Count
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
    (CASE WHEN m1.YearTo < m2.YearTo THEN m1.YearTo ELSE m2.YearTo END) -
    (CASE WHEN m1.YearFrom > m2.YearFrom THEN m1.YearFrom ELSE m2.YearFrom END) + 1 <= 3

UNION ALL

SELECT
    'Nested Ranges' AS SuspicionType,
    COUNT(*) AS Count
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
    (m1.YearFrom <= m2.YearFrom AND m1.YearTo >= m2.YearTo)
    OR (m2.YearFrom <= m1.YearFrom AND m2.YearTo >= m1.YearTo)

UNION ALL

SELECT
    'Large Gap (≥5 years)' AS SuspicionType,
    COUNT(*) AS Count
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
    CASE
        WHEN m2.YearFrom > m1.YearFrom THEN m2.YearFrom - m1.YearFrom
        ELSE m1.YearFrom - m2.YearFrom
    END >= 5

UNION ALL

SELECT
    'Different Commercial Names' AS SuspicionType,
    COUNT(*) AS Count
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
    m1.CommercialName IS NOT NULL
    AND m2.CommercialName IS NOT NULL
    AND m1.CommercialName != ''
    AND m2.CommercialName != ''
    AND m1.CommercialName != m2.CommercialName;
