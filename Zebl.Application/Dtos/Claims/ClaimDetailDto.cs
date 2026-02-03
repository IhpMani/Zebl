namespace Zebl.Application.Dtos.Claims
{
    public class ClaimDetailDto
    {
        public int ClaID { get; set; }
        public int ClaPatFID { get; set; }
        public string? ClaStatus { get; set; }
        public DateTime? ClaDateTimeCreated { get; set; }
        public decimal? ClaTotalChargeTRIG { get; set; }
        public decimal? ClaTotalAmtPaidCC { get; set; }
        public decimal? ClaTotalBalanceCC { get; set; }

        public PatientDto? Patient { get; set; }
        public List<ServiceLineDto> ServiceLines { get; set; } = new();
    }

    public class PatientDto
    {
        public int PatID { get; set; }
        public string? PatFirstName { get; set; }
        public string? PatLastName { get; set; }
        public DateTime? PatBirthDate { get; set; }
    }

    public class ServiceLineDto
    {
        public int SrvID { get; set; }
        public DateTime? SrvFromDate { get; set; }
        public DateTime? SrvToDate { get; set; }
        public string? SrvProcedureCode { get; set; }
        public string? SrvDesc { get; set; }
        public decimal? SrvCharges { get; set; }
        public decimal? SrvUnits { get; set; }
        public decimal? SrvTotalBalanceCC { get; set; }
    }
}
