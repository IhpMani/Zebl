namespace Zebl.Application.Domain;

/// <summary>
/// Domain entity for Connection Library. Pure domain object with no EF attributes or data annotations.
/// </summary>
public class ConnectionLibrary
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string Host { get; set; } = null!;

    public int Port { get; set; }

    public string Username { get; set; } = null!;

    public string EncryptedPassword { get; set; } = null!;

    public string? UploadDirectory { get; set; }

    public string? DownloadDirectory { get; set; }

    public string? DownloadPattern { get; set; }

    public bool AutoRenameFiles { get; set; }

    public bool AllowMoveOrDelete { get; set; }

    public string? AutoFileExtension { get; set; }

    public bool UseWithInterfacesOnly { get; set; }

    public bool DownloadFromSubdirectories { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    /// <summary>
    /// Constructor enforcing required fields: Name, Host, Username, EncryptedPassword.
    /// </summary>
    public ConnectionLibrary(string name, string host, string username, string encryptedPassword)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required", nameof(name));
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentException("Host is required", nameof(host));
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username is required", nameof(username));
        if (string.IsNullOrWhiteSpace(encryptedPassword))
            throw new ArgumentException("EncryptedPassword is required", nameof(encryptedPassword));

        Name = name;
        Host = host;
        Username = username;
        EncryptedPassword = encryptedPassword;
        Id = Guid.NewGuid();
        Port = 22; // Default SFTP port
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
        ModifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Validates business rules for the entity.
    /// </summary>
    public void Validate()
    {
        if (Port <= 0)
            Port = 22;

        if (AutoRenameFiles && string.IsNullOrWhiteSpace(AutoFileExtension))
            throw new InvalidOperationException("AutoFileExtension is required when AutoRenameFiles is true.");
    }

    /// <summary>
    /// Private parameterless constructor for EF Core materialization only.
    /// </summary>
    private ConnectionLibrary()
    {
    }
}
