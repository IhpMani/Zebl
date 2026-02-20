-- Run this script against your Zebl database to create the EdiReport table for EDI Reports feature.

IF OBJECT_ID('EdiReport', 'U') IS NULL
BEGIN
    CREATE TABLE EdiReport (
        Id uniqueidentifier NOT NULL,
        ReceiverLibraryId uniqueidentifier NOT NULL,
        ConnectionLibraryId uniqueidentifier NULL,
        ClaimId uniqueidentifier NULL,
        FileName nvarchar(500) NOT NULL,
        FileType nvarchar(20) NOT NULL,
        Direction nvarchar(20) NOT NULL,
        Status nvarchar(50) NOT NULL DEFAULT N'Generated',
        TraceNumber nvarchar(100) NULL,
        PayerName nvarchar(255) NULL,
        PaymentAmount decimal(18,2) NULL,
        CreatedAt datetime2 NOT NULL,
        SentAt datetime2 NULL,
        ReceivedAt datetime2 NULL,
        IsArchived bit NOT NULL DEFAULT 0,
        CONSTRAINT PK_EdiReport_Id PRIMARY KEY (Id)
    );
    CREATE INDEX IX_EdiReport_CreatedAt ON EdiReport (CreatedAt);
    CREATE INDEX IX_EdiReport_Status ON EdiReport (Status);
END;
