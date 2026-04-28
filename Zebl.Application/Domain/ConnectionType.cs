namespace Zebl.Application.Domain;

/// <summary>
/// Explicit transport channel for a connection library row. Never infer from host/port.
/// </summary>
public enum ConnectionType
{
    Sftp = 0,
    Http = 1,
    Api = 2
}
