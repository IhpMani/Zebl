using System;

namespace Zebl.Infrastructure.Persistence.Entities;

public class ListValue
{
    public int ListValueID { get; set; }

    public string ListType { get; set; } = null!;

    public string Value { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedDate { get; set; }

    public string? CreatedBy { get; set; }
}
