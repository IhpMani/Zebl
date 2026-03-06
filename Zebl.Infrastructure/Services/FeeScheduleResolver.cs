using Zebl.Application.Domain;
using Zebl.Application.Services;

namespace Zebl.Infrastructure.Services;

/// <summary>
/// Resolves the correct charge / allowed / adjustment values for the selected procedure code.
/// </summary>
public class FeeScheduleResolver : IFeeScheduleResolver
{
    public FeeScheduleResult Resolve(IProcedureCode code, int? payerId, string? rateClass)
    {
        if (code == null)
            return new FeeScheduleResult { Charge = 0, Allowed = 0, Adjustment = 0 };

        return new FeeScheduleResult
        {
            Charge = code.ProcCharge,
            Allowed = code.ProcAllowed,
            Adjustment = code.ProcAdjust
        };
    }
}
