namespace Zebl.Application.Domain;

public class EligibilityResponse
{
    public int Id { get; set; }

    public int EligibilityRequestId { get; set; }

    public string? CoverageStatus { get; set; }

    public string? PlanName { get; set; }

    public decimal? DeductibleAmount { get; set; }

    public decimal? CopayAmount { get; set; }

    public decimal? CoinsurancePercent { get; set; }

    public DateTime? CoverageStartDate { get; set; }

    public DateTime? CoverageEndDate { get; set; }

    public string Raw271 { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

