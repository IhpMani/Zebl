-- Add PatPaymentMatchingKey and PayPaymentMatchingKey columns (EZClaim schema)
-- Fixes 500 error: Invalid column name 'PatPaymentMatchingKey'
-- Idempotent - safe to run multiple times

USE [EZClaimTest]
GO

-- Patient.PatPaymentMatchingKey
IF COL_LENGTH('dbo.Patient', 'PatPaymentMatchingKey') IS NULL
BEGIN
    ALTER TABLE [dbo].[Patient] ADD [PatPaymentMatchingKey] VARCHAR(30) NULL;
    PRINT 'Added column Patient.PatPaymentMatchingKey';
END
GO

-- Payer.PayPaymentMatchingKey
IF COL_LENGTH('dbo.Payer', 'PayPaymentMatchingKey') IS NULL
BEGIN
    ALTER TABLE [dbo].[Payer] ADD [PayPaymentMatchingKey] VARCHAR(30) NULL;
    PRINT 'Added column Payer.PayPaymentMatchingKey';
END
GO
