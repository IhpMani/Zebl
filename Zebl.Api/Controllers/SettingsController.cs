using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zebl.Application.Abstractions;
using Zebl.Application.Services;

namespace Zebl.Api.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize(Policy = "RequireAuth")]
public class SettingsController : ControllerBase
{
    private readonly ICurrentContext _currentContext;
    private readonly ISendingClaimsSettingsService _sendingClaimsSettingsService;

    public SettingsController(ICurrentContext currentContext, ISendingClaimsSettingsService sendingClaimsSettingsService)
    {
        _currentContext = currentContext;
        _sendingClaimsSettingsService = sendingClaimsSettingsService;
    }

    [HttpGet("sending-claims")]
    public async Task<IActionResult> GetSendingClaims(CancellationToken cancellationToken)
    {
        var data = await _sendingClaimsSettingsService.GetSettingsAsync(
            _currentContext.TenantId,
            _currentContext.FacilityId,
            cancellationToken);
        return Ok(data);
    }

    [HttpPut("sending-claims")]
    public async Task<IActionResult> UpdateSendingClaims([FromBody] SendingClaimsSettingsDto request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _sendingClaimsSettingsService.UpdateSettingsAsync(
                _currentContext.TenantId,
                _currentContext.FacilityId,
                request,
                cancellationToken);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
