namespace Zebl.Application.Dtos.ConnectionLibrary;

public class ConnectionLibraryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Host { get; set; } = null!;
    public int Port { get; set; }
    public string Username { get; set; } = null!;
    
    /// <summary>
    /// Password is always masked in DTOs. Never expose EncryptedPassword.
    /// </summary>
    public string Password { get; set; } = "********";
    
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
}
