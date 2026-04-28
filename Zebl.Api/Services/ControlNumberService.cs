using System.Data;
using Microsoft.EntityFrameworkCore;
using Zebl.Application.Services;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Api.Services;

public sealed class ControlNumberService : IControlNumberService
{
    private readonly ZeblDbContext _db;

    public ControlNumberService(ZeblDbContext db)
    {
        _db = db;
    }

    public Task<string> GetNextInterchangeControlNumber(int tenantId, int facilityId, CancellationToken cancellationToken = default)
        => GetNextAsync(
            tenantId,
            facilityId,
            s => s.LastInterchangeNumber,
            (s, next) => s.LastInterchangeNumber = next,
            next => next.ToString("D9"),
            cancellationToken);

    public Task<string> GetNextGroupControlNumber(int tenantId, int facilityId, CancellationToken cancellationToken = default)
        => GetNextAsync(
            tenantId,
            facilityId,
            s => s.LastGroupNumber,
            (s, next) => s.LastGroupNumber = next,
            next => next.ToString(),
            cancellationToken);

    public Task<string> GetNextTransactionControlNumber(int tenantId, int facilityId, CancellationToken cancellationToken = default)
        => GetNextAsync(
            tenantId,
            facilityId,
            s => s.LastTransactionNumber,
            (s, next) => s.LastTransactionNumber = next,
            next => next.ToString(),
            cancellationToken);

    private async Task<string> GetNextAsync(
        int tenantId,
        int facilityId,
        Func<ControlNumberSequence, long> getCurrent,
        Action<ControlNumberSequence, long> setCurrent,
        Func<long, string> format,
        CancellationToken cancellationToken)
    {
        if (tenantId <= 0 || facilityId <= 0)
            throw new ArgumentException("TenantId and FacilityId must be greater than zero.");

        async Task<string> AcquireAndFormatAsync()
        {
            var sequence = await _db.ControlNumberSequences
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.FacilityId == facilityId, cancellationToken);

            if (sequence == null)
            {
                sequence = new ControlNumberSequence
                {
                    TenantId = tenantId,
                    FacilityId = facilityId,
                    LastInterchangeNumber = 0,
                    LastGroupNumber = 0,
                    LastTransactionNumber = 0
                };
                _db.ControlNumberSequences.Add(sequence);
                await _db.SaveChangesAsync(cancellationToken);
            }

            var next = checked(getCurrent(sequence) + 1);
            setCurrent(sequence, next);
            await _db.SaveChangesAsync(cancellationToken);
            return format(next);
        }

        // Join an ambient transaction (e.g. send-batch per-claim) — EF forbids nested BeginTransaction on the same connection.
        if (_db.Database.CurrentTransaction != null)
            return await AcquireAndFormatAsync();

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                var result = await AcquireAndFormatAsync();
                await tx.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }
}
