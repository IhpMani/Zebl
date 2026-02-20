using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace Zebl.Application.Dtos.Payments
{
    /// <summary>
    /// Payment list row: Payment table + Patient (Account #, Name, Pat Classification) + Payer (Payer Name, Pay Classification).
    /// </summary>
    public class PaymentListItemDto
    {
        [Required]
        public int PmtID { get; set; }
        public DateTime PmtDateTimeCreated { get; set; }
        public DateTime PmtDateTimeModified { get; set; }
        public string? PmtCreatedUserName { get; set; }
        public string? PmtLastUserName { get; set; }
        public DateOnly PmtDate { get; set; }
        public decimal PmtAmount { get; set; }
        public decimal? PmtRemainingCC { get; set; }
        public decimal PmtChargedPlatformFee { get; set; }
        public string? PmtMethod { get; set; }
        public string? PmtNote { get; set; }
        public string? Pmt835Ref { get; set; }
        public string? PmtOtherReference1 { get; set; }
        public int PmtPatFID { get; set; }
        public int? PmtPayFID { get; set; }
        public int PmtBFEPFID { get; set; }
        public string? PmtAuthCode { get; set; }
        public decimal PmtDisbursedTRIG { get; set; }
        /// <summary>From Payer.PayName (PmtPayFID).</summary>
        public string? PmtPayerName { get; set; }
        /// <summary>From Payer.PayClassification.</summary>
        public string? PayClassification { get; set; }
        /// <summary>From Patient.PatAccountNo (PmtPatFID).</summary>
        public string? PatAccountNo { get; set; }
        public string? PatLastName { get; set; }
        public string? PatFirstName { get; set; }
        public string? PatFullNameCC { get; set; }
        public string? PatClassification { get; set; }
        public Dictionary<string, object?>? AdditionalColumns { get; set; }
    }
}
