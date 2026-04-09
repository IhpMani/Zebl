namespace Zebl.Application.Abstractions;

/// <summary>
/// Resolves the current tenant for the HTTP request (e.g. from <c>X-Tenant-Key</c>).
/// </summary>
public interface ITenantContext
{
    int TenantId { get; }
}
