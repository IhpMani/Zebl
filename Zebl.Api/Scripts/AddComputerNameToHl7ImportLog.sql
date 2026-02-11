-- Add ComputerName column to existing Hl7_Import_Log table
-- The table already exists in the database (per DBML)
-- This script only adds the ComputerName column if it's missing

USE [EZClaimTest]
GO

PRINT '=== Adding ComputerName column to Hl7_Import_Log table ===';
GO

IF EXISTS (
    SELECT 1 
    FROM sys.tables 
    WHERE name = 'Hl7_Import_Log' 
    AND schema_id = SCHEMA_ID('dbo')
)
BEGIN
    IF NOT EXISTS (
        SELECT 1 
        FROM sys.columns 
        WHERE object_id = OBJECT_ID('dbo.Hl7_Import_Log') 
        AND name = 'ComputerName'
    )
    BEGIN
        ALTER TABLE [dbo].[Hl7_Import_Log]
        ADD [ComputerName] NVARCHAR(100) NULL;
        
        PRINT 'Added ComputerName column to Hl7_Import_Log table';
    END
    ELSE
    BEGIN
        PRINT 'ComputerName column already exists in Hl7_Import_Log table';
    END
END
ELSE
BEGIN
    PRINT 'ERROR: Table Hl7_Import_Log does not exist in database. Please verify the table exists per DBML.';
END
GO

PRINT '=== ComputerName column migration complete ===';
GO
