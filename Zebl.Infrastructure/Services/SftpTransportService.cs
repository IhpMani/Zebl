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
        if (connection.ConnectionType != ConnectionType.Sftp)
            throw new InvalidOperationException("SFTP transport requires ConnectionType SFTP.");

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
        if (connection.ConnectionType != ConnectionType.Sftp)
            throw new InvalidOperationException("SFTP transport requires ConnectionType SFTP.");

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

    public async Task UploadFileAsync(ConnectionLibrary connection, string fileName, Stream contentStream, CancellationToken cancellationToken = default)
    {
        if (connection.ConnectionType != ConnectionType.Sftp)
            throw new InvalidOperationException("SFTP transport requires ConnectionType SFTP.");
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("FileName is required", nameof(fileName));
        if (contentStream == null)
            throw new ArgumentNullException(nameof(contentStream));
        if (string.IsNullOrEmpty(connection.EncryptedPassword))
            throw new InvalidOperationException("Connection has no password set.");

        var decryptedPassword = _encryptionService.Decrypt(connection.EncryptedPassword);
        using var client = new Renci.SshNet.SftpClient(connection.Host, connection.Port, connection.Username, decryptedPassword);
        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            client.Connect();
            if (!client.IsConnected)
                throw new InvalidOperationException("Failed to connect to SFTP server.");
            var uploadPath = string.IsNullOrWhiteSpace(connection.UploadDirectory)
                ? fileName
                : $"{connection.UploadDirectory.TrimEnd('/')}/{fileName}";
            if (contentStream.CanSeek)
                contentStream.Position = 0;
            client.UploadFile(contentStream, uploadPath, true);
            client.Disconnect();
        }, cancellationToken);
    }

    public async Task UploadFileAsync(
        string host,
        int port,
        string username,
        string password,
        string? uploadDirectory,
        string fileName,
        Stream contentStream,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("FileName is required", nameof(fileName));
        if (contentStream == null)
            throw new ArgumentNullException(nameof(contentStream));

        using var client = new Renci.SshNet.SftpClient(host, port, username, password);
        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            client.Connect();

            if (!client.IsConnected)
                throw new InvalidOperationException("Failed to connect to SFTP server.");

            var uploadPath = string.IsNullOrWhiteSpace(uploadDirectory)
                ? fileName
                : $"{uploadDirectory.TrimEnd('/')}/{fileName}";

            if (contentStream.CanSeek)
                contentStream.Position = 0;
            client.UploadFile(contentStream, uploadPath, true);

            client.Disconnect();
        }, cancellationToken);
    }

    /// <summary>
    /// Downloads files from the SFTP server matching the download pattern. Returns file name, remote path, and content for each file.
    /// </summary>
    public async Task<List<SftpInboundFile>> DownloadFilesAsync(ConnectionLibrary connection)
    {
        if (connection.ConnectionType != ConnectionType.Sftp)
            throw new InvalidOperationException("SFTP transport requires ConnectionType SFTP.");

        if (string.IsNullOrEmpty(connection.EncryptedPassword))
            throw new InvalidOperationException("Connection has no password set.");

        var result = new List<SftpInboundFile>();

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
                result.Add(new SftpInboundFile(file.Name, file.FullName, stream.ToArray()));
            }

            client.Disconnect();
        });

        return result;
    }

    public async Task MoveInboundFileAsync(ConnectionLibrary connection, string sourceFullPath, InboundLifecycleTarget target, CancellationToken cancellationToken = default)
    {
        if (connection.ConnectionType != ConnectionType.Sftp)
            throw new InvalidOperationException("SFTP transport requires ConnectionType SFTP.");
        if (string.IsNullOrWhiteSpace(sourceFullPath))
            throw new ArgumentException("Source path is required.", nameof(sourceFullPath));
        if (string.IsNullOrEmpty(connection.EncryptedPassword))
            throw new InvalidOperationException("Connection has no password set.");

        var decryptedPassword = _encryptionService.Decrypt(connection.EncryptedPassword);
        using var client = new Renci.SshNet.SftpClient(connection.Host, connection.Port, connection.Username, decryptedPassword);
        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            client.Connect();
            if (!client.IsConnected)
                throw new InvalidOperationException("Failed to connect to SFTP server.");

            var root = string.IsNullOrWhiteSpace(connection.DownloadDirectory) ? "/" : connection.DownloadDirectory!.TrimEnd('/');
            var folder = target == InboundLifecycleTarget.Processed ? "processed" : "failed";
            var destinationDirectory = $"{root}/{folder}";
            EnsureDirectory(client, destinationDirectory);

            var fileName = sourceFullPath.Split('/').Last();
            var destinationPath = $"{destinationDirectory.TrimEnd('/')}/{fileName}";
            if (client.Exists(destinationPath))
                client.DeleteFile(destinationPath);

            client.RenameFile(sourceFullPath, destinationPath);
            client.Disconnect();
        }, cancellationToken).ConfigureAwait(false);
    }

    private static void EnsureDirectory(Renci.SshNet.SftpClient client, string fullPath)
    {
        var parts = fullPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var current = "/";
        foreach (var part in parts)
        {
            current = current.EndsWith("/") ? current + part : current + "/" + part;
            if (!client.Exists(current))
                client.CreateDirectory(current);
        }
    }
}

public sealed record SftpInboundFile(string FileName, string FullPath, byte[] Content);
public enum InboundLifecycleTarget
{
    Processed,
    Failed
}
