namespace Zebl.Application.Domain;

/// <summary>
/// Domain abstraction for procedure code library entries. Implemented by the EF entity so Application services do not depend on Infrastructure.
/// </summary>
public interface IProcedureCode
{
    string ProcCode { get; }
    decimal ProcCharge { get; }
    decimal ProcAllowed { get; }
    decimal ProcAdjust { get; }
    int ProcUnits { get; }
    string? ProcCategory { get; }
    string? ProcDescription { get; }
    DateTime? ProcStart { get; }
    DateTime? ProcEnd { get; }
    int? ProcBillingPhyFID { get; }
    int? ProcPayFID { get; }
    string? ProcRateClass { get; }
    string? ProcProductCode { get; }
}
