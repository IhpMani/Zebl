using Microsoft.Extensions.Logging;
using Zebl.Application.Domain;
using Zebl.Application.Dtos.ConnectionLibrary;
using Zebl.Application.Repositories;

namespace Zebl.Application.Services;

/// <summary>
/// Application service for Connection Library. Contains business rules and depends only on repositories and encryption service.
/// NO EF references here.
/// </summary>
public class ConnectionLibraryService
{
    private readonly IConnectionLibraryRepository _repository;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<ConnectionLibraryService> _logger;

    public ConnectionLibraryService(
        IConnectionLibraryRepository repository,
        IEncryptionService encryptionService,
        ILogger<ConnectionLibraryService> logger)
    {
        _repository = repository;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public async Task<ConnectionLibraryDto?> GetByIdAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        return entity == null ? null : MapToDto(entity);
    }

    public async Task<List<ConnectionLibraryDto>> GetAllAsync()
    {
        try
        {
            var entities = await _repository.GetAllAsync();
            return entities.Select(MapToDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving connections");
            throw;
        }
    }

    public async Task<ConnectionLibraryDto> CreateAsync(CreateConnectionLibraryCommand command)
    {
        // Business rule: Name must be unique
        if (await _repository.ExistsByNameAsync(command.Name))
        {
            throw new InvalidOperationException($"A connection library with name '{command.Name}' already exists.");
        }

        // Business rule: Default Port = 22 if not provided
        var port = command.Port > 0 ? command.Port : 22;

        // Business rule: Encrypt password before saving
        var encryptedPassword = _encryptionService.Encrypt(command.Password);

        // Business rule: Validate AutoRenameFiles requires AutoFileExtension
        if (command.AutoRenameFiles && string.IsNullOrWhiteSpace(command.AutoFileExtension))
        {
            throw new InvalidOperationException("AutoFileExtension is required when AutoRenameFiles is true.");
        }

        var entity = new ConnectionLibrary(command.Name, command.Host, command.Username, encryptedPassword)
        {
            Port = port,
            UploadDirectory = command.UploadDirectory,
            DownloadDirectory = command.DownloadDirectory,
            DownloadPattern = command.DownloadPattern,
            AutoRenameFiles = command.AutoRenameFiles,
            AllowMoveOrDelete = command.AllowMoveOrDelete,
            AutoFileExtension = command.AutoFileExtension,
            UseWithInterfacesOnly = command.UseWithInterfacesOnly,
            DownloadFromSubdirectories = command.DownloadFromSubdirectories,
            IsActive = command.IsActive
        };

        entity.Validate();

        await _repository.AddAsync(entity);
        return MapToDto(entity);
    }

    public async Task<ConnectionLibraryDto> UpdateAsync(Guid id, UpdateConnectionLibraryCommand command)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
        {
            throw new InvalidOperationException($"Connection library with id '{id}' not found.");
        }

        // Business rule: Name must be unique (check if name changed)
        if (entity.Name != command.Name)
        {
            if (await _repository.ExistsByNameAsync(command.Name))
            {
                throw new InvalidOperationException($"A connection library with name '{command.Name}' already exists.");
            }
        }

        // Business rule: Default Port = 22 if not provided
        var port = command.Port > 0 ? command.Port : 22;

        // Business rule: Encrypt password only if provided
        var encryptedPassword = entity.EncryptedPassword; // Keep existing if not provided
        if (!string.IsNullOrWhiteSpace(command.Password))
        {
            encryptedPassword = _encryptionService.Encrypt(command.Password);
        }

        // Business rule: Validate AutoRenameFiles requires AutoFileExtension
        if (command.AutoRenameFiles && string.IsNullOrWhiteSpace(command.AutoFileExtension))
        {
            throw new InvalidOperationException("AutoFileExtension is required when AutoRenameFiles is true.");
        }

        // Update entity properties
        entity.Name = command.Name;
        entity.Host = command.Host;
        entity.Port = port;
        entity.Username = command.Username;
        entity.EncryptedPassword = encryptedPassword;
        entity.UploadDirectory = command.UploadDirectory;
        entity.DownloadDirectory = command.DownloadDirectory;
        entity.DownloadPattern = command.DownloadPattern;
        entity.AutoRenameFiles = command.AutoRenameFiles;
        entity.AllowMoveOrDelete = command.AllowMoveOrDelete;
        entity.AutoFileExtension = command.AutoFileExtension;
        entity.UseWithInterfacesOnly = command.UseWithInterfacesOnly;
        entity.DownloadFromSubdirectories = command.DownloadFromSubdirectories;
        entity.IsActive = command.IsActive;
        entity.ModifiedAt = DateTime.UtcNow;

        entity.Validate();

        await _repository.UpdateAsync(entity);
        return MapToDto(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
        {
            throw new InvalidOperationException($"Connection library with id '{id}' not found.");
        }

        await _repository.DeleteAsync(id);
    }

    private ConnectionLibraryDto MapToDto(ConnectionLibrary entity)
    {
        // Never decrypt in Application layer. Decrypt only in SftpTransportService (Infrastructure).
        return new ConnectionLibraryDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Host = entity.Host,
            Port = entity.Port,
            Username = entity.Username,
            Password = "********",
            UploadDirectory = entity.UploadDirectory,
            DownloadDirectory = entity.DownloadDirectory,
            DownloadPattern = entity.DownloadPattern,
            AutoRenameFiles = entity.AutoRenameFiles,
            AllowMoveOrDelete = entity.AllowMoveOrDelete,
            AutoFileExtension = entity.AutoFileExtension,
            UseWithInterfacesOnly = entity.UseWithInterfacesOnly,
            DownloadFromSubdirectories = entity.DownloadFromSubdirectories,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            ModifiedAt = entity.ModifiedAt
        };
    }
}
