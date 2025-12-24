using System;
using System.Collections.Generic;

namespace Zebl.Infrastructure.Persistence.Entities;

public partial class Adjustment
{
    public int AdjID { get; set; }

    public DateTime AdjDateTimeCreated { get; set; }

    public DateTime AdjDateTimeModified { get; set; }

    public Guid? AdjCreatedUserGUID { get; set; }

    public Guid? AdjLastUserGUID { get; set; }

    public string? AdjCreatedUserName { get; set; }

    public string? AdjLastUserName { get; set; }

    public string? AdjCreatedComputerName { get; set; }

    public string? AdjLastComputerName { get; set; }

    public string? Adj835Ref { get; set; }

    public decimal AdjAmount { get; set; }

    public string? AdjBatchOperationReference { get; set; }

    public DateOnly? AdjDate { get; set; }

    public string AdjGroupCode { get; set; } = null!;

    public string? AdjNote { get; set; }

    public string? AdjOtherReference1 { get; set; }

    public int AdjPayFID { get; set; }

    public int AdjPmtFID { get; set; }

    public decimal AdjReasonAmount { get; set; }

    public string? AdjReasonCode { get; set; }

    public string? AdjRemarkCode { get; set; }

    public int AdjSrvFID { get; set; }

    public Guid AdjSrvGUID { get; set; }

    public int AdjTaskFID { get; set; }

    public bool AdjTrackOnly { get; set; }

    public virtual Payer AdjPayF { get; set; } = null!;

    public virtual Payment AdjPmtF { get; set; } = null!;

    public virtual Service_Line AdjSrv { get; set; } = null!;

    public virtual Service_Line AdjSrvF { get; set; } = null!;

    public virtual Service_Line AdjTaskF { get; set; } = null!;
}
