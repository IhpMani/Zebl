namespace Zebl.Application.Dtos.Claims
{
    public class ClaimListItemDto
    {
        public int ClaID { get; set; }
        public string? ClaStatus { get; set; }
        public DateTime? ClaDateTimeCreated { get; set; }
        public decimal? ClaTotalChargeTRIG { get; set; }
        public decimal? ClaTotalAmtPaidCC { get; set; }
        public decimal? ClaTotalBalanceCC { get; set; }
    }
}
