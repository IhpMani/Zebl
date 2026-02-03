using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace Zebl.Application.Dtos.Disbursements
{
    public class DisbursementListItemDto
    {
        [Required]
        public int DisbID { get; set; }
        
        public DateTime DisbDateTimeCreated { get; set; }
        
        public decimal DisbAmount { get; set; }
        
        public int DisbPmtFID { get; set; }
        
        public int DisbSrvFID { get; set; }
        
        public string? DisbCode { get; set; }
        
        public string? DisbNote { get; set; }
        
        public Dictionary<string, object?>? AdditionalColumns { get; set; }
    }
}
