-- Create Interface_Import_Log table for file-level import history
-- Separate from Claim_Audit - only for interface/file imports
-- This script is idempotent - safe to run multiple times

USE [EZClaimTest]
GO

PRINT '=== Creating Interface_Import_Log table ===';
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.tables
    WHERE name = 'Interface_Import_Log'
    AND schema_id = SCHEMA_ID('dbo')
)
BEGIN
    CREATE TABLE [dbo].[Interface_Import_Log] (
        [ImportID] INT IDENTITY(1,1) NOT NULL,
        [FileName] NVARCHAR(255) NOT NULL,
        [ImportDate] DATETIME2 NOT NULL,
        [UserName] NVARCHAR(100) NULL,
        [ComputerName] NVARCHAR(100) NULL,
        [NewPatientsCount] INT NOT NULL DEFAULT 0,
        [UpdatedPatientsCount] INT NOT NULL DEFAULT 0,
        [NewClaimsCount] INT NOT NULL DEFAULT 0,
        [DuplicateClaimsCount] INT NOT NULL DEFAULT 0,
        [TotalAmount] MONEY NOT NULL DEFAULT 0,
        [Notes] NVARCHAR(500) NULL,
        CONSTRAINT [PK_Interface_Import_Log] PRIMARY KEY CLUSTERED ([ImportID] ASC)
    );

    PRINT 'Created table Interface_Import_Log';
END
ELSE
BEGIN
    PRINT 'Table Interface_Import_Log already exists';
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_Interface_Import_Log_ImportDate'
    AND object_id = OBJECT_ID('dbo.Interface_Import_Log')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Interface_Import_Log_ImportDate]
    ON [dbo].[Interface_Import_Log] ([ImportDate] DESC);

    PRINT 'Created index IX_Interface_Import_Log_ImportDate';
END
GO

PRINT '=== Interface_Import_Log table setup complete ===';
GO
