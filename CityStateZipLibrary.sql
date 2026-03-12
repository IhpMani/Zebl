-- Manual SQL script (if EF migrations are not used)
-- Creates: CityStateZipLibrary + index IX_CityStateZip_State

IF OBJECT_ID(N'[dbo].[CityStateZipLibrary]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[CityStateZipLibrary] (
        [Id] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_CityStateZipLibrary_Id] PRIMARY KEY,
        [City] nvarchar(100) NOT NULL,
        [State] nvarchar(10) NOT NULL,
        [Zip] nvarchar(15) NOT NULL,
        [IsActive] bit NOT NULL CONSTRAINT [DF_CityStateZipLibrary_IsActive] DEFAULT ((1)),
        [CreatedAt] datetime2 NOT NULL CONSTRAINT [DF_CityStateZipLibrary_CreatedAt] DEFAULT (sysutcdatetime()),
        [UpdatedAt] datetime2 NOT NULL CONSTRAINT [DF_CityStateZipLibrary_UpdatedAt] DEFAULT (sysutcdatetime())
    );
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_CityStateZip_State'
      AND object_id = OBJECT_ID(N'[dbo].[CityStateZipLibrary]')
)
BEGIN
    CREATE INDEX [IX_CityStateZip_State]
    ON [dbo].[CityStateZipLibrary] ([State]);
END
GO

