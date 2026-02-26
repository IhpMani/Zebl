using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zebl.Application.Dtos.Common;
using Zebl.Application.Dtos.Payments;
using Zebl.Application.Exceptions;
using Zebl.Application.Repositories;
using Zebl.Application.Services;

namespace Zebl.Api.Controllers
{
    [ApiController]
    [Route("api/payments")]
    [Authorize(Policy = "RequireAuth")]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentRepository _paymentRepo;
        private readonly IPaymentService _paymentService;
        private readonly IServiceLineRepository _serviceLineRepo;
        private readonly ILogger<PaymentsController> _logger;

        public PaymentsController(IPaymentRepository paymentRepo, IPaymentService paymentService, IServiceLineRepository serviceLineRepo, ILogger<PaymentsController> logger)
        {
            _paymentRepo = paymentRepo;
            _paymentService = paymentService;
            _serviceLineRepo = serviceLineRepo;
            _logger = logger;
        }

        /// <summary>Get service lines for payment entry grid by patient (and optional payer).</summary>
        [HttpGet("entry/service-lines"), ActionName(nameof(GetServiceLinesForEntry))]
        public async Task<IActionResult> GetServiceLinesForEntry([FromQuery] int patientId, [FromQuery] int? payerId = null)
        {
            if (patientId <= 0)
                return BadRequest(new ErrorResponseDto { ErrorCode = "INVALID_ARGUMENT", Message = "patientId is required and must be greater than 0." });
            var isPayerSource = payerId.HasValue && payerId.Value > 0;
            var lines = await _serviceLineRepo.GetPaymentEntryLinesAsync(patientId, payerId, isPayerSource);
            return Ok(new ApiResponse<List<PaymentEntryServiceLineDto>> { Data = lines });
        }

        /// <summary>Get a single payment by ID for the edit form.</summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetPaymentById(int id)
        {
            var p = await _paymentRepo.GetPaymentForEditAsync(id);
            if (p == null)
                return NotFound(new ErrorResponseDto { ErrorCode = "NOT_FOUND", Message = "Payment not found." });
            return Ok(new ApiResponse<PaymentForEditDto> { Data = p });
        }

        /// <summary>Create payment (payment entry). Validates source, duplicate, applies to service lines, creates adjustments, recalculates claim totals.</summary>
        [HttpPost]
        public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentCommand command)
        {
            if (command == null)
                return BadRequest(new ErrorResponseDto { ErrorCode = "INVALID_ARGUMENT", Message = "Request body required." });
            try
            {
                var paymentId = await _paymentService.CreatePaymentAsync(command);
                return Ok(new ApiResponse<int> { Data = paymentId });
            }
            catch (DuplicatePaymentException ex)
            {
                return Conflict(new ErrorResponseDto { ErrorCode = "DUPLICATE_PAYMENT", Message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ErrorResponseDto { ErrorCode = "VALIDATION", Message = ex.Message });
            }
        }

        /// <summary>Auto-apply remaining payment amount to service lines (oldest claim first).</summary>
        [HttpPost("{id:int}/auto-apply")]
        public async Task<IActionResult> AutoApply(int id)
        {
            try
            {
                await _paymentService.AutoApplyPaymentAsync(id);
                return Ok(new ApiResponse<object> { Data = new { paymentId = id } });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ErrorResponseDto { ErrorCode = "VALIDATION", Message = ex.Message });
            }
        }

        /// <summary>Disburse remaining amount to given service line applications.</summary>
        [HttpPost("{id:int}/disburse")]
        public async Task<IActionResult> Disburse(int id, [FromBody] List<ServiceLineApplicationDto> applications)
        {
            if (applications == null) applications = new List<ServiceLineApplicationDto>();
            try
            {
                await _paymentService.DisburseRemainingAsync(id, applications);
                return Ok(new ApiResponse<object> { Data = new { paymentId = id } });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ErrorResponseDto { ErrorCode = "VALIDATION", Message = ex.Message });
            }
        }

        /// <summary>Modify payment (reverses and recreates with new command). Recalculates balances. Returns new payment ID.</summary>
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Modify(int id, [FromBody] CreatePaymentCommand command)
        {
            if (command == null)
                return BadRequest(new ErrorResponseDto { ErrorCode = "INVALID_ARGUMENT", Message = "Request body required." });
            try
            {
                var newId = await _paymentService.ModifyPaymentAsync(id, command);
                return Ok(new ApiResponse<int> { Data = newId });
            }
            catch (DuplicatePaymentException ex)
            {
                return Conflict(new ErrorResponseDto { ErrorCode = "DUPLICATE_PAYMENT", Message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ErrorResponseDto { ErrorCode = "VALIDATION", Message = ex.Message });
            }
        }

        /// <summary>Delete payment: reverse disbursements, delete adjustments, recalculate service lines and claim totals.</summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _paymentService.RemovePaymentAsync(id);
            return NoContent();
        }

        // =========================================================
        // 🔴 CLAIM DETAILS PAYMENTS (FAST + SAFE)
        // Claim Details MUST CALL ONLY THIS
        // =========================================================
        [HttpGet("claims/{claId:int}")]
        public async Task<IActionResult> GetPaymentsForClaim(int claId)
        {
            if (claId <= 0)
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_ARGUMENT",
                    Message = "Invalid Claim ID"
                });
            }

            var (payments, claimFound) = await _paymentRepo.GetPaymentsForClaimAsync(claId);
            if (!claimFound)
                return NotFound();

            return Ok(new ApiResponse<List<PaymentDto>>
            {
                Data = payments
            });
        }

        // =========================================================
        // 🟡 GLOBAL PAYMENTS SEARCH (FIND → PAYMENTS)
        // NOT USED BY CLAIM DETAILS
        // =========================================================
        [HttpGet("list")]
        public async Task<IActionResult> GetPayments(
            int page = 1,
            int pageSize = 25,
            int? patientId = null,
            [FromQuery] string? additionalColumns = null)
        {
            if (page < 1 || pageSize < 1 || pageSize > 100)
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_ARGUMENT",
                    Message = "Invalid paging values"
                });
            }

            var (data, totalCount) = await _paymentRepo.GetPaymentListAsync(page, pageSize, patientId);

            return Ok(new ApiResponse<List<PaymentListItemDto>>
            {
                Data = data,
                Meta = new PaginationMetaDto
                {
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                }
            });
        }

        // =========================================================
        // 🟢 UI CONFIG ONLY
        // =========================================================
        [HttpGet("available-columns")]
        public IActionResult GetAvailableColumns()
        {
            var columns = RelatedColumnConfig.GetAvailableColumns()["Payment"];

            return Ok(new ApiResponse<List<RelatedColumnDefinition>>
            {
                Data = columns
            });
        }
    }
}
