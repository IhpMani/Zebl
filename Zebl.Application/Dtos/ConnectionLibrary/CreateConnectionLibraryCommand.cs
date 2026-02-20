using System.ComponentModel.DataAnnotations;

namespace Zebl.Application.Dtos.ConnectionLibrary;

public class CreateConnectionLibraryCommand
{
    [Required(ErrorMessage = "Name is required")]
    [MaxLength(255)]
    public string Name { get; set; } = null!;

    [Required(ErrorMessage = "Host is required")]
    [MaxLength(255)]
    public string Host { get; set; } = null!;

    [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535")]
    public int Port { get; set; } = 22;

    [Required(ErrorMessage = "Username is required")]
    [MaxLength(255)]
    public string Username { get; set; } = null!;

    [Required(ErrorMessage = "Password is required")]
    public string Password { get; set; } = null!; // Plain text password from client

    [MaxLength(500)]
    public string? UploadDirectory { get; set; }

    [MaxLength(500)]
    public string? DownloadDirectory { get; set; }

    [MaxLength(100)]
    public string? DownloadPattern { get; set; }

    public bool AutoRenameFiles { get; set; }

    public bool AllowMoveOrDelete { get; set; }

    [MaxLength(10)]
    public string? AutoFileExtension { get; set; }

    public bool UseWithInterfacesOnly { get; set; }

    public bool DownloadFromSubdirectories { get; set; }

    public bool IsActive { get; set; } = true;
}
