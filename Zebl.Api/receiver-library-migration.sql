IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE TABLE [AppUser] (
        [UserGUID] uniqueidentifier NOT NULL,
        [UserName] nvarchar(100) NOT NULL,
        [Email] nvarchar(200) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetime2 NOT NULL DEFAULT ((sysutcdatetime())),
        [PasswordHash] varbinary(64) NULL,
        [PasswordSalt] varbinary(32) NULL,
        CONSTRAINT [PK__AppUser__81B7740C5F82BC9D] PRIMARY KEY ([UserGUID])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE TABLE [Claim_Audit] (
        [AuditID] int NOT NULL IDENTITY,
        [ClaFID] int NOT NULL,
        [ActivityType] nvarchar(50) NOT NULL,
        [ActivityDate] datetime2 NOT NULL,
        [UserName] nvarchar(100) NULL,
        [ComputerName] nvarchar(100) NULL,
        [Notes] nvarchar(500) NULL,
        [TotalCharge] money NULL,
        [InsuranceBalance] money NULL,
        [PatientBalance] money NULL,
        CONSTRAINT [PK_Claim_Audit] PRIMARY KEY ([AuditID])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE TABLE [Hl7_Import_Log] (
        [ImportLogID] int NOT NULL IDENTITY,
        [FileName] nvarchar(255) NOT NULL,
        [ImportDateTime] datetime2 NOT NULL,
        [NewPatientsCount] int NOT NULL,
        [UpdatedPatientsCount] int NOT NULL,
        [NewClaimsCount] int NOT NULL,
        [NewServiceLinesCount] int NOT NULL,
        [ImportSuccessful] bit NOT NULL,
        [ErrorMessage] nvarchar(1000) NULL,
        [ImportedBy] nvarchar(100) NULL,
        [ComputerName] nvarchar(100) NULL,
        CONSTRAINT [PK__Hl7_Impo__ImportLogID] PRIMARY KEY ([ImportLogID])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE TABLE [Interface_Import_Log] (
        [ImportID] int NOT NULL IDENTITY,
        [FileName] nvarchar(255) NOT NULL,
        [ImportDate] datetime2 NOT NULL,
        [UserName] nvarchar(100) NULL,
        [ComputerName] nvarchar(100) NULL,
        [NewPatientsCount] int NOT NULL,
        [UpdatedPatientsCount] int NOT NULL,
        [NewClaimsCount] int NOT NULL,
        [DuplicateClaimsCount] int NOT NULL,
        [TotalAmount] money NOT NULL,
        [Notes] nvarchar(500) NULL,
        CONSTRAINT [PK_Interface_Import_Log] PRIMARY KEY ([ImportID])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE TABLE [ListValue] (
        [ListValueID] int NOT NULL IDENTITY,
        [ListType] nvarchar(100) NOT NULL,
        [Value] nvarchar(255) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK__ListValue__ListValueID] PRIMARY KEY ([ListValueID])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE TABLE [Payer] (
        [PayID] int NOT NULL IDENTITY,
        [PayDateTimeCreated] datetime2 NOT NULL,
        [PayDateTimeModified] datetime2 NOT NULL,
        [PayCreatedUserGUID] uniqueidentifier NULL,
        [PayLastUserGUID] uniqueidentifier NULL,
        [PayCreatedUserName] varchar(50) NULL,
        [PayLastUserName] varchar(50) NULL,
        [PayCreatedComputerName] varchar(100) NULL,
        [PayLastComputerName] varchar(100) NULL,
        [PayName] varchar(50) NULL,
        [PayExternalID] varchar(80) NULL,
        [PayAddr1] varchar(50) NULL,
        [PayAddr2] varchar(50) NULL,
        [PayAlwaysExportSupervisingProvider] bit NOT NULL,
        [PayBox1] varchar(10) NULL,
        [PayCity] varchar(50) NULL,
        [PayClaimFilingIndicator] varchar(5) NULL,
        [PayClaimType] varchar(50) NOT NULL,
        [PayClassification] varchar(30) NULL,
        [PayEligibilityPhyID] int NOT NULL,
        [PayEligibilityPayerID] varchar(80) NULL,
        [PayEmail] varchar(255) NULL,
        [PayExportAuthIn2400] bit NOT NULL,
        [PayExportBillingTaxonomy] bit NOT NULL,
        [PayExportOtherPayerOfficeNumber2330B] bit NOT NULL,
        [PayExportOriginalRefIn2330B] bit NOT NULL,
        [PayExportPatientAmtDueIn2430] bit NOT NULL,
        [PayExportPatientForPOS12] bit NOT NULL,
        [PayExportPaymentDateIn2330B] bit NOT NULL,
        [PayExportSSN] bit NOT NULL,
        [PayFaxNo] varchar(25) NULL,
        [PayFollowUpDays] int NOT NULL,
        [PayForwardsClaims] bit NOT NULL,
        [PayICDIndicator] varchar(1) NULL,
        [PayIgnoreRenderingProvider] bit NOT NULL,
        [PayInactive] bit NOT NULL,
        [PayInsTypeCode] varchar(10) NULL,
        [PayNotes] varchar(255) NULL,
        [PayOfficeNumber] varchar(30) NULL,
        [PayPaymentMatchingKey] varchar(30) NULL,
        [PayPhoneNo] varchar(25) NULL,
        [PayPrintBox30] bit NOT NULL,
        [PayFormatDateBox14And15] bit NOT NULL,
        [PayState] varchar(2) NULL,
        [PaySubmissionMethod] varchar(20) NOT NULL,
        [PaySuppressWhenPrinting] bit NOT NULL,
        [PayTotalUndisbursedPaymentsTRIG] money NOT NULL,
        [PayExportTrackedPRAdjs] bit NOT NULL,
        [PayUseTotalAppliedInBox29] bit NOT NULL,
        [PayWebsite] varchar(max) NULL,
        [PayZip] varchar(15) NULL,
        [PayNameWithInactiveCC] AS (case when [PayInactive]=(1) then 'INACTIVE - '+isnull([PayName],'') else isnull([PayName],'') end) PERSISTED,
        [PayCityStateZipCC] AS ((((isnull([PayCity],'')+', ')+isnull([PayState],''))+' ')+isnull([PayZip],'')) PERSISTED,
        CONSTRAINT [PK__Payer__EE8FCE2FDCFA87D9] PRIMARY KEY ([PayID])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE TABLE [Physician] (
        [PhyID] int NOT NULL IDENTITY,
        [PhyDateTimeCreated] datetime2 NOT NULL,
        [PhyDateTimeModified] datetime2 NOT NULL,
        [PhyCreatedUserGUID] uniqueidentifier NULL,
        [PhyLastUserGUID] uniqueidentifier NULL,
        [PhyCreatedUserName] varchar(50) NULL,
        [PhyLastUserName] varchar(50) NULL,
        [PhyCreatedComputerName] varchar(100) NULL,
        [PhyLastComputerName] varchar(100) NULL,
        [PhyAddress1] varchar(55) NULL,
        [PhyAddress2] varchar(55) NULL,
        [PhyCity] varchar(50) NULL,
        [PhyEMail] varchar(80) NULL,
        [PhyEntityType] varchar(1) NULL,
        [PhyFax] varchar(80) NULL,
        [PhyFirstName] varchar(35) NULL,
        [PhyInactive] bit NOT NULL,
        [PhyLastName] varchar(60) NULL,
        [PhyMiddleName] varchar(25) NULL,
        [PhyName] varchar(100) NULL,
        [PhyNotes] varchar(max) NULL,
        [PhyNPI] varchar(20) NULL,
        [PhyPrimaryCodeType] varchar(2) NULL,
        [PhyPrimaryIDCode] varchar(80) NULL,
        [PhyRateClass] varchar(50) NULL,
        [PhySignatureOnFile] bit NOT NULL,
        [PhySpecialtyCode] varchar(30) NULL,
        [PhyState] varchar(2) NULL,
        [PhySuffix] varchar(10) NULL,
        [PhyTelephone] varchar(80) NULL,
        [PhyType] varchar(50) NOT NULL,
        [PhyZip] varchar(15) NULL,
        [PhyFirstMiddleLastNameCC] AS ((((isnull([PhyFirstName],'')+' ')+isnull([PhyMiddleName],''))+' ')+isnull([PhyLastName],'')) PERSISTED,
        [PhyFullNameCC] AS ((isnull([PhyLastName],'')+', ')+isnull([PhyFirstName],'')) PERSISTED,
        [PhyNameWithInactiveCC] AS (case when [PhyInactive]=(1) then 'INACTIVE - '+isnull([PhyName],'') else isnull([PhyName],'') end) PERSISTED,
        [PhyCityStateZipCC] AS ((isnull([PhyCity],'')+', ')+isnull([PhyState],'')) PERSISTED,
        CONSTRAINT [PK__Physicia__5FEDBF914E56D8C8] PRIMARY KEY ([PhyID])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE TABLE [ReceiverLibrary] (
        [Id] uniqueidentifier NOT NULL,
        [LibraryEntryName] nvarchar(255) NOT NULL,
        [ExportFormat] nvarchar(50) NOT NULL,
        [ClaimType] nvarchar(100) NULL,
        [SubmitterType] int NOT NULL,
        [BusinessOrLastName] nvarchar(255) NULL,
        [FirstName] nvarchar(255) NULL,
        [SubmitterId] nvarchar(100) NULL,
        [ContactName] nvarchar(255) NULL,
        [ContactType] nvarchar(50) NULL,
        [ContactValue] nvarchar(255) NULL,
        [ReceiverName] nvarchar(255) NULL,
        [ReceiverId] nvarchar(100) NULL,
        [AuthorizationInfo] nvarchar(50) NULL,
        [SecurityInfo] nvarchar(50) NULL,
        [SenderId] nvarchar(50) NULL,
        [InterchangeReceiverId] nvarchar(50) NULL,
        [AcknowledgeRequested] bit NOT NULL,
        [TestProdIndicator] nvarchar(1) NULL,
        [SenderCode] nvarchar(50) NULL,
        [ReceiverCode] nvarchar(50) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetime2 NOT NULL DEFAULT ((sysutcdatetime())),
        [ModifiedAt] datetime2 NOT NULL DEFAULT ((sysutcdatetime())),
        CONSTRAINT [PK_ReceiverLibrary_Id] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE TABLE [Insured] (
        [InsGUID] uniqueidentifier NOT NULL DEFAULT ((newid())),
        [InsFirstName] nvarchar(50) NULL,
        [InsLastName] nvarchar(50) NULL,
        [InsMI] nvarchar(5) NULL,
        [InsDateTimeCreated] datetime2 NOT NULL,
        [InsDateTimeModified] datetime2 NOT NULL,
        [InsCreatedUserGUID] uniqueidentifier NULL,
        [InsLastUserGUID] uniqueidentifier NULL,
        [InsCreatedUserName] varchar(50) NULL,
        [InsLastUserName] varchar(50) NULL,
        [InsCreatedComputerName] varchar(100) NULL,
        [InsLastComputerName] varchar(100) NULL,
        [InsAcceptAssignment] smallint NOT NULL,
        [InsAdditionalRefID] varchar(30) NULL,
        [InsAddress] varchar(50) NULL,
        [InsBirthDate] date NULL,
        [InsCity] varchar(50) NULL,
        [InsClaimFilingIndicator] varchar(5) NULL,
        [InsEmployer] varchar(50) NULL,
        [InsGroupNumber] varchar(50) NULL,
        [InsIDNumber] varchar(50) NULL,
        [InsPayID] int NOT NULL,
        [InsPlanName] varchar(50) NULL,
        [InsPhone] varchar(25) NULL,
        [InsSex] varchar(1) NULL,
        [InsSSN] varchar(15) NULL,
        [InsState] varchar(10) NULL,
        [InsZip] varchar(20) NULL,
        [InsCityStateZipCC] AS ((isnull([InsCity],'')+', ')+isnull([InsState],'')) PERSISTED,
        CONSTRAINT [PK__Insured__B098F0EA21F654F8] PRIMARY KEY ([InsGUID]),
        CONSTRAINT [FK_Insured_Payer] FOREIGN KEY ([InsPayID]) REFERENCES [Payer] ([PayID])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE TABLE [Patient] (
        [PatID] int NOT NULL IDENTITY,
        [PatFirstName] nvarchar(50) NULL,
        [PatLastName] nvarchar(50) NULL,
        [PatMI] nvarchar(5) NULL,
        [PatDateTimeCreated] datetime2 NOT NULL,
        [PatDateTimeModified] datetime2 NOT NULL,
        [PatCreatedUserGUID] uniqueidentifier NULL,
        [PatLastUserGUID] uniqueidentifier NULL,
        [PatCreatedUserName] varchar(50) NULL,
        [PatLastUserName] varchar(50) NULL,
        [PatCreatedComputerName] varchar(100) NULL,
        [PatLastComputerName] varchar(100) NULL,
        [PatAccountNo] varchar(50) NULL,
        [PatActive] bit NOT NULL,
        [PatAddress] varchar(50) NULL,
        [PatAddress2] varchar(50) NULL,
        [PatAptReminderPref] varchar(20) NOT NULL,
        [PatAuthTracking] bit NULL,
        [PatBillingPhyFID] int NOT NULL,
        [PatBirthDate] date NULL,
        [PatBox8Reserved] varchar(60) NULL,
        [PatBox9bReserved] varchar(30) NULL,
        [PatBox9cReserved] varchar(30) NULL,
        [PatCellPhoneNo] varchar(25) NULL,
        [PatCellSMTPHost] varchar(100) NULL,
        [PatCity] varchar(50) NULL,
        [PatClaLibFID] int NOT NULL,
        [PatClaimDefaults] xml NULL,
        [PatClassification] varchar(100) NULL,
        [PatCoPayAmount] money NULL,
        [PatCoPayPercent] real NULL,
        [PatCustomField1] varchar(255) NULL,
        [PatCustomField2] varchar(255) NULL,
        [PatCustomField3] varchar(255) NULL,
        [PatCustomField4] varchar(255) NULL,
        [PatCustomField5] varchar(255) NULL,
        [PatDiagnosis1] varchar(30) NULL,
        [PatDiagnosis2] varchar(30) NULL,
        [PatDiagnosis3] varchar(30) NULL,
        [PatDiagnosis4] varchar(30) NULL,
        [PatDiagnosis5] varchar(30) NULL,
        [PatDiagnosis6] varchar(30) NULL,
        [PatDiagnosis7] varchar(30) NULL,
        [PatDiagnosis8] varchar(30) NULL,
        [PatDiagnosis9] varchar(30) NULL,
        [PatDiagnosis10] varchar(30) NULL,
        [PatDiagnosis11] varchar(30) NULL,
        [PatDiagnosis12] varchar(30) NULL,
        [PatDontSendPromotions] bit NOT NULL,
        [PatDontSendStatements] bit NOT NULL,
        [PatEmergencyContactName] varchar(100) NULL,
        [PatEmergencyContactPhoneNo] varchar(25) NULL,
        [PatEmergencyContactRelation] varchar(50) NULL,
        [PatEmployed] int NULL,
        [PatExternalFID] varchar(50) NULL,
        [PatPaymentMatchingKey] nvarchar(max) NULL,
        [PatFacilityPhyFID] int NOT NULL,
        [PatFaxNo] varchar(25) NULL,
        [PatFirstDateTRIG] date NULL,
        [PatHeight] varchar(10) NULL,
        [PatHomePhoneNo] varchar(25) NULL,
        [PatInsuredSigOnFile] bit NOT NULL,
        [PatLastAppointmentKeptTRIG] datetime2 NULL,
        [PatLastAppointmentNotKeptTRIG] datetime2 NULL,
        [PatLastServiceDateTRIG] date NULL,
        [PatLastCellSMPTHostUpdate] datetime2 NULL,
        [PatLastStatementDateTRIG] datetime2 NULL,
        [PatLastPatPmtDateTRIG] date NULL,
        [PatLocked] bit NOT NULL,
        [PatMarried] smallint NULL,
        [PatMemberID] varchar(30) NULL,
        [PatOrderingPhyFID] int NOT NULL,
        [PatPhoneNo] varchar(25) NULL,
        [PatPhyPrintDate] bit NULL,
        [PatPriEmail] varchar(255) NULL,
        [PatPrintSigDate] bit NULL,
        [PatReferringPhyFID] int NOT NULL,
        [PatRecallDate] date NULL,
        [PatReminderNote] varchar(max) NULL,
        [PatReminderNoteEvent] varchar(50) NOT NULL,
        [PatRenderingPhyFID] int NOT NULL,
        [PatResourceWants] xml NULL,
        [PatSecEmail] varchar(255) NULL,
        [PatSex] varchar(1) NULL,
        [PatSigOnFile] bit NOT NULL,
        [PatSigSource] varchar(1) NULL,
        [PatSigText] varchar(50) NULL,
        [PatSSN] varchar(15) NULL,
        [PatState] varchar(10) NULL,
        [PatStatementAddressLine1] varchar(50) NULL,
        [PatStatementAddressLine2] varchar(50) NULL,
        [PatStatementCity] varchar(29) NULL,
        [PatStatementName] varchar(45) NULL,
        [PatStatementMessage] varchar(255) NULL,
        [PatStatementState] varchar(10) NULL,
        [PatStatementZipCode] varchar(10) NULL,
        [PatSupervisingPhyFID] int NOT NULL,
        [PatTotalInsBalanceTRIG] money NOT NULL,
        [PatTotalPatBalanceTRIG] money NOT NULL,
        [PatTotalUndisbursedPaymentsTRIG] money NOT NULL,
        [PatWeight] varchar(10) NULL,
        [PatWorkPhoneNo] varchar(25) NULL,
        [PatZip] varchar(20) NULL,
        [PatLastPaymentRequestTRIG] datetime2 NULL,
        [PatFirstNameTruncatedCC] AS (left(isnull([PatFirstName],''),case when charindex(' ',isnull([PatFirstName]+' ',' '))=(0) then len(isnull([PatFirstName],'')) else charindex(' ',isnull([PatFirstName]+' ',' '))-(1) end)) PERSISTED,
        [PatLastNameTruncatedCC] AS (left(isnull([PatLastName],''),case when charindex(' ',isnull([PatLastName]+' ',' '))=(0) then len(isnull([PatLastName],'')) else charindex(' ',isnull([PatLastName]+' ',' '))-(1) end)) PERSISTED,
        [PatFullNameCC] AS ((((isnull([PatLastName],'')+', ')+isnull([PatFirstName],''))+' ')+isnull([PatMI],'')) PERSISTED,
        [PatFullNameFMLCC] AS ((((isnull([PatFirstName],'')+' ')+isnull([PatMI],''))+' ')+isnull([PatLastName],'')) PERSISTED,
        [PatDiagnosisCodesCC] AS ((((((((((((((((((((((isnull([PatDiagnosis1],'')+'|')+isnull([PatDiagnosis2],''))+'|')+isnull([PatDiagnosis3],''))+'|')+isnull([PatDiagnosis4],''))+'|')+isnull([PatDiagnosis5],''))+'|')+isnull([PatDiagnosis6],''))+'|')+isnull([PatDiagnosis7],''))+'|')+isnull([PatDiagnosis8],''))+'|')+isnull([PatDiagnosis9],''))+'|')+isnull([PatDiagnosis10],''))+'|')+isnull([PatDiagnosis11],''))+'|')+isnull([PatDiagnosis12],'')) PERSISTED,
        [PatTotalBalanceCC] AS (isnull([PatTotalInsBalanceTRIG],(0))+isnull([PatTotalPatBalanceTRIG],(0))) PERSISTED,
        [PatTotalBalanceIncludingUndisbursedPatPmtsCC] AS ((isnull([PatTotalInsBalanceTRIG],(0))+isnull([PatTotalPatBalanceTRIG],(0)))-isnull([PatTotalUndisbursedPaymentsTRIG],(0))) PERSISTED,
        [PatTotalPatBalanceIncludingUndisbursedPatPmtsCC] AS (isnull([PatTotalPatBalanceTRIG],(0))-isnull([PatTotalUndisbursedPaymentsTRIG],(0))) PERSISTED,
        [PatCityStateZipCC] AS ((isnull([PatCity],'')+', ')+isnull([PatState],'')) PERSISTED,
        [PatStatementCityStateZipCC] AS ((isnull([PatStatementCity],'')+', ')+isnull([PatStatementState],'')) PERSISTED,
        CONSTRAINT [PK__Patient__F0DDA98550DB60C7] PRIMARY KEY ([PatID]),
        CONSTRAINT [FK_Patient_BillingPhysician] FOREIGN KEY ([PatBillingPhyFID]) REFERENCES [Physician] ([PhyID]),
        CONSTRAINT [FK_Patient_FacilityPhysician] FOREIGN KEY ([PatFacilityPhyFID]) REFERENCES [Physician] ([PhyID]),
        CONSTRAINT [FK_Patient_OrderingPhysician] FOREIGN KEY ([PatOrderingPhyFID]) REFERENCES [Physician] ([PhyID]),
        CONSTRAINT [FK_Patient_ReferringPhysician] FOREIGN KEY ([PatReferringPhyFID]) REFERENCES [Physician] ([PhyID]),
        CONSTRAINT [FK_Patient_RenderingPhysician] FOREIGN KEY ([PatRenderingPhyFID]) REFERENCES [Physician] ([PhyID]),
        CONSTRAINT [FK_Patient_SupervisingPhysician] FOREIGN KEY ([PatSupervisingPhyFID]) REFERENCES [Physician] ([PhyID])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE TABLE [Procedure_Code] (
        [ProcID] int NOT NULL IDENTITY,
        [ProcDateTimeCreated] datetime2 NOT NULL,
        [ProcDateTimeModified] datetime2 NOT NULL,
        [ProcCreatedUserGUID] uniqueidentifier NULL,
        [ProcLastUserGUID] uniqueidentifier NULL,
        [ProcCreatedUserName] varchar(50) NULL,
        [ProcLastUserName] varchar(50) NULL,
        [ProcCreatedComputerName] varchar(100) NULL,
        [ProcLastComputerName] varchar(100) NULL,
        [ProcAdjust] money NOT NULL,
        [ProcAllowed] money NOT NULL,
        [ProcBillingPhyFID] int NOT NULL,
        [ProcCategory] varchar(50) NULL,
        [ProcCharge] money NOT NULL,
        [ProcCost] money NOT NULL,
        [ProcCMNReq] bit NOT NULL,
        [ProcCode] varchar(50) NOT NULL,
        [ProcCoPayReq] bit NOT NULL,
        [ProcDescription] varchar(255) NULL,
        [ProcDescriptionReq] bit NOT NULL,
        [ProcDrugUnitCount] real NOT NULL,
        [ProcDrugUnitMeasurement] varchar(2) NULL,
        [ProcModifier1] varchar(2) NULL,
        [ProcModifier2] varchar(2) NULL,
        [ProcModifier3] varchar(2) NULL,
        [ProcModifier4] varchar(2) NULL,
        [ProcNote] varchar(255) NULL,
        [ProcNDCCode] varchar(48) NULL,
        [ProcPayFID] int NOT NULL,
        [ProcProductCode] varchar(50) NULL,
        [ProcRateClass] varchar(50) NULL,
        [ProcRevenueCode] varchar(4) NULL,
        [ProcRVUMalpractice] real NOT NULL,
        [ProcRVUWork] real NOT NULL,
        [ProcSubCategory] varchar(50) NULL,
        [ProcUnits] real NOT NULL,
        [ProcStart] date NULL,
        [ProcEnd] date NULL,
        [ProcModifiersCC] AS (((isnull([ProcModifier1],'')+isnull([ProcModifier2],''))+isnull([ProcModifier3],''))+isnull([ProcModifier4],'')) PERSISTED,
        CONSTRAINT [PK__Procedur__07C21CFE69FC361F] PRIMARY KEY ([ProcID]),
        CONSTRAINT [FK_ProcedureCode_BillingPhysician] FOREIGN KEY ([ProcBillingPhyFID]) REFERENCES [Physician] ([PhyID]),
        CONSTRAINT [FK_ProcedureCode_Payer] FOREIGN KEY ([ProcPayFID]) REFERENCES [Payer] ([PayID])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE TABLE [Claim] (
        [ClaID] int NOT NULL IDENTITY,
        [ClaDateTimeCreated] datetime2 NOT NULL,
        [ClaDateTimeModified] datetime2 NOT NULL,
        [ClaCreatedUserGUID] uniqueidentifier NULL,
        [ClaLastUserGUID] uniqueidentifier NULL,
        [ClaCreatedUserName] varchar(50) NULL,
        [ClaLastUserName] varchar(50) NULL,
        [ClaCreatedComputerName] varchar(100) NULL,
        [ClaLastComputerName] varchar(100) NULL,
        [ClaAccidentDate] date NULL,
        [ClaAcuteManifestationDate] date NULL,
        [ClaAdmissionHour] varchar(4) NULL,
        [ClaAdmissionSource] varchar(1) NULL,
        [ClaAdmissionType] varchar(1) NULL,
        [ClaAdmittedDate] date NULL,
        [ClaAdmittingDiagnosis] varchar(30) NULL,
        [ClaArchived] bit NULL,
        [ClaAssumedCareDate] date NULL,
        [ClaAttendingPhyFID] int NOT NULL,
        [ClaAuthorizedReturnToWorkDate] date NULL,
        [ClaBillDate] date NULL,
        [ClaBillingPhyFID] int NOT NULL,
        [ClaBillTo] int NULL,
        [ClaBox10dClaimCodes] varchar(20) NULL,
        [ClaBox11bOtherClaimIDQualifier] varchar(2) NULL,
        [ClaBox22CodeOverride] varchar(50) NULL,
        [ClaBox33bOverride] varchar(32) NULL,
        [ClaClassification] varchar(30) NULL,
        [ClaCLIANumber] varchar(15) NULL,
        [ClaCMNCertOnFile] varchar(1) NULL,
        [ClaCMNCertTypeCode] varchar(1) NULL,
        [ClaCMNFormIdentificationCode] varchar(20) NULL,
        [ClaCMNInitialDate] date NULL,
        [ClaCMNLengthOfNeed] int NULL,
        [ClaCMNRevisedDate] date NULL,
        [ClaCMNSignedDate] date NULL,
        [ClaCN1Segment] varchar(100) NULL,
        [ClaConditionCode1] varchar(5) NULL,
        [ClaConditionCode2] varchar(5) NULL,
        [ClaConditionCode3] varchar(5) NULL,
        [ClaConditionCode4] varchar(5) NULL,
        [ClaAdditionalData] xml NULL,
        [ClaCustomField1] varchar(255) NULL,
        [ClaCustomField2] varchar(255) NULL,
        [ClaCustomField3] varchar(255) NULL,
        [ClaCustomField4] varchar(255) NULL,
        [ClaCustomField5] varchar(255) NULL,
        [ClaDateLastSeen] date NULL,
        [ClaDateOfCurrent] date NULL,
        [ClaDateTotalFrom] date NULL,
        [ClaDateTotalThrough] date NULL,
        [ClaDelayCode] varchar(2) NULL,
        [ClaDiagnosis1] varchar(30) NULL,
        [ClaDiagnosis2] varchar(30) NULL,
        [ClaDiagnosis3] varchar(30) NULL,
        [ClaDiagnosis4] varchar(30) NULL,
        [ClaDiagnosis5] varchar(30) NULL,
        [ClaDiagnosis6] varchar(30) NULL,
        [ClaDiagnosis7] varchar(30) NULL,
        [ClaDiagnosis8] varchar(30) NULL,
        [ClaDiagnosis9] varchar(30) NULL,
        [ClaDiagnosis10] varchar(30) NULL,
        [ClaDiagnosis11] varchar(30) NULL,
        [ClaDiagnosis12] varchar(30) NULL,
        [ClaDiagnosis13] varchar(30) NULL,
        [ClaDiagnosis14] varchar(30) NULL,
        [ClaDiagnosis15] varchar(30) NULL,
        [ClaDiagnosis16] varchar(30) NULL,
        [ClaDiagnosis17] varchar(30) NULL,
        [ClaDiagnosis18] varchar(30) NULL,
        [ClaDiagnosis19] varchar(30) NULL,
        [ClaDiagnosis20] varchar(30) NULL,
        [ClaDiagnosis21] varchar(30) NULL,
        [ClaDiagnosis22] varchar(30) NULL,
        [ClaDiagnosis23] varchar(30) NULL,
        [ClaDiagnosis24] varchar(30) NULL,
        [ClaDiagnosis25] varchar(30) NULL,
        [ClaDiagnosisCodesCC] AS ((((((((((((((((((((((((((((((((((((((((((((((((isnull([ClaDiagnosis1],'')+'|')+isnull([ClaDiagnosis2],''))+'|')+isnull([ClaDiagnosis3],''))+'|')+isnull([ClaDiagnosis4],''))+'|')+isnull([ClaDiagnosis5],''))+'|')+isnull([ClaDiagnosis6],''))+'|')+isnull([ClaDiagnosis7],''))+'|')+isnull([ClaDiagnosis8],''))+'|')+isnull([ClaDiagnosis9],''))+'|')+isnull([ClaDiagnosis10],''))+'|')+isnull([ClaDiagnosis11],''))+'|')+isnull([ClaDiagnosis12],''))+'|')+isnull([ClaDiagnosis13],''))+'|')+isnull([ClaDiagnosis14],''))+'|')+isnull([ClaDiagnosis15],''))+'|')+isnull([ClaDiagnosis16],''))+'|')+isnull([ClaDiagnosis17],''))+'|')+isnull([ClaDiagnosis18],''))+'|')+isnull([ClaDiagnosis19],''))+'|')+isnull([ClaDiagnosis20],''))+'|')+isnull([ClaDiagnosis21],''))+'|')+isnull([ClaDiagnosis22],''))+'|')+isnull([ClaDiagnosis23],''))+'|')+isnull([ClaDiagnosis24],''))+'|')+isnull([ClaDiagnosis25],'')) PERSISTED,
        [ClaDisabilityBeginDate] date NULL,
        [ClaDisabilityEndDate] date NULL,
        [ClaDischargedDate] date NULL,
        [ClaDischargedHour] datetime2 NULL,
        [ClaDMEFormData] varchar(max) NULL,
        [ClaEDINotes] varchar(80) NULL,
        [ClaEPSDTReferral] varchar(2) NULL,
        [ClaExternalFID] varchar(50) NULL,
        [ClaFacilityPhyFID] int NOT NULL,
        [ClaFirstDateTRIG] date NULL,
        [ClaFirstDateOfInjury] date NULL,
        [ClaFirstInsPaymentDateTRIG] date NULL,
        [ClaHearingAndPrescriptionDate] date NULL,
        [ClaHomeboundInd] varchar(1) NULL,
        [ClaHospiceInd] varchar(1) NULL,
        [ClaICDIndicator] varchar(2) NOT NULL,
        [ClaIDENumber] varchar(20) NULL,
        [ClaInitialTreatmentDate] date NULL,
        [ClaIgnoreAppliedAmount] smallint NOT NULL,
        [ClaInsuranceTypeCodeOverride] varchar(10) NULL,
        [ClaInvoiceNumber] varchar(38) NULL,
        [ClaK3FileInformation] varchar(80) NULL,
        [ClaLabCharges] varchar(20) NULL,
        [ClaLastDateTRIG] date NULL,
        [ClaLastExportedDate] date NULL,
        [ClaLastMenstrualDate] date NULL,
        [ClaLastPrintedDate] date NULL,
        [ClaLastWorkedDate] date NULL,
        [ClaLastXRayDate] date NULL,
        [ClaLocked] bit NOT NULL,
        [ClaMammographyCert] varchar(10) NULL,
        [ClaMedicalRecordNumber] varchar(50) NULL,
        [ClaMedicaidResubmissionCode] varchar(50) NULL,
        [ClaMOASegment] varchar(30) NULL,
        [ClaOperatingPhyFID] int NOT NULL,
        [ClaOrderingPhyFID] int NOT NULL,
        [ClaOriginalRefNo] varchar(50) NULL,
        [ClaOutsideLab] int NULL,
        [ClaPaidDateTRIG] date NULL,
        [ClaPaperWorkControlNumber] varchar(80) NULL,
        [ClaPaperWorkInd] varchar(20) NULL,
        [ClaPaperWorkTransmissionCode] varchar(2) NULL,
        [ClaPatFID] int NOT NULL,
        [ClaPatientReasonDiagnosis1] varchar(30) NULL,
        [ClaPatientReasonDiagnosis2] varchar(30) NULL,
        [ClaPatientReasonDiagnosis3] varchar(30) NULL,
        [ClaPatientStatus] varchar(2) NULL,
        [ClaPOAIndicator] varchar(25) NULL,
        [ClaPPSCode] varchar(30) NULL,
        [ClaPricingExceptionCode] varchar(2) NULL,
        [ClaPrincipalProcedureCode] varchar(30) NULL,
        [ClaPrincipalProcedureDate] date NULL,
        [ClaPrintUnitCharge] bit NOT NULL,
        [ClaProviderAgreementCode] varchar(1) NULL,
        [ClaRecurUntilDate] date NULL,
        [ClaRecurringTimeFrame] varchar(50) NULL,
        [ClaReferralNumber] varchar(50) NULL,
        [ClaReferringPhyFID] int NOT NULL,
        [ClaRelatedTo] smallint NULL,
        [ClaRelatedToState] varchar(4) NULL,
        [ClaRelinquishedCareDate] date NULL,
        [ClaRemarks] varchar(80) NULL,
        [ClaRenderingPhyFID] int NOT NULL,
        [ClaReserved10] varchar(25) NULL,
        [ClaReserved19] varchar(75) NULL,
        [ClaSimilarIllnessDate] date NULL,
        [ClaSpecialProgramIndicator] varchar(5) NULL,
        [ClaStatementCoversFromOverride] date NULL,
        [ClaStatementCoversThroughOverride] date NULL,
        [ClaStatus] varchar(20) NULL,
        [ClaSubmissionMethod] varchar(20) NOT NULL,
        [ClaSupervisingPhyFID] int NOT NULL,
        [ClaTypeOfBill] varchar(4) NULL,
        [ClaTotalCOAdjTRIG] money NULL,
        [ClaTotalCRAdjTRIG] money NULL,
        [ClaTotalOAAdjTRIG] money NULL,
        [ClaTotalPIAdjTRIG] money NULL,
        [ClaTotalPRAdjTRIG] money NULL,
        [ClaTotalAdjCC] AS ((((isnull([ClaTotalCOAdjTRIG],(0))+isnull([ClaTotalCRAdjTRIG],(0)))+isnull([ClaTotalOAAdjTRIG],(0)))+isnull([ClaTotalPIAdjTRIG],(0)))+isnull([ClaTotalPRAdjTRIG],(0))) PERSISTED,
        [ClaTotalServiceLineCountTRIG] int NOT NULL,
        [ClaTotalChargeTRIG] money NOT NULL,
        [ClaTotalInsAmtPaidTRIG] money NOT NULL,
        [ClaTotalInsBalanceTRIG] money NOT NULL,
        [ClaTotalPatAmtPaidTRIG] money NOT NULL,
        [ClaTotalPatBalanceTRIG] money NOT NULL,
        [ClaTotalAmtAppliedCC] AS ((((((isnull([ClaTotalPatAmtPaidTRIG],(0))+isnull([ClaTotalInsAmtPaidTRIG],(0)))+isnull([ClaTotalCOAdjTRIG],(0)))+isnull([ClaTotalCRAdjTRIG],(0)))+isnull([ClaTotalOAAdjTRIG],(0)))+isnull([ClaTotalPIAdjTRIG],(0)))+isnull([ClaTotalPRAdjTRIG],(0))) PERSISTED,
        [ClaTotalAmtPaidCC] AS (isnull([ClaTotalPatAmtPaidTRIG],(0))+isnull([ClaTotalInsAmtPaidTRIG],(0))) PERSISTED,
        [ClaTotalBalanceCC] AS ((isnull([ClaTotalChargeTRIG],(0))-(isnull([ClaTotalPatAmtPaidTRIG],(0))+isnull([ClaTotalInsAmtPaidTRIG],(0))))-((((isnull([ClaTotalCOAdjTRIG],(0))+isnull([ClaTotalCRAdjTRIG],(0)))+isnull([ClaTotalOAAdjTRIG],(0)))+isnull([ClaTotalPIAdjTRIG],(0)))+isnull([ClaTotalPRAdjTRIG],(0)))) PERSISTED,
        CONSTRAINT [PK__Claim__E8193A9B6764AAF8] PRIMARY KEY ([ClaID]),
        CONSTRAINT [FK_Claim_AttendingPhy] FOREIGN KEY ([ClaAttendingPhyFID]) REFERENCES [Physician] ([PhyID]),
        CONSTRAINT [FK_Claim_BillingPhy] FOREIGN KEY ([ClaBillingPhyFID]) REFERENCES [Physician] ([PhyID]),
        CONSTRAINT [FK_Claim_FacilityPhy] FOREIGN KEY ([ClaFacilityPhyFID]) REFERENCES [Physician] ([PhyID]),
        CONSTRAINT [FK_Claim_OperatingPhy] FOREIGN KEY ([ClaOperatingPhyFID]) REFERENCES [Physician] ([PhyID]),
        CONSTRAINT [FK_Claim_OrderingPhy] FOREIGN KEY ([ClaOrderingPhyFID]) REFERENCES [Physician] ([PhyID]),
        CONSTRAINT [FK_Claim_Patient] FOREIGN KEY ([ClaPatFID]) REFERENCES [Patient] ([PatID]),
        CONSTRAINT [FK_Claim_ReferringPhy] FOREIGN KEY ([ClaReferringPhyFID]) REFERENCES [Physician] ([PhyID]),
        CONSTRAINT [FK_Claim_RenderingPhy] FOREIGN KEY ([ClaRenderingPhyFID]) REFERENCES [Physician] ([PhyID]),
        CONSTRAINT [FK_Claim_SupervisingPhy] FOREIGN KEY ([ClaSupervisingPhyFID]) REFERENCES [Physician] ([PhyID])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE TABLE [Patient_Insured] (
        [PatInsGUID] uniqueidentifier NOT NULL DEFAULT ((newid())),
        [PatInsDateTimeCreated] datetime2 NOT NULL,
        [PatInsDateTimeModified] datetime2 NOT NULL,
        [PatInsCreatedUserGUID] uniqueidentifier NULL,
        [PatInsLastUserGUID] uniqueidentifier NULL,
        [PatInsCreatedUserName] varchar(50) NULL,
        [PatInsLastUserName] varchar(50) NULL,
        [PatInsCreatedComputerName] varchar(100) NULL,
        [PatInsLastComputerName] varchar(100) NULL,
        [PatInsEligANSI] varchar(max) NULL,
        [PatInsEligDate] date NULL,
        [PatInsEligStatus] varchar(15) NULL,
        [PatInsInsGUID] uniqueidentifier NOT NULL,
        [PatInsPatFID] int NOT NULL,
        [PatInsRelationToInsured] int NOT NULL,
        [PatInsSequence] int NOT NULL,
        [PatInsSequenceDescriptionCC] AS (case when [PatInsSequence]=(1) then 'Primary' when [PatInsSequence]=(2) then 'Secondary' else '' end) PERSISTED,
        [PatInsEligStatusDisplayTextCC] AS (case when [PatInsEligStatus]='A' then 'Active' else [PatInsEligStatus] end) PERSISTED,
        CONSTRAINT [PK__Patient___CA04E544A8F0AA6F] PRIMARY KEY ([PatInsGUID]),
        CONSTRAINT [FK_PatientInsured_Insured] FOREIGN KEY ([PatInsInsGUID]) REFERENCES [Insured] ([InsGUID]),
        CONSTRAINT [FK_PatientInsured_Patient] FOREIGN KEY ([PatInsPatFID]) REFERENCES [Patient] ([PatID])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE TABLE [Payment] (
        [PmtID] int NOT NULL IDENTITY,
        [PmtDateTimeCreated] datetime2 NOT NULL,
        [PmtDateTimeModified] datetime2 NOT NULL,
        [PmtCreatedUserGUID] uniqueidentifier NULL,
        [PmtLastUserGUID] uniqueidentifier NULL,
        [PmtCreatedUserName] varchar(50) NULL,
        [PmtLastUserName] varchar(50) NULL,
        [PmtCreatedComputerName] varchar(100) NULL,
        [PmtLastComputerName] varchar(100) NULL,
        [Pmt835Ref] varchar(50) NULL,
        [PmtAmount] money NOT NULL,
        [PmtBatchOperationReference] varchar(100) NULL,
        [PmtBFEPFID] int NOT NULL,
        [PmtDate] date NOT NULL,
        [PmtDisbursedTRIG] money NOT NULL,
        [PmtMethod] varchar(20) NULL,
        [PmtOtherReference1] varchar(80) NULL,
        [PmtOtherReference2] varchar(80) NULL,
        [PmtPatFID] int NOT NULL,
        [PmtPayFID] int NULL,
        [PmtAuthCode] varchar(25) NULL,
        [PmtCardEntryContext] tinyint NULL,
        [PmtCardEntryMethod] tinyint NULL,
        [PmtNameOnCard] varchar(100) NULL,
        [PmtIssuerResponseCode] varchar(2) NULL,
        [PmtResponseCode] varchar(2) NULL,
        [PmtChargedPlatformFee] money NOT NULL,
        [PmtTransactionType] tinyint NULL,
        [PmtNote] varchar(255) NULL,
        [PmtRemainingCC] AS (isnull([PmtAmount],(0))-isnull([PmtDisbursedTRIG],(0))) PERSISTED,
        CONSTRAINT [PK__Payment__1587830DB215BEEC] PRIMARY KEY ([PmtID]),
        CONSTRAINT [FK_Payment_BFEPhysician] FOREIGN KEY ([PmtBFEPFID]) REFERENCES [Physician] ([PhyID]),
        CONSTRAINT [FK_Payment_Patient] FOREIGN KEY ([PmtPatFID]) REFERENCES [Patient] ([PatID]),
        CONSTRAINT [FK_Payment_Payer] FOREIGN KEY ([PmtPayFID]) REFERENCES [Payer] ([PayID])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE TABLE [Claim_Insured] (
        [ClaInsGUID] uniqueidentifier NOT NULL DEFAULT ((newid())),
        [ClaInsFirstName] varchar(50) NULL,
        [ClaInsLastName] varchar(50) NULL,
        [ClaInsMI] varchar(5) NULL,
        [ClaInsDateTimeCreated] datetime2 NOT NULL,
        [ClaInsDateTimeModified] datetime2 NOT NULL,
        [ClaInsCreatedUserGUID] uniqueidentifier NULL,
        [ClaInsLastUserGUID] uniqueidentifier NULL,
        [ClaInsCreatedUserName] varchar(50) NULL,
        [ClaInsLastUserName] varchar(50) NULL,
        [ClaInsCreatedComputerName] varchar(100) NULL,
        [ClaInsLastComputerName] varchar(100) NULL,
        [ClaInsAcceptAssignment] smallint NULL,
        [ClaInsAdditionalRefID] varchar(30) NULL,
        [ClaInsAddress] varchar(50) NULL,
        [ClaInsBirthDate] date NULL,
        [ClaInsCity] varchar(50) NULL,
        [ClaInsClaFID] int NOT NULL,
        [ClaInsClaimFilingIndicator] varchar(5) NULL,
        [ClaInsEmployer] varchar(50) NULL,
        [ClaInsGroupNumber] varchar(50) NULL,
        [ClaInsIDNumber] varchar(50) NULL,
        [ClaInsPayFID] int NOT NULL,
        [ClaInsPatFID] int NOT NULL,
        [ClaInsPhone] varchar(25) NULL,
        [ClaInsPlanName] varchar(50) NULL,
        [ClaInsPriorAuthorizationNumber] varchar(50) NULL,
        [ClaInsRelationToInsured] int NOT NULL,
        [ClaInsSequence] int NULL,
        [ClaInsSex] varchar(1) NULL,
        [ClaInsSSN] varchar(15) NULL,
        [ClaInsState] varchar(10) NULL,
        [ClaInsZip] varchar(20) NULL,
        [ClaInsSequenceDescriptionCC] AS (case when [ClaInsSequence]=(1) then 'Primary' when [ClaInsSequence]=(2) then 'Secondary' else '' end) PERSISTED,
        [ClaInsCityStateZipCC] AS ((isnull([ClaInsCity],'')+', ')+isnull([ClaInsState],'')) PERSISTED,
        CONSTRAINT [PK__Claim_In__BE610E8C4D3B9F38] PRIMARY KEY ([ClaInsGUID]),
        CONSTRAINT [FK_ClaimInsured_Claim] FOREIGN KEY ([ClaInsClaFID]) REFERENCES [Claim] ([ClaID]),
        CONSTRAINT [FK_ClaimInsured_Patient] FOREIGN KEY ([ClaInsPatFID]) REFERENCES [Patient] ([PatID]),
        CONSTRAINT [FK_ClaimInsured_Payer] FOREIGN KEY ([ClaInsPayFID]) REFERENCES [Payer] ([PayID])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE TABLE [Service_Line] (
        [SrvID] int NOT NULL IDENTITY,
        [SrvDateTimeCreated] datetime2 NOT NULL,
        [SrvDateTimeModified] datetime2 NOT NULL,
        [SrvCreatedUserGUID] uniqueidentifier NULL,
        [SrvLastUserGUID] uniqueidentifier NULL,
        [SrvCreatedUserName] varchar(50) NULL,
        [SrvLastUserName] varchar(50) NULL,
        [SrvCreatedComputerName] varchar(100) NULL,
        [SrvLastComputerName] varchar(100) NULL,
        [SrvAllowedAmt] money NOT NULL,
        [SrvApprovedAmt] money NOT NULL,
        [SrvAttachCMN] bit NOT NULL,
        [SrvAuthorizationOverride] varchar(50) NULL,
        [SrvCharges] money NOT NULL,
        [SrvClaFID] int NULL,
        [SrvCoPayAmountDue] money NOT NULL,
        [SrvCost] money NOT NULL,
        [SrvCustomField1] varchar(255) NULL,
        [SrvCustomField2] varchar(255) NULL,
        [SrvCustomField3] varchar(255) NULL,
        [SrvCustomField4] varchar(255) NULL,
        [SrvCustomField5] varchar(255) NULL,
        [SrvDesc] varchar(255) NULL,
        [SrvDiagnosisPointer] varchar(10) NULL,
        [SrvDrugUnitCount] float NULL,
        [SrvDrugUnitMeasurement] varchar(2) NULL,
        [SrvDrugUnitPrice] money NULL,
        [SrvEMG] varchar(5) NULL,
        [SrvEndTime] datetime2 NULL,
        [SrvEPSDT] varchar(5) NULL,
        [SrvExpectedPriPmt] money NOT NULL,
        [SrvFirstInsPaymentDateTRIG] date NULL,
        [SrvFromDate] date NOT NULL,
        [SrvGUID] uniqueidentifier NOT NULL DEFAULT ((newid())),
        [SrvK3FileInformation] varchar(80) NULL,
        [SrvModifier1] varchar(2) NULL,
        [SrvModifier2] varchar(2) NULL,
        [SrvModifier3] varchar(2) NULL,
        [SrvModifier4] varchar(2) NULL,
        [SrvNationalDrugCode] varchar(48) NULL,
        [SrvNonCoveredCharges] money NOT NULL,
        [SrvPatBalanceReasonCode] varchar(20) NULL,
        [SrvPlace] varchar(5) NULL,
        [SrvPrescriptionNumber] varchar(30) NULL,
        [SrvPrintLineItem] bit NOT NULL,
        [SrvProcedureCode] varchar(48) NULL,
        [SrvProductCode] varchar(50) NULL,
        [SrvRespChangeDate] datetime2 NOT NULL,
        [SrvResponsibleParty] int NOT NULL,
        [SrvRevenueCode] varchar(4) NULL,
        [SrvSortTiebreaker] int NOT NULL,
        [SrvStartTime] datetime2 NULL,
        [SrvToDate] date NOT NULL,
        [SrvTotalCOAdjTRIG] money NOT NULL,
        [SrvTotalCRAdjTRIG] money NOT NULL,
        [SrvTotalOAAdjTRIG] money NOT NULL,
        [SrvTotalPIAdjTRIG] money NOT NULL,
        [SrvTotalPRAdjTRIG] money NOT NULL,
        [SrvTotalInsAmtPaidTRIG] money NOT NULL,
        [SrvTotalPatAmtPaidTRIG] money NOT NULL,
        [SrvUnits] real NULL,
        [SrvPerUnitChargesCC] AS (case when [SrvUnits]>(0) then [SrvCharges]/[SrvUnits] else (0) end) PERSISTED,
        [SrvModifiersCC] AS (((isnull([SrvModifier1],'')+isnull([SrvModifier2],''))+isnull([SrvModifier3],''))+isnull([SrvModifier4],'')) PERSISTED,
        [SrvRespDaysAgedCC] AS (datediff(day,[SrvRespChangeDate],sysutcdatetime())),
        [SrvTotalAdjCC] AS ((((isnull([SrvTotalCOAdjTRIG],(0))+isnull([SrvTotalCRAdjTRIG],(0)))+isnull([SrvTotalOAAdjTRIG],(0)))+isnull([SrvTotalPIAdjTRIG],(0)))+isnull([SrvTotalPRAdjTRIG],(0))) PERSISTED,
        [SrvTotalOtherAdjCC] AS (((isnull([SrvTotalCRAdjTRIG],(0))+isnull([SrvTotalOAAdjTRIG],(0)))+isnull([SrvTotalPIAdjTRIG],(0)))+isnull([SrvTotalPRAdjTRIG],(0))) PERSISTED,
        [SrvTotalAmtAppliedCC] AS ((((((isnull([SrvTotalPatAmtPaidTRIG],(0))+isnull([SrvTotalInsAmtPaidTRIG],(0)))+isnull([SrvTotalCOAdjTRIG],(0)))+isnull([SrvTotalCRAdjTRIG],(0)))+isnull([SrvTotalOAAdjTRIG],(0)))+isnull([SrvTotalPIAdjTRIG],(0)))+isnull([SrvTotalPRAdjTRIG],(0))) PERSISTED,
        [SrvTotalAmtPaidCC] AS (isnull([SrvTotalPatAmtPaidTRIG],(0))+isnull([SrvTotalInsAmtPaidTRIG],(0))) PERSISTED,
        [SrvTotalBalanceCC] AS ((isnull([SrvCharges],(0))-(isnull([SrvTotalPatAmtPaidTRIG],(0))+isnull([SrvTotalInsAmtPaidTRIG],(0))))-((((isnull([SrvTotalCOAdjTRIG],(0))+isnull([SrvTotalCRAdjTRIG],(0)))+isnull([SrvTotalOAAdjTRIG],(0)))+isnull([SrvTotalPIAdjTRIG],(0)))+isnull([SrvTotalPRAdjTRIG],(0)))) PERSISTED,
        [SrvTotalInsBalanceCC] money NULL,
        [SrvTotalPatBalanceCC] money NULL,
        [SrvTotalMinutesCC] AS (case when [SrvStartTime] IS NOT NULL AND [SrvEndTime] IS NOT NULL then datediff(minute,[SrvStartTime],[SrvEndTime]) else (0) end) PERSISTED,
        [SrvAdditionalData] xml NULL,
        [SrvNOCOverride] varchar(80) NULL,
        CONSTRAINT [PK__Service___0367D191FA398414] PRIMARY KEY ([SrvID]),
        CONSTRAINT [AK_Service_Line_SrvGUID] UNIQUE ([SrvGUID]),
        CONSTRAINT [FK_ServiceLine_Claim] FOREIGN KEY ([SrvClaFID]) REFERENCES [Claim] ([ClaID]),
        CONSTRAINT [FK_ServiceLine_ResponsibleParty] FOREIGN KEY ([SrvResponsibleParty]) REFERENCES [Payer] ([PayID])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE TABLE [Adjustment] (
        [AdjID] int NOT NULL IDENTITY,
        [AdjDateTimeCreated] datetime2 NOT NULL,
        [AdjDateTimeModified] datetime2 NOT NULL,
        [AdjCreatedUserGUID] uniqueidentifier NULL,
        [AdjLastUserGUID] uniqueidentifier NULL,
        [AdjCreatedUserName] varchar(50) NULL,
        [AdjLastUserName] varchar(50) NULL,
        [AdjCreatedComputerName] varchar(100) NULL,
        [AdjLastComputerName] varchar(100) NULL,
        [Adj835Ref] varchar(50) NULL,
        [AdjAmount] money NOT NULL,
        [AdjBatchOperationReference] varchar(100) NULL,
        [AdjDate] date NULL,
        [AdjGroupCode] varchar(2) NOT NULL,
        [AdjNote] varchar(100) NULL,
        [AdjOtherReference1] varchar(80) NULL,
        [AdjPayFID] int NOT NULL,
        [AdjPmtFID] int NOT NULL,
        [AdjReasonAmount] money NOT NULL,
        [AdjReasonCode] varchar(20) NULL,
        [AdjRemarkCode] varchar(50) NULL,
        [AdjSrvFID] int NOT NULL,
        [AdjSrvGUID] uniqueidentifier NOT NULL,
        [AdjTaskFID] int NOT NULL,
        [AdjTrackOnly] bit NOT NULL,
        CONSTRAINT [PK__Adjustme__A065A852DF7CF3F7] PRIMARY KEY ([AdjID]),
        CONSTRAINT [FK_Adjustment_Payer] FOREIGN KEY ([AdjPayFID]) REFERENCES [Payer] ([PayID]),
        CONSTRAINT [FK_Adjustment_Payment] FOREIGN KEY ([AdjPmtFID]) REFERENCES [Payment] ([PmtID]),
        CONSTRAINT [FK_Adjustment_ServiceLine_SrvGUID] FOREIGN KEY ([AdjSrvGUID]) REFERENCES [Service_Line] ([SrvGUID]),
        CONSTRAINT [FK_Adjustment_ServiceLine_SrvID] FOREIGN KEY ([AdjSrvFID]) REFERENCES [Service_Line] ([SrvID]),
        CONSTRAINT [FK_Adjustment_TaskSrv] FOREIGN KEY ([AdjTaskFID]) REFERENCES [Service_Line] ([SrvID])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE TABLE [Disbursement] (
        [DisbID] int NOT NULL IDENTITY,
        [DisbDateTimeCreated] datetime2 NOT NULL,
        [DisbDateTimeModified] datetime2 NOT NULL,
        [DisbCreatedUserGUID] uniqueidentifier NULL,
        [DisbLastUserGUID] uniqueidentifier NULL,
        [DisbCreatedUserName] varchar(50) NULL,
        [DisbLastUserName] varchar(50) NULL,
        [DisbCreatedComputerName] varchar(100) NULL,
        [DisbLastComputerName] varchar(100) NULL,
        [DisbAmount] money NOT NULL,
        [DisbBatchOperationReference] varchar(100) NULL,
        [DisbCode] varchar(20) NULL,
        [DisbNote] varchar(100) NULL,
        [DisbPmtFID] int NOT NULL,
        [DisbSrvFID] int NOT NULL,
        [DisbSrvGUID] uniqueidentifier NOT NULL,
        CONSTRAINT [PK__Disburse__140396A9001F6084] PRIMARY KEY ([DisbID]),
        CONSTRAINT [FK_Disbursement_Payment] FOREIGN KEY ([DisbPmtFID]) REFERENCES [Payment] ([PmtID]),
        CONSTRAINT [FK_Disbursement_ServiceLine] FOREIGN KEY ([DisbSrvFID]) REFERENCES [Service_Line] ([SrvID])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE INDEX [IX_Adjustment_AdjPayFID] ON [Adjustment] ([AdjPayFID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE INDEX [IX_Adjustment_AdjPmtFID] ON [Adjustment] ([AdjPmtFID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE INDEX [IX_Adjustment_AdjSrvFID] ON [Adjustment] ([AdjSrvFID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE INDEX [IX_Adjustment_AdjSrvGUID] ON [Adjustment] ([AdjSrvGUID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE INDEX [IX_Adjustment_AdjTaskFID] ON [Adjustment] ([AdjTaskFID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE INDEX [IX_Claim_ClaAttendingPhyFID] ON [Claim] ([ClaAttendingPhyFID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE INDEX [IX_Claim_ClaBillingPhyFID] ON [Claim] ([ClaBillingPhyFID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE INDEX [IX_Claim_ClaFacilityPhyFID] ON [Claim] ([ClaFacilityPhyFID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE INDEX [IX_Claim_ClaOperatingPhyFID] ON [Claim] ([ClaOperatingPhyFID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE INDEX [IX_Claim_ClaOrderingPhyFID] ON [Claim] ([ClaOrderingPhyFID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE INDEX [IX_Claim_ClaPatFID] ON [Claim] ([ClaPatFID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE INDEX [IX_Claim_ClaReferringPhyFID] ON [Claim] ([ClaReferringPhyFID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE INDEX [IX_Claim_ClaRenderingPhyFID] ON [Claim] ([ClaRenderingPhyFID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE INDEX [IX_Claim_ClaSupervisingPhyFID] ON [Claim] ([ClaSupervisingPhyFID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE INDEX [IX_Claim_Insured_ClaInsClaFID] ON [Claim_Insured] ([ClaInsClaFID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE INDEX [IX_Claim_Insured_ClaInsPatFID] ON [Claim_Insured] ([ClaInsPatFID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE INDEX [IX_Claim_Insured_ClaInsPayFID] ON [Claim_Insured] ([ClaInsPayFID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE INDEX [IX_Disbursement_DisbPmtFID] ON [Disbursement] ([DisbPmtFID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE INDEX [IX_Disbursement_DisbSrvFID] ON [Disbursement] ([DisbSrvFID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE INDEX [IX_Insured_InsPayID] ON [Insured] ([InsPayID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE INDEX [IX_Patient_PatBillingPhyFID] ON [Patient] ([PatBillingPhyFID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE INDEX [IX_Patient_PatFacilityPhyFID] ON [Patient] ([PatFacilityPhyFID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE INDEX [IX_Patient_PatOrderingPhyFID] ON [Patient] ([PatOrderingPhyFID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE INDEX [IX_Patient_PatReferringPhyFID] ON [Patient] ([PatReferringPhyFID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE INDEX [IX_Patient_PatRenderingPhyFID] ON [Patient] ([PatRenderingPhyFID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE INDEX [IX_Patient_PatSupervisingPhyFID] ON [Patient] ([PatSupervisingPhyFID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE INDEX [IX_Patient_Insured_PatInsInsGUID] ON [Patient_Insured] ([PatInsInsGUID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE INDEX [IX_Patient_Insured_PatInsPatFID] ON [Patient_Insured] ([PatInsPatFID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE INDEX [IX_Payment_PmtBFEPFID] ON [Payment] ([PmtBFEPFID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE INDEX [IX_Payment_PmtPatFID] ON [Payment] ([PmtPatFID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE INDEX [IX_Payment_PmtPayFID] ON [Payment] ([PmtPayFID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE INDEX [IX_Procedure_Code_ProcBillingPhyFID] ON [Procedure_Code] ([ProcBillingPhyFID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE INDEX [IX_Procedure_Code_ProcPayFID] ON [Procedure_Code] ([ProcPayFID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ReceiverLibrary_LibraryEntryName] ON [ReceiverLibrary] ([LibraryEntryName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE INDEX [IX_Service_Line_SrvClaFID] ON [Service_Line] ([SrvClaFID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE INDEX [IX_Service_Line_SrvResponsibleParty] ON [Service_Line] ([SrvResponsibleParty]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_ServiceLine_SrvGUID] ON [Service_Line] ([SrvGUID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219091310_AddReceiverLibrary'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260219091310_AddReceiverLibrary', N'9.0.11');
END;

COMMIT;
GO

