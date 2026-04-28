namespace Zebl.Application.Services;

public interface IControlNumberService
{
    Task<string> GetNextInterchangeControlNumber(int tenantId, int facilityId, CancellationToken cancellationToken = default);
    Task<string> GetNextGroupControlNumber(int tenantId, int facilityId, CancellationToken cancellationToken = default);
    Task<string> GetNextTransactionControlNumber(int tenantId, int facilityId, CancellationToken cancellationToken = default);
}
