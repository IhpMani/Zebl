namespace Zebl.Application.Edi.Parsing;

/// <summary>
/// AK9 functional group status line.
/// </summary>
public sealed class Edi999Ak9Line
{
    public string? FunctionalGroupAcknowledgeCode { get; init; }
    public string? IncludedTransactionSets { get; init; }
    public string? ReceivedTransactionSets { get; init; }
    public string? AcceptedTransactionSets { get; init; }
}

