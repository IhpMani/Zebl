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
        
        // Additional columns from related tables (key-value pairs)
        public Dictionary<string, object?>? AdditionalColumns { get; set; }
    }
}
