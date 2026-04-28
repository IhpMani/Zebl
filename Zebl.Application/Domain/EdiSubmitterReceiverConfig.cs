namespace Zebl.Application.Domain;

public class EdiSubmitterReceiverConfig
{
    public Guid Id { get; set; }
    public string? SubmitterName { get; set; }
    public string? SubmitterId { get; set; }
    public string? ReceiverName { get; set; }
    public string? ReceiverId { get; set; }
    public string? AuthorizationInfoQualifier { get; set; }
    public string? AuthorizationInfo { get; set; }
    public string? SecurityInfoQualifier { get; set; }
    public string? SecurityInfo { get; set; }
    public string? SenderQualifier { get; set; }
    public string? SenderId { get; set; }
    public string? ReceiverQualifier { get; set; }
    public string? InterchangeReceiverId { get; set; }
    public string? SenderCode { get; set; }
    public string? ReceiverCode { get; set; }
    public string? TestProdIndicator { get; set; }

    public static EdiSubmitterReceiverConfig FromReceiverLibrary(ReceiverLibrary selected)
    {
        return new EdiSubmitterReceiverConfig
        {
            Id = selected.Id,
            SubmitterName = selected.BusinessOrLastName,
            SubmitterId = selected.SubmitterId,
            ReceiverName = selected.ReceiverName,
            ReceiverId = selected.ReceiverId,
            AuthorizationInfoQualifier = selected.AuthorizationInfoQualifier,
            AuthorizationInfo = selected.AuthorizationInfo,
            SecurityInfoQualifier = selected.SecurityInfoQualifier,
            SecurityInfo = selected.SecurityInfo,
            SenderQualifier = selected.SenderQualifier,
            SenderId = selected.SenderId,
            ReceiverQualifier = selected.ReceiverQualifier,
            InterchangeReceiverId = selected.InterchangeReceiverId,
            SenderCode = selected.SenderCode,
            ReceiverCode = selected.ReceiverCode,
            TestProdIndicator = selected.TestProdIndicator
        };
    }
}
