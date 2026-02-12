using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zebl.Api.Services;
using Zebl.Application.Dtos.Common;
using Zebl.Application.Dtos.Schema;

namespace Zebl.Api.Controllers;

[ApiController]
[Route("api/schema")]
[Authorize(Policy = "RequireAuth")]
public class SchemaController : ControllerBase
{
    private readonly EntityMetadataService _metadataService;
    private readonly ILogger<SchemaController> _logger;

    public SchemaController(EntityMetadataService metadataService, ILogger<SchemaController> logger)
    {
        _metadataService = metadataService;
        _logger = logger;
    }

    /// <summary>
    /// Get column metadata for entity using EF Core model introspection
    /// Returns business columns with FK relationships identified
    /// Excludes audit fields, GUIDs, and system fields
    /// </summary>
    [HttpGet("columns")]
    public IActionResult GetEntityColumns([FromQuery] string table)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(table))
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_PARAMETER",
                    Message = "Table name is required"
                });
            }

            if (!_metadataService.IsEntitySupported(table))
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_ENTITY",
                    Message = $"Entity '{table}' is not supported"
                });
            }

            var metadata = _metadataService.GetEntityColumns(table);

            _logger.LogInformation("Retrieved {Count} columns for entity {Entity}", 
                metadata.Columns.Count, table);

            return Ok(new ApiResponse<EntityColumnsResponse>
            {
                Data = metadata
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid entity requested: {Entity}", table);
            return BadRequest(new ErrorResponseDto
            {
                ErrorCode = "INVALID_ENTITY",
                Message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting columns for entity {Entity}", table);
            return StatusCode(500, new ErrorResponseDto
            {
                ErrorCode = "SERVER_ERROR",
                Message = "Failed to retrieve entity columns"
            });
        }
    }

    /// <summary>
    /// Get list of available entities with metadata configuration
    /// </summary>
    [HttpGet("entities")]
    public IActionResult GetAvailableEntities()
    {
        try
        {
            var entities = _metadataService.GetAvailableEntities();
            
            return Ok(new ApiResponse<List<string>>
            {
                Data = entities
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available entities");
            return StatusCode(500, new ErrorResponseDto
            {
                ErrorCode = "SERVER_ERROR",
                Message = "Failed to retrieve available entities"
            });
        }
    }
}
