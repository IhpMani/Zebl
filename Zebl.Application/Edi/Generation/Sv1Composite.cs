namespace Zebl.Application.Edi.Generation;

/// <summary>
/// Structured composite for SV1-01 professional service element.
/// </summary>
public sealed class Sv1Composite
{
    public required string ProductOrServiceIdQualifier { get; init; }
    public required string ProcedureCode { get; init; }
    public string Modifier1 { get; init; } = "";
    public string Modifier2 { get; init; } = "";
    public string Modifier3 { get; init; } = "";
    public string Modifier4 { get; init; } = "";
}
