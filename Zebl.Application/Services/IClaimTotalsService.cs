using Zebl.Application.Domain;

namespace Zebl.Application.Services;

/// <summary>
/// Domain service: recalculates claim totals from service line totals (no SQL).
/// </summary>
public interface IClaimTotalsService
{
    /// <summary>
    /// Aggregate service line totals into claim totals.
    /// </summary>
    ClaimTotals RecalculateFromServiceLines(IEnumerable<ServiceLineTotals> serviceLines);
}
