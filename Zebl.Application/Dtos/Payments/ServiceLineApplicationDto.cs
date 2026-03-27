using System.Text.Json.Serialization;

namespace Zebl.Application.Dtos.Payments;

/// <summary>
/// Application of payment (and adjustments) to a single service line.
/// </summary>
public class ServiceLineApplicationDto
{
    public int ServiceLineId { get; set; }
    public decimal PaymentAmount { get; set; }
    // Backward-compatible alias: some clients send "amount" instead of "paymentAmount".
    [JsonPropertyName("amount")]
    public decimal Amount
    {
        get => PaymentAmount;
        set => PaymentAmount = value;
    }
    public List<AdjustmentInputDto> Adjustments { get; set; } = new();
}
