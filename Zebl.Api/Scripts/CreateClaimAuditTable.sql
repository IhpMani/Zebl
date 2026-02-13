-- Create Claim_Audit table for claim notes/activity (Claim Edited, manual notes, etc.)
-- Idempotent - safe to run multiple times
-- One row per note; when a claim is edited, a new row is inserted.
-- Run AddClaimAuditFinancialSnapshotColumns.sql after this to add TotalCharge, InsuranceBalance, PatientBalance.

USE [EZClaimTest]
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Claim_Audit' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE [dbo].[Claim_Audit] (
        [AuditID] INT IDENTITY(1,1) NOT NULL,
        [ClaFID] INT NOT NULL,
        [ActivityType] NVARCHAR(50) NOT NULL,
        [ActivityDate] DATETIME2 NOT NULL,
        [UserName] NVARCHAR(100) NULL,
        [ComputerName] NVARCHAR(100) NULL,
        [Notes] NVARCHAR(500) NULL,
        CONSTRAINT [PK_Claim_Audit] PRIMARY KEY ([AuditID]),
        CONSTRAINT [FK_Claim_Audit_Claim] FOREIGN KEY ([ClaFID]) REFERENCES [dbo].[Claim]([ClaID])
    );
    PRINT 'Created table Claim_Audit';
END
ELSE
BEGIN
    PRINT 'Table Claim_Audit already exists';
END
GO
