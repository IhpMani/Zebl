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
        
        public string? SrvModifier1 { get; set; }

        public string? SrvModifier2 { get; set; }

        public string? SrvModifier3 { get; set; }

        public string? SrvModifier4 { get; set; }

        public string? SrvDesc { get; set; }
        
        public decimal SrvCharges { get; set; }

        public decimal SrvAllowedAmt { get; set; }
        
        public float? SrvUnits { get; set; }
        
        public decimal SrvTotalInsAmtPaidTRIG { get; set; }

        public decimal SrvTotalPatAmtPaidTRIG { get; set; }

        public decimal? SrvTotalBalanceCC { get; set; }

        public decimal? SrvTotalAmtAppliedCC { get; set; }
        
        public decimal? SrvTotalAmtPaidCC { get; set; }

        public int SrvResponsibleParty { get; set; }

        public string? SrvNationalDrugCode { get; set; }

        public double? SrvDrugUnitCount { get; set; }

        public string? SrvDrugUnitMeasurement { get; set; }

        public string? SrvPrescriptionNumber { get; set; }

        public string? SrvRevenueCode { get; set; }
        
        public Dictionary<string, object?>? AdditionalColumns { get; set; }
    }
}
