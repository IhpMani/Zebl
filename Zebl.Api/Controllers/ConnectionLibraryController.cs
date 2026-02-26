using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zebl.Application.Dtos.Common;
using Zebl.Application.Dtos.ConnectionLibrary;
using Zebl.Application.Services;
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
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ConnectionLibraryController> _logger;

    public ConnectionLibraryController(
        ConnectionLibraryService service,
        Infrastructure.Services.SftpTransportService sftpService,
        Zebl.Application.Repositories.IConnectionLibraryRepository repository,
        IHttpClientFactory httpClientFactory,
        ILogger<ConnectionLibraryController> logger)
    {
        _service = service;
        _sftpService = sftpService;
        _repository = repository;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Gets all connection libraries.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _service.GetAllAsync();
        return Ok(result);
    }

    /// <summary>
    /// Gets a connection library by ID.
    /// </summary>
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

    /// <summary>
    /// Creates a new connection library.
    /// </summary>
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

    /// <summary>
    /// Updates an existing connection library.
    /// </summary>
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

    /// <summary>
    /// Deletes a connection library.
    /// </summary>
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
    /// Tests connection: HTTP(S) URL (e.g. http://localhost:5001) or SFTP.
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

            var host = (entity.Host ?? "").Trim();
            bool isConnected;
            string? httpUrl = null;

            if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                httpUrl = host;
            }
            // Treat Host + Port as HTTP when port is common for HTTP (e.g. mock on 5001, or 80/443)
            else if (entity.Port == 5001 || entity.Port == 80 || entity.Port == 443)
            {
                var scheme = entity.Port == 443 ? "https" : "http";
                httpUrl = $"{scheme}://{host}:{entity.Port}";
            }
            else if (host.Contains(":5001", StringComparison.OrdinalIgnoreCase) && !host.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                httpUrl = "http://" + host;
            }

            if (httpUrl != null)
            {
                isConnected = await TestHttpConnectionAsync(httpUrl);
            }
            else
            {
                isConnected = await _sftpService.TestConnectionAsync(entity);
            }

            if (isConnected)
            {
                return Ok(new ApiResponse<object>
                {
                    Data = new { success = true, message = "Connection test successful." }
                });
            }
            else
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "CONNECTION_FAILED",
                    Message = "Connection test failed. Please check your credentials and network settings."
                });
            }
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

    /// <summary>
    /// Tests HTTP(S) endpoint (e.g. http://localhost:5001). Tries /api/get-reports first, then base URL.
    /// </summary>
    private async Task<bool> TestHttpConnectionAsync(string baseUrl)
    {
        var baseNormalized = baseUrl.TrimEnd('/');
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);

        Exception? lastEx = null;
        var urlsToTry = new[] { $"{baseNormalized}/api/get-reports", baseNormalized + "/" };
        foreach (var url in urlsToTry)
        {
            try
            {
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("HTTP connection test succeeded: {Url}", url);
                    return true;
                }
                lastEx = new InvalidOperationException($"HTTP {(int)response.StatusCode} at {url}");
            }
            catch (Exception ex)
            {
                lastEx = ex;
                _logger.LogDebug(ex, "HTTP test failed for {Url}", url);
            }
        }

        var msg = lastEx?.Message ?? "Request failed.";
        if (lastEx is HttpRequestException)
            throw new InvalidOperationException($"Cannot reach {baseUrl}. Is the server running? {msg}");
        throw new InvalidOperationException($"HTTP connection test failed: {msg}");
    }
}
