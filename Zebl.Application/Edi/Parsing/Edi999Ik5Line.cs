namespace Zebl.Application.Edi.Parsing;

/// <summary>
/// IK5 — implementation acknowledgment segment at transaction set level.
/// </summary>
public sealed class Edi999Ik5Line
{
    public string? TransactionSetAcknowledgmentCode { get; init; }
    public string? ImplementationTransactionSetSyntaxErrorCode { get; init; }
    public string? ErrorDescription { get; init; }
}
