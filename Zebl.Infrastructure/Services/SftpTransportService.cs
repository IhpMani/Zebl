using System.Text;
using Microsoft.Extensions.Logging;
using Zebl.Application.Domain;
using Zebl.Application.Services;

namespace Zebl.Infrastructure.Services;

/// <summary>
/// SFTP transport service for file operations. Decrypts password internally.
/// </summary>
public class SftpTransportService
{
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<SftpTransportService> _logger;

    public SftpTransportService(
        IEncryptionService encryptionService,
        ILogger<SftpTransportService> logger)
    {
        _encryptionService = encryptionService;
        _logger = logger;
    }

    /// <summary>
    /// Tests SFTP connection using the provided connection library.
    /// Throws InvalidOperationException with a descriptive message when the test fails.
    /// </summary>
    public async Task<bool> TestConnectionAsync(ConnectionLibrary connection)
    {
        if (string.IsNullOrEmpty(connection.EncryptedPassword))
        {
            _logger.LogWarning("Connection {Id} has no password set", connection.Id);
            throw new InvalidOperationException("Connection has no password set. Please save the connection with a password and try again.");
        }
        if (string.IsNullOrWhiteSpace(connection.Host))
        {
            throw new InvalidOperationException("Connection has no host configured.");
        }
        try
        {
            // Decrypt password ONLY inside this service
            var decryptedPassword = _encryptionService.Decrypt(connection.EncryptedPassword);

            using var client = new Renci.SshNet.SftpClient(connection.Host, connection.Port, connection.Username ?? "", decryptedPassword);

            await Task.Run(() =>
            {
                client.Connect();
                var isConnected = client.IsConnected;
                client.Disconnect();
                return isConnected;
            });

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SFTP connection test failed for {Host}:{Port}", connection.Host, connection.Port);
            var message = ex.InnerException?.Message ?? ex.Message;
            if (message.Contains("Connection refused", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Cannot reach {connection.Host}:{connection.Port}. Connection refused. Is the SFTP server running? (Note: HTTP URLs like http://localhost:5001 are not SFTP.)");
            if (message.Contains("No such host", StringComparison.OrdinalIgnoreCase) || message.Contains("could not be resolved", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Host '{connection.Host}' could not be resolved. Check the host name.");
            if (message.Contains("authentication", StringComparison.OrdinalIgnoreCase) || message.Contains("password", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Authentication failed. Check username and password.");
            throw new InvalidOperationException($"Connection test failed: {message}");
        }
    }

    /// <summary>
    /// Uploads a file to the SFTP server.
    /// </summary>
    public async Task UploadFileAsync(ConnectionLibrary connection, string fileName, string content)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("FileName is required", nameof(fileName));
        if (content == null)
            throw new ArgumentNullException(nameof(content));
        if (string.IsNullOrEmpty(connection.EncryptedPassword))
            throw new InvalidOperationException("Connection has no password set.");

        // Decrypt password ONLY inside this service
        var decryptedPassword = _encryptionService.Decrypt(connection.EncryptedPassword);

        using var client = new Renci.SshNet.SftpClient(connection.Host, connection.Port, connection.Username, decryptedPassword);
        
        await Task.Run(() =>
        {
            client.Connect();
            
            if (!client.IsConnected)
                throw new InvalidOperationException("Failed to connect to SFTP server.");

            var uploadPath = string.IsNullOrWhiteSpace(connection.UploadDirectory)
                ? fileName
                : $"{connection.UploadDirectory.TrimEnd('/')}/{fileName}";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            client.UploadFile(stream, uploadPath);
            
            client.Disconnect();
        });
    }

    /// <summary>
    /// Downloads files from the SFTP server matching the download pattern. Returns file name and content for each file.
    /// </summary>
    public async Task<List<(string FileName, string Content)>> DownloadFilesAsync(ConnectionLibrary connection)
    {
        if (string.IsNullOrEmpty(connection.EncryptedPassword))
            throw new InvalidOperationException("Connection has no password set.");

        var result = new List<(string FileName, string Content)>();

        var decryptedPassword = _encryptionService.Decrypt(connection.EncryptedPassword);

        using var client = new Renci.SshNet.SftpClient(connection.Host, connection.Port, connection.Username, decryptedPassword);

        await Task.Run(() =>
        {
            client.Connect();

            if (!client.IsConnected)
                throw new InvalidOperationException("Failed to connect to SFTP server.");

            var downloadPath = connection.DownloadDirectory ?? "/";

            var files = client.ListDirectory(downloadPath)
                .Where(f => !f.IsDirectory && !f.Name.StartsWith("."))
                .ToList();

            if (!string.IsNullOrWhiteSpace(connection.DownloadPattern))
            {
                var pattern = connection.DownloadPattern.Replace("*", ".*");
                var regex = new System.Text.RegularExpressions.Regex($"^{pattern}$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                files = files.Where(f => regex.IsMatch(f.Name)).ToList();
            }

            foreach (var file in files)
            {
                using var stream = new MemoryStream();
                client.DownloadFile(file.FullName, stream);
                stream.Position = 0;
                using var reader = new StreamReader(stream);
                var content = reader.ReadToEnd();
                result.Add((file.Name, content));
            }

            client.Disconnect();
        });

        return result;
    }
}
