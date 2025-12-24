using System;
using System.Collections.Generic;

namespace Zebl.Infrastructure.Persistence.Entities;

public partial class Disbursement
{
    public int DisbID { get; set; }

    public DateTime DisbDateTimeCreated { get; set; }

    public DateTime DisbDateTimeModified { get; set; }

    public Guid? DisbCreatedUserGUID { get; set; }

    public Guid? DisbLastUserGUID { get; set; }

    public string? DisbCreatedUserName { get; set; }

    public string? DisbLastUserName { get; set; }

    public string? DisbCreatedComputerName { get; set; }

    public string? DisbLastComputerName { get; set; }

    public decimal DisbAmount { get; set; }

    public string? DisbBatchOperationReference { get; set; }

    public string? DisbCode { get; set; }

    public string? DisbNote { get; set; }

    public int DisbPmtFID { get; set; }

    public int DisbSrvFID { get; set; }

    public Guid DisbSrvGUID { get; set; }

    public virtual Payment DisbPmtF { get; set; } = null!;

    public virtual Service_Line DisbSrvF { get; set; } = null!;
}
