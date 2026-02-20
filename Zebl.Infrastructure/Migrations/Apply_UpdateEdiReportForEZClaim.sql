-- Migration: Update EdiReport table to match EZClaim behavior
-- Adds: Note, IsRead, FileSize, FileContent columns
-- Removes: ClaimId column (if exists)

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EdiReport')
BEGIN
    -- Table doesn't exist, create it with all columns
    CREATE TABLE [dbo].[EdiReport] (
        [Id] uniqueidentifier NOT NULL PRIMARY KEY,
        [ReceiverLibraryId] uniqueidentifier NULL,
        [ConnectionLibraryId] uniqueidentifier NULL,
        [FileName] nvarchar(500) NOT NULL,
        [FileType] nvarchar(20) NOT NULL,
        [Direction] nvarchar(20) NOT NULL,
        [Status] nvarchar(50) NOT NULL DEFAULT 'Generated',
        [TraceNumber] nvarchar(100) NULL,
        [PayerName] nvarchar(255) NULL,
        [PaymentAmount] decimal(18,2) NULL,
        [Note] nvarchar(255) NULL,
        [IsArchived] bit NOT NULL DEFAULT 0,
        [IsRead] bit NOT NULL DEFAULT 0,
        [FileSize] bigint NOT NULL DEFAULT 0,
        [FileContent] varbinary(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [SentAt] datetime2 NULL,
        [ReceivedAt] datetime2 NULL
    );

    CREATE INDEX [IX_EdiReport_CreatedAt] ON [dbo].[EdiReport] ([CreatedAt]);
    CREATE INDEX [IX_EdiReport_Status] ON [dbo].[EdiReport] ([Status]);
END
ELSE
BEGIN
    -- Table exists, add missing columns
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EdiReport') AND name = 'Note')
    BEGIN
        ALTER TABLE [dbo].[EdiReport] ADD [Note] nvarchar(255) NULL;
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EdiReport') AND name = 'IsRead')
    BEGIN
        ALTER TABLE [dbo].[EdiReport] ADD [IsRead] bit NOT NULL DEFAULT 0;
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EdiReport') AND name = 'FileSize')
    BEGIN
        ALTER TABLE [dbo].[EdiReport] ADD [FileSize] bigint NOT NULL DEFAULT 0;
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EdiReport') AND name = 'FileContent')
    BEGIN
        ALTER TABLE [dbo].[EdiReport] ADD [FileContent] varbinary(max) NULL;
    END

    -- Remove ClaimId if it exists (not in EZClaim spec)
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EdiReport') AND name = 'ClaimId')
    BEGIN
        ALTER TABLE [dbo].[EdiReport] DROP COLUMN [ClaimId];
    END

    -- Update ReceiverLibraryId to nullable if not already
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EdiReport') AND name = 'ReceiverLibraryId' AND is_nullable = 0)
    BEGIN
        ALTER TABLE [dbo].[EdiReport] ALTER COLUMN [ReceiverLibraryId] uniqueidentifier NULL;
    END
END

-- Ensure indexes exist
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_EdiReport_CreatedAt' AND object_id = OBJECT_ID('EdiReport'))
BEGIN
    CREATE INDEX [IX_EdiReport_CreatedAt] ON [dbo].[EdiReport] ([CreatedAt]);
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_EdiReport_Status' AND object_id = OBJECT_ID('EdiReport'))
BEGIN
    CREATE INDEX [IX_EdiReport_Status] ON [dbo].[EdiReport] ([Status]);
END
