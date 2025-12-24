using System;
using System.Collections.Generic;

namespace Zebl.Infrastructure.Persistence.Entities;

public partial class Payment
{
    public int PmtID { get; set; }

    public DateTime PmtDateTimeCreated { get; set; }

    public DateTime PmtDateTimeModified { get; set; }

    public Guid? PmtCreatedUserGUID { get; set; }

    public Guid? PmtLastUserGUID { get; set; }

    public string? PmtCreatedUserName { get; set; }

    public string? PmtLastUserName { get; set; }

    public string? PmtCreatedComputerName { get; set; }

    public string? PmtLastComputerName { get; set; }

    public string? Pmt835Ref { get; set; }

    public decimal PmtAmount { get; set; }

    public string? PmtBatchOperationReference { get; set; }

    public int PmtBFEPFID { get; set; }

    public DateOnly PmtDate { get; set; }

    public decimal PmtDisbursedTRIG { get; set; }

    public string? PmtMethod { get; set; }

    public string? PmtOtherReference1 { get; set; }

    public string? PmtOtherReference2 { get; set; }

    public int PmtPatFID { get; set; }

    public int? PmtPayFID { get; set; }

    public string? PmtAuthCode { get; set; }

    public byte? PmtCardEntryContext { get; set; }

    public byte? PmtCardEntryMethod { get; set; }

    public string? PmtNameOnCard { get; set; }

    public string? PmtIssuerResponseCode { get; set; }

    public string? PmtResponseCode { get; set; }

    public decimal PmtChargedPlatformFee { get; set; }

    public byte? PmtTransactionType { get; set; }

    public string? PmtNote { get; set; }

    public decimal? PmtRemainingCC { get; set; }

    public virtual ICollection<Adjustment> Adjustments { get; set; } = new List<Adjustment>();

    public virtual ICollection<Disbursement> Disbursements { get; set; } = new List<Disbursement>();

    public virtual Physician PmtBFEPF { get; set; } = null!;

    public virtual Patient PmtPatF { get; set; } = null!;

    public virtual Payer? PmtPayF { get; set; }
}
