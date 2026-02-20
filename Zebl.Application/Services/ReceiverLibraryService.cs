using Zebl.Application.Domain;
using Zebl.Application.Dtos.ReceiverLibrary;
using Zebl.Application.Repositories;

namespace Zebl.Application.Services;

/// <summary>
/// Application service for Receiver Library. Contains business rules and depends only on IReceiverLibraryRepository.
/// NO EF references here.
/// </summary>
public class ReceiverLibraryService
{
    private readonly IReceiverLibraryRepository _repository;

    public ReceiverLibraryService(IReceiverLibraryRepository repository)
    {
        _repository = repository;
    }

    public async Task<ReceiverLibraryDto?> GetByIdAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        return entity == null ? null : MapToDto(entity);
    }

    public async Task<List<ReceiverLibraryDto>> GetAllAsync()
    {
        var entities = await _repository.GetAllAsync();
        return entities.Select(MapToDto).ToList();
    }

    public async Task<ReceiverLibraryDto> CreateAsync(CreateReceiverLibraryCommand command)
    {
        // Business rule: LibraryEntryName must be unique
        if (await _repository.ExistsByNameAsync(command.LibraryEntryName))
        {
            throw new InvalidOperationException($"A receiver library with name '{command.LibraryEntryName}' already exists.");
        }

        // Business rule: Default IsActive = true (already set in command, but ensure it)
        var entity = new ReceiverLibrary(command.LibraryEntryName, command.ExportFormat)
        {
            ClaimType = command.ClaimType,
            SubmitterType = command.SubmitterType,
            BusinessOrLastName = command.BusinessOrLastName,
            FirstName = command.FirstName,
            SubmitterId = command.SubmitterId,
            ContactName = command.ContactName,
            ContactType = command.ContactType,
            ContactValue = command.ContactValue,
            ReceiverName = command.ReceiverName,
            ReceiverId = command.ReceiverId,
            AuthorizationInfoQualifier = command.AuthorizationInfoQualifier,
            AuthorizationInfo = command.AuthorizationInfo,
            SecurityInfoQualifier = command.SecurityInfoQualifier,
            SecurityInfo = command.SecurityInfo,
            SenderQualifier = command.SenderQualifier,
            SenderId = command.SenderId,
            ReceiverQualifier = command.ReceiverQualifier,
            InterchangeReceiverId = command.InterchangeReceiverId,
            AcknowledgeRequested = command.AcknowledgeRequested,
            TestProdIndicator = command.TestProdIndicator,
            SenderCode = command.SenderCode,
            ReceiverCode = command.ReceiverCode,
            IsActive = command.IsActive
        };

        // Validate required ISA fields (business rule)
        ValidateIsaFields(entity);

        await _repository.AddAsync(entity);
        return MapToDto(entity);
    }

    public async Task<ReceiverLibraryDto> UpdateAsync(Guid id, UpdateReceiverLibraryCommand command)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
        {
            throw new InvalidOperationException($"Receiver library with id '{id}' not found.");
        }

        // Business rule: LibraryEntryName must be unique (check if name changed)
        if (entity.LibraryEntryName != command.LibraryEntryName)
        {
            if (await _repository.ExistsByNameAsync(command.LibraryEntryName))
            {
                throw new InvalidOperationException($"A receiver library with name '{command.LibraryEntryName}' already exists.");
            }
        }

        // Update entity properties
        entity.LibraryEntryName = command.LibraryEntryName;
        entity.ExportFormat = command.ExportFormat;
        entity.ClaimType = command.ClaimType;
        entity.SubmitterType = command.SubmitterType;
        entity.BusinessOrLastName = command.BusinessOrLastName;
        entity.FirstName = command.FirstName;
        entity.SubmitterId = command.SubmitterId;
        entity.ContactName = command.ContactName;
        entity.ContactType = command.ContactType;
        entity.ContactValue = command.ContactValue;
        entity.ReceiverName = command.ReceiverName;
        entity.ReceiverId = command.ReceiverId;
        entity.AuthorizationInfoQualifier = command.AuthorizationInfoQualifier;
        entity.AuthorizationInfo = command.AuthorizationInfo;
        entity.SecurityInfoQualifier = command.SecurityInfoQualifier;
        entity.SecurityInfo = command.SecurityInfo;
        entity.SenderQualifier = command.SenderQualifier;
        entity.SenderId = command.SenderId;
        entity.ReceiverQualifier = command.ReceiverQualifier;
        entity.InterchangeReceiverId = command.InterchangeReceiverId;
        entity.AcknowledgeRequested = command.AcknowledgeRequested;
        entity.TestProdIndicator = command.TestProdIndicator;
        entity.SenderCode = command.SenderCode;
        entity.ReceiverCode = command.ReceiverCode;
        entity.IsActive = command.IsActive;
        entity.ModifiedAt = DateTime.UtcNow;

        // Validate required ISA fields (business rule)
        ValidateIsaFields(entity);

        await _repository.UpdateAsync(entity);
        return MapToDto(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
        {
            throw new InvalidOperationException($"Receiver library with id '{id}' not found.");
        }

        await _repository.DeleteAsync(id);
    }

    private void ValidateIsaFields(ReceiverLibrary entity)
    {
        // Business rule: SenderQualifier must be exactly 2 characters
        if (!string.IsNullOrWhiteSpace(entity.SenderQualifier) && entity.SenderQualifier.Length != 2)
        {
            throw new InvalidOperationException("SenderQualifier must be exactly 2 characters.");
        }

        // Business rule: ReceiverQualifier must be exactly 2 characters
        if (!string.IsNullOrWhiteSpace(entity.ReceiverQualifier) && entity.ReceiverQualifier.Length != 2)
        {
            throw new InvalidOperationException("ReceiverQualifier must be exactly 2 characters.");
        }

        // Business rule: SenderId max 15 characters
        if (!string.IsNullOrWhiteSpace(entity.SenderId) && entity.SenderId.Length > 15)
        {
            throw new InvalidOperationException("SenderId must not exceed 15 characters.");
        }

        // Business rule: InterchangeReceiverId (ISA08) max 15 characters
        if (!string.IsNullOrWhiteSpace(entity.InterchangeReceiverId) && entity.InterchangeReceiverId.Length > 15)
        {
            throw new InvalidOperationException("InterchangeReceiverId (ISA08) must not exceed 15 characters.");
        }
    }

    private ReceiverLibraryDto MapToDto(ReceiverLibrary entity)
    {
        return new ReceiverLibraryDto
        {
            Id = entity.Id,
            LibraryEntryName = entity.LibraryEntryName,
            ExportFormat = entity.ExportFormat,
            ClaimType = entity.ClaimType,
            SubmitterType = entity.SubmitterType,
            BusinessOrLastName = entity.BusinessOrLastName,
            FirstName = entity.FirstName,
            SubmitterId = entity.SubmitterId,
            ContactName = entity.ContactName,
            ContactType = entity.ContactType,
            ContactValue = entity.ContactValue,
            ReceiverName = entity.ReceiverName,
            ReceiverId = entity.ReceiverId,
            AuthorizationInfoQualifier = entity.AuthorizationInfoQualifier,
            AuthorizationInfo = entity.AuthorizationInfo,
            SecurityInfoQualifier = entity.SecurityInfoQualifier,
            SecurityInfo = entity.SecurityInfo,
            SenderQualifier = entity.SenderQualifier,
            SenderId = entity.SenderId,
            ReceiverQualifier = entity.ReceiverQualifier,
            InterchangeReceiverId = entity.InterchangeReceiverId,
            AcknowledgeRequested = entity.AcknowledgeRequested,
            TestProdIndicator = entity.TestProdIndicator,
            SenderCode = entity.SenderCode,
            ReceiverCode = entity.ReceiverCode,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            ModifiedAt = entity.ModifiedAt
        };
    }
}
