using Microsoft.EntityFrameworkCore;
using Zebl.Application.Domain;
using Zebl.Application.Repositories;
using Zebl.Infrastructure.Persistence.Context;

namespace Zebl.Infrastructure.Repositories;

public class ClaimSubmissionRepository : IClaimSubmissionRepository
{
    private readonly ZeblDbContext _context;

    public ClaimSubmissionRepository(ZeblDbContext context)
    {
        _context = context;
    }

    public async Task<string> GetNextTransactionControlNumberAsync()
    {
        var existing = await _context.ClaimSubmissions
            .AsNoTracking()
            .Select(s => s.TransactionControlNumber)
            .ToListAsync();

        long max = 0;
        foreach (var tcn in existing)
        {
            if (string.IsNullOrWhiteSpace(tcn)) continue;
            var trimmed = tcn.Trim();
            if (trimmed.Length == 0) continue;
            if (long.TryParse(trimmed, System.Globalization.NumberStyles.None, null, out var n) && n > max)
                max = n;
        }

        return (max + 1).ToString("D9");
    }

    public async Task AddAsync(ClaimSubmission entity)
    {
        await _context.ClaimSubmissions.AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<ClaimSubmission?> GetByTransactionControlNumberAsync(string transactionControlNumber)
    {
        if (string.IsNullOrWhiteSpace(transactionControlNumber))
            return null;

        var trimmed = transactionControlNumber.Trim();
        return await _context.ClaimSubmissions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TransactionControlNumber == trimmed);
    }
}
