using Zebl.Application.Domain;

namespace Zebl.Application.Services;

/// <summary>
/// Handles "Not Otherwise Classified" codes during ANSI 837 export (e.g. SV101-7).
/// </summary>
public interface INOC837Formatter
{
    /// <summary>
    /// If ProcCategory == "NOC", return ProcDescription; otherwise null.
    /// </summary>
    string? FormatDescription(IProcedureCode code);
}
