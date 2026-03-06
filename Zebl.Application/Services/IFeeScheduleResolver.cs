using Zebl.Application.Domain;

namespace Zebl.Application.Services;

/// <summary>
/// Resolves the correct charge / allowed / adjustment values for the selected procedure code.
/// </summary>
public interface IFeeScheduleResolver
{
    /// <summary>
    /// Resolve fee schedule from the procedure code. If payer-specific values exist use them; otherwise generic library values.
    /// </summary>
    FeeScheduleResult Resolve(IProcedureCode code, int? payerId, string? rateClass);
}
