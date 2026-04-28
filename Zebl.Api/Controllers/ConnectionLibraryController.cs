using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zebl.Application.Domain;
using Zebl.Application.Dtos.Common;
using Zebl.Application.Dtos.ConnectionLibrary;
using Zebl.Application.Services;
using Zebl.Infrastructure.Services;
using ErrorResponseDto = Zebl.Application.Dtos.Common.ErrorResponseDto;

namespace Zebl.Api.Controllers;

[ApiController]
[Route("api/connections")]
[Authorize(Policy = "RequireAuth")]
public class ConnectionLibraryController : ControllerBase
{
    private readonly ConnectionLibraryService _service;
    private readonly Infrastructure.Services.SftpTransportService _sftpService;
    private readonly Zebl.Application.Repositories.IConnectionLibraryRepository _repository;
    private readonly HttpInboundTransportService _httpInboundTransport;
    private readonly ILogger<ConnectionLibraryController> _logger;

    public ConnectionLibraryController(
        ConnectionLibraryService service,
        Infrastructure.Services.SftpTransportService sftpService,
        Zebl.Application.Repositories.IConnectionLibraryRepository repository,
        HttpInboundTransportService httpInboundTransport,
        ILogger<ConnectionLibraryController> logger)
    {
        _service = service;
        _sftpService = sftpService;
        _repository = repository;
        _httpInboundTransport = httpInboundTransport;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = (await _service.GetAllAsync())
            .Where(c => c.IsActive)
            .ToList();
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var connection = await _service.GetByIdAsync(id);
        if (connection == null)
        {
            return NotFound(new ErrorResponseDto
            {
                ErrorCode = "NOT_FOUND",
                Message = $"Connection library with id '{id}' not found"
            });
        }
        return Ok(new ApiResponse<ConnectionLibraryDto> { Data = connection });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateConnectionLibraryCommand command)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ErrorResponseDto
            {
                ErrorCode = "VALIDATION_ERROR",
                Message = "Invalid model state"
            });
        }

        try
        {
            var connection = await _service.CreateAsync(command);
            return CreatedAtAction(nameof(GetById), new { id = connection.Id },
                new ApiResponse<ConnectionLibraryDto>
                {
                    Data = connection
                });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Business rule violation creating connection library");
            return BadRequest(new ErrorResponseDto
            {
                ErrorCode = "BUSINESS_RULE_VIOLATION",
                Message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating connection library");
            return StatusCode(500, new ErrorResponseDto
            {
                ErrorCode = "INTERNAL_ERROR",
                Message = "An error occurred while creating the connection library"
            });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateConnectionLibraryCommand command)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ErrorResponseDto
            {
                ErrorCode = "VALIDATION_ERROR",
                Message = "Invalid model state"
            });
        }

        try
        {
            var connection = await _service.UpdateAsync(id, command);
            return Ok(new ApiResponse<ConnectionLibraryDto>
            {
                Data = connection
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Business rule violation updating connection library {Id}", id);
            return BadRequest(new ErrorResponseDto
            {
                ErrorCode = "BUSINESS_RULE_VIOLATION",
                Message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating connection library {Id}", id);
            return StatusCode(500, new ErrorResponseDto
            {
                ErrorCode = "INTERNAL_ERROR",
                Message = "An error occurred while updating the connection library"
            });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _service.DeleteAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Business rule violation deleting connection library {Id}", id);
            return NotFound(new ErrorResponseDto
            {
                ErrorCode = "NOT_FOUND",
                Message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting connection library {Id}", id);
            return StatusCode(500, new ErrorResponseDto
            {
                ErrorCode = "INTERNAL_ERROR",
                Message = "An error occurred while deleting the connection library"
            });
        }
    }

    /// <summary>
    /// Tests connectivity using explicit <see cref="ConnectionType"/> (never inferred from host/port).
    /// </summary>
    [HttpPost("{id}/test")]
    public async Task<IActionResult> TestConnection(Guid id)
    {
        try
        {
            var entity = await _repository.GetByIdAsync(id);
            if (entity == null)
            {
                return NotFound(new ErrorResponseDto
                {
                    ErrorCode = "NOT_FOUND",
                    Message = $"Connection library with id '{id}' not found"
                });
            }

            bool ok;
            switch (entity.ConnectionType)
            {
                case ConnectionType.Sftp:
                    ok = await _sftpService.TestConnectionAsync(entity);
                    break;
                case ConnectionType.Http:
                case ConnectionType.Api:
                    _ = await _httpInboundTransport.FetchAsync(entity, HttpContext.RequestAborted);
                    ok = true;
                    break;
                default:
                    throw new InvalidOperationException("Unsupported connection type.");
            }

            if (ok)
            {
                return Ok(new ApiResponse<object>
                {
                    Data = new { success = true, message = "Connection test successful." }
                });
            }

            return BadRequest(new ErrorResponseDto
            {
                ErrorCode = "CONNECTION_FAILED",
                Message = "Connection test failed. Please check your credentials and network settings."
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Connection test failed for library {Id}", id);
            return BadRequest(new ErrorResponseDto
            {
                ErrorCode = "CONNECTION_FAILED",
                Message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing connection for library {Id}", id);
            return StatusCode(500, new ErrorResponseDto
            {
                ErrorCode = "INTERNAL_ERROR",
                Message = ex.Message
            });
        }
    }
}
