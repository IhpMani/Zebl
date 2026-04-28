using System.Text;
using Microsoft.Extensions.Options;
using Renci.SshNet;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Api.Services;

public sealed class ClearinghouseClient : IClearinghouseClient
{
    private readonly ClearinghouseSettings _settings;
    private readonly ILogger<ClearinghouseClient> _logger;

    public ClearinghouseClient(IOptions<ClearinghouseSettings> settings, ILogger<ClearinghouseClient> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public Task<SubmissionResult> SubmitBatchAsync(ClaimBatch batch, string ediContent)
        => UploadAsync($"batch-{batch.Id:N}.837", ediContent, batch.Id.ToString());

    public Task<SubmissionResult> UploadEligibilityAsync(string fileName, string ediContent, CancellationToken cancellationToken = default)
        => UploadAsync(fileName, ediContent, fileName);

    private Task<SubmissionResult> UploadAsync(string fileName, string ediContent, string operationId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_settings.Host) ||
                string.IsNullOrWhiteSpace(_settings.Username) ||
                string.IsNullOrWhiteSpace(_settings.Password))
            {
                return Task.FromResult(new SubmissionResult
                {
                    Success = false,
                    Message = "Clearinghouse settings are incomplete."
                });
            }

            var remotePath = string.IsNullOrWhiteSpace(_settings.RemotePath) ? "/" : _settings.RemotePath.Trim();
            var remoteFilePath = remotePath.EndsWith("/")
                ? $"{remotePath}{fileName}"
                : $"{remotePath}/{fileName}";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(ediContent));
            using var client = new SftpClient(_settings.Host, _settings.Port, _settings.Username, _settings.Password);
            client.Connect();
            client.UploadFile(stream, remoteFilePath, true);
            client.Disconnect();

            _logger.LogInformation(
                "Clearinghouse upload succeeded for operation {OperationId}, file {FileName}, path {RemotePath}",
                operationId,
                fileName,
                remotePath);

            return Task.FromResult(new SubmissionResult
            {
                Success = true,
                Message = $"Uploaded {fileName} to {remotePath}."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Clearinghouse upload failed for operation {OperationId}", operationId);
            return Task.FromResult(new SubmissionResult
            {
                Success = false,
                Message = ex.Message
            });
        }
    }
}
