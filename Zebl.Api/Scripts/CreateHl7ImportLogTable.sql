-- Create Hl7_Import_Log table for tracking HL7 DFT import operations
-- This script is idempotent - safe to run multiple times
-- Database: EZClaimTest (or EZClaim)

USE [EZClaimTest]
GO

PRINT '=== Creating Hl7_Import_Log table ===';
GO

-- Check if table exists
IF NOT EXISTS (
    SELECT 1 
    FROM sys.tables 
    WHERE name = 'Hl7_Import_Log' 
    AND schema_id = SCHEMA_ID('dbo')
)
BEGIN
    CREATE TABLE [dbo].[Hl7_Import_Log] (
        [ImportLogID] INT IDENTITY(1,1) NOT NULL,
        [FileName] NVARCHAR(255) NOT NULL,
        [ImportDateTime] DATETIME2 NOT NULL,
        [NewPatientsCount] INT NOT NULL DEFAULT 0,
        [UpdatedPatientsCount] INT NOT NULL DEFAULT 0,
        [NewClaimsCount] INT NOT NULL DEFAULT 0,
        [NewServiceLinesCount] INT NOT NULL DEFAULT 0,
        [ImportSuccessful] BIT NOT NULL DEFAULT 1,
        [ErrorMessage] NVARCHAR(1000) NULL,
        [ImportedBy] NVARCHAR(100) NULL,
        [ComputerName] NVARCHAR(100) NULL,
        CONSTRAINT [PK__Hl7_Impo__ImportLogID] PRIMARY KEY CLUSTERED ([ImportLogID] ASC)
    );
    
    PRINT 'Created table Hl7_Import_Log';
END
ELSE
BEGIN
    PRINT 'Table Hl7_Import_Log already exists';
    
    -- Check if ComputerName column exists, add it if missing
    IF NOT EXISTS (
        SELECT 1 
        FROM sys.columns 
        WHERE object_id = OBJECT_ID('dbo.Hl7_Import_Log') 
        AND name = 'ComputerName'
    )
    BEGIN
        ALTER TABLE [dbo].[Hl7_Import_Log]
        ADD [ComputerName] NVARCHAR(100) NULL;
        
        PRINT 'Added ComputerName column to existing Hl7_Import_Log table';
    END
    ELSE
    BEGIN
        PRINT 'ComputerName column already exists';
    END
END
GO

-- Create index on ImportDateTime for faster history queries
IF NOT EXISTS (
    SELECT 1 
    FROM sys.indexes 
    WHERE name = 'IX_Hl7_Import_Log_ImportDateTime' 
    AND object_id = OBJECT_ID('dbo.Hl7_Import_Log')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Hl7_Import_Log_ImportDateTime]
    ON [dbo].[Hl7_Import_Log] ([ImportDateTime] DESC);
    
    PRINT 'Created index IX_Hl7_Import_Log_ImportDateTime';
END
ELSE
BEGIN
    PRINT 'Index IX_Hl7_Import_Log_ImportDateTime already exists';
END
GO

PRINT '=== Hl7_Import_Log table setup complete ===';
GO
