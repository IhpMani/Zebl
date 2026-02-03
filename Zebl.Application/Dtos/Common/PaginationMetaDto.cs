using System.ComponentModel.DataAnnotations;

namespace Zebl.Application.Dtos.Common
{
    public class PaginationMetaDto
    {
        [Range(1, int.MaxValue)]
        public int Page { get; set; }
        
        [Range(1, 100)]
        public int PageSize { get; set; }
        
        [Range(0, int.MaxValue)]
        public int TotalCount { get; set; }
    }
}
