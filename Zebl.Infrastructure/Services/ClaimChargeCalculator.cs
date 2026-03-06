using System;
using Zebl.Application.Domain;
using Zebl.Application.Services;

namespace Zebl.Infrastructure.Services;

/// <summary>
/// Centralizes billing math and unit recalculations. Library entries may store charges with ProcUnits > 1; per-unit charge is used for scaling.
/// </summary>
public class ClaimChargeCalculator : IClaimChargeCalculator
{
    public decimal RecalculateCharge(decimal charge, int oldUnits, int newUnits)
    {
        if (oldUnits <= 0) return charge;
        return Math.Round(charge / oldUnits * newUnits, 2, MidpointRounding.AwayFromZero);
    }

    public ClaimChargeResult Calculate(
        IProcedureCode code,
        int units,
        decimal existingCharge,
        decimal existingAllowed,
        bool hasContractAdjustment = false)
    {
        if (code == null)
        {
            return new ClaimChargeResult
            {
                Charge = existingCharge,
                Allowed = existingAllowed,
                Adjustment = 0,
                OverwriteCharge = false,
                OverwriteAllowed = false,
                OverwriteAdjustment = false
            };
        }

        var overwriteCharge = code.ProcCharge != 0;
        var overwriteAllowed = code.ProcAllowed != 0;
        var overwriteAdjustment = code.ProcAdjust != 0 && !hasContractAdjustment;

        decimal perUnitCharge = code.ProcCharge;
        if (code.ProcUnits > 1)
            perUnitCharge = code.ProcCharge / code.ProcUnits;
        decimal perUnitAllowed = code.ProcAllowed;
        if (code.ProcUnits > 1)
            perUnitAllowed = code.ProcAllowed / code.ProcUnits;

        var charge = overwriteCharge
            ? Math.Round(perUnitCharge * units, 2, MidpointRounding.AwayFromZero)
            : existingCharge;
        var allowed = overwriteAllowed
            ? Math.Round(perUnitAllowed * units, 2, MidpointRounding.AwayFromZero)
            : existingAllowed;
        var adjustment = overwriteAdjustment ? code.ProcAdjust : 0;

        return new ClaimChargeResult
        {
            Charge = charge,
            Allowed = allowed,
            Adjustment = adjustment,
            OverwriteCharge = overwriteCharge,
            OverwriteAllowed = overwriteAllowed,
            OverwriteAdjustment = overwriteAdjustment
        };
    }
}
