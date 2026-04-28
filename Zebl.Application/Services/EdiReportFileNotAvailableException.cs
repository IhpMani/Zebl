namespace Zebl.Application.Services;

/// <summary>
/// Raised when persisted EDI bytes cannot be read from external storage.
/// </summary>
public sealed class EdiReportFileNotAvailableException : InvalidOperationException
{
    public EdiReportFileNotAvailableException(string message) : base(message)
    {
    }
}
