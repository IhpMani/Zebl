namespace Zebl.Infrastructure.Persistence.Entities;

/// <summary>
/// Entity for claim-specific activity only.
/// Does NOT include interface import logs - those go to Interface_Import_Log.
/// Activity types: Claim Created, Claim Edited, Payment Applied, Manual Notes
/// </summary>
public partial class Claim_Audit
{
    public int AuditID { get; set; }

    public int ClaFID { get; set; }

    public string ActivityType { get; set; } = null!;

    public DateTime ActivityDate { get; set; }

    public string? UserName { get; set; }

    public string? ComputerName { get; set; }

    public string? Notes { get; set; }

    /// <summary>Financial snapshot: ClaTotalChargeTRIG at time of activity.</summary>
    public decimal? TotalCharge { get; set; }

    /// <summary>Financial snapshot: ClaTotalInsBalanceTRIG at time of activity.</summary>
    public decimal? InsuranceBalance { get; set; }

    /// <summary>Financial snapshot: ClaTotalPatBalanceTRIG at time of activity.</summary>
    public decimal? PatientBalance { get; set; }
}
