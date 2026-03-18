using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zebl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppUser",
                columns: table => new
                {
                    UserGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    PasswordHash = table.Column<byte[]>(type: "varbinary(64)", maxLength: 64, nullable: true),
                    PasswordSalt = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__AppUser__81B7740C5F82BC9D", x => x.UserGUID);
                });

            migrationBuilder.CreateTable(
                name: "CityStateZipLibrary",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Zip = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CityStateZipLibrary_Id", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Claim_Audit",
                columns: table => new
                {
                    AuditID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClaFID = table.Column<int>(type: "int", nullable: false),
                    ActivityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ActivityDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ComputerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TotalCharge = table.Column<decimal>(type: "money", nullable: true),
                    InsuranceBalance = table.Column<decimal>(type: "money", nullable: true),
                    PatientBalance = table.Column<decimal>(type: "money", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Claim_Audit", x => x.AuditID);
                });

            migrationBuilder.CreateTable(
                name: "ClaimRejection",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClaimId = table.Column<int>(type: "int", nullable: true),
                    EdiReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Segment = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Element = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TransactionControlNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimRejection_Id", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClaimSubmission",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClaimId = table.Column<int>(type: "int", nullable: false),
                    TransactionControlNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PatientControlNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BatchId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FileControlNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SubmissionDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimSubmission_Id", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClaimTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TemplateName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    AvailableToPatientId = table.Column<int>(type: "int", nullable: true),
                    BillingProviderId = table.Column<int>(type: "int", nullable: true),
                    RenderingProviderId = table.Column<int>(type: "int", nullable: true),
                    ServiceFacilityId = table.Column<int>(type: "int", nullable: true),
                    ReferringProviderId = table.Column<int>(type: "int", nullable: true),
                    OrderingProviderId = table.Column<int>(type: "int", nullable: true),
                    SupervisingProviderId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConnectionLibrary",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Host = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Port = table.Column<int>(type: "int", nullable: false, defaultValue: 22),
                    Username = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    EncryptedPassword = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    UploadDirectory = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DownloadDirectory = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DownloadPattern = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AutoRenameFiles = table.Column<bool>(type: "bit", nullable: false),
                    AllowMoveOrDelete = table.Column<bool>(type: "bit", nullable: false),
                    AutoFileExtension = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UseWithInterfacesOnly = table.Column<bool>(type: "bit", nullable: false),
                    DownloadFromSubdirectories = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectionLibrary_Id", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomFieldDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    FieldKey = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Label = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    FieldType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomFieldDefinitions_Id", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomFieldValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: false),
                    FieldKey = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Value = table.Column<string>(type: "varchar(max)", unicode: false, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomFieldValues_Id", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Diagnosis_Code",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    CodeType = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Diagnosis_Code_Id", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EdiReport",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceiverLibraryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConnectionLibraryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Generated"),
                    TraceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PayerName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PaymentAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    FileContent = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EdiReport_Id", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EligibilityRequest",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    PayerId = table.Column<int>(type: "int", nullable: false),
                    PolicyNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResponseReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EdiReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EligibilityRequest_Id", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EligibilityResponse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EligibilityRequestId = table.Column<int>(type: "int", nullable: false),
                    CoverageStatus = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PlanName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DeductibleAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CopayAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CoinsurancePercent = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CoverageStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CoverageEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Raw271 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EligibilityResponse_Id", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EraException",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EdiReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimId = table.Column<int>(type: "int", nullable: true),
                    ServiceLineId = table.Column<int>(type: "int", nullable: true),
                    ExceptionType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    EraClaimIdentifier = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AssignedUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EraException_Id", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Hl7_Import_Log",
                columns: table => new
                {
                    ImportLogID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ImportDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NewPatientsCount = table.Column<int>(type: "int", nullable: false),
                    UpdatedPatientsCount = table.Column<int>(type: "int", nullable: false),
                    NewClaimsCount = table.Column<int>(type: "int", nullable: false),
                    NewServiceLinesCount = table.Column<int>(type: "int", nullable: false),
                    ImportSuccessful = table.Column<bool>(type: "bit", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ImportedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ComputerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Hl7_Impo__ImportLogID", x => x.ImportLogID);
                });

            migrationBuilder.CreateTable(
                name: "Interface_Import_Log",
                columns: table => new
                {
                    ImportID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ImportDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ComputerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NewPatientsCount = table.Column<int>(type: "int", nullable: false),
                    UpdatedPatientsCount = table.Column<int>(type: "int", nullable: false),
                    NewClaimsCount = table.Column<int>(type: "int", nullable: false),
                    DuplicateClaimsCount = table.Column<int>(type: "int", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "money", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Interface_Import_Log", x => x.ImportID);
                });

            migrationBuilder.CreateTable(
                name: "ListValue",
                columns: table => new
                {
                    ListValueID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ListType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__ListValue__ListValueID", x => x.ListValueID);
                });

            migrationBuilder.CreateTable(
                name: "Modifier_Code",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Description = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Modifier_Code_Id", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Payer",
                columns: table => new
                {
                    PayID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayDateTimeCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PayDateTimeModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PayCreatedUserGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PayLastUserGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PayCreatedUserName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    PayLastUserName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    PayCreatedComputerName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    PayLastComputerName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    PayName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    PayExternalID = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: true),
                    PayAddr1 = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    PayAddr2 = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    PayAlwaysExportSupervisingProvider = table.Column<bool>(type: "bit", nullable: false),
                    PayBox1 = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    PayCity = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    PayClaimFilingIndicator = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: true),
                    PayClaimType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    PayClassification = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    PayEligibilityPhyID = table.Column<int>(type: "int", nullable: false),
                    PayEligibilityPayerID = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: true),
                    PayEmail = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    PayExportAuthIn2400 = table.Column<bool>(type: "bit", nullable: false),
                    PayExportBillingTaxonomy = table.Column<bool>(type: "bit", nullable: false),
                    PayExportOtherPayerOfficeNumber2330B = table.Column<bool>(type: "bit", nullable: false),
                    PayExportOriginalRefIn2330B = table.Column<bool>(type: "bit", nullable: false),
                    PayExportPatientAmtDueIn2430 = table.Column<bool>(type: "bit", nullable: false),
                    PayExportPatientForPOS12 = table.Column<bool>(type: "bit", nullable: false),
                    PayExportPaymentDateIn2330B = table.Column<bool>(type: "bit", nullable: false),
                    PayExportSSN = table.Column<bool>(type: "bit", nullable: false),
                    PayFaxNo = table.Column<string>(type: "varchar(25)", unicode: false, maxLength: 25, nullable: true),
                    PayFollowUpDays = table.Column<int>(type: "int", nullable: false),
                    PayForwardsClaims = table.Column<bool>(type: "bit", nullable: false),
                    PayICDIndicator = table.Column<string>(type: "varchar(1)", unicode: false, maxLength: 1, nullable: true),
                    PayIgnoreRenderingProvider = table.Column<bool>(type: "bit", nullable: false),
                    PayInactive = table.Column<bool>(type: "bit", nullable: false),
                    PayInsTypeCode = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    PayNotes = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    PayOfficeNumber = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    PayPaymentMatchingKey = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    PayPhoneNo = table.Column<string>(type: "varchar(25)", unicode: false, maxLength: 25, nullable: true),
                    PayPrintBox30 = table.Column<bool>(type: "bit", nullable: false),
                    PayFormatDateBox14And15 = table.Column<bool>(type: "bit", nullable: false),
                    PayState = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: true),
                    PaySubmissionMethod = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    PaySuppressWhenPrinting = table.Column<bool>(type: "bit", nullable: false),
                    PayTotalUndisbursedPaymentsTRIG = table.Column<decimal>(type: "money", nullable: false),
                    PayExportTrackedPRAdjs = table.Column<bool>(type: "bit", nullable: false),
                    PayUseTotalAppliedInBox29 = table.Column<bool>(type: "bit", nullable: false),
                    PayWebsite = table.Column<string>(type: "varchar(max)", unicode: false, nullable: true),
                    PayZip = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: true),
                    PayNameWithInactiveCC = table.Column<string>(type: "varchar(61)", unicode: false, maxLength: 61, nullable: false, computedColumnSql: "(case when [PayInactive]=(1) then 'INACTIVE - '+isnull([PayName],'') else isnull([PayName],'') end)", stored: true),
                    PayCityStateZipCC = table.Column<string>(type: "varchar(70)", unicode: false, maxLength: 70, nullable: false, computedColumnSql: "((((isnull([PayCity],'')+', ')+isnull([PayState],''))+' ')+isnull([PayZip],''))", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Payer__EE8FCE2FDCFA87D9", x => x.PayID);
                });

            migrationBuilder.CreateTable(
                name: "Physician",
                columns: table => new
                {
                    PhyID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PhyDateTimeCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PhyDateTimeModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PhyCreatedUserGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PhyLastUserGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PhyCreatedUserName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    PhyLastUserName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    PhyCreatedComputerName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    PhyLastComputerName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    PhyAddress1 = table.Column<string>(type: "varchar(55)", unicode: false, maxLength: 55, nullable: true),
                    PhyAddress2 = table.Column<string>(type: "varchar(55)", unicode: false, maxLength: 55, nullable: true),
                    PhyCity = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    PhyEMail = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: true),
                    PhyEntityType = table.Column<string>(type: "varchar(1)", unicode: false, maxLength: 1, nullable: true),
                    PhyFax = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: true),
                    PhyFirstName = table.Column<string>(type: "varchar(35)", unicode: false, maxLength: 35, nullable: true),
                    PhyInactive = table.Column<bool>(type: "bit", nullable: false),
                    PhyLastName = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: true),
                    PhyMiddleName = table.Column<string>(type: "varchar(25)", unicode: false, maxLength: 25, nullable: true),
                    PhyName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    PhyNotes = table.Column<string>(type: "varchar(max)", unicode: false, nullable: true),
                    PhyNPI = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    PhyPrimaryCodeType = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: true),
                    PhyPrimaryIDCode = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: true),
                    PhyRateClass = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    PhySignatureOnFile = table.Column<bool>(type: "bit", nullable: false),
                    PhySpecialtyCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    PhyState = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: true),
                    PhySuffix = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    PhyTelephone = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: true),
                    PhyType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    PhyZip = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: true),
                    PhyFirstMiddleLastNameCC = table.Column<string>(type: "varchar(122)", unicode: false, maxLength: 122, nullable: false, computedColumnSql: "((((isnull([PhyFirstName],'')+' ')+isnull([PhyMiddleName],''))+' ')+isnull([PhyLastName],''))", stored: true),
                    PhyFullNameCC = table.Column<string>(type: "varchar(97)", unicode: false, maxLength: 97, nullable: false, computedColumnSql: "((isnull([PhyLastName],'')+', ')+isnull([PhyFirstName],''))", stored: true),
                    PhyNameWithInactiveCC = table.Column<string>(type: "varchar(111)", unicode: false, maxLength: 111, nullable: false, computedColumnSql: "(case when [PhyInactive]=(1) then 'INACTIVE - '+isnull([PhyName],'') else isnull([PhyName],'') end)", stored: true),
                    PhyCityStateZipCC = table.Column<string>(type: "varchar(54)", unicode: false, maxLength: 54, nullable: false, computedColumnSql: "((isnull([PhyCity],'')+', ')+isnull([PhyState],''))", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Physicia__5FEDBF914E56D8C8", x => x.PhyID);
                });

            migrationBuilder.CreateTable(
                name: "Place_of_Service",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Description = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Place_of_Service_Id", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProgramSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Section = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: false),
                    SettingsJson = table.Column<string>(type: "varchar(max)", unicode: false, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramSettings_Id", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Reason_Code",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Description = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reason_Code_Id", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReceiverLibrary",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LibraryEntryName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ExportFormat = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SubmitterType = table.Column<int>(type: "int", nullable: false),
                    BusinessOrLastName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    FirstName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    SubmitterId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ContactName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ContactType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ContactValue = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ReceiverName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ReceiverId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AuthorizationInfoQualifier = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    AuthorizationInfo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    SecurityInfoQualifier = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    SecurityInfo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    SenderQualifier = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    SenderId = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    ReceiverQualifier = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    InterchangeReceiverId = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    AcknowledgeRequested = table.Column<bool>(type: "bit", nullable: false),
                    TestProdIndicator = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    SenderCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReceiverCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiverLibrary_Id", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Remark_Code",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Remark_Code_Id", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScrubRule",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Condition = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PayerId = table.Column<int>(type: "int", nullable: true),
                    ProgramId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScrubRule_Id", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SecondaryForwardableAdjustmentRules",
                columns: table => new
                {
                    GroupCode = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    ReasonCode = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    ForwardToSecondary = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecondaryForwardableAdjustmentRules", x => new { x.GroupCode, x.ReasonCode });
                });

            migrationBuilder.CreateTable(
                name: "Insured",
                columns: table => new
                {
                    InsGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    InsFirstName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    InsLastName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    InsMI = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    InsDateTimeCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InsDateTimeModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InsCreatedUserGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InsLastUserGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InsCreatedUserName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    InsLastUserName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    InsCreatedComputerName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    InsLastComputerName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    InsAcceptAssignment = table.Column<short>(type: "smallint", nullable: false),
                    InsAdditionalRefID = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    InsAddress = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    InsBirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                    InsCity = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    InsClaimFilingIndicator = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: true),
                    InsEmployer = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    InsGroupNumber = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    InsIDNumber = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    InsPayID = table.Column<int>(type: "int", nullable: false),
                    InsPlanName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    InsPhone = table.Column<string>(type: "varchar(25)", unicode: false, maxLength: 25, nullable: true),
                    InsSex = table.Column<string>(type: "varchar(1)", unicode: false, maxLength: 1, nullable: true),
                    InsSSN = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: true),
                    InsState = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    InsZip = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    InsCityStateZipCC = table.Column<string>(type: "varchar(62)", unicode: false, maxLength: 62, nullable: false, computedColumnSql: "((isnull([InsCity],'')+', ')+isnull([InsState],''))", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Insured__B098F0EA21F654F8", x => x.InsGUID);
                    table.ForeignKey(
                        name: "FK_Insured_Payer",
                        column: x => x.InsPayID,
                        principalTable: "Payer",
                        principalColumn: "PayID");
                });

            migrationBuilder.CreateTable(
                name: "Patient",
                columns: table => new
                {
                    PatID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatFirstName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PatLastName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PatMI = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    PatDateTimeCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PatDateTimeModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PatCreatedUserGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PatLastUserGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PatCreatedUserName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    PatLastUserName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    PatCreatedComputerName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    PatLastComputerName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    PatAccountNo = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    PatActive = table.Column<bool>(type: "bit", nullable: false),
                    PatAddress = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    PatAddress2 = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    PatAptReminderPref = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    PatAuthTracking = table.Column<bool>(type: "bit", nullable: true),
                    PatBillingPhyFID = table.Column<int>(type: "int", nullable: false),
                    PatBirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PatBox8Reserved = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: true),
                    PatBox9bReserved = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    PatBox9cReserved = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    PatCellPhoneNo = table.Column<string>(type: "varchar(25)", unicode: false, maxLength: 25, nullable: true),
                    PatCellSMTPHost = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    PatCity = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    PatClaLibFID = table.Column<int>(type: "int", nullable: false),
                    PatClaimDefaults = table.Column<string>(type: "xml", nullable: true),
                    PatClassification = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    PatCoPayAmount = table.Column<decimal>(type: "money", nullable: true),
                    PatCoPayPercent = table.Column<float>(type: "real", nullable: true),
                    PatCustomField1 = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    PatCustomField2 = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    PatCustomField3 = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    PatCustomField4 = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    PatCustomField5 = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    PatDiagnosis1 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    PatDiagnosis2 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    PatDiagnosis3 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    PatDiagnosis4 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    PatDiagnosis5 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    PatDiagnosis6 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    PatDiagnosis7 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    PatDiagnosis8 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    PatDiagnosis9 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    PatDiagnosis10 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    PatDiagnosis11 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    PatDiagnosis12 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    PatDontSendPromotions = table.Column<bool>(type: "bit", nullable: false),
                    PatDontSendStatements = table.Column<bool>(type: "bit", nullable: false),
                    PatEmergencyContactName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    PatEmergencyContactPhoneNo = table.Column<string>(type: "varchar(25)", unicode: false, maxLength: 25, nullable: true),
                    PatEmergencyContactRelation = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    PatEmployed = table.Column<int>(type: "int", nullable: true),
                    PatExternalFID = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    PatPaymentMatchingKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PatFacilityPhyFID = table.Column<int>(type: "int", nullable: false),
                    PatFaxNo = table.Column<string>(type: "varchar(25)", unicode: false, maxLength: 25, nullable: true),
                    PatFirstDateTRIG = table.Column<DateOnly>(type: "date", nullable: true),
                    PatHeight = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    PatHomePhoneNo = table.Column<string>(type: "varchar(25)", unicode: false, maxLength: 25, nullable: true),
                    PatInsuredSigOnFile = table.Column<bool>(type: "bit", nullable: false),
                    PatLastAppointmentKeptTRIG = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PatLastAppointmentNotKeptTRIG = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PatLastServiceDateTRIG = table.Column<DateOnly>(type: "date", nullable: true),
                    PatLastCellSMPTHostUpdate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PatLastStatementDateTRIG = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PatLastPatPmtDateTRIG = table.Column<DateOnly>(type: "date", nullable: true),
                    PatLocked = table.Column<bool>(type: "bit", nullable: false),
                    PatMarried = table.Column<short>(type: "smallint", nullable: true),
                    PatMemberID = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    PatOrderingPhyFID = table.Column<int>(type: "int", nullable: false),
                    PatPhoneNo = table.Column<string>(type: "varchar(25)", unicode: false, maxLength: 25, nullable: true),
                    PatPhyPrintDate = table.Column<bool>(type: "bit", nullable: true),
                    PatPriEmail = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    PatPrintSigDate = table.Column<bool>(type: "bit", nullable: true),
                    PatReferringPhyFID = table.Column<int>(type: "int", nullable: false),
                    PatRecallDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PatReminderNote = table.Column<string>(type: "varchar(max)", unicode: false, nullable: true),
                    PatReminderNoteEvent = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    PatRenderingPhyFID = table.Column<int>(type: "int", nullable: false),
                    PatResourceWants = table.Column<string>(type: "xml", nullable: true),
                    PatSecEmail = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    PatSex = table.Column<string>(type: "varchar(1)", unicode: false, maxLength: 1, nullable: true),
                    PatSigOnFile = table.Column<bool>(type: "bit", nullable: false),
                    PatSigSource = table.Column<string>(type: "varchar(1)", unicode: false, maxLength: 1, nullable: true),
                    PatSigText = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    PatSSN = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: true),
                    PatState = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    PatStatementAddressLine1 = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    PatStatementAddressLine2 = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    PatStatementCity = table.Column<string>(type: "varchar(29)", unicode: false, maxLength: 29, nullable: true),
                    PatStatementName = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: true),
                    PatStatementMessage = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    PatStatementState = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    PatStatementZipCode = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    PatSupervisingPhyFID = table.Column<int>(type: "int", nullable: false),
                    PatTotalInsBalanceTRIG = table.Column<decimal>(type: "money", nullable: false),
                    PatTotalPatBalanceTRIG = table.Column<decimal>(type: "money", nullable: false),
                    PatTotalUndisbursedPaymentsTRIG = table.Column<decimal>(type: "money", nullable: false),
                    PatWeight = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    PatWorkPhoneNo = table.Column<string>(type: "varchar(25)", unicode: false, maxLength: 25, nullable: true),
                    PatZip = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    PatLastPaymentRequestTRIG = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PatFirstNameTruncatedCC = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true, computedColumnSql: "(left(isnull([PatFirstName],''),case when charindex(' ',isnull([PatFirstName]+' ',' '))=(0) then len(isnull([PatFirstName],'')) else charindex(' ',isnull([PatFirstName]+' ',' '))-(1) end))", stored: true),
                    PatLastNameTruncatedCC = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true, computedColumnSql: "(left(isnull([PatLastName],''),case when charindex(' ',isnull([PatLastName]+' ',' '))=(0) then len(isnull([PatLastName],'')) else charindex(' ',isnull([PatLastName]+' ',' '))-(1) end))", stored: true),
                    PatFullNameCC = table.Column<string>(type: "nvarchar(108)", maxLength: 108, nullable: false, computedColumnSql: "((((isnull([PatLastName],'')+', ')+isnull([PatFirstName],''))+' ')+isnull([PatMI],''))", stored: true),
                    PatFullNameFMLCC = table.Column<string>(type: "nvarchar(107)", maxLength: 107, nullable: false, computedColumnSql: "((((isnull([PatFirstName],'')+' ')+isnull([PatMI],''))+' ')+isnull([PatLastName],''))", stored: true),
                    PatDiagnosisCodesCC = table.Column<string>(type: "varchar(371)", unicode: false, maxLength: 371, nullable: false, computedColumnSql: "((((((((((((((((((((((isnull([PatDiagnosis1],'')+'|')+isnull([PatDiagnosis2],''))+'|')+isnull([PatDiagnosis3],''))+'|')+isnull([PatDiagnosis4],''))+'|')+isnull([PatDiagnosis5],''))+'|')+isnull([PatDiagnosis6],''))+'|')+isnull([PatDiagnosis7],''))+'|')+isnull([PatDiagnosis8],''))+'|')+isnull([PatDiagnosis9],''))+'|')+isnull([PatDiagnosis10],''))+'|')+isnull([PatDiagnosis11],''))+'|')+isnull([PatDiagnosis12],''))", stored: true),
                    PatTotalBalanceCC = table.Column<decimal>(type: "money", nullable: true, computedColumnSql: "(isnull([PatTotalInsBalanceTRIG],(0))+isnull([PatTotalPatBalanceTRIG],(0)))", stored: true),
                    PatTotalBalanceIncludingUndisbursedPatPmtsCC = table.Column<decimal>(type: "money", nullable: true, computedColumnSql: "((isnull([PatTotalInsBalanceTRIG],(0))+isnull([PatTotalPatBalanceTRIG],(0)))-isnull([PatTotalUndisbursedPaymentsTRIG],(0)))", stored: true),
                    PatTotalPatBalanceIncludingUndisbursedPatPmtsCC = table.Column<decimal>(type: "money", nullable: true, computedColumnSql: "(isnull([PatTotalPatBalanceTRIG],(0))-isnull([PatTotalUndisbursedPaymentsTRIG],(0)))", stored: true),
                    PatCityStateZipCC = table.Column<string>(type: "varchar(62)", unicode: false, maxLength: 62, nullable: false, computedColumnSql: "((isnull([PatCity],'')+', ')+isnull([PatState],''))", stored: true),
                    PatStatementCityStateZipCC = table.Column<string>(type: "varchar(41)", unicode: false, maxLength: 41, nullable: false, computedColumnSql: "((isnull([PatStatementCity],'')+', ')+isnull([PatStatementState],''))", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Patient__F0DDA98550DB60C7", x => x.PatID);
                    table.ForeignKey(
                        name: "FK_Patient_BillingPhysician",
                        column: x => x.PatBillingPhyFID,
                        principalTable: "Physician",
                        principalColumn: "PhyID");
                    table.ForeignKey(
                        name: "FK_Patient_FacilityPhysician",
                        column: x => x.PatFacilityPhyFID,
                        principalTable: "Physician",
                        principalColumn: "PhyID");
                    table.ForeignKey(
                        name: "FK_Patient_OrderingPhysician",
                        column: x => x.PatOrderingPhyFID,
                        principalTable: "Physician",
                        principalColumn: "PhyID");
                    table.ForeignKey(
                        name: "FK_Patient_ReferringPhysician",
                        column: x => x.PatReferringPhyFID,
                        principalTable: "Physician",
                        principalColumn: "PhyID");
                    table.ForeignKey(
                        name: "FK_Patient_RenderingPhysician",
                        column: x => x.PatRenderingPhyFID,
                        principalTable: "Physician",
                        principalColumn: "PhyID");
                    table.ForeignKey(
                        name: "FK_Patient_SupervisingPhysician",
                        column: x => x.PatSupervisingPhyFID,
                        principalTable: "Physician",
                        principalColumn: "PhyID");
                });

            migrationBuilder.CreateTable(
                name: "Procedure_Code",
                columns: table => new
                {
                    ProcID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProcDateTimeCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcDateTimeModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcCreatedUserGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProcLastUserGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProcCreatedUserName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ProcLastUserName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ProcCreatedComputerName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    ProcLastComputerName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    ProcAdjust = table.Column<decimal>(type: "money", nullable: false),
                    ProcAllowed = table.Column<decimal>(type: "money", nullable: false),
                    ProcBillingPhyFID = table.Column<int>(type: "int", nullable: false),
                    ProcCategory = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ProcCharge = table.Column<decimal>(type: "money", nullable: false),
                    ProcCost = table.Column<decimal>(type: "money", nullable: false),
                    ProcCMNReq = table.Column<bool>(type: "bit", nullable: false),
                    ProcCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    ProcCoPayReq = table.Column<bool>(type: "bit", nullable: false),
                    ProcDescription = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    ProcDescriptionReq = table.Column<bool>(type: "bit", nullable: false),
                    ProcDrugUnitCount = table.Column<float>(type: "real", nullable: false),
                    ProcDrugUnitMeasurement = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: true),
                    ProcModifier1 = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: true),
                    ProcModifier2 = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: true),
                    ProcModifier3 = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: true),
                    ProcModifier4 = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: true),
                    ProcNote = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    ProcNDCCode = table.Column<string>(type: "varchar(48)", unicode: false, maxLength: 48, nullable: true),
                    ProcPayFID = table.Column<int>(type: "int", nullable: false),
                    ProcProductCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ProcRateClass = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ProcRevenueCode = table.Column<string>(type: "varchar(4)", unicode: false, maxLength: 4, nullable: true),
                    ProcRVUMalpractice = table.Column<float>(type: "real", nullable: false),
                    ProcRVUWork = table.Column<float>(type: "real", nullable: false),
                    ProcSubCategory = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ProcUnits = table.Column<float>(type: "real", nullable: false),
                    ProcStart = table.Column<DateOnly>(type: "date", nullable: true),
                    ProcEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    ProcModifiersCC = table.Column<string>(type: "varchar(8)", unicode: false, maxLength: 8, nullable: false, computedColumnSql: "(((isnull([ProcModifier1],'')+isnull([ProcModifier2],''))+isnull([ProcModifier3],''))+isnull([ProcModifier4],''))", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Procedur__07C21CFE69FC361F", x => x.ProcID);
                    table.ForeignKey(
                        name: "FK_ProcedureCode_BillingPhysician",
                        column: x => x.ProcBillingPhyFID,
                        principalTable: "Physician",
                        principalColumn: "PhyID");
                    table.ForeignKey(
                        name: "FK_ProcedureCode_Payer",
                        column: x => x.ProcPayFID,
                        principalTable: "Payer",
                        principalColumn: "PayID");
                });

            migrationBuilder.CreateTable(
                name: "Claim",
                columns: table => new
                {
                    ClaID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClaDateTimeCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClaDateTimeModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClaCreatedUserGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClaLastUserGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClaCreatedUserName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ClaLastUserName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ClaCreatedComputerName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    ClaLastComputerName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    ClaAccidentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaAcuteManifestationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaAdmissionHour = table.Column<string>(type: "varchar(4)", unicode: false, maxLength: 4, nullable: true),
                    ClaAdmissionSource = table.Column<string>(type: "varchar(1)", unicode: false, maxLength: 1, nullable: true),
                    ClaAdmissionType = table.Column<string>(type: "varchar(1)", unicode: false, maxLength: 1, nullable: true),
                    ClaAdmittedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaAdmittingDiagnosis = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ClaArchived = table.Column<bool>(type: "bit", nullable: true),
                    ClaAssumedCareDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaAttendingPhyFID = table.Column<int>(type: "int", nullable: false),
                    ClaAuthorizedReturnToWorkDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaBillDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaBillingPhyFID = table.Column<int>(type: "int", nullable: false),
                    ClaBillTo = table.Column<int>(type: "int", nullable: true),
                    ClaBox10dClaimCodes = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    ClaBox11bOtherClaimIDQualifier = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: true),
                    ClaBox22CodeOverride = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ClaBox33bOverride = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: true),
                    ClaClassification = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ClaCLIANumber = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: true),
                    ClaCMNCertOnFile = table.Column<string>(type: "varchar(1)", unicode: false, maxLength: 1, nullable: true),
                    ClaCMNCertTypeCode = table.Column<string>(type: "varchar(1)", unicode: false, maxLength: 1, nullable: true),
                    ClaCMNFormIdentificationCode = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    ClaCMNInitialDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaCMNLengthOfNeed = table.Column<int>(type: "int", nullable: true),
                    ClaCMNRevisedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaCMNSignedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaCN1Segment = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    ClaConditionCode1 = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: true),
                    ClaConditionCode2 = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: true),
                    ClaConditionCode3 = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: true),
                    ClaConditionCode4 = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: true),
                    ClaAdditionalData = table.Column<string>(type: "xml", nullable: true),
                    ClaCustomField1 = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    ClaCustomField2 = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    ClaCustomField3 = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    ClaCustomField4 = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    ClaCustomField5 = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    ClaDateLastSeen = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaDateOfCurrent = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaDateTotalFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaDateTotalThrough = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaDelayCode = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: true),
                    ClaDiagnosis1 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ClaDiagnosis2 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ClaDiagnosis3 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ClaDiagnosis4 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ClaDiagnosis5 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ClaDiagnosis6 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ClaDiagnosis7 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ClaDiagnosis8 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ClaDiagnosis9 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ClaDiagnosis10 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ClaDiagnosis11 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ClaDiagnosis12 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ClaDiagnosis13 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ClaDiagnosis14 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ClaDiagnosis15 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ClaDiagnosis16 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ClaDiagnosis17 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ClaDiagnosis18 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ClaDiagnosis19 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ClaDiagnosis20 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ClaDiagnosis21 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ClaDiagnosis22 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ClaDiagnosis23 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ClaDiagnosis24 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ClaDiagnosis25 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ClaDiagnosisCodesCC = table.Column<string>(type: "varchar(774)", unicode: false, maxLength: 774, nullable: false, computedColumnSql: "((((((((((((((((((((((((((((((((((((((((((((((((isnull([ClaDiagnosis1],'')+'|')+isnull([ClaDiagnosis2],''))+'|')+isnull([ClaDiagnosis3],''))+'|')+isnull([ClaDiagnosis4],''))+'|')+isnull([ClaDiagnosis5],''))+'|')+isnull([ClaDiagnosis6],''))+'|')+isnull([ClaDiagnosis7],''))+'|')+isnull([ClaDiagnosis8],''))+'|')+isnull([ClaDiagnosis9],''))+'|')+isnull([ClaDiagnosis10],''))+'|')+isnull([ClaDiagnosis11],''))+'|')+isnull([ClaDiagnosis12],''))+'|')+isnull([ClaDiagnosis13],''))+'|')+isnull([ClaDiagnosis14],''))+'|')+isnull([ClaDiagnosis15],''))+'|')+isnull([ClaDiagnosis16],''))+'|')+isnull([ClaDiagnosis17],''))+'|')+isnull([ClaDiagnosis18],''))+'|')+isnull([ClaDiagnosis19],''))+'|')+isnull([ClaDiagnosis20],''))+'|')+isnull([ClaDiagnosis21],''))+'|')+isnull([ClaDiagnosis22],''))+'|')+isnull([ClaDiagnosis23],''))+'|')+isnull([ClaDiagnosis24],''))+'|')+isnull([ClaDiagnosis25],''))", stored: true),
                    ClaDisabilityBeginDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaDisabilityEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaDischargedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaDischargedHour = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClaDMEFormData = table.Column<string>(type: "varchar(max)", unicode: false, nullable: true),
                    ClaEDINotes = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: true),
                    ClaEPSDTReferral = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: true),
                    ClaExternalFID = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ClaFacilityPhyFID = table.Column<int>(type: "int", nullable: false),
                    ClaFirstDateTRIG = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaFirstDateOfInjury = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaFirstInsPaymentDateTRIG = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaHearingAndPrescriptionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaHomeboundInd = table.Column<string>(type: "varchar(1)", unicode: false, maxLength: 1, nullable: true),
                    ClaHospiceInd = table.Column<string>(type: "varchar(1)", unicode: false, maxLength: 1, nullable: true),
                    ClaICDIndicator = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: false),
                    ClaIDENumber = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    ClaInitialTreatmentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaIgnoreAppliedAmount = table.Column<short>(type: "smallint", nullable: false),
                    ClaInsuranceTypeCodeOverride = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    ClaInvoiceNumber = table.Column<string>(type: "varchar(38)", unicode: false, maxLength: 38, nullable: true),
                    ClaK3FileInformation = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: true),
                    ClaLabCharges = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    ClaLastDateTRIG = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaLastExportedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaLastMenstrualDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaLastPrintedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaLastWorkedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaLastXRayDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaLocked = table.Column<bool>(type: "bit", nullable: false),
                    ClaMammographyCert = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    ClaMedicalRecordNumber = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ClaMedicaidResubmissionCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ClaMOASegment = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ClaOperatingPhyFID = table.Column<int>(type: "int", nullable: false),
                    ClaOrderingPhyFID = table.Column<int>(type: "int", nullable: false),
                    ClaOriginalRefNo = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ClaOutsideLab = table.Column<int>(type: "int", nullable: true),
                    ClaPaidDateTRIG = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaPaperWorkControlNumber = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: true),
                    ClaPaperWorkInd = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    ClaPaperWorkTransmissionCode = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: true),
                    ClaPatFID = table.Column<int>(type: "int", nullable: false),
                    ClaPatientReasonDiagnosis1 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ClaPatientReasonDiagnosis2 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ClaPatientReasonDiagnosis3 = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ClaPatientStatus = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: true),
                    ClaPOAIndicator = table.Column<string>(type: "varchar(25)", unicode: false, maxLength: 25, nullable: true),
                    ClaPPSCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ClaPricingExceptionCode = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: true),
                    ClaPrincipalProcedureCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ClaPrincipalProcedureDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaPrintUnitCharge = table.Column<bool>(type: "bit", nullable: false),
                    ClaProviderAgreementCode = table.Column<string>(type: "varchar(1)", unicode: false, maxLength: 1, nullable: true),
                    ClaRecurUntilDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaRecurringTimeFrame = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ClaReferralNumber = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ClaReferringPhyFID = table.Column<int>(type: "int", nullable: false),
                    ClaRelatedTo = table.Column<short>(type: "smallint", nullable: true),
                    ClaRelatedToState = table.Column<string>(type: "varchar(4)", unicode: false, maxLength: 4, nullable: true),
                    ClaRelinquishedCareDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaRemarks = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: true),
                    ClaRenderingPhyFID = table.Column<int>(type: "int", nullable: false),
                    ClaReserved10 = table.Column<string>(type: "varchar(25)", unicode: false, maxLength: 25, nullable: true),
                    ClaReserved19 = table.Column<string>(type: "varchar(75)", unicode: false, maxLength: 75, nullable: true),
                    ClaSimilarIllnessDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaSpecialProgramIndicator = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: true),
                    ClaStatementCoversFromOverride = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaStatementCoversThroughOverride = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaStatus = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    ClaSubmissionMethod = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    ClaSupervisingPhyFID = table.Column<int>(type: "int", nullable: false),
                    ClaTypeOfBill = table.Column<string>(type: "varchar(4)", unicode: false, maxLength: 4, nullable: true),
                    ClaClaimType = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    ClaPrimaryClaimFID = table.Column<int>(type: "int", nullable: true),
                    ClaTotalCOAdjTRIG = table.Column<decimal>(type: "money", nullable: true),
                    ClaTotalCRAdjTRIG = table.Column<decimal>(type: "money", nullable: true),
                    ClaTotalOAAdjTRIG = table.Column<decimal>(type: "money", nullable: true),
                    ClaTotalPIAdjTRIG = table.Column<decimal>(type: "money", nullable: true),
                    ClaTotalPRAdjTRIG = table.Column<decimal>(type: "money", nullable: true),
                    ClaTotalAdjCC = table.Column<decimal>(type: "money", nullable: true, computedColumnSql: "((((isnull([ClaTotalCOAdjTRIG],(0))+isnull([ClaTotalCRAdjTRIG],(0)))+isnull([ClaTotalOAAdjTRIG],(0)))+isnull([ClaTotalPIAdjTRIG],(0)))+isnull([ClaTotalPRAdjTRIG],(0)))", stored: true),
                    ClaTotalServiceLineCountTRIG = table.Column<int>(type: "int", nullable: false),
                    ClaTotalChargeTRIG = table.Column<decimal>(type: "money", nullable: false),
                    ClaTotalInsAmtPaidTRIG = table.Column<decimal>(type: "money", nullable: false),
                    ClaTotalInsBalanceTRIG = table.Column<decimal>(type: "money", nullable: false),
                    ClaTotalPatAmtPaidTRIG = table.Column<decimal>(type: "money", nullable: false),
                    ClaTotalPatBalanceTRIG = table.Column<decimal>(type: "money", nullable: false),
                    ClaTotalAmtAppliedCC = table.Column<decimal>(type: "money", nullable: true, computedColumnSql: "((((((isnull([ClaTotalPatAmtPaidTRIG],(0))+isnull([ClaTotalInsAmtPaidTRIG],(0)))+isnull([ClaTotalCOAdjTRIG],(0)))+isnull([ClaTotalCRAdjTRIG],(0)))+isnull([ClaTotalOAAdjTRIG],(0)))+isnull([ClaTotalPIAdjTRIG],(0)))+isnull([ClaTotalPRAdjTRIG],(0)))", stored: true),
                    ClaTotalAmtPaidCC = table.Column<decimal>(type: "money", nullable: true, computedColumnSql: "(isnull([ClaTotalPatAmtPaidTRIG],(0))+isnull([ClaTotalInsAmtPaidTRIG],(0)))", stored: true),
                    ClaTotalBalanceCC = table.Column<decimal>(type: "money", nullable: true, computedColumnSql: "((isnull([ClaTotalChargeTRIG],(0))-(isnull([ClaTotalPatAmtPaidTRIG],(0))+isnull([ClaTotalInsAmtPaidTRIG],(0))))-((((isnull([ClaTotalCOAdjTRIG],(0))+isnull([ClaTotalCRAdjTRIG],(0)))+isnull([ClaTotalOAAdjTRIG],(0)))+isnull([ClaTotalPIAdjTRIG],(0)))+isnull([ClaTotalPRAdjTRIG],(0))))", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Claim__E8193A9B6764AAF8", x => x.ClaID);
                    table.ForeignKey(
                        name: "FK_Claim_AttendingPhy",
                        column: x => x.ClaAttendingPhyFID,
                        principalTable: "Physician",
                        principalColumn: "PhyID");
                    table.ForeignKey(
                        name: "FK_Claim_BillingPhy",
                        column: x => x.ClaBillingPhyFID,
                        principalTable: "Physician",
                        principalColumn: "PhyID");
                    table.ForeignKey(
                        name: "FK_Claim_FacilityPhy",
                        column: x => x.ClaFacilityPhyFID,
                        principalTable: "Physician",
                        principalColumn: "PhyID");
                    table.ForeignKey(
                        name: "FK_Claim_OperatingPhy",
                        column: x => x.ClaOperatingPhyFID,
                        principalTable: "Physician",
                        principalColumn: "PhyID");
                    table.ForeignKey(
                        name: "FK_Claim_OrderingPhy",
                        column: x => x.ClaOrderingPhyFID,
                        principalTable: "Physician",
                        principalColumn: "PhyID");
                    table.ForeignKey(
                        name: "FK_Claim_Patient",
                        column: x => x.ClaPatFID,
                        principalTable: "Patient",
                        principalColumn: "PatID");
                    table.ForeignKey(
                        name: "FK_Claim_ReferringPhy",
                        column: x => x.ClaReferringPhyFID,
                        principalTable: "Physician",
                        principalColumn: "PhyID");
                    table.ForeignKey(
                        name: "FK_Claim_RenderingPhy",
                        column: x => x.ClaRenderingPhyFID,
                        principalTable: "Physician",
                        principalColumn: "PhyID");
                    table.ForeignKey(
                        name: "FK_Claim_SupervisingPhy",
                        column: x => x.ClaSupervisingPhyFID,
                        principalTable: "Physician",
                        principalColumn: "PhyID");
                });

            migrationBuilder.CreateTable(
                name: "Patient_Insured",
                columns: table => new
                {
                    PatInsGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    PatInsDateTimeCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PatInsDateTimeModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PatInsCreatedUserGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PatInsLastUserGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PatInsCreatedUserName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    PatInsLastUserName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    PatInsCreatedComputerName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    PatInsLastComputerName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    PatInsEligANSI = table.Column<string>(type: "varchar(max)", unicode: false, nullable: true),
                    PatInsEligDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PatInsEligStatus = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: true),
                    PatInsInsGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatInsPatFID = table.Column<int>(type: "int", nullable: false),
                    PatInsRelationToInsured = table.Column<int>(type: "int", nullable: false),
                    PatInsSequence = table.Column<int>(type: "int", nullable: false),
                    PatInsSequenceDescriptionCC = table.Column<string>(type: "varchar(9)", unicode: false, maxLength: 9, nullable: false, computedColumnSql: "(case when [PatInsSequence]=(1) then 'Primary' when [PatInsSequence]=(2) then 'Secondary' else '' end)", stored: true),
                    PatInsEligStatusDisplayTextCC = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: true, computedColumnSql: "(case when [PatInsEligStatus]='A' then 'Active' else [PatInsEligStatus] end)", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Patient___CA04E544A8F0AA6F", x => x.PatInsGUID);
                    table.ForeignKey(
                        name: "FK_PatientInsured_Insured",
                        column: x => x.PatInsInsGUID,
                        principalTable: "Insured",
                        principalColumn: "InsGUID");
                    table.ForeignKey(
                        name: "FK_PatientInsured_Patient",
                        column: x => x.PatInsPatFID,
                        principalTable: "Patient",
                        principalColumn: "PatID");
                });

            migrationBuilder.CreateTable(
                name: "Payment",
                columns: table => new
                {
                    PmtID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PmtDateTimeCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PmtDateTimeModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PmtCreatedUserGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PmtLastUserGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PmtCreatedUserName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    PmtLastUserName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    PmtCreatedComputerName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    PmtLastComputerName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    Pmt835Ref = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    PmtAmount = table.Column<decimal>(type: "money", nullable: false),
                    PmtBatchOperationReference = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    PmtBFEPFID = table.Column<int>(type: "int", nullable: false),
                    PmtDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PmtDisbursedTRIG = table.Column<decimal>(type: "money", nullable: false),
                    PmtMethod = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    PmtOtherReference1 = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: true),
                    PmtOtherReference2 = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: true),
                    PmtPatFID = table.Column<int>(type: "int", nullable: false),
                    PmtPayFID = table.Column<int>(type: "int", nullable: true),
                    PmtAuthCode = table.Column<string>(type: "varchar(25)", unicode: false, maxLength: 25, nullable: true),
                    PmtCardEntryContext = table.Column<byte>(type: "tinyint", nullable: true),
                    PmtCardEntryMethod = table.Column<byte>(type: "tinyint", nullable: true),
                    PmtNameOnCard = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    PmtIssuerResponseCode = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: true),
                    PmtResponseCode = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: true),
                    PmtChargedPlatformFee = table.Column<decimal>(type: "money", nullable: false),
                    PmtTransactionType = table.Column<byte>(type: "tinyint", nullable: true),
                    PmtNote = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    PmtRemainingCC = table.Column<decimal>(type: "money", nullable: true, computedColumnSql: "(isnull([PmtAmount],(0))-isnull([PmtDisbursedTRIG],(0)))", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Payment__1587830DB215BEEC", x => x.PmtID);
                    table.ForeignKey(
                        name: "FK_Payment_BFEPhysician",
                        column: x => x.PmtBFEPFID,
                        principalTable: "Physician",
                        principalColumn: "PhyID");
                    table.ForeignKey(
                        name: "FK_Payment_Patient",
                        column: x => x.PmtPatFID,
                        principalTable: "Patient",
                        principalColumn: "PatID");
                    table.ForeignKey(
                        name: "FK_Payment_Payer",
                        column: x => x.PmtPayFID,
                        principalTable: "Payer",
                        principalColumn: "PayID");
                });

            migrationBuilder.CreateTable(
                name: "Claim_Insured",
                columns: table => new
                {
                    ClaInsGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    ClaInsFirstName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ClaInsLastName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ClaInsMI = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: true),
                    ClaInsDateTimeCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClaInsDateTimeModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClaInsCreatedUserGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClaInsLastUserGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClaInsCreatedUserName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ClaInsLastUserName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ClaInsCreatedComputerName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    ClaInsLastComputerName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    ClaInsAcceptAssignment = table.Column<short>(type: "smallint", nullable: true),
                    ClaInsAdditionalRefID = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ClaInsAddress = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ClaInsBirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaInsCity = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ClaInsClaFID = table.Column<int>(type: "int", nullable: false),
                    ClaInsClaimFilingIndicator = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: true),
                    ClaInsEmployer = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ClaInsGroupNumber = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ClaInsIDNumber = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ClaInsPayFID = table.Column<int>(type: "int", nullable: false),
                    ClaInsPatFID = table.Column<int>(type: "int", nullable: false),
                    ClaInsPhone = table.Column<string>(type: "varchar(25)", unicode: false, maxLength: 25, nullable: true),
                    ClaInsPlanName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ClaInsPriorAuthorizationNumber = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ClaInsRelationToInsured = table.Column<int>(type: "int", nullable: false),
                    ClaInsSequence = table.Column<int>(type: "int", nullable: true),
                    ClaInsSex = table.Column<string>(type: "varchar(1)", unicode: false, maxLength: 1, nullable: true),
                    ClaInsSSN = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: true),
                    ClaInsState = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    ClaInsZip = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    ClaInsSequenceDescriptionCC = table.Column<string>(type: "varchar(9)", unicode: false, maxLength: 9, nullable: false, computedColumnSql: "(case when [ClaInsSequence]=(1) then 'Primary' when [ClaInsSequence]=(2) then 'Secondary' else '' end)", stored: true),
                    ClaInsCityStateZipCC = table.Column<string>(type: "varchar(62)", unicode: false, maxLength: 62, nullable: false, computedColumnSql: "((isnull([ClaInsCity],'')+', ')+isnull([ClaInsState],''))", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Claim_In__BE610E8C4D3B9F38", x => x.ClaInsGUID);
                    table.ForeignKey(
                        name: "FK_ClaimInsured_Claim",
                        column: x => x.ClaInsClaFID,
                        principalTable: "Claim",
                        principalColumn: "ClaID");
                    table.ForeignKey(
                        name: "FK_ClaimInsured_Patient",
                        column: x => x.ClaInsPatFID,
                        principalTable: "Patient",
                        principalColumn: "PatID");
                    table.ForeignKey(
                        name: "FK_ClaimInsured_Payer",
                        column: x => x.ClaInsPayFID,
                        principalTable: "Payer",
                        principalColumn: "PayID");
                });

            migrationBuilder.CreateTable(
                name: "Service_Line",
                columns: table => new
                {
                    SrvID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SrvDateTimeCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SrvDateTimeModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SrvCreatedUserGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SrvLastUserGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SrvCreatedUserName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    SrvLastUserName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    SrvCreatedComputerName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    SrvLastComputerName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    SrvAllowedAmt = table.Column<decimal>(type: "money", nullable: false),
                    SrvApprovedAmt = table.Column<decimal>(type: "money", nullable: false),
                    SrvAttachCMN = table.Column<bool>(type: "bit", nullable: false),
                    SrvAuthorizationOverride = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    SrvCharges = table.Column<decimal>(type: "money", nullable: false),
                    SrvClaFID = table.Column<int>(type: "int", nullable: true),
                    SrvCoPayAmountDue = table.Column<decimal>(type: "money", nullable: false),
                    SrvCost = table.Column<decimal>(type: "money", nullable: false),
                    SrvCustomField1 = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    SrvCustomField2 = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    SrvCustomField3 = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    SrvCustomField4 = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    SrvCustomField5 = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    SrvDesc = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    SrvDiagnosisPointer = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    SrvDrugUnitCount = table.Column<double>(type: "float", nullable: true),
                    SrvDrugUnitMeasurement = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: true),
                    SrvDrugUnitPrice = table.Column<decimal>(type: "money", nullable: true),
                    SrvEMG = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: true),
                    SrvEndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SrvEPSDT = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: true),
                    SrvExpectedPriPmt = table.Column<decimal>(type: "money", nullable: false),
                    SrvFirstInsPaymentDateTRIG = table.Column<DateOnly>(type: "date", nullable: true),
                    SrvFromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SrvGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    SrvK3FileInformation = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: true),
                    SrvModifier1 = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: true),
                    SrvModifier2 = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: true),
                    SrvModifier3 = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: true),
                    SrvModifier4 = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: true),
                    SrvNationalDrugCode = table.Column<string>(type: "varchar(48)", unicode: false, maxLength: 48, nullable: true),
                    SrvNonCoveredCharges = table.Column<decimal>(type: "money", nullable: false),
                    SrvPatBalanceReasonCode = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    SrvPlace = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: true),
                    SrvPrescriptionNumber = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    SrvPrintLineItem = table.Column<bool>(type: "bit", nullable: false),
                    SrvProcedureCode = table.Column<string>(type: "varchar(48)", unicode: false, maxLength: 48, nullable: true),
                    SrvProductCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    SrvRespChangeDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SrvResponsibleParty = table.Column<int>(type: "int", nullable: false),
                    SrvRevenueCode = table.Column<string>(type: "varchar(4)", unicode: false, maxLength: 4, nullable: true),
                    SrvSortTiebreaker = table.Column<int>(type: "int", nullable: false),
                    SrvStartTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SrvToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SrvTotalCOAdjTRIG = table.Column<decimal>(type: "money", nullable: false),
                    SrvTotalCRAdjTRIG = table.Column<decimal>(type: "money", nullable: false),
                    SrvTotalOAAdjTRIG = table.Column<decimal>(type: "money", nullable: false),
                    SrvTotalPIAdjTRIG = table.Column<decimal>(type: "money", nullable: false),
                    SrvTotalPRAdjTRIG = table.Column<decimal>(type: "money", nullable: false),
                    SrvTotalInsAmtPaidTRIG = table.Column<decimal>(type: "money", nullable: false),
                    SrvTotalPatAmtPaidTRIG = table.Column<decimal>(type: "money", nullable: false),
                    SrvUnits = table.Column<float>(type: "real", nullable: true),
                    SrvPerUnitChargesCC = table.Column<float>(type: "real", nullable: true, computedColumnSql: "(case when [SrvUnits]>(0) then [SrvCharges]/[SrvUnits] else (0) end)", stored: true),
                    SrvModifiersCC = table.Column<string>(type: "varchar(8)", unicode: false, maxLength: 8, nullable: false, computedColumnSql: "(((isnull([SrvModifier1],'')+isnull([SrvModifier2],''))+isnull([SrvModifier3],''))+isnull([SrvModifier4],''))", stored: true),
                    SrvRespDaysAgedCC = table.Column<int>(type: "int", nullable: true, computedColumnSql: "(datediff(day,[SrvRespChangeDate],sysutcdatetime()))", stored: false),
                    SrvTotalAdjCC = table.Column<decimal>(type: "money", nullable: true, computedColumnSql: "((((isnull([SrvTotalCOAdjTRIG],(0))+isnull([SrvTotalCRAdjTRIG],(0)))+isnull([SrvTotalOAAdjTRIG],(0)))+isnull([SrvTotalPIAdjTRIG],(0)))+isnull([SrvTotalPRAdjTRIG],(0)))", stored: true),
                    SrvTotalOtherAdjCC = table.Column<decimal>(type: "money", nullable: true, computedColumnSql: "(((isnull([SrvTotalCRAdjTRIG],(0))+isnull([SrvTotalOAAdjTRIG],(0)))+isnull([SrvTotalPIAdjTRIG],(0)))+isnull([SrvTotalPRAdjTRIG],(0)))", stored: true),
                    SrvTotalAmtAppliedCC = table.Column<decimal>(type: "money", nullable: true, computedColumnSql: "((((((isnull([SrvTotalPatAmtPaidTRIG],(0))+isnull([SrvTotalInsAmtPaidTRIG],(0)))+isnull([SrvTotalCOAdjTRIG],(0)))+isnull([SrvTotalCRAdjTRIG],(0)))+isnull([SrvTotalOAAdjTRIG],(0)))+isnull([SrvTotalPIAdjTRIG],(0)))+isnull([SrvTotalPRAdjTRIG],(0)))", stored: true),
                    SrvTotalAmtPaidCC = table.Column<decimal>(type: "money", nullable: true, computedColumnSql: "(isnull([SrvTotalPatAmtPaidTRIG],(0))+isnull([SrvTotalInsAmtPaidTRIG],(0)))", stored: true),
                    SrvTotalBalanceCC = table.Column<decimal>(type: "money", nullable: true, computedColumnSql: "((isnull([SrvCharges],(0))-(isnull([SrvTotalPatAmtPaidTRIG],(0))+isnull([SrvTotalInsAmtPaidTRIG],(0))))-((((isnull([SrvTotalCOAdjTRIG],(0))+isnull([SrvTotalCRAdjTRIG],(0)))+isnull([SrvTotalOAAdjTRIG],(0)))+isnull([SrvTotalPIAdjTRIG],(0)))+isnull([SrvTotalPRAdjTRIG],(0))))", stored: true),
                    SrvTotalInsBalanceCC = table.Column<decimal>(type: "money", nullable: true),
                    SrvTotalPatBalanceCC = table.Column<decimal>(type: "money", nullable: true),
                    SrvTotalMinutesCC = table.Column<int>(type: "int", nullable: true, computedColumnSql: "(case when [SrvStartTime] IS NOT NULL AND [SrvEndTime] IS NOT NULL then datediff(minute,[SrvStartTime],[SrvEndTime]) else (0) end)", stored: true),
                    SrvAdditionalData = table.Column<string>(type: "xml", nullable: true),
                    SrvNOCOverride = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Service___0367D191FA398414", x => x.SrvID);
                    table.UniqueConstraint("AK_Service_Line_SrvGUID", x => x.SrvGUID);
                    table.ForeignKey(
                        name: "FK_ServiceLine_Claim",
                        column: x => x.SrvClaFID,
                        principalTable: "Claim",
                        principalColumn: "ClaID");
                    table.ForeignKey(
                        name: "FK_ServiceLine_ResponsibleParty",
                        column: x => x.SrvResponsibleParty,
                        principalTable: "Payer",
                        principalColumn: "PayID");
                });

            migrationBuilder.CreateTable(
                name: "Adjustment",
                columns: table => new
                {
                    AdjID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdjDateTimeCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AdjDateTimeModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AdjCreatedUserGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AdjLastUserGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AdjCreatedUserName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    AdjLastUserName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    AdjCreatedComputerName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    AdjLastComputerName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    Adj835Ref = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    AdjAmount = table.Column<decimal>(type: "money", nullable: false),
                    AdjBatchOperationReference = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    AdjDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AdjGroupCode = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: false),
                    AdjNote = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    AdjOtherReference1 = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: true),
                    AdjPayFID = table.Column<int>(type: "int", nullable: false),
                    AdjPmtFID = table.Column<int>(type: "int", nullable: false),
                    AdjReasonAmount = table.Column<decimal>(type: "money", nullable: false),
                    AdjReasonCode = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    AdjRemarkCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    AdjSrvFID = table.Column<int>(type: "int", nullable: false),
                    AdjSrvGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdjTaskFID = table.Column<int>(type: "int", nullable: false),
                    AdjTrackOnly = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Adjustme__A065A852DF7CF3F7", x => x.AdjID);
                    table.ForeignKey(
                        name: "FK_Adjustment_Payer",
                        column: x => x.AdjPayFID,
                        principalTable: "Payer",
                        principalColumn: "PayID");
                    table.ForeignKey(
                        name: "FK_Adjustment_Payment",
                        column: x => x.AdjPmtFID,
                        principalTable: "Payment",
                        principalColumn: "PmtID");
                    table.ForeignKey(
                        name: "FK_Adjustment_ServiceLine_SrvGUID",
                        column: x => x.AdjSrvGUID,
                        principalTable: "Service_Line",
                        principalColumn: "SrvGUID");
                    table.ForeignKey(
                        name: "FK_Adjustment_ServiceLine_SrvID",
                        column: x => x.AdjSrvFID,
                        principalTable: "Service_Line",
                        principalColumn: "SrvID");
                    table.ForeignKey(
                        name: "FK_Adjustment_TaskSrv",
                        column: x => x.AdjTaskFID,
                        principalTable: "Service_Line",
                        principalColumn: "SrvID");
                });

            migrationBuilder.CreateTable(
                name: "Disbursement",
                columns: table => new
                {
                    DisbID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DisbDateTimeCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DisbDateTimeModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DisbCreatedUserGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DisbLastUserGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DisbCreatedUserName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    DisbLastUserName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    DisbCreatedComputerName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    DisbLastComputerName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    DisbAmount = table.Column<decimal>(type: "money", nullable: false),
                    DisbBatchOperationReference = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    DisbCode = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    DisbNote = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    DisbPmtFID = table.Column<int>(type: "int", nullable: false),
                    DisbSrvFID = table.Column<int>(type: "int", nullable: false),
                    DisbSrvGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Disburse__140396A9001F6084", x => x.DisbID);
                    table.ForeignKey(
                        name: "FK_Disbursement_Payment",
                        column: x => x.DisbPmtFID,
                        principalTable: "Payment",
                        principalColumn: "PmtID");
                    table.ForeignKey(
                        name: "FK_Disbursement_ServiceLine",
                        column: x => x.DisbSrvFID,
                        principalTable: "Service_Line",
                        principalColumn: "SrvID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Adjustment_AdjPayFID",
                table: "Adjustment",
                column: "AdjPayFID");

            migrationBuilder.CreateIndex(
                name: "IX_Adjustment_AdjPmtFID",
                table: "Adjustment",
                column: "AdjPmtFID");

            migrationBuilder.CreateIndex(
                name: "IX_Adjustment_AdjSrvFID",
                table: "Adjustment",
                column: "AdjSrvFID");

            migrationBuilder.CreateIndex(
                name: "IX_Adjustment_AdjSrvGUID",
                table: "Adjustment",
                column: "AdjSrvGUID");

            migrationBuilder.CreateIndex(
                name: "IX_Adjustment_AdjTaskFID",
                table: "Adjustment",
                column: "AdjTaskFID");

            migrationBuilder.CreateIndex(
                name: "IX_CityStateZip_State",
                table: "CityStateZipLibrary",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_Claim_ClaAttendingPhyFID",
                table: "Claim",
                column: "ClaAttendingPhyFID");

            migrationBuilder.CreateIndex(
                name: "IX_Claim_ClaBillingPhyFID",
                table: "Claim",
                column: "ClaBillingPhyFID");

            migrationBuilder.CreateIndex(
                name: "IX_Claim_ClaFacilityPhyFID",
                table: "Claim",
                column: "ClaFacilityPhyFID");

            migrationBuilder.CreateIndex(
                name: "IX_Claim_ClaOperatingPhyFID",
                table: "Claim",
                column: "ClaOperatingPhyFID");

            migrationBuilder.CreateIndex(
                name: "IX_Claim_ClaOrderingPhyFID",
                table: "Claim",
                column: "ClaOrderingPhyFID");

            migrationBuilder.CreateIndex(
                name: "IX_Claim_ClaPatFID",
                table: "Claim",
                column: "ClaPatFID");

            migrationBuilder.CreateIndex(
                name: "IX_Claim_ClaReferringPhyFID",
                table: "Claim",
                column: "ClaReferringPhyFID");

            migrationBuilder.CreateIndex(
                name: "IX_Claim_ClaRenderingPhyFID",
                table: "Claim",
                column: "ClaRenderingPhyFID");

            migrationBuilder.CreateIndex(
                name: "IX_Claim_ClaSupervisingPhyFID",
                table: "Claim",
                column: "ClaSupervisingPhyFID");

            migrationBuilder.CreateIndex(
                name: "IX_Claim_Insured_ClaInsClaFID",
                table: "Claim_Insured",
                column: "ClaInsClaFID");

            migrationBuilder.CreateIndex(
                name: "IX_Claim_Insured_ClaInsPatFID",
                table: "Claim_Insured",
                column: "ClaInsPatFID");

            migrationBuilder.CreateIndex(
                name: "IX_Claim_Insured_ClaInsPayFID",
                table: "Claim_Insured",
                column: "ClaInsPayFID");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimRejection_ClaimId",
                table: "ClaimRejection",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimRejection_EdiReportId",
                table: "ClaimRejection",
                column: "EdiReportId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimRejection_Status",
                table: "ClaimRejection",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimRejection_TransactionControlNumber",
                table: "ClaimRejection",
                column: "TransactionControlNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimSubmission_BatchId",
                table: "ClaimSubmission",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimSubmission_BatchId_TransactionControlNumber",
                table: "ClaimSubmission",
                columns: new[] { "BatchId", "TransactionControlNumber" },
                unique: true,
                filter: "[BatchId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimSubmission_ClaimId",
                table: "ClaimSubmission",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_ConnectionLibrary_Name",
                table: "ConnectionLibrary",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldDefinitions_EntityType_FieldKey",
                table: "CustomFieldDefinitions",
                columns: new[] { "EntityType", "FieldKey" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldValues_EntityType_EntityId_FieldKey",
                table: "CustomFieldValues",
                columns: new[] { "EntityType", "EntityId", "FieldKey" });

            migrationBuilder.CreateIndex(
                name: "IX_Diagnosis_Code_Code",
                table: "Diagnosis_Code",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_Disbursement_DisbPmtFID",
                table: "Disbursement",
                column: "DisbPmtFID");

            migrationBuilder.CreateIndex(
                name: "IX_Disbursement_DisbSrvFID",
                table: "Disbursement",
                column: "DisbSrvFID");

            migrationBuilder.CreateIndex(
                name: "IX_EdiReport_CreatedAt",
                table: "EdiReport",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_EdiReport_Status",
                table: "EdiReport",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_EligibilityRequest_PatientId",
                table: "EligibilityRequest",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_EligibilityRequest_PayerId",
                table: "EligibilityRequest",
                column: "PayerId");

            migrationBuilder.CreateIndex(
                name: "IX_EligibilityResponse_RequestId",
                table: "EligibilityResponse",
                column: "EligibilityRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_EraException_ClaimId",
                table: "EraException",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_EraException_EdiReportId",
                table: "EraException",
                column: "EdiReportId");

            migrationBuilder.CreateIndex(
                name: "IX_EraException_Status",
                table: "EraException",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Insured_InsPayID",
                table: "Insured",
                column: "InsPayID");

            migrationBuilder.CreateIndex(
                name: "IX_Modifier_Code_Code",
                table: "Modifier_Code",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_Patient_PatBillingPhyFID",
                table: "Patient",
                column: "PatBillingPhyFID");

            migrationBuilder.CreateIndex(
                name: "IX_Patient_PatFacilityPhyFID",
                table: "Patient",
                column: "PatFacilityPhyFID");

            migrationBuilder.CreateIndex(
                name: "IX_Patient_PatOrderingPhyFID",
                table: "Patient",
                column: "PatOrderingPhyFID");

            migrationBuilder.CreateIndex(
                name: "IX_Patient_PatReferringPhyFID",
                table: "Patient",
                column: "PatReferringPhyFID");

            migrationBuilder.CreateIndex(
                name: "IX_Patient_PatRenderingPhyFID",
                table: "Patient",
                column: "PatRenderingPhyFID");

            migrationBuilder.CreateIndex(
                name: "IX_Patient_PatSupervisingPhyFID",
                table: "Patient",
                column: "PatSupervisingPhyFID");

            migrationBuilder.CreateIndex(
                name: "IX_Patient_Insured_PatInsInsGUID",
                table: "Patient_Insured",
                column: "PatInsInsGUID");

            migrationBuilder.CreateIndex(
                name: "IX_Patient_Insured_PatInsPatFID",
                table: "Patient_Insured",
                column: "PatInsPatFID");

            migrationBuilder.CreateIndex(
                name: "IX_Payment_PmtBFEPFID",
                table: "Payment",
                column: "PmtBFEPFID");

            migrationBuilder.CreateIndex(
                name: "IX_Payment_PmtPatFID",
                table: "Payment",
                column: "PmtPatFID");

            migrationBuilder.CreateIndex(
                name: "IX_Payment_PmtPayFID",
                table: "Payment",
                column: "PmtPayFID");

            migrationBuilder.CreateIndex(
                name: "IX_Place_of_Service_Code",
                table: "Place_of_Service",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_Procedure_Code_ProcBillingPhyFID",
                table: "Procedure_Code",
                column: "ProcBillingPhyFID");

            migrationBuilder.CreateIndex(
                name: "IX_Procedure_Code_ProcCode_ProcProductCode",
                table: "Procedure_Code",
                columns: new[] { "ProcCode", "ProcProductCode" },
                unique: true,
                filter: "[ProcProductCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Procedure_Code_ProcPayFID",
                table: "Procedure_Code",
                column: "ProcPayFID");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramSettings_Section",
                table: "ProgramSettings",
                column: "Section");

            migrationBuilder.CreateIndex(
                name: "IX_Reason_Code_Code",
                table: "Reason_Code",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiverLibrary_LibraryEntryName",
                table: "ReceiverLibrary",
                column: "LibraryEntryName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Remark_Code_Code",
                table: "Remark_Code",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_ScrubRule_IsActive",
                table: "ScrubRule",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ScrubRule_PayerId",
                table: "ScrubRule",
                column: "PayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Service_Line_SrvClaFID",
                table: "Service_Line",
                column: "SrvClaFID");

            migrationBuilder.CreateIndex(
                name: "IX_Service_Line_SrvResponsibleParty",
                table: "Service_Line",
                column: "SrvResponsibleParty");

            migrationBuilder.CreateIndex(
                name: "UQ_ServiceLine_SrvGUID",
                table: "Service_Line",
                column: "SrvGUID",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Adjustment");

            migrationBuilder.DropTable(
                name: "AppUser");

            migrationBuilder.DropTable(
                name: "CityStateZipLibrary");

            migrationBuilder.DropTable(
                name: "Claim_Audit");

            migrationBuilder.DropTable(
                name: "Claim_Insured");

            migrationBuilder.DropTable(
                name: "ClaimRejection");

            migrationBuilder.DropTable(
                name: "ClaimSubmission");

            migrationBuilder.DropTable(
                name: "ClaimTemplates");

            migrationBuilder.DropTable(
                name: "ConnectionLibrary");

            migrationBuilder.DropTable(
                name: "CustomFieldDefinitions");

            migrationBuilder.DropTable(
                name: "CustomFieldValues");

            migrationBuilder.DropTable(
                name: "Diagnosis_Code");

            migrationBuilder.DropTable(
                name: "Disbursement");

            migrationBuilder.DropTable(
                name: "EdiReport");

            migrationBuilder.DropTable(
                name: "EligibilityRequest");

            migrationBuilder.DropTable(
                name: "EligibilityResponse");

            migrationBuilder.DropTable(
                name: "EraException");

            migrationBuilder.DropTable(
                name: "Hl7_Import_Log");

            migrationBuilder.DropTable(
                name: "Interface_Import_Log");

            migrationBuilder.DropTable(
                name: "ListValue");

            migrationBuilder.DropTable(
                name: "Modifier_Code");

            migrationBuilder.DropTable(
                name: "Patient_Insured");

            migrationBuilder.DropTable(
                name: "Place_of_Service");

            migrationBuilder.DropTable(
                name: "Procedure_Code");

            migrationBuilder.DropTable(
                name: "ProgramSettings");

            migrationBuilder.DropTable(
                name: "Reason_Code");

            migrationBuilder.DropTable(
                name: "ReceiverLibrary");

            migrationBuilder.DropTable(
                name: "Remark_Code");

            migrationBuilder.DropTable(
                name: "ScrubRule");

            migrationBuilder.DropTable(
                name: "SecondaryForwardableAdjustmentRules");

            migrationBuilder.DropTable(
                name: "Payment");

            migrationBuilder.DropTable(
                name: "Service_Line");

            migrationBuilder.DropTable(
                name: "Insured");

            migrationBuilder.DropTable(
                name: "Claim");

            migrationBuilder.DropTable(
                name: "Payer");

            migrationBuilder.DropTable(
                name: "Patient");

            migrationBuilder.DropTable(
                name: "Physician");
        }
    }
}
