namespace Zebl.Application.Exceptions;

/// <summary>
/// Thrown when a payment would duplicate an existing one (same amount + reference1).
/// </summary>
public class DuplicatePaymentException : InvalidOperationException
{
    public DuplicatePaymentException(string message) : base(message) { }
    public DuplicatePaymentException(string message, Exception inner) : base(message, inner) { }
}
