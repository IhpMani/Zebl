namespace Zebl.Application.Dtos.Payments;

/// <summary>
/// Application of payment (and adjustments) to a single service line.
/// </summary>
public class ServiceLineApplicationDto
{
    public int ServiceLineId { get; set; }
    public decimal PaymentAmount { get; set; }
    public List<AdjustmentInputDto> Adjustments { get; set; } = new();
}
