using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Renci.SshNet;
using Zebl.Application.Abstractions;
using Zebl.Application.Domain;
using Zebl.Application.Services;
using Zebl.Infrastructure.Persistence.Context;

namespace Zebl.Api.Services;

public sealed class EligibilityPollingService : BackgroundService
{
    private const int PollingIntervalMinutes = 2;
    private const int DefaultSftpPort = 22;
    private const string EligibilityIncomingPath = "/incoming/eligibility";
    private const string EligibilityProcessedPath = "/incoming/eligibility/processed";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EligibilityPollingService> _logger;

    public EligibilityPollingService(
        IServiceScopeFactory scopeFactory,
        ILogger<EligibilityPollingService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var correlationId = Guid.NewGuid().ToString("N");
            using var _ambient = CorrelationContext.Push(correlationId);
            using var _scope = _logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId });
            try
            {
                await PollOnceAsync(correlationId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Eligibility polling iteration failed. CorrelationId={CorrelationId}", correlationId);
            }

            await Task.Delay(TimeSpan.FromMinutes(PollingIntervalMinutes), stoppingToken);
        }
    }

    private async Task PollOnceAsync(string correlationId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var settingsProvider = scope.ServiceProvider.GetRequiredService<IEligibilitySettingsProvider>();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ZeblDbContext>>();
        var parser = scope.ServiceProvider.GetRequiredService<IEligibilityParser>();
        var eligibilitySettings = await settingsProvider.GetForEligibilityCheckAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(eligibilitySettings.Server) ||
            string.IsNullOrWhiteSpace(eligibilitySettings.Username) ||
            string.IsNullOrWhiteSpace(eligibilitySettings.Password))
        {
            return;
        }

        var (host, port) = ParseServer(eligibilitySettings.Server);
        using var client = new SftpClient(host, port, eligibilitySettings.Username, eligibilitySettings.Password);
        client.Connect();

        var incomingPath = EligibilityIncomingPath;
        var processedPath = EligibilityProcessedPath;
        EnsureRemoteDirectory(client, incomingPath);
        if (!client.Exists(processedPath))
            client.CreateDirectory(processedPath);

        var files = client.ListDirectory(incomingPath)
            .Where(f => !f.IsDirectory && f.Name.EndsWith(".271", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (files.Count == 0)
        {
            client.Disconnect();
            return;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        foreach (var file in files)
        {
            try
            {
                using var stream = new MemoryStream();
                client.DownloadFile(file.FullName, stream);
                stream.Position = 0;
                using var reader = new StreamReader(stream, Encoding.UTF8);
                var raw271 = await reader.ReadToEndAsync(cancellationToken);

                var controlNumber = ExtractControlNumber(raw271);
                if (string.IsNullOrWhiteSpace(controlNumber))
                {
                    _logger.LogWarning("Skipping 271 file {FileName}: control number not found. CorrelationId={CorrelationId}", file.Name, correlationId);
                    continue;
                }

                var request = await db.EligibilityRequests
                    .Where(r => r.Status == "Sent" && r.ControlNumber == controlNumber)
                    .OrderByDescending(r => r.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                if (request == null)
                {
                    _logger.LogWarning("No matching eligibility request for control number {ControlNumber} (file {FileName}). CorrelationId={CorrelationId}", controlNumber, file.Name, correlationId);
                    continue;
                }

                var parsed = parser.Parse(raw271);
                request.Status = "Completed";

                db.EligibilityResponses.Add(new EligibilityResponse
                {
                    RequestId = request.Id,
                    Raw271 = raw271,
                    EligibilityStatus = parsed.EligibilityStatus,
                    ErrorMessage = parsed.ErrorMessage,
                    CreatedAt = DateTime.UtcNow
                });

                await db.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Eligibility Response received -> requestId={requestId}, CorrelationId={CorrelationId}", request.Id, correlationId);

                var archivePath = $"{processedPath.TrimEnd('/')}/{file.Name}";
                if (client.Exists(archivePath))
                    client.DeleteFile(archivePath);
                client.RenameFile(file.FullName, archivePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed processing 271 file {FileName}. CorrelationId={CorrelationId}", file.Name, correlationId);
            }
        }

        client.Disconnect();
    }

    private static string? ExtractControlNumber(string raw271)
    {
        var segments = raw271.Split('~', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var isaControl = (string?)null;
        var stControl = (string?)null;

        foreach (var segment in segments)
        {
            if (segment.StartsWith("ISA*", StringComparison.Ordinal))
            {
                var parts = segment.Split('*');
                if (parts.Length > 13 && !string.IsNullOrWhiteSpace(parts[13]))
                    isaControl = parts[13].Trim();
            }
            else if (segment.StartsWith("ST*", StringComparison.Ordinal))
            {
                var parts = segment.Split('*');
                if (parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2]))
                    stControl = parts[2].Trim();
            }
        }

        if (!string.IsNullOrWhiteSpace(stControl))
            return stControl;

        if (string.IsNullOrWhiteSpace(isaControl))
            return null;
        return isaControl.Length > 20 ? isaControl[..20] : isaControl.PadLeft(20, '0');
    }

    private static (string Host, int Port) ParseServer(string serverValue)
    {
        var input = serverValue.Trim();
        if (!input.Contains("://", StringComparison.Ordinal))
            input = "sftp://" + input;

        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Host))
            throw new InvalidOperationException("Program Setup Patient Eligibility server is invalid.");

        return (uri.Host, uri.Port > 0 ? uri.Port : DefaultSftpPort);
    }

    private static void EnsureRemoteDirectory(SftpClient client, string fullPath)
    {
        var parts = fullPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var current = "/";
        foreach (var part in parts)
        {
            current = current.EndsWith("/")
                ? current + part
                : current + "/" + part;
            if (!client.Exists(current))
                client.CreateDirectory(current);
        }
    }
}
