using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zebl.Application.Domain;
using Zebl.Application.Dtos.Common;
using Zebl.Application.Dtos.ReceiverLibrary;
using Zebl.Application.Services;

namespace Zebl.Api.Controllers;

[ApiController]
[Route("api/receiver-library")]
[Authorize(Policy = "RequireAuth")]
public class ReceiverLibraryController : ControllerBase
{
    private readonly ReceiverLibraryService _service;
    private readonly ILogger<ReceiverLibraryController> _logger;

    public ReceiverLibraryController(
        ReceiverLibraryService service,
        ILogger<ReceiverLibraryController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var result = await _service.GetAllAsync();
            return Ok(new ApiResponse<List<ReceiverLibraryDto>>
            {
                Data = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all receiver libraries");
            return StatusCode(500, new ErrorResponseDto
            {
                ErrorCode = "INTERNAL_ERROR",
                Message = "An error occurred while retrieving receiver libraries"
            });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null)
            {
                return NotFound(new ErrorResponseDto
                {
                    ErrorCode = "NOT_FOUND",
                    Message = $"Receiver library with id '{id}' not found"
                });
            }

            return Ok(new ApiResponse<ReceiverLibraryDto>
            {
                Data = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting receiver library by id: {Id}", id);
            return StatusCode(500, new ErrorResponseDto
            {
                ErrorCode = "INTERNAL_ERROR",
                Message = "An error occurred while retrieving the receiver library"
            });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReceiverLibraryCommand command)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ErrorResponseDto
            {
                ErrorCode = "VALIDATION_ERROR",
                Message = "Invalid receiver library data"
            });
        }

        try
        {
            var result = await _service.CreateAsync(command);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, new ApiResponse<ReceiverLibraryDto>
            {
                Data = result
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ErrorResponseDto
            {
                ErrorCode = "DUPLICATE_NAME",
                Message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating receiver library");
            return StatusCode(500, new ErrorResponseDto
            {
                ErrorCode = "INTERNAL_ERROR",
                Message = "An error occurred while creating the receiver library"
            });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateReceiverLibraryCommand command)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ErrorResponseDto
            {
                ErrorCode = "VALIDATION_ERROR",
                Message = "Invalid receiver library data"
            });
        }

        try
        {
            var result = await _service.UpdateAsync(id, command);
            return Ok(new ApiResponse<ReceiverLibraryDto>
            {
                Data = result
            });
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("not found"))
            {
                return NotFound(new ErrorResponseDto
                {
                    ErrorCode = "NOT_FOUND",
                    Message = ex.Message
                });
            }
            return Conflict(new ErrorResponseDto
            {
                ErrorCode = "DUPLICATE_NAME",
                Message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating receiver library: {Id}", id);
            return StatusCode(500, new ErrorResponseDto
            {
                ErrorCode = "INTERNAL_ERROR",
                Message = "An error occurred while updating the receiver library"
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
            return NotFound(new ErrorResponseDto
            {
                ErrorCode = "NOT_FOUND",
                Message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting receiver library: {Id}", id);
            return StatusCode(500, new ErrorResponseDto
            {
                ErrorCode = "INTERNAL_ERROR",
                Message = "An error occurred while deleting the receiver library"
            });
        }
    }

    [HttpGet("export-formats")]
    public IActionResult GetExportFormats()
    {
        var formats = Enum.GetValues(typeof(ExportFormat))
            .Cast<ExportFormat>()
            .Select(e => new 
            { 
                Value = (int)e, 
                Name = e switch
                {
                    ExportFormat.Ansi837_wTilde => "ANSI 837 w/~",
                    ExportFormat.Eligibility270 => "Eligibility Inquiry 270",
                    _ => e.ToString()
                }
            })
            .ToList();

        return Ok(new ApiResponse<List<object>>
        {
            Data = formats.Cast<object>().ToList()
        });
    }
}
