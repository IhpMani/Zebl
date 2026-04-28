using Serilog.Core;
using Serilog.Events;
using Zebl.Application.Services;

namespace Zebl.Api.Logging;

public sealed class CorrelationIdEnricher : ILogEventEnricher
{
    public const string MissingFlagProperty = "CorrelationMissing";

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var correlationId = CorrelationContext.CurrentId;
        var isMissing = string.IsNullOrWhiteSpace(correlationId);
        if (isMissing)
            correlationId = $"missing-{Guid.NewGuid():N}";

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CorrelationId", correlationId));
        if (isMissing)
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(MissingFlagProperty, true));
    }
}

