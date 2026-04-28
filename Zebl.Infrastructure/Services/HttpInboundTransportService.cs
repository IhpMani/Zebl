using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Zebl.Application.Domain;
using Zebl.Application.Services;

namespace Zebl.Infrastructure.Services;

/// <summary>
/// Inbound transport for HTTP/API <see cref="ConnectionType"/> connections (manifest fetch only).
/// </summary>
public sealed class HttpInboundTransportService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<HttpInboundTransportService> _logger;

    public HttpInboundTransportService(
        IHttpClientFactory httpClientFactory,
        IEncryptionService encryptionService,
        ILogger<HttpInboundTransportService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<HttpInboundTransportItem>> FetchAsync(ConnectionLibrary connection, CancellationToken cancellationToken = default)
    {
        var baseUrl = (connection.Host ?? "").Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(baseUrl))
            throw new InvalidOperationException("Host (base URL) is required for HTTP/API connections.");

        var path = string.IsNullOrWhiteSpace(connection.InboundFetchPath)
            ? "/api/get-reports"
            : connection.InboundFetchPath!.Trim();
        if (!path.StartsWith('/'))
            path = "/" + path;

        var url = baseUrl + path;

        string? connectionUsername = string.IsNullOrWhiteSpace(connection.Username) ? null : connection.Username.Trim();
        string? connectionPassword = null;
        if (!string.IsNullOrWhiteSpace(connection.EncryptedPassword))
            connectionPassword = _encryptionService.Decrypt(connection.EncryptedPassword);

        async Task<HttpResponseMessage> SendWithBasicAsync(string user, string pass)
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            var token = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{user}:{pass}"));
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
            return await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        }

        HttpResponseMessage response;
        try
        {
            if (!string.IsNullOrWhiteSpace(connectionUsername) && !string.IsNullOrWhiteSpace(connectionPassword))
                response = await SendWithBasicAsync(connectionUsername!, connectionPassword!).ConfigureAwait(false);
            else
                response = await SendWithBasicAsync("Admin", "Admin@123").ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized &&
                (connectionUsername != "Admin" || connectionPassword != "Admin@123"))
            {
                response.Dispose();
                response = await SendWithBasicAsync("Admin", "Admin@123").ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Inbound HTTP EDI fetch failed: {Url}", url);
            throw new InvalidOperationException(
                $"Could not reach the report server at {url}. Ensure the connection host is correct and the server is running. {ex.Message}", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var preview = body.Length > 200 ? body[..200] + "..." : body;
                throw new InvalidOperationException(
                    $"Report server returned {(int)response.StatusCode} {response.ReasonPhrase}. URL: {url}. Response: {preview}");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Array)
                return Array.Empty<HttpInboundTransportItem>();

            var list = new List<HttpInboundTransportItem>();
            foreach (var item in root.EnumerateArray())
            {
                var fileName = item.TryGetProperty("fileName", out var fn) ? fn.GetString() ?? "report.edi" : "report.edi";
                var fileType = item.TryGetProperty("fileType", out var ft) ? ft.GetString() ?? ".EDI" : ".EDI";
                var payer = item.TryGetProperty("payer", out var p) ? p.GetString() : null;
                decimal? paymentAmount = null;
                if (item.TryGetProperty("paymentAmount", out var pa) && pa.ValueKind == JsonValueKind.Number)
                    paymentAmount = pa.GetDecimal();
                var note = item.TryGetProperty("note", out var n) ? n.GetString() : null;
                var traceNumber = item.TryGetProperty("traceNumber", out var tn) ? tn.GetString() : null;
                byte[]? raw = null;
                if (item.TryGetProperty("contentBase64", out var b64) && b64.ValueKind == JsonValueKind.String)
                {
                    try
                    {
                        raw = Convert.FromBase64String(b64.GetString() ?? "");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Invalid contentBase64 for file {FileName}", fileName);
                        throw;
                    }
                }

                list.Add(new HttpInboundTransportItem(fileName, fileType.TrimStart('.'), payer, paymentAmount, note, traceNumber, raw));
            }

            return list;
        }
    }
}

public sealed record HttpInboundTransportItem(
    string FileName,
    string FileType,
    string? PayerName,
    decimal? PaymentAmount,
    string? Note,
    string? TraceNumber,
    byte[]? RawContent);
