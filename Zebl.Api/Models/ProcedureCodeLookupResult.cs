using Zebl.Application.Domain;

namespace Zebl.Api.Models;

/// <summary>
/// Result of procedure code lookup for claim entry.
/// Includes overwrite rules for applying library values to a service line.
/// </summary>
public class ProcedureCodeLookupResult
{
    /// <summary>
    /// The matched procedure code (serializes as full entity at runtime).
    /// </summary>
    public IProcedureCode? ProcedureCode { get; set; }

    public bool OverwriteCharge { get; set; }
    public bool OverwriteAllowed { get; set; }
    public bool OverwriteAdjustment { get; set; }
    public string? NocDescription { get; set; }
}
