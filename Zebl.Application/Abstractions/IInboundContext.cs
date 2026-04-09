namespace Zebl.Application.Abstractions;

/// <summary>
/// Deterministic tenant/facility scope for inbound integrations (HL7/EDI).
/// Resolved from integration binding metadata, never from payload.
/// </summary>
public interface IInboundContext
{
    int TenantId { get; }
    int FacilityId { get; }
    int IntegrationId { get; }
}
