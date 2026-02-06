-- Create unique indexes for HL7 DFT import idempotency
-- These indexes enforce EZClaim-compatible deduplication at the database level
-- Database: EZClaimTest (or EZClaim)

USE [EZClaimTest]
GO

PRINT '=== Creating unique indexes for HL7 DFT import idempotency ===';
GO

-- 1. Patient deduplication by MRN (PatAccountNo)
-- Note: Patient table may already have a unique constraint/index on PatAccountNo
-- Check if index exists before creating
IF NOT EXISTS (
    SELECT 1 
    FROM sys.indexes 
    WHERE name = 'IX_Patient_PatAccountNo' 
    AND object_id = OBJECT_ID('dbo.Patient')
)
BEGIN
    -- Create unique index on PatAccountNo (MRN)
    CREATE UNIQUE NONCLUSTERED INDEX [IX_Patient_PatAccountNo]
    ON [dbo].[Patient] ([PatAccountNo])
    WHERE [PatAccountNo] IS NOT NULL;
    
    PRINT 'Created unique index IX_Patient_PatAccountNo';
END
ELSE
BEGIN
    PRINT 'Index IX_Patient_PatAccountNo already exists';
END
GO

-- 2. Claim deduplication by Patient + DOS + Visit/Account
-- Match: ClaPatFID + ClaFirstDateTRIG + (ClaMedicalRecordNumber OR ClaAdmittedDate)
-- Note: This is a composite unique constraint that allows one claim per patient per DOS per visit
-- Since SQL Server doesn't support OR conditions in unique indexes, we'll create a filtered unique index
-- that covers the most common case: Patient + DOS + Visit Number
IF NOT EXISTS (
    SELECT 1 
    FROM sys.indexes 
    WHERE name = 'IX_Claim_Patient_DOS_Visit' 
    AND object_id = OBJECT_ID('dbo.Claim')
)
BEGIN
    -- Create unique index on Patient + DOS + Visit Number (for HL7 imports)
    -- This prevents duplicate claims for the same patient, DOS, and visit
    CREATE UNIQUE NONCLUSTERED INDEX [IX_Claim_Patient_DOS_Visit]
    ON [dbo].[Claim] ([ClaPatFID], [ClaFirstDateTRIG], [ClaMedicalRecordNumber])
    WHERE [ClaFirstDateTRIG] IS NOT NULL 
    AND [ClaMedicalRecordNumber] IS NOT NULL;
    
    PRINT 'Created unique index IX_Claim_Patient_DOS_Visit';
END
ELSE
BEGIN
    PRINT 'Index IX_Claim_Patient_DOS_Visit already exists';
END
GO

-- Alternative index for claims without visit number but with admit date
IF NOT EXISTS (
    SELECT 1 
    FROM sys.indexes 
    WHERE name = 'IX_Claim_Patient_DOS_AdmitDate' 
    AND object_id = OBJECT_ID('dbo.Claim')
)
BEGIN
    -- Create unique index on Patient + DOS + Admit Date (for HL7 imports without visit number)
    CREATE UNIQUE NONCLUSTERED INDEX [IX_Claim_Patient_DOS_AdmitDate]
    ON [dbo].[Claim] ([ClaPatFID], [ClaFirstDateTRIG], [ClaAdmittedDate])
    WHERE [ClaFirstDateTRIG] IS NOT NULL 
    AND [ClaAdmittedDate] IS NOT NULL
    AND [ClaMedicalRecordNumber] IS NULL;
    
    PRINT 'Created unique index IX_Claim_Patient_DOS_AdmitDate';
END
ELSE
BEGIN
    PRINT 'Index IX_Claim_Patient_DOS_AdmitDate already exists';
END
GO

-- 3. Service Line deduplication by Claim + CPT + DOS
-- Match: SrvClaFID + SrvProcedureCode + SrvFromDate
IF NOT EXISTS (
    SELECT 1 
    FROM sys.indexes 
    WHERE name = 'IX_ServiceLine_Claim_CPT_DOS' 
    AND object_id = OBJECT_ID('dbo.Service_Line')
)
BEGIN
    -- Create unique index on Claim + CPT + DOS
    CREATE UNIQUE NONCLUSTERED INDEX [IX_ServiceLine_Claim_CPT_DOS]
    ON [dbo].[Service_Line] ([SrvClaFID], [SrvProcedureCode], [SrvFromDate])
    WHERE [SrvProcedureCode] IS NOT NULL 
    AND [SrvFromDate] IS NOT NULL;
    
    PRINT 'Created unique index IX_ServiceLine_Claim_CPT_DOS';
END
ELSE
BEGIN
    PRINT 'Index IX_ServiceLine_Claim_CPT_DOS already exists';
END
GO

-- Verify indexes were created
PRINT '=== Verification ===';
GO

SELECT 
    i.name AS IndexName,
    OBJECT_NAME(i.object_id) AS TableName,
    STRING_AGG(c.name, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS IndexColumns
FROM sys.indexes i
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
WHERE i.name IN ('IX_Patient_PatAccountNo', 'IX_Claim_Patient_DOS_Visit', 'IX_Claim_Patient_DOS_AdmitDate', 'IX_ServiceLine_Claim_CPT_DOS')
AND i.is_unique = 1
GROUP BY i.name, OBJECT_NAME(i.object_id)
ORDER BY TableName, IndexName;
GO

PRINT '=== Script completed ===';
GO
