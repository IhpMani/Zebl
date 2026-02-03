namespace Zebl.Application.Dtos.Common
{
    public class ErrorResponseDto
    {
        public string ErrorCode { get; set; } = "INTERNAL_ERROR";
        public string Message { get; set; } = "An unexpected error occurred";
        public string TraceId { get; set; } = string.Empty;
        public string? Details { get; set; }
    }
}
