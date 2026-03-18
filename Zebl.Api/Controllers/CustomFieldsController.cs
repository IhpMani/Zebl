using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Api.Controllers;

[ApiController]
[Route("api/custom-fields")]
[Authorize(Policy = "RequireAuth")]
public class CustomFieldsController : ControllerBase
{
    private const int MaxFieldsPerEntityType = 5;
    private static readonly HashSet<string> ValidEntityTypes = new(StringComparer.OrdinalIgnoreCase)
        { "Patient", "Claim", "ServiceLine" };
    private static readonly HashSet<string> ValidFieldTypes = new(StringComparer.OrdinalIgnoreCase)
        { "TEXT", "TEXT-LIST", "CURRENCY", "DATE", "TIME", "NUMBER", "YESNO" };

    private readonly ZeblDbContext _db;

    public CustomFieldsController(ZeblDbContext db)
    {
        _db = db;
    }

    public sealed class CustomFieldDefinitionDto
    {
        public int Id { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public string FieldKey { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string FieldType { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public sealed class CreateCustomFieldRequest
    {
        public string EntityType { get; set; } = string.Empty;
        public string FieldKey { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string FieldType { get; set; } = string.Empty;
        public int SortOrder { get; set; }
    }

    public sealed class UpdateCustomFieldRequest
    {
        public string? Label { get; set; }
        public string? FieldType { get; set; }
        public int? SortOrder { get; set; }
    }

    public sealed class SaveCustomFieldValueRequest
    {
        public string EntityType { get; set; } = string.Empty;
        public int EntityId { get; set; }
        public string FieldKey { get; set; } = string.Empty;
        public string? Value { get; set; }
    }

    private static string NormalizeEntityType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var v = value.Trim();
        if (string.Equals(v, "patient", StringComparison.OrdinalIgnoreCase)) return "Patient";
        if (string.Equals(v, "claim", StringComparison.OrdinalIgnoreCase)) return "Claim";
        if (string.Equals(v, "serviceline", StringComparison.OrdinalIgnoreCase)) return "ServiceLine";
        return v;
    }

    /// <summary>
    /// GET /api/custom-fields/{entityType}
    /// Returns active custom field definitions for the entity type (Patient, Claim, ServiceLine).
    /// </summary>
    [HttpGet("{entityType}")]
    public async Task<IActionResult> GetByEntityType(string entityType, CancellationToken cancellationToken)
    {
        var normalized = NormalizeEntityType(entityType);
        if (string.IsNullOrEmpty(normalized) || !ValidEntityTypes.Contains(normalized))
            return BadRequest(new { error = "Invalid entity type. Use Patient, Claim, or ServiceLine." });

        var list = await _db.CustomFieldDefinitions
            .AsNoTracking()
            .Where(x => x.EntityType == normalized && x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .Select(x => new CustomFieldDefinitionDto
            {
                Id = x.Id,
                EntityType = x.EntityType,
                FieldKey = x.FieldKey,
                Label = x.Label,
                FieldType = x.FieldType,
                SortOrder = x.SortOrder,
                IsActive = x.IsActive,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(list);
    }

    /// <summary>
    /// POST /api/custom-fields - Create new field definition.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomFieldRequest request, CancellationToken cancellationToken)
    {
        var entityType = NormalizeEntityType(request?.EntityType);
        if (string.IsNullOrEmpty(entityType) || !ValidEntityTypes.Contains(entityType))
            return BadRequest(new { error = "Invalid entity type." });

        var fieldKey = (request?.FieldKey ?? "").Trim();
        if (fieldKey.Length == 0 || fieldKey.Length > 50)
            return BadRequest(new { error = "FieldKey must be 1–50 characters." });

        var label = (request?.Label ?? "").Trim();
        if (label.Length == 0 || label.Length > 100)
            return BadRequest(new { error = "Label must be 1–100 characters." });

        if (string.IsNullOrEmpty(request?.FieldType) || !ValidFieldTypes.Contains(request.FieldType))
            return BadRequest(new { error = "Invalid field type." });

        var count = await _db.CustomFieldDefinitions
            .CountAsync(x => x.EntityType == entityType && x.IsActive, cancellationToken);
        if (count >= MaxFieldsPerEntityType)
            return BadRequest(new { error = $"Maximum {MaxFieldsPerEntityType} fields allowed for {entityType}." });

        var exists = await _db.CustomFieldDefinitions
            .AnyAsync(x => x.EntityType == entityType && x.FieldKey == fieldKey, cancellationToken);
        if (exists)
            return BadRequest(new { error = "A field with this FieldKey already exists for this entity type." });

        var sortOrder = request?.SortOrder ?? 0;

        var entity = new CustomFieldDefinition
        {
            EntityType = entityType,
            FieldKey = fieldKey,
            Label = label,
            FieldType = request!.FieldType,
            SortOrder = sortOrder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _db.CustomFieldDefinitions.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetByEntityType), new { entityType }, new CustomFieldDefinitionDto
        {
            Id = entity.Id,
            EntityType = entity.EntityType,
            FieldKey = entity.FieldKey,
            Label = entity.Label,
            FieldType = entity.FieldType,
            SortOrder = entity.SortOrder,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt
        });
    }

    /// <summary>
    /// PUT /api/custom-fields/{id} - Update field definition.
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCustomFieldRequest request, CancellationToken cancellationToken)
    {
        var entity = await _db.CustomFieldDefinitions.FindAsync(new object[] { id }, cancellationToken);
        if (entity == null)
            return NotFound();

        if (request?.Label != null)
        {
            var label = request.Label.Trim();
            if (label.Length > 100)
                return BadRequest(new { error = "Label must be at most 100 characters." });
            entity.Label = label;
        }
        if (request?.FieldType != null)
        {
            if (!ValidFieldTypes.Contains(request.FieldType))
                return BadRequest(new { error = "Invalid field type." });
            entity.FieldType = request.FieldType;
        }
        if (request?.SortOrder != null)
            entity.SortOrder = request.SortOrder.Value;

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new CustomFieldDefinitionDto
        {
            Id = entity.Id,
            EntityType = entity.EntityType,
            FieldKey = entity.FieldKey,
            Label = entity.Label,
            FieldType = entity.FieldType,
            SortOrder = entity.SortOrder,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt
        });
    }

    /// <summary>
    /// DELETE /api/custom-fields/{id} - Deactivate field (soft delete).
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        var entity = await _db.CustomFieldDefinitions.FindAsync(new object[] { id }, cancellationToken);
        if (entity == null)
            return NotFound();
        entity.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// POST /api/custom-fields/value - Save a single custom field value.
    /// </summary>
    [HttpPost("value")]
    public async Task<IActionResult> SaveValue([FromBody] SaveCustomFieldValueRequest request, CancellationToken cancellationToken)
    {
        var entityType = NormalizeEntityType(request?.EntityType);
        if (string.IsNullOrEmpty(entityType) || !ValidEntityTypes.Contains(entityType))
            return BadRequest(new { error = "Invalid entity type." });

        if (request!.EntityId <= 0)
            return BadRequest(new { error = "EntityId must be a positive integer." });

        var fieldKey = (request.FieldKey ?? "").Trim();
        if (fieldKey.Length == 0)
            return BadRequest(new { error = "FieldKey is required." });

        var value = request.Value ?? string.Empty;

        var existing = await _db.CustomFieldValues
            .FirstOrDefaultAsync(
                x => x.EntityType == entityType && x.EntityId == request.EntityId && x.FieldKey == fieldKey,
                cancellationToken);

        if (existing != null)
        {
            existing.Value = value;
        }
        else
        {
            _db.CustomFieldValues.Add(new CustomFieldValue
            {
                EntityType = entityType,
                EntityId = request.EntityId,
                FieldKey = fieldKey,
                Value = value
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// GET /api/custom-fields/values/{entityType}/{entityId} - Get all custom field values for an entity.
    /// </summary>
    [HttpGet("values/{entityType}/{entityId:int}")]
    public async Task<IActionResult> GetValues(string entityType, int entityId, CancellationToken cancellationToken)
    {
        var normalized = NormalizeEntityType(entityType);
        if (string.IsNullOrEmpty(normalized) || !ValidEntityTypes.Contains(normalized))
            return BadRequest(new { error = "Invalid entity type." });

        var values = await _db.CustomFieldValues
            .AsNoTracking()
            .Where(x => x.EntityType == normalized && x.EntityId == entityId)
            .Select(x => new { x.FieldKey, x.Value })
            .ToListAsync(cancellationToken);

        var dict = values.ToDictionary(x => x.FieldKey, x => x.Value ?? string.Empty);
        return Ok(dict);
    }
}
