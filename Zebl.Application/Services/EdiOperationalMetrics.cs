using System.Diagnostics.Metrics;

namespace Zebl.Application.Services;

public static class EdiOperationalMetrics
{
    private static double _processingSampleRate = 1.0d;

    public static readonly Meter Meter = new("Zebl.Edi", "1.0.0");

    public static readonly Histogram<double> ProcessingMs = Meter.CreateHistogram<double>("edi.processing.ms");
    public static readonly Histogram<double> QueueWaitMs = Meter.CreateHistogram<double>("edi.queue.wait.ms");
    public static readonly Histogram<int> ConcurrencyInUse = Meter.CreateHistogram<int>("edi.concurrency.in_use");
    public static readonly Counter<long> FailureCount = Meter.CreateCounter<long>("edi.failure.count");
    public static readonly Counter<long> RetryCount = Meter.CreateCounter<long>("edi.retry.count");
    public static readonly Counter<long> ValidationFailureCount = Meter.CreateCounter<long>("edi.validation.failure.count");
    public static readonly UpDownCounter<long> QueueDepth = Meter.CreateUpDownCounter<long>("edi.queue.depth");

    public static void ConfigureProcessingSampling(double sampleRate)
    {
        _processingSampleRate = Math.Clamp(sampleRate, 0d, 1d);
    }

    public static bool ShouldSampleProcessing()
    {
        if (_processingSampleRate >= 1d)
            return true;
        if (_processingSampleRate <= 0d)
            return false;
        return Random.Shared.NextDouble() < _processingSampleRate;
    }
}

