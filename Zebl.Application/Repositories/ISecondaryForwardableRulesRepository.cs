namespace Zebl.Application.Repositories;

/// <summary>
/// Rules for which adjustment GroupCode+ReasonCode are forwardable to secondary. No hardcoding in service.
/// </summary>
public interface ISecondaryForwardableRulesRepository
{
    /// <summary>
    /// Returns true if this group+reason should be forwarded to secondary. If no rule found, defaults to false (do not forward).
    /// </summary>
    Task<bool> IsForwardableAsync(string groupCode, string? reasonCode);
}
