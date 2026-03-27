IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'[dbo].[Service_Line]') AND name = N'IX_ServiceLine_Claim'
)
BEGIN
    CREATE INDEX IX_ServiceLine_Claim ON [dbo].[Service_Line] ([SrvClaFID]);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'[dbo].[Adjustment]') AND name = N'IX_Adjustment_Service'
)
BEGIN
    CREATE INDEX IX_Adjustment_Service ON [dbo].[Adjustment] ([AdjSrvFID]);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'[dbo].[Disbursement]') AND name = N'IX_Disbursement_Service'
)
BEGIN
    CREATE INDEX IX_Disbursement_Service ON [dbo].[Disbursement] ([DisbSrvFID]);
END;
GO
