using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace Zebl.Application.Dtos.Adjustments
{
    public class AdjustmentListItemDto
    {
        [Required]
        public int AdjID { get; set; }
        
        public DateTime AdjDateTimeCreated { get; set; }
        
        public DateOnly? AdjDate { get; set; }
        
        public decimal AdjAmount { get; set; }
        
        public string AdjGroupCode { get; set; } = null!;
        
        public string? AdjReasonCode { get; set; }
        
        public string? AdjNote { get; set; }
        
        public int AdjSrvFID { get; set; }
        
        public int AdjPmtFID { get; set; }
        
        public int AdjPayFID { get; set; }
        
        public string? Adj835Ref { get; set; }
        
        public Dictionary<string, object?>? AdditionalColumns { get; set; }
    }
}
