using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zebl.Application.Domain;
using Zebl.Application.Dtos.Common;
using Zebl.Application.Dtos.Payers;
using Zebl.Application.Services;

namespace Zebl.Api.Controllers;

[ApiController]
[Route("api/payers")]
[Authorize(Policy = "RequireAuth")]
public class PayersController : ControllerBase
{
    private readonly PayerService _payerService;
    private readonly ILogger<PayersController> _logger;

    public PayersController(PayerService payerService, ILogger<PayersController> logger)
    {
        _payerService = payerService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetPayers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        [FromQuery] bool inactive = false,
        [FromQuery] string? classificationList = null)
    {
        if (page < 1 || pageSize < 1 || pageSize > 5000)
        {
            return BadRequest(new ErrorResponseDto
            {
                ErrorCode = "INVALID_ARGUMENT",
                Message = "Page must be at least 1 and page size must be between 1 and 5000"
            });
        }

        var (items, totalCount) = await _payerService.GetPagedAsync(page, pageSize, inactive, classificationList);
        var data = items.Select(p => new PayerListItemDto
        {
            PayID = p.PayID,
            PayDateTimeCreated = p.PayDateTimeCreated,
            PayName = p.PayName,
            PayClassification = p.PayClassification,
            PayClaimType = p.PayClaimType,
            PayExternalID = p.PayExternalID,
            PayAddr1 = p.PayAddr1,
            PayCity = p.PayCity,
            PayState = p.PayState,
            PayZip = p.PayZip,
            PayPhoneNo = p.PayPhoneNo,
            PayEmail = p.PayEmail,
            PayInactive = p.PayInactive,
            PaySubmissionMethod = p.PaySubmissionMethod,
            AdditionalColumns = new Dictionary<string, object?>()
        }).ToList();

        return Ok(new { data, totalCount });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var payer = await _payerService.GetByIdAsync(id);
        if (payer == null)
            return NotFound();
        return Ok(MapToDetailDto(payer));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePayerRequest request)
    {
        if (request == null)
            return BadRequest(new ErrorResponseDto { ErrorCode = "INVALID_REQUEST", Message = "Request body is required." });

        var payer = MapRequestToDomain(request);
        try
        {
            var created = await _payerService.CreateAsync(payer);
            return StatusCode(201, MapToDetailDto(created));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Payer ID is required"))
        {
            return BadRequest(new ErrorResponseDto { ErrorCode = "VALIDATION_ERROR", Message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePayerRequest request)
    {
        if (request == null || request.PayID != id)
            return BadRequest(new ErrorResponseDto { ErrorCode = "INVALID_REQUEST", Message = "Request body or ID mismatch." });

        var payer = MapRequestToDomain(request);
        payer.PayID = id;
        try
        {
            await _payerService.UpdateAsync(payer);
            var updated = await _payerService.GetByIdAsync(id);
            return Ok(MapToDetailDto(updated!));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Payer ID is required"))
        {
            return BadRequest(new ErrorResponseDto { ErrorCode = "VALIDATION_ERROR", Message = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound();
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _payerService.DeleteAsync(id);
            return Ok(new { success = true });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("in use"))
        {
            return StatusCode(409, new ErrorResponseDto { ErrorCode = "IN_USE", Message = ex.Message });
        }
    }

    [HttpGet("available-columns")]
    public IActionResult GetAvailableColumns()
    {
        var availableColumns = RelatedColumnConfig.GetAvailableColumns()["Payer"];
        return Ok(new ApiResponse<List<RelatedColumnDefinition>>
        {
            Data = availableColumns
        });
    }

    private static PayerDetailDto MapToDetailDto(Payer p)
    {
        return new PayerDetailDto
        {
            PayID = p.PayID,
            PayDateTimeCreated = p.PayDateTimeCreated,
            PayDateTimeModified = p.PayDateTimeModified,
            PayName = p.PayName,
            PayExternalID = p.PayExternalID,
            PayAddr1 = p.PayAddr1,
            PayAddr2 = p.PayAddr2,
            PayBox1 = p.PayBox1,
            PayCity = p.PayCity,
            PayState = p.PayState,
            PayZip = p.PayZip,
            PayPhoneNo = p.PayPhoneNo,
            PayEmail = p.PayEmail,
            PayFaxNo = p.PayFaxNo,
            PayWebsite = p.PayWebsite,
            PayNotes = p.PayNotes,
            PayOfficeNumber = p.PayOfficeNumber,
            PaySubmissionMethod = p.PaySubmissionMethod,
            PayClaimFilingIndicator = p.PayClaimFilingIndicator,
            PayClaimType = p.PayClaimType,
            PayInsTypeCode = p.PayInsTypeCode,
            PayClassification = p.PayClassification,
            PayPaymentMatchingKey = p.PayPaymentMatchingKey,
            PayEligibilityPayerID = p.PayEligibilityPayerID,
            PayEligibilityPhyID = p.PayEligibilityPhyID,
            PayFollowUpDays = p.PayFollowUpDays,
            PayICDIndicator = p.PayICDIndicator,
            PayInactive = p.PayInactive,
            PayIgnoreRenderingProvider = p.PayIgnoreRenderingProvider,
            PayForwardsClaims = p.PayForwardsClaims,
            PayExportAuthIn2400 = p.PayExportAuthIn2400,
            PayExportSSN = p.PayExportSSN,
            PayExportOriginalRefIn2330B = p.PayExportOriginalRefIn2330B,
            PayExportPaymentDateIn2330B = p.PayExportPaymentDateIn2330B,
            PayExportPatientAmtDueIn2430 = p.PayExportPatientAmtDueIn2430,
            PayUseTotalAppliedInBox29 = p.PayUseTotalAppliedInBox29,
            PayPrintBox30 = p.PayPrintBox30,
            PaySuppressWhenPrinting = p.PaySuppressWhenPrinting
        };
    }

    private static Payer MapRequestToDomain(CreatePayerRequest r)
    {
        return new Payer
        {
            PayName = r.PayName,
            PayExternalID = r.PayExternalID,
            PayAddr1 = r.PayAddr1,
            PayAddr2 = r.PayAddr2,
            PayBox1 = r.PayBox1,
            PayCity = r.PayCity,
            PayState = r.PayState,
            PayZip = r.PayZip,
            PayPhoneNo = r.PayPhoneNo,
            PayEmail = r.PayEmail,
            PayFaxNo = r.PayFaxNo,
            PayWebsite = r.PayWebsite,
            PayNotes = r.PayNotes,
            PayOfficeNumber = r.PayOfficeNumber,
            PaySubmissionMethod = r.PaySubmissionMethod ?? "Paper",
            PayClaimFilingIndicator = r.PayClaimFilingIndicator,
            PayClaimType = r.PayClaimType ?? "Professional",
            PayInsTypeCode = r.PayInsTypeCode,
            PayClassification = r.PayClassification,
            PayPaymentMatchingKey = r.PayPaymentMatchingKey,
            PayEligibilityPayerID = r.PayEligibilityPayerID,
            PayEligibilityPhyID = r.PayEligibilityPhyID,
            PayFollowUpDays = r.PayFollowUpDays,
            PayICDIndicator = r.PayICDIndicator,
            PayInactive = r.PayInactive,
            PayIgnoreRenderingProvider = r.PayIgnoreRenderingProvider,
            PayForwardsClaims = r.PayForwardsClaims,
            PayExportAuthIn2400 = r.PayExportAuthIn2400,
            PayExportSSN = r.PayExportSSN,
            PayExportOriginalRefIn2330B = r.PayExportOriginalRefIn2330B,
            PayExportPaymentDateIn2330B = r.PayExportPaymentDateIn2330B,
            PayExportPatientAmtDueIn2430 = r.PayExportPatientAmtDueIn2430,
            PayUseTotalAppliedInBox29 = r.PayUseTotalAppliedInBox29,
            PayPrintBox30 = r.PayPrintBox30,
            PaySuppressWhenPrinting = r.PaySuppressWhenPrinting
        };
    }
}
