using Zebl.Application.Domain;

namespace Zebl.Application.Services;

/// <summary>
/// Processes 835 ERA files: payer match, forwarding logic, auto-post payments. No EDI parse in controller.
/// </summary>
public interface IEraPostingService
{
    /// <summary>
    /// Processes an ERA file: match payer (Payment Matching Key), apply forwarding rules, create payments and adjustments.
    /// Does not throw on payer match failure; logs and returns PartiallyProcessed.
    /// </summary>
    Task<EraPostingResult> ProcessEraAsync(EraFile era);
}
