using Microsoft.Extensions.Options;
using Zebl.Application.Options;
using Zebl.Infrastructure.Services;
using Xunit;

namespace Zebl.Tests;

public class AdaptiveEdiProcessingLimiterTests
{
    [Fact]
    public async Task HighLoad_ReducesTargetConcurrency()
    {
        var monitor = new SequenceLoadMonitor(
            new EdiSystemLoadSnapshot(20, 0.40),
            new EdiSystemLoadSnapshot(95, 0.95),
            new EdiSystemLoadSnapshot(95, 0.95),
            new EdiSystemLoadSnapshot(95, 0.95));

        await using var limiter = new EdiProcessingLimiter(
            monitor,
            Options.Create(new EdiAdaptiveLimiterOptions
            {
                InitialConcurrency = 6,
                MinConcurrency = 2,
                MaxConcurrency = 10,
                ControlLoopIntervalMs = 60,
                ScaleUpStep = 1,
                ScaleDownStep = 1,
                CooldownTicks = 0
            }));

        await Task.Delay(280);
        var snapshot = limiter.GetSnapshot();
        Assert.True(snapshot.MaxConcurrency < 6, $"Expected target < 6, actual {snapshot.MaxConcurrency}");
    }

    [Fact]
    public async Task LowLoad_WithQueue_IncreasesTargetConcurrency()
    {
        var monitor = new SequenceLoadMonitor(
            new EdiSystemLoadSnapshot(95, 0.90),
            new EdiSystemLoadSnapshot(30, 0.35),
            new EdiSystemLoadSnapshot(30, 0.35),
            new EdiSystemLoadSnapshot(30, 0.35),
            new EdiSystemLoadSnapshot(30, 0.35));

        await using var limiter = new EdiProcessingLimiter(
            monitor,
            Options.Create(new EdiAdaptiveLimiterOptions
            {
                InitialConcurrency = 2,
                MinConcurrency = 2,
                MaxConcurrency = 6,
                ControlLoopIntervalMs = 60,
                ScaleUpStep = 1,
                ScaleDownStep = 1,
                CooldownTicks = 0
            }));

        var slot1 = await limiter.AcquireInboundSlotAsync();
        var waitTask = limiter.AcquireInboundSlotAsync();
        await Task.Delay(260);
        var snapshot = limiter.GetSnapshot();
        await slot1.DisposeAsync();
        var slot2 = await waitTask;
        await slot2.DisposeAsync();

        Assert.True(snapshot.MaxConcurrency > 2, $"Expected target > 2, actual {snapshot.MaxConcurrency}");
    }

    [Fact]
    public async Task SustainedAndBurstLoad_RemainsWithinBounds_AndStable()
    {
        var monitor = new SequenceLoadMonitor(
            Enumerable.Repeat(new EdiSystemLoadSnapshot(40, 0.45), 10).ToArray());
        await using var limiter = new EdiProcessingLimiter(
            monitor,
            Options.Create(new EdiAdaptiveLimiterOptions
            {
                InitialConcurrency = 4,
                MinConcurrency = 2,
                MaxConcurrency = 8,
                ControlLoopIntervalMs = 80,
                ScaleUpStep = 1,
                ScaleDownStep = 1,
                CooldownTicks = 1
            }));

        var sustained = Enumerable.Range(0, 40).Select(async _ =>
        {
            await using var slot = await limiter.AcquireInboundSlotAsync();
            await Task.Delay(10);
        });
        await Task.WhenAll(sustained);

        var burst = Enumerable.Range(0, 80).Select(async _ =>
        {
            await using var slot = await limiter.AcquireInboundSlotAsync();
            await Task.Delay(5);
        });
        await Task.WhenAll(burst);

        var snapshot = limiter.GetSnapshot();
        Assert.InRange(snapshot.MaxConcurrency, 2, 8);
        Assert.True(snapshot.QueueDepth >= 0);
    }

    private sealed class SequenceLoadMonitor : IEdiSystemLoadMonitor
    {
        private readonly EdiSystemLoadSnapshot[] _samples;
        private int _index;

        public SequenceLoadMonitor(params EdiSystemLoadSnapshot[] samples)
        {
            _samples = samples;
        }

        public EdiSystemLoadSnapshot Sample()
        {
            if (_samples.Length == 0)
                return new EdiSystemLoadSnapshot(30, 0.30);
            var i = Interlocked.Increment(ref _index) - 1;
            if (i >= _samples.Length)
                i = _samples.Length - 1;
            return _samples[i];
        }
    }
}

