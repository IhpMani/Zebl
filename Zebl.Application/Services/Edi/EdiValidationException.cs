namespace Zebl.Application.Services.Edi;

public sealed class EdiValidationException : InvalidOperationException
{
    public EdiValidationException(string message) : base(message)
    {
    }
}

