using Serilog.Core;
using Serilog.Events;

namespace Zebl.Api.Logging;

public sealed class CorrelationIdRequiredFilter : ILogEventFilter
{
    public bool IsEnabled(LogEvent logEvent)
        => logEvent.Properties.ContainsKey("CorrelationId");
}

