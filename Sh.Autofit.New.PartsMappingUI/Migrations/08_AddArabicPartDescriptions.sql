-- =====================================================================
-- Migration 08: Add ArabicPartDescriptions table
-- Purpose: Support Arabic descriptions for parts (for sticker printing)
-- Date: 2026-01-14
-- =====================================================================

USE [Sh.Autofit]
GO

-- =====================================================================
-- Table: ArabicPartDescriptions
-- Purpose: Store Arabic language descriptions for parts
-- =====================================================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ArabicPartDescriptions]') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[ArabicPartDescriptions] (
        [ArabicDescriptionId] INT IDENTITY(1,1) NOT NULL,
        [ItemKey] NVARCHAR(20) NOT NULL,
        [ArabicDescription] NVARCHAR(1000) NOT NULL,
        [IsActive] BIT NOT NULL DEFAULT 1,
        [CreatedAt] DATETIME2(3) NOT NULL DEFAULT GETDATE(),
        [UpdatedAt] DATETIME2(3) NOT NULL DEFAULT GETDATE(),
        [CreatedBy] NVARCHAR(100) NULL,
        [UpdatedBy] NVARCHAR(100) NULL,

        CONSTRAINT [PK_ArabicPartDescriptions] PRIMARY KEY CLUSTERED ([ArabicDescriptionId] ASC),
        CONSTRAINT [UQ_ArabicPartDescriptions_ItemKey] UNIQUE ([ItemKey])
    );

    PRINT 'Created table: ArabicPartDescriptions';
END
ELSE
BEGIN
    PRINT 'Table ArabicPartDescriptions already exists';
END
GO

-- =====================================================================
-- Create indexes for performance
-- =====================================================================

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ArabicPartDescriptions_ItemKey')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ArabicPartDescriptions_ItemKey]
    ON [dbo].[ArabicPartDescriptions]([ItemKey])
    WHERE [IsActive] = 1;

    PRINT 'Created index: IX_ArabicPartDescriptions_ItemKey';
END
ELSE
BEGIN
    PRINT 'Index IX_ArabicPartDescriptions_ItemKey already exists';
END
GO

-- =====================================================================
-- Verification
-- =====================================================================

PRINT '=================================================================';
PRINT 'Verifying migration 08: ArabicPartDescriptions';
PRINT '=================================================================';

-- Check table exists
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ArabicPartDescriptions]') AND type = 'U')
    PRINT '✓ Table ArabicPartDescriptions exists';
ELSE
    PRINT '✗ ERROR: Table ArabicPartDescriptions does not exist';

-- Check unique constraint
IF EXISTS (SELECT * FROM sys.key_constraints WHERE name = 'UQ_ArabicPartDescriptions_ItemKey')
    PRINT '✓ Unique constraint on ItemKey exists';
ELSE
    PRINT '✗ ERROR: Unique constraint on ItemKey does not exist';

-- Check index
IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ArabicPartDescriptions_ItemKey')
    PRINT '✓ Index IX_ArabicPartDescriptions_ItemKey exists';
ELSE
    PRINT '✗ ERROR: Index IX_ArabicPartDescriptions_ItemKey does not exist';

PRINT '=================================================================';
PRINT 'Migration 08: Add ArabicPartDescriptions completed successfully';
PRINT '=================================================================';
GO
