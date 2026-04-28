using Zebl.Application.Domain;

namespace Zebl.Application.Abstractions;

/// <summary>
/// Optional hook after an inbound EDI report row is persisted (e.g. claims module reacts to 999).
/// Implementations live outside core EDI persistence to keep boundaries clear.
/// </summary>
public interface IEdiInboundPostProcessor
{
    Task ProcessInboundPersistedAsync(EdiReport report, ReadOnlyMemory<byte> ediBytes, string fileType, string correlationId, CancellationToken cancellationToken = default);
}
