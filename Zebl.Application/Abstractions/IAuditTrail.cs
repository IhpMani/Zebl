using Zebl.Application.Dtos.Common;

namespace Zebl.Application.Abstractions;

public interface IAuditTrail
{
    Task WriteAsync(
        Guid? userId,
        int? tenantId,
        AuditMetadata metadata,
        CancellationToken cancellationToken = default);
}
