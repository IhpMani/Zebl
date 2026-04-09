namespace Zebl.Api.Services;

/// <summary>Thrown when tenant header does not match JWT or platform context rules are violated.</summary>
public sealed class TenantSecurityException : Exception
{
    public string ErrorCode { get; }

    public TenantSecurityException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }
}
