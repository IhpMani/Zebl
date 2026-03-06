using Zebl.Application.Domain;

namespace Zebl.Application.Services;

/// <summary>
/// Centralizes billing math and unit recalculations.
/// </summary>
public interface IClaimChargeCalculator
{
    /// <summary>
    /// Recalculate charge when units change: newCharge = charge / oldUnits * newUnits.
    /// </summary>
    decimal RecalculateCharge(decimal charge, int oldUnits, int newUnits);

    /// <summary>
    /// Calculate charge, allowed, and overwrite flags for a service line from the procedure code.
    /// Charge overwrite only if ProcCharge != 0; Allowed overwrite only if ProcAllowed != 0;
    /// Adjustment overwrite only if no contract adjustment exists.
    /// </summary>
    ClaimChargeResult Calculate(
        IProcedureCode code,
        int units,
        decimal existingCharge,
        decimal existingAllowed,
        bool hasContractAdjustment = false);
}
