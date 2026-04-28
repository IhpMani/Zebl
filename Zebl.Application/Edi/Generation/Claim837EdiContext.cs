using Zebl.Application.Domain;
using Zebl.Application.Dtos.Claims;

namespace Zebl.Application.Edi.Generation;

/// <summary>
/// All inputs required to build an 837 interchange from claim-side data (no DB access in builders).
/// </summary>
public sealed class Claim837EdiContext
{
    public required Claim837ExportData Data { get; init; }
    public required Payer Payer { get; init; }
    public required string ClaimFilingIndicator { get; init; }
    public string? InsuranceTypeCode { get; init; }
}
