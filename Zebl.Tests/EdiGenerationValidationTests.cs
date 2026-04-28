using Zebl.Application.Domain;
using Zebl.Application.Dtos.Claims;
using Zebl.Application.Edi.Generation;
using Zebl.Application.Services.Edi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Zebl.Tests;

public class EdiGenerationValidationTests
{
    [Fact]
    public void Build837_ProducesValidEnvelopeCountsAndControlNumbers()
    {
        var ctx = new Claim837EdiContext
        {
            Data = new Claim837ExportData
            {
                ClaimId = 101,
                ClaEdiClaimId = "101",
                PrimaryInsured = new ClaimInsuredExportDto
                {
                    ClaInsFirstName = "JOHN",
                    ClaInsLastName = "DOE",
                    ClaInsGroupNumber = "GRP1",
                    ClaInsBirthDate = new DateOnly(1980, 1, 1),
                    ClaInsSex = "M"
                },
                Patient = new PatientExportDto
                {
                    PatFirstName = "JOHN",
                    PatLastName = "DOE",
                    PatBirthDate = new DateOnly(1980, 1, 1),
                    PatSex = "M"
                },
                BillingProvider = new ProviderExportDto
                {
                    PhyFirstName = "BILL",
                    PhyLastName = "PROVIDER",
                    PhyNPI = "1234567893"
                },
                ServiceLines =
                {
                    new ServiceLine837ExportDto
                    {
                        SrvID = 1,
                        SrvProcedureCode = "99213",
                        SrvCharges = 100,
                        SrvUnits = 1,
                        SrvFromDate = new DateOnly(2026, 4, 20)
                    }
                }
            },
            Payer = new Payer
            {
                PayName = "PAYER",
                PayAddr1 = "ADDR",
                PayCity = "CITY",
                PayState = "ST",
                PayZip = "12345",
                PayClaimType = "HCFA",
                PaySubmissionMethod = "EDI"
            },
            ClaimFilingIndicator = "CI",
            InsuranceTypeCode = "12"
        };

        var cfg = new EdiSubmitterReceiverConfig
        {
            SubmitterName = "SUBMITTER",
            SubmitterId = "SUBID",
            ReceiverName = "RECEIVER",
            ReceiverId = "RCVID",
            AuthorizationInfoQualifier = "00",
            SecurityInfoQualifier = "00",
            SenderQualifier = "ZZ",
            SenderId = "SENDERID",
            ReceiverQualifier = "ZZ",
            InterchangeReceiverId = "RECEIVERID",
            SenderCode = "SENDERCODE",
            ReceiverCode = "RECEIVERCODE",
            TestProdIndicator = "T"
        };

        var controls = new EdiControlNumbers
        {
            InterchangeControlNumber = "123",
            GroupControlNumber = "456",
            TransactionControlNumber = "789"
        };

        var edi = Claim837Builder.BuildInterchange(ctx, cfg, controls);
        var validator = new EdiValidationService(new NullLogger<EdiValidationService>());
        validator.Validate(edi, OutboundEdiKind.Claim837);

        Assert.EndsWith("~", edi);
        Assert.Contains("ISA*", edi);
        Assert.Contains("GS*HC*", edi);
        Assert.Contains("ST*837*", edi);
    }
}

