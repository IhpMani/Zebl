using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace Zebl.Application.Dtos.Services
{
    public class ServiceListItemDto
    {
        [Required]
        public int SrvID { get; set; }
        
        public int? SrvClaFID { get; set; }
        
        public DateTime SrvDateTimeCreated { get; set; }
        
        public DateOnly SrvFromDate { get; set; }
        
        public DateOnly SrvToDate { get; set; }
        
        public string? SrvProcedureCode { get; set; }
        
        public string? SrvDesc { get; set; }
        
        public decimal SrvCharges { get; set; }
        
        public float? SrvUnits { get; set; }
        
        public decimal? SrvTotalBalanceCC { get; set; }
        
        public decimal? SrvTotalAmtPaidCC { get; set; }
        
        public Dictionary<string, object?>? AdditionalColumns { get; set; }
    }
}
