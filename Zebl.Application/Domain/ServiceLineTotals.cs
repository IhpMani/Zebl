namespace Zebl.Application.Domain;

/// <summary>
/// Service line totals for recalculation (charges, paid, adjustments).
/// </summary>
public class ServiceLineTotals
{
    public int SrvID { get; set; }
    public Guid SrvGUID { get; set; }
    public int? ClaID { get; set; }
    public decimal Charges { get; set; }
    public decimal TotalInsAmtPaid { get; set; }
    public decimal TotalPatAmtPaid { get; set; }
    public decimal TotalCOAdj { get; set; }
    public decimal TotalCRAdj { get; set; }
    public decimal TotalOAAdj { get; set; }
    public decimal TotalPIAdj { get; set; }
    public decimal TotalPRAdj { get; set; }
}
