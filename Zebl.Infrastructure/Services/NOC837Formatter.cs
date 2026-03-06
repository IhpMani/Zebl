using System;
using Zebl.Application.Domain;
using Zebl.Application.Services;

namespace Zebl.Infrastructure.Services;

/// <summary>
/// Handles "Not Otherwise Classified" codes during ANSI 837 export (e.g. SV101-7).
/// </summary>
public class NOC837Formatter : INOC837Formatter
{
    public string? FormatDescription(IProcedureCode code)
    {
        if (code == null)
            return null;
        return string.Equals(code.ProcCategory, "NOC", StringComparison.OrdinalIgnoreCase)
            ? code.ProcDescription
            : null;
    }
}
