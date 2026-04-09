namespace Zebl.Application.Dtos.Common;

/// <summary>Structured audit payload (serialized to <c>AuditLogs.Metadata</c> only — no arbitrary JSON).</summary>
public sealed class AuditMetadata
{
    public string Action { get; set; } = string.Empty;

    public string Actor { get; set; } = string.Empty;

    public string Target { get; set; } = string.Empty;

    public string Context { get; set; } = string.Empty;
}
