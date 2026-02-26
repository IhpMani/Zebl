namespace Zebl.Application.Dtos.Payments;

/// <summary>
/// One row for the payment entry grid: service line with display fields and balance.
/// </summary>
public class PaymentEntryServiceLineDto
{
    public int ServiceLineId { get; set; }
    public string? Name { get; set; }
    public string? Dos { get; set; }
    public string? Proc { get; set; }
    public decimal Charge { get; set; }
    public string? Responsible { get; set; }
    public decimal Applied { get; set; }
    public decimal Balance { get; set; }
}
