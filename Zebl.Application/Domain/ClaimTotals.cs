namespace Zebl.Application.Domain;

/// <summary>
/// Recalculated claim totals from service lines (domain service output).
/// </summary>
public class ClaimTotals
{
    public decimal TotalCharge { get; set; }
    public decimal TotalInsAmtPaid { get; set; }
    public decimal TotalPatAmtPaid { get; set; }
    public decimal TotalCOAdj { get; set; }
    public decimal TotalCRAdj { get; set; }
    public decimal TotalOAAdj { get; set; }
    public decimal TotalPIAdj { get; set; }
    public decimal TotalPRAdj { get; set; }
}
