using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Zebl.Application.Abstractions;
using Zebl.Application.Dtos.Common;
using Zebl.Application.Options;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Infrastructure.Services;

public sealed class AuditTrailService : IAuditTrail
{
    private readonly IDbContextFactory<ZeblDbContext> _dbFactory;
    private readonly AuditTrailOptions _options;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public AuditTrailService(
        IDbContextFactory<ZeblDbContext> dbFactory,
        IOptions<AuditTrailOptions> options)
    {
        _dbFactory = dbFactory;
        _options = options.Value;
    }

    public async Task WriteAsync(
        Guid? userId,
        int? tenantId,
        AuditMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        if (string.IsNullOrWhiteSpace(metadata.Action))
            return;

        if (string.IsNullOrWhiteSpace(_options.IntegritySecret))
            throw new InvalidOperationException("AuditTrail:IntegritySecret is required for audit logging.");

        var action = metadata.Action.Trim();
        var metaJson = JsonSerializer.Serialize(metadata, JsonOpts);

        var timestampUtc = DateTime.UtcNow;

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var previousHash = await db.AuditLogs.AsNoTracking()
            .OrderByDescending(a => a.Id)
            .Select(a => a.Hash)
            .FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(previousHash))
            previousHash = null;

        var payload =
            $"{action}|{userId}|{tenantId}|{timestampUtc:O}|{metaJson}|{previousHash ?? string.Empty}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload + _options.IntegritySecret));
        var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        db.AuditLogs.Add(new AuditLog
        {
            Action = action.Length > 256 ? action[..256] : action,
            UserId = userId,
            TenantId = tenantId,
            TimestampUtc = timestampUtc,
            Metadata = metaJson,
            Hash = hash,
            PreviousHash = string.IsNullOrEmpty(previousHash) ? null : previousHash
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
