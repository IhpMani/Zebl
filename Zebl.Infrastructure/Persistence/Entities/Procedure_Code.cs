using System;
using System.Collections.Generic;
using Zebl.Application.Domain;

namespace Zebl.Infrastructure.Persistence.Entities;

public partial class Procedure_Code : IProcedureCode
{
    DateTime? IProcedureCode.ProcStart => ProcStart.HasValue ? ProcStart.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null;
    DateTime? IProcedureCode.ProcEnd => ProcEnd.HasValue ? ProcEnd.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null;
    int IProcedureCode.ProcUnits => (int)ProcUnits;
    int? IProcedureCode.ProcBillingPhyFID => ProcBillingPhyFID;
    int? IProcedureCode.ProcPayFID => ProcPayFID;

    public int ProcID { get; set; }

    public int TenantId { get; set; }

    public DateTime ProcDateTimeCreated { get; set; }

    public DateTime ProcDateTimeModified { get; set; }

    public Guid? ProcCreatedUserGUID { get; set; }

    public Guid? ProcLastUserGUID { get; set; }

    public string? ProcCreatedUserName { get; set; }

    public string? ProcLastUserName { get; set; }

    public string? ProcCreatedComputerName { get; set; }

    public string? ProcLastComputerName { get; set; }

    public decimal ProcAdjust { get; set; }

    public decimal ProcAllowed { get; set; }

    public int ProcBillingPhyFID { get; set; }

    public string? ProcCategory { get; set; }

    public decimal ProcCharge { get; set; }

    public decimal ProcCost { get; set; }

    public bool ProcCMNReq { get; set; }

    public string ProcCode { get; set; } = null!;

    public bool ProcCoPayReq { get; set; }

    public string? ProcDescription { get; set; }

    public bool ProcDescriptionReq { get; set; }

    public float ProcDrugUnitCount { get; set; }

    public string? ProcDrugUnitMeasurement { get; set; }

    public string? ProcModifier1 { get; set; }

    public string? ProcModifier2 { get; set; }

    public string? ProcModifier3 { get; set; }

    public string? ProcModifier4 { get; set; }

    public string? ProcNote { get; set; }

    public string? ProcNDCCode { get; set; }

    public int? ProcPayFID { get; set; }

    public string? ProcProductCode { get; set; }

    public string? ProcRateClass { get; set; }

    public string? ProcRevenueCode { get; set; }

    public float ProcRVUMalpractice { get; set; }

    public float ProcRVUWork { get; set; }

    public string? ProcSubCategory { get; set; }

    public float ProcUnits { get; set; }

    public DateOnly? ProcStart { get; set; }

    public DateOnly? ProcEnd { get; set; }

    public string ProcModifiersCC { get; set; } = null!;

    public virtual Physician? ProcBillingPhyF { get; set; } = null!;

    public virtual Payer? ProcPayF { get; set; } = null!;
}
