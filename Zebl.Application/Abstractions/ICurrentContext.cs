namespace Zebl.Application.Abstractions;

/// <summary>
/// Tenant and facility scope for the current request (user or future import binding).
/// </summary>
public interface ICurrentContext
{
    int TenantId { get; }
    int FacilityId { get; }
}
