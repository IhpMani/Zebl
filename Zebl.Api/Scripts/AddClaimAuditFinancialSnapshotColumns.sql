-- Add financial snapshot columns to Claim_Audit (EZClaim-style history)
-- Idempotent - safe to run multiple times
-- Run after CreateClaimAuditTable.sql

USE [EZClaimTest]
GO

IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Claim_Audit' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    IF COL_LENGTH('dbo.Claim_Audit', 'TotalCharge') IS NULL
    BEGIN
        ALTER TABLE [dbo].[Claim_Audit] ADD [TotalCharge] MONEY NULL;
        PRINT 'Added column TotalCharge';
    END

    IF COL_LENGTH('dbo.Claim_Audit', 'InsuranceBalance') IS NULL
    BEGIN
        ALTER TABLE [dbo].[Claim_Audit] ADD [InsuranceBalance] MONEY NULL;
        PRINT 'Added column InsuranceBalance';
    END

    IF COL_LENGTH('dbo.Claim_Audit', 'PatientBalance') IS NULL
    BEGIN
        ALTER TABLE [dbo].[Claim_Audit] ADD [PatientBalance] MONEY NULL;
        PRINT 'Added column PatientBalance';
    END
END
GO
