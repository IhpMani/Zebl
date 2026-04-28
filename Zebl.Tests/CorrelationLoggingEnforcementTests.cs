using Serilog;
using Serilog.Core;
using Serilog.Events;
using Zebl.Api.Logging;
using Zebl.Application.Services;
using Xunit;

namespace Zebl.Tests;

public class CorrelationLoggingEnforcementTests
{
    [Fact]
    public void Logs_WithoutCorrelation_AreNotEmitted()
    {
        var sink = new InMemorySink();
        var logger = new LoggerConfiguration()
            .Filter.With(new CorrelationIdRequiredFilter())
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Information("no correlation");

        Assert.Empty(sink.Events);
    }

    [Fact]
    public void Enricher_InjectsCorrelation_ForEmission()
    {
        var sink = new InMemorySink();
        var logger = new LoggerConfiguration()
            .Enrich.With(new CorrelationIdEnricher())
            .Filter.With(new CorrelationIdRequiredFilter())
            .WriteTo.Sink(sink)
            .CreateLogger();

        using var _ = CorrelationContext.Push("corr-test-1");
        logger.Information("with correlation");

        var evt = Assert.Single(sink.Events);
        Assert.True(evt.Properties.ContainsKey("CorrelationId"));
    }

    [Fact]
    public void Enricher_AssignsFallbackCorrelation_WhenAmbientMissing()
    {
        var sink = new InMemorySink();
        var logger = new LoggerConfiguration()
            .Enrich.With(new CorrelationIdEnricher())
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Information("startup/third-party log");

        var evt = Assert.Single(sink.Events);
        var correlation = evt.Properties["CorrelationId"].ToString();
        Assert.Contains("missing-", correlation, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class InMemorySink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = new();

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}

