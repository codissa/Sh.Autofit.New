-- Part Kits Database Schema
-- This schema allows creating reusable sets of parts (kits) that can be mapped together

-- Table: PartKit
-- Stores metadata about part kits (e.g., "Brake System Kit", "Oil Change Kit")
CREATE TABLE PartKit (
    PartKitId INT IDENTITY(1,1) PRIMARY KEY,
    KitName NVARCHAR(200) NOT NULL,
    Description NVARCHAR(1000) NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedBy NVARCHAR(100) NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy NVARCHAR(100) NULL,
    UpdatedAt DATETIME2 NULL,

    CONSTRAINT UK_PartKit_KitName UNIQUE (KitName)
);

-- Table: PartKitItem
-- Stores the parts that belong to each kit (many-to-many relationship)
CREATE TABLE PartKitItem (
    PartKitItemId INT IDENTITY(1,1) PRIMARY KEY,
    PartKitId INT NOT NULL,
    PartItemKey NVARCHAR(50) NOT NULL,
    DisplayOrder INT NULL,
    Notes NVARCHAR(500) NULL,
    CreatedBy NVARCHAR(100) NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_PartKitItem_PartKit FOREIGN KEY (PartKitId)
        REFERENCES PartKit(PartKitId) ON DELETE CASCADE,

    -- Prevent duplicate parts in the same kit
    CONSTRAINT UK_PartKitItem_Kit_Part UNIQUE (PartKitId, PartItemKey)
);

-- Create indexes for better query performance
CREATE INDEX IX_PartKit_IsActive ON PartKit(IsActive);
CREATE INDEX IX_PartKit_CreatedBy ON PartKit(CreatedBy);
CREATE INDEX IX_PartKitItem_PartKitId ON PartKitItem(PartKitId);
CREATE INDEX IX_PartKitItem_PartItemKey ON PartKitItem(PartItemKey);

GO

-- Sample data for testing
-- Uncomment to insert sample kits

/*
-- Insert sample part kits
INSERT INTO PartKit (KitName, Description, CreatedBy)
VALUES
    (N'ערכת בלמים מלאה', N'כולל דיסקים, רפידות, נוזל בלמים', N'admin'),
    (N'ערכת שמן מלאה', N'כולל שמן מנוע, פילטר שמן, פילטר אוויר', N'admin'),
    (N'ערכת תחזוקה שנתית', N'כל החלקים הנדרשים לתחזוקה שנתית', N'admin');

-- Insert sample part kit items
-- Note: Replace with actual PartItemKey values from your database
INSERT INTO PartKitItem (PartKitId, PartItemKey, DisplayOrder, CreatedBy)
VALUES
    (1, 'BRAKE-DISC-001', 1, N'admin'),
    (1, 'BRAKE-PAD-001', 2, N'admin'),
    (1, 'BRAKE-FLUID-001', 3, N'admin'),

    (2, 'OIL-5W30-001', 1, N'admin'),
    (2, 'FILTER-OIL-001', 2, N'admin'),
    (2, 'FILTER-AIR-001', 3, N'admin');
*/

GO
