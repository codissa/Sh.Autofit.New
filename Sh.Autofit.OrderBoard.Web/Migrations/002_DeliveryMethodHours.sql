-- =============================================
-- Sh.Autofit.OrderBoard — Add time windows to DeliveryMethods
-- Target: Sh.Autofit database (dbo schema)
-- =============================================

USE [Sh.Autofit];
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DeliveryMethods') AND name = 'WindowStartTime')
BEGIN
    ALTER TABLE dbo.DeliveryMethods ADD WindowStartTime TIME NULL;
    ALTER TABLE dbo.DeliveryMethods ADD WindowEndTime TIME NULL;
END
GO

PRINT 'DeliveryMethods: WindowStartTime, WindowEndTime columns added.';
GO
