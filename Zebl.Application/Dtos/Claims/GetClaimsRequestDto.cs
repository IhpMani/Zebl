using System.ComponentModel.DataAnnotations;

namespace Zebl.Application.Dtos.Claims
{
    public class GetClaimsRequestDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "Page must be at least 1")]
        public int Page { get; set; } = 1;

        [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100")]
        public int PageSize { get; set; } = 25;

        [MaxLength(20, ErrorMessage = "Status cannot exceed 20 characters")]
        public string? Status { get; set; }

        public DateTime? FromDate { get; set; }

        public DateTime? ToDate { get; set; }
    }
}

