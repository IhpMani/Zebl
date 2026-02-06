-- Fix Claim.ClaICDIndicator column length to VARCHAR(2) to support ICD-09 and ICD-10
-- This aligns with EZClaim behavior where ICD indicators are "09" or "10"
-- Database: EZClaimTest (or EZClaim)

USE [EZClaimTest]
GO

PRINT '=== Inspecting Claim.ClaICDIndicator column ===';
GO

-- Check current column definition
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_SCHEMA = 'dbo' 
AND TABLE_NAME = 'Claim' 
AND COLUMN_NAME = 'ClaICDIndicator';
GO

-- Check for default constraints
SELECT 
    dc.name AS ConstraintName,
    dc.definition AS DefaultValue
FROM sys.default_constraints dc
INNER JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
WHERE OBJECT_NAME(dc.parent_object_id) = 'Claim'
AND c.name = 'ClaICDIndicator';
GO

-- Check for triggers that might modify ClaICDIndicator
SELECT 
    t.name AS TriggerName,
    OBJECT_DEFINITION(t.object_id) AS TriggerDefinition
FROM sys.triggers t
INNER JOIN sys.objects o ON t.parent_id = o.object_id
WHERE o.name = 'Claim'
AND OBJECT_DEFINITION(t.object_id) LIKE '%ClaICDIndicator%';
GO

PRINT '=== Fixing Claim.ClaICDIndicator column ===';
GO

-- Step 1: Drop existing default constraint if it exists (to allow column modification)
IF EXISTS (
    SELECT 1 
    FROM sys.default_constraints 
    WHERE parent_object_id = OBJECT_ID('dbo.Claim')
    AND parent_column_id = COLUMNPROPERTY(OBJECT_ID('dbo.Claim'), 'ClaICDIndicator', 'ColumnId')
)
BEGIN
    DECLARE @ConstraintName NVARCHAR(200);
    SELECT @ConstraintName = name 
    FROM sys.default_constraints 
    WHERE parent_object_id = OBJECT_ID('dbo.Claim')
    AND parent_column_id = COLUMNPROPERTY(OBJECT_ID('dbo.Claim'), 'ClaICDIndicator', 'ColumnId');
    
    EXEC('ALTER TABLE [dbo].[Claim] DROP CONSTRAINT [' + @ConstraintName + ']');
    PRINT 'Dropped existing default constraint: ' + @ConstraintName;
END
GO

-- Step 2: Update column to VARCHAR(2) NOT NULL
IF EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = 'dbo' 
    AND TABLE_NAME = 'Claim' 
    AND COLUMN_NAME = 'ClaICDIndicator'
)
BEGIN
    -- Check if column length needs to be updated
    DECLARE @CurrentLength INT;
    SELECT @CurrentLength = CHARACTER_MAXIMUM_LENGTH
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = 'dbo' 
    AND TABLE_NAME = 'Claim' 
    AND COLUMN_NAME = 'ClaICDIndicator';
    
    IF @CurrentLength < 2
    BEGIN
        -- Update any existing '1' values to '10' before altering column
        UPDATE [dbo].[Claim]
        SET [ClaICDIndicator] = '10'
        WHERE [ClaICDIndicator] = '1' OR LEN([ClaICDIndicator]) = 1;
        
        PRINT 'Updated existing single-character values to ''10''';
        
        -- Alter column to VARCHAR(2) NOT NULL
        ALTER TABLE [dbo].[Claim]
        ALTER COLUMN [ClaICDIndicator] VARCHAR(2) NOT NULL;
        
        PRINT 'Updated Claim.ClaICDIndicator to VARCHAR(2) NOT NULL';
    END
    ELSE
    BEGIN
        PRINT 'Claim.ClaICDIndicator already has correct length (' + CAST(@CurrentLength AS VARCHAR(10)) + ')';
    END
END
ELSE
BEGIN
    PRINT 'ERROR: Claim.ClaICDIndicator column does not exist!';
END
GO

-- Step 3: Add default constraint '10' if missing
IF NOT EXISTS (
    SELECT 1 
    FROM sys.default_constraints 
    WHERE parent_object_id = OBJECT_ID('dbo.Claim')
    AND parent_column_id = COLUMNPROPERTY(OBJECT_ID('dbo.Claim'), 'ClaICDIndicator', 'ColumnId')
)
BEGIN
    ALTER TABLE [dbo].[Claim]
    ADD CONSTRAINT [DF_Claim_ClaICDIndicator] DEFAULT ('10') FOR [ClaICDIndicator];
    
    PRINT 'Added default constraint DF_Claim_ClaICDIndicator = ''10''';
END
ELSE
BEGIN
    PRINT 'Default constraint already exists for ClaICDIndicator';
END
GO

-- Step 4: Verify final state
PRINT '=== Verification ===';
GO

SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_SCHEMA = 'dbo' 
AND TABLE_NAME = 'Claim' 
AND COLUMN_NAME = 'ClaICDIndicator';
GO

SELECT 
    dc.name AS ConstraintName,
    dc.definition AS DefaultValue
FROM sys.default_constraints dc
INNER JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
WHERE OBJECT_NAME(dc.parent_object_id) = 'Claim'
AND c.name = 'ClaICDIndicator';
GO

PRINT '=== Script completed ===';
GO
