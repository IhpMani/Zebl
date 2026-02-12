-- Create ListValue table for storing user-added custom values
-- This is a minimal helper table to store custom dropdown values
-- Database: EZClaimTest (or EZClaim)

USE [EZClaimTest]
GO

PRINT '=== Creating ListValue table ===';
GO

-- Check if table exists
IF NOT EXISTS (
    SELECT 1 
    FROM sys.tables 
    WHERE name = 'ListValue' 
    AND schema_id = SCHEMA_ID('dbo')
)
BEGIN
    CREATE TABLE [dbo].[ListValue] (
        [ListValueID] INT IDENTITY(1,1) NOT NULL,
        [ListType] NVARCHAR(100) NOT NULL,  -- e.g., "Claim Classification", "Patient Classification"
        [Value] NVARCHAR(255) NOT NULL,     -- The custom value added by user
        [IsActive] BIT NOT NULL DEFAULT 1,
        [CreatedDate] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [CreatedBy] NVARCHAR(100) NULL,
        CONSTRAINT [PK__ListValue__ListValueID] PRIMARY KEY CLUSTERED ([ListValueID] ASC)
    );
    
    -- Unique constraint to prevent duplicate values per list type
    CREATE UNIQUE NONCLUSTERED INDEX [UX_ListValue_Type_Value]
    ON [dbo].[ListValue] ([ListType], [Value]);
    
    PRINT 'Created table ListValue';
END
ELSE
BEGIN
    PRINT 'Table ListValue already exists';
END
GO

PRINT '=== ListValue table setup complete ===';
GO
