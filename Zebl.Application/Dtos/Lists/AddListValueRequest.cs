using System.ComponentModel.DataAnnotations;

namespace Zebl.Application.Dtos.Lists;

public class AddListValueRequest
{
    [Required]
    public string ListType { get; set; } = null!;
    
    [Required]
    public string Value { get; set; } = null!;
}
