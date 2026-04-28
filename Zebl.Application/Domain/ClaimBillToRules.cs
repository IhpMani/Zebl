namespace Zebl.Application.Domain;

public static class ClaimBillToRules
{
    public static bool IsValidValue(int value) =>
        value is (int)ClaimBillTo.Patient or (int)ClaimBillTo.Primary or (int)ClaimBillTo.Secondary;

    /// <summary>
    /// Deterministically resolve the stored ClaBillTo value:
    /// - If the claim has no insurance, bill-to is forced to Patient.
    /// - Otherwise, it is resolved from request/current (preferring request) and validated.
    /// - Null never leaves the result null.
    /// </summary>
    public static int Resolve(
        int? requestedBillTo,
        int? currentBillTo,
        bool hasInsurance)
    {
        var defaultBillTo = hasInsurance
            ? (int)ClaimBillTo.Primary
            : (int)ClaimBillTo.Patient;

        var resolved = requestedBillTo ?? currentBillTo ?? defaultBillTo;

        if (!IsValidValue(resolved))
            throw new InvalidOperationException("ClaBillTo must be one of 0 (Patient), 1 (Primary), 2 (Secondary/Final).");

        if (!hasInsurance)
            return (int)ClaimBillTo.Patient;

        return resolved;
    }
}

