using System;

namespace Zebl.Application.Dtos.Claims
{
    public class UpdateClaimRequest
    {
        public string? ClaClassification { get; set; }
        public string? ClaStatus { get; set; }
        public string? ClaSubmissionMethod { get; set; }
        public int? ClaRenderingPhyFID { get; set; }
        public int? ClaFacilityPhyFID { get; set; }
        public string? ClaInvoiceNumber { get; set; }
        public DateTime? ClaAdmittedDate { get; set; }
        public DateTime? ClaDischargedDate { get; set; }
        public DateTime? ClaDateLastSeen { get; set; }
        public string? ClaEDINotes { get; set; }
        public string? ClaRemarks { get; set; }
        public int? ClaRelatedTo { get; set; }
        public string? ClaRelatedToState { get; set; }
        public bool? ClaLocked { get; set; }
        public string? ClaDelayCode { get; set; }
        public string? ClaMedicaidResubmissionCode { get; set; }
        public string? ClaOriginalRefNo { get; set; }
        public string? ClaPaperWorkTransmissionCode { get; set; }
        public string? ClaPaperWorkControlNumber { get; set; }
        public string? ClaPaperWorkInd { get; set; }

        /// <summary>
        /// Additional data stored in ClaAdditionalData XML. Serialized on save.
        /// </summary>
        public ClaimAdditionalData? AdditionalData { get; set; }

        // Optional note text used when creating a claim audit record
        public string? NoteText { get; set; }
    }
}
