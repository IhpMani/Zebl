namespace Zebl.Application.Dtos.Claims;

public class UpdateClaimRequest
{
    /// <summary>Claim Status - values from Libraries → List → Claim Status</summary>
    public string? ClaStatus { get; set; }
    /// <summary>Facility / Claim Classification - values from Libraries → List → Claim Classification</summary>
    public string? ClaClassification { get; set; }
    
    public DateTime? ClaAdmittedDate { get; set; }
    public DateTime? ClaDischargedDate { get; set; }
    public DateTime? ClaDateLastSeen { get; set; }
    public string? ClaEDINotes { get; set; }
    public string? ClaRemarks { get; set; }
    public int? ClaRelatedTo { get; set; }
    public string? ClaRelatedToState { get; set; }
    public bool? ClaLocked { get; set; }
}
