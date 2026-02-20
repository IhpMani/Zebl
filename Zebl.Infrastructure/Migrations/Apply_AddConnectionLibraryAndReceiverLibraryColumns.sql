-- Run this script against your Zebl database (e.g. in SSMS) to fix 500 errors for Connection Library and Receiver Library.
-- Use the same database as your API connection string.

-- 1. Add missing columns to ReceiverLibrary (skip if column already exists)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ReceiverLibrary') AND name = 'AuthorizationInfoQualifier')
    ALTER TABLE ReceiverLibrary ADD AuthorizationInfoQualifier nvarchar(2) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ReceiverLibrary') AND name = 'ReceiverQualifier')
    ALTER TABLE ReceiverLibrary ADD ReceiverQualifier nvarchar(2) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ReceiverLibrary') AND name = 'SecurityInfoQualifier')
    ALTER TABLE ReceiverLibrary ADD SecurityInfoQualifier nvarchar(2) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ReceiverLibrary') AND name = 'SenderQualifier')
    ALTER TABLE ReceiverLibrary ADD SenderQualifier nvarchar(2) NULL;

-- 2. Create ConnectionLibrary table if it does not exist
IF OBJECT_ID('ConnectionLibrary', 'U') IS NULL
BEGIN
    CREATE TABLE ConnectionLibrary (
        Id uniqueidentifier NOT NULL,
        Name nvarchar(255) NOT NULL,
        Host nvarchar(255) NOT NULL,
        Port int NOT NULL DEFAULT 22,
        Username nvarchar(255) NOT NULL,
        EncryptedPassword nvarchar(2000) NOT NULL,
        UploadDirectory nvarchar(500) NULL,
        DownloadDirectory nvarchar(500) NULL,
        DownloadPattern nvarchar(100) NULL,
        AutoRenameFiles bit NOT NULL,
        AllowMoveOrDelete bit NOT NULL,
        AutoFileExtension nvarchar(10) NULL,
        UseWithInterfacesOnly bit NOT NULL,
        DownloadFromSubdirectories bit NOT NULL,
        IsActive bit NOT NULL DEFAULT 1,
        CreatedAt datetime2 NOT NULL,
        ModifiedAt datetime2 NOT NULL,
        CONSTRAINT PK_ConnectionLibrary_Id PRIMARY KEY (Id)
    );
    CREATE UNIQUE INDEX IX_ConnectionLibrary_Name ON ConnectionLibrary (Name);
END;

-- 3. Tell EF this migration was applied (so "dotnet ef database update" won't run it again)
IF NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20260219120000_AddConnectionLibraryAndReceiverLibraryColumns')
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES (N'20260219120000_AddConnectionLibraryAndReceiverLibraryColumns', N'9.0.11');
