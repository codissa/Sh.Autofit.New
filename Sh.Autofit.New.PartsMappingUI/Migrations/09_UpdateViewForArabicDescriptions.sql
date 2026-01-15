-- =====================================================================
-- Migration 09: Update vw_Parts view to include Arabic descriptions
-- Purpose: Add Arabic description column to existing parts view
-- Date: 2026-01-14
-- =====================================================================

USE [Sh.Autofit]
GO

-- =====================================================================
-- Update vw_Parts view to include Arabic descriptions
-- =====================================================================

CREATE OR ALTER VIEW dbo.vw_Parts AS
SELECT
    i.ItemKey AS PartNumber,
    i.ItemName AS PartName,
    i.Price AS RetailPrice,
    i.PurchPrice AS CostPrice,
    i.Quantity AS StockQuantity,

    -- OEM numbers from ExtraNotes (NoteID 2, 5, 6, 7, 8)
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
    CASE WHEN ISNULL(i.IntrItem, 0) = 0 THEN 1 ELSE 0 END AS IsActive,

    -- Metadata from PartsMetadata table (if exists)
    pm.CompatibilityNotes,
    ISNULL(pm.UniversalPart, 0) AS UniversalPart,
    pm.ImageUrl,
    pm.DatasheetUrl,
    pm.CustomDescription,
    ISNULL(pm.UseCustomDescription, 0) AS UseCustomDescription,
    pm.UpdatedAt AS MetadataUpdatedAt,

    -- Arabic description from ArabicPartDescriptions table (NEW)
    apd.ArabicDescription

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
LEFT JOIN dbo.ArabicPartDescriptions apd ON i.ItemKey = apd.ItemKey AND apd.IsActive = 1  -- NEW JOIN
WHERE ISNULL(i.TreeType, 0) = 0; -- Only items, not folders/categories
GO

PRINT '=================================================================';
PRINT 'Updated view: vw_Parts now includes ArabicDescription column';
PRINT '=================================================================';
GO
