namespace Zebl.Application.Dtos.Payments
{
    public class PaymentDto
    {
        public int PmtID { get; set; }
        public DateTime? PmtDate { get; set; }
        public decimal? PmtAmount { get; set; }
        public string? PmtMethod { get; set; }
        public decimal? PmtRemainingCC { get; set; }
        public string? PmtNote { get; set; }
    }
}
