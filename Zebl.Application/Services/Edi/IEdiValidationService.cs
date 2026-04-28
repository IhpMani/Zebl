namespace Zebl.Application.Services.Edi;

public interface IEdiValidationService
{
    void Validate(string ediContent, OutboundEdiKind expectedKind);
}

