using Zebl.Application.Domain;
using Zebl.Application.Dtos.Claims;

namespace Zebl.Application.Edi.Generation;

public static class Eligibility270EnvelopeMappers
{
    public static Eligibility270Envelope FromReceiverAndClaim837Export(
        ReceiverLibrary receiver,
        Claim837ExportData data,
        string interchangeControl,
        string groupControl,
        string setControl)
    {
        if (data.Payer == null)
            throw new InvalidOperationException("Payer is required for 270 generation.");
        if (data.Patient == null)
            throw new InvalidOperationException("Patient is required for 270 generation.");
        if (data.PrimaryInsured == null)
            throw new InvalidOperationException("Primary insured is required for 270 generation.");
        if (data.BillingProvider == null)
            throw new InvalidOperationException("Billing provider is required for 270 generation.");

        var payerEligibilityId = Required(data.Payer.PayEligibilityPayerID, "Payer eligibility ID");
        var submitterId = Required(receiver.SubmitterId, "Submitter ID");
        var receiverName = Required(receiver.ReceiverName, "Receiver name");
        var receiverId = Required(receiver.ReceiverId, "Receiver ID");
        var senderQualifier = Required(receiver.SenderQualifier, "ISA sender qualifier");
        var senderId = Required(receiver.SenderId, "ISA sender ID");
        var receiverQualifier = Required(receiver.ReceiverQualifier, "ISA receiver qualifier");
        var interchangeReceiverId = Required(receiver.InterchangeReceiverId, "ISA receiver ID");
        var authQualifier = Required(receiver.AuthorizationInfoQualifier, "ISA authorization qualifier");
        var secQualifier = Required(receiver.SecurityInfoQualifier, "ISA security qualifier");
        var gsSender = string.IsNullOrWhiteSpace(receiver.SenderCode) ? submitterId : receiver.SenderCode!;
        var gsReceiver = string.IsNullOrWhiteSpace(receiver.ReceiverCode) ? receiverId : receiver.ReceiverCode!;
        var testProd = string.IsNullOrWhiteSpace(receiver.TestProdIndicator) ? "T" : receiver.TestProdIndicator!;

        var providerName = Required(data.BillingProvider.PhyLastName ?? data.BillingProvider.PhyName, "Provider name");
        var subscriberLast = Required(data.PrimaryInsured.ClaInsLastName, "Subscriber last name");
        var subscriberFirst = Required(data.PrimaryInsured.ClaInsFirstName, "Subscriber first name");
        var subscriberMemberId = Required(data.PrimaryInsured.ClaInsIDNumber, "Subscriber ID");
        var patDob = data.Patient.PatBirthDate ?? throw new InvalidOperationException("Patient date of birth is required for 270 generation.");

        var ic = interchangeControl.Length > 9 ? interchangeControl[..9] : interchangeControl.PadLeft(9, '0');

        return new Eligibility270Envelope
        {
            InterchangeControlNumber = ic,
            GroupControlNumber = groupControl,
            TransactionSetControlNumber = setControl,
            AuthInfoQualifier = authQualifier,
            SecurityInfoQualifier = secQualifier,
            SenderQualifier = senderQualifier,
            SenderId = senderId,
            ReceiverQualifier = receiverQualifier,
            InterchangeReceiverId = interchangeReceiverId,
            TestProdIndicator = testProd,
            GsSender = gsSender,
            GsReceiver = gsReceiver,
            SubmitterId = submitterId,
            ReceiverName = receiverName,
            ReceiverId = receiverId,
            ProviderName = providerName,
            ProviderNpi = Required(data.BillingProvider.PhyNPI, "Provider NPI"),
            SubscriberLastName = subscriberLast,
            SubscriberFirstName = subscriberFirst,
            SubscriberMemberId = subscriberMemberId,
            PayerEligibilityId = payerEligibilityId,
            PatientBirthDate = patDob,
            PatientSex = data.Patient.PatSex
        };
    }

    private static string Required(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{fieldName} is required.");
        return value.Trim();
    }
}
