namespace Zebl.Application.Domain;

/// <summary>
/// Canonical claim status values stored in Claim.ClaStatus (string form via ToString()).
/// </summary>
public enum ClaimStatus
{
    OnHold = 0,
    Submitted = 1,
    RTS = 2,
    Other = 3
}
