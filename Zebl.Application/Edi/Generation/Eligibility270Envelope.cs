namespace Zebl.Application.Edi.Generation;

/// <summary>
/// Normalized inputs for a 270 eligibility inquiry interchange (no EF entities).
/// </summary>
public sealed class Eligibility270Envelope
{
    public required string InterchangeControlNumber { get; init; }
    public required string GroupControlNumber { get; init; }
    public required string TransactionSetControlNumber { get; init; }

    public required string AuthInfoQualifier { get; init; }
    public required string SecurityInfoQualifier { get; init; }
    public required string SenderQualifier { get; init; }
    public required string SenderId { get; init; }
    public required string ReceiverQualifier { get; init; }
    public required string InterchangeReceiverId { get; init; }
    public required string TestProdIndicator { get; init; }
    public required string GsSender { get; init; }
    public required string GsReceiver { get; init; }
    public required string SubmitterId { get; init; }
    public required string ReceiverName { get; init; }
    public required string ReceiverId { get; init; }
    public required string ProviderName { get; init; }
    public required string ProviderNpi { get; init; }
    public required string SubscriberLastName { get; init; }
    public required string SubscriberFirstName { get; init; }
    public required string SubscriberMemberId { get; init; }
    public required string PayerEligibilityId { get; init; }
    public required DateOnly PatientBirthDate { get; init; }
    public string? PatientSex { get; init; }
}
