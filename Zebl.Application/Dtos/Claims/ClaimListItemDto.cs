using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Zebl.Application.Dtos.Claims
{
    public class ClaimListItemDto
    {
        [Required]
        public int ClaID { get; set; }
        
        [MaxLength(20)]
        public string? ClaStatus { get; set; }
        
        public DateTime? ClaDateTimeCreated { get; set; }
        
        [Range(0, double.MaxValue, ErrorMessage = "Total charge must be non-negative")]
        public decimal? ClaTotalChargeTRIG { get; set; }
        
        [Range(0, double.MaxValue, ErrorMessage = "Total amount paid must be non-negative")]
        public decimal? ClaTotalAmtPaidCC { get; set; }
        
        public decimal? ClaTotalBalanceCC { get; set; }
        
        // Important columns
        public string? ClaClassification { get; set; }
        public int ClaPatFID { get; set; }
        public int ClaAttendingPhyFID { get; set; }
        public int ClaBillingPhyFID { get; set; }
        public int ClaReferringPhyFID { get; set; }
        public DateOnly? ClaBillDate { get; set; }
        public string? ClaTypeOfBill { get; set; }
        public string? ClaAdmissionType { get; set; }
        public string? ClaPatientStatus { get; set; }
        public string? ClaDiagnosis1 { get; set; }
        public string? ClaDiagnosis2 { get; set; }
        public string? ClaDiagnosis3 { get; set; }
        public string? ClaDiagnosis4 { get; set; }
        public DateOnly? ClaFirstDateTRIG { get; set; }
        public DateOnly? ClaLastDateTRIG { get; set; }
        
        // Additional columns from related tables (key-value pairs)
        public Dictionary<string, object?>? AdditionalColumns { get; set; }
    }
}
