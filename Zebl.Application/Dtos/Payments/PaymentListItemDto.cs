using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace Zebl.Application.Dtos.Payments
{
    public class PaymentListItemDto
    {
        [Required]
        public int PmtID { get; set; }
        
        public DateTime PmtDateTimeCreated { get; set; }
        
        public DateOnly PmtDate { get; set; }
        
        public decimal PmtAmount { get; set; }
        
        public int PmtPatFID { get; set; }
        
        public int? PmtPayFID { get; set; }
        
        public string? PmtMethod { get; set; }
        
        public string? Pmt835Ref { get; set; }
        
        public decimal PmtDisbursedTRIG { get; set; }
        
        public decimal? PmtRemainingCC { get; set; }
        
        public Dictionary<string, object?>? AdditionalColumns { get; set; }
    }
}
