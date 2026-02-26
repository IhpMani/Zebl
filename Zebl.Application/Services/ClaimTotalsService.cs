using Zebl.Application.Domain;

namespace Zebl.Application.Services;

/// <summary>
/// Recalculates claim totals from service line totals. No SQL.
/// </summary>
public class ClaimTotalsService : IClaimTotalsService
{
    public ClaimTotals RecalculateFromServiceLines(IEnumerable<ServiceLineTotals> serviceLines)
    {
        var list = serviceLines.ToList();
        return new ClaimTotals
        {
            TotalCharge = list.Sum(s => s.Charges),
            TotalInsAmtPaid = list.Sum(s => s.TotalInsAmtPaid),
            TotalPatAmtPaid = list.Sum(s => s.TotalPatAmtPaid),
            TotalCOAdj = list.Sum(s => s.TotalCOAdj),
            TotalCRAdj = list.Sum(s => s.TotalCRAdj),
            TotalOAAdj = list.Sum(s => s.TotalOAAdj),
            TotalPIAdj = list.Sum(s => s.TotalPIAdj),
            TotalPRAdj = list.Sum(s => s.TotalPRAdj)
        };
    }
}
