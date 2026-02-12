using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace Zebl.Application.Dtos.Payers
{
    public class PayerListItemDto
    {
        [Required]
        public int PayID { get; set; }
        
        public DateTime PayDateTimeCreated { get; set; }
        
        public string? PayName { get; set; }
        
        public string? PayClassification { get; set; }
        
        public string PayClaimType { get; set; } = null!;
        
        public string? PayExternalID { get; set; }
        
        public string? PayAddr1 { get; set; }
        
        public string? PayCity { get; set; }
        
        public string? PayState { get; set; }
        
        public string? PayZip { get; set; }
        
        public string? PayPhoneNo { get; set; }
        
        public string? PayEmail { get; set; }
        
        public bool PayInactive { get; set; }
        
        public string PaySubmissionMethod { get; set; } = null!;
        
        public Dictionary<string, object?>? AdditionalColumns { get; set; }
    }
}
