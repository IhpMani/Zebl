namespace Zebl.Application.Dtos.Adjustments
{
    public class AdjustmentDto
    {
        public int AdjID { get; set; }
        public string? AdjGroupCode { get; set; }
        public string? AdjReasonCode { get; set; }
        public decimal? AdjReasonAmount { get; set; }
        public string? AdjNote { get; set; }
    }
}
