using Microsoft.Extensions.Options;
using Zebl.Application.Options;
using Zebl.Application.Services;

namespace Zebl.Infrastructure.Services;

public sealed class EdiProcessingLimiter : IEdiProcessingLimiter, IAsyncDisposable
{
    private readonly object _sync = new();
    private readonly IEdiSystemLoadMonitor _loadMonitor;
    private readonly int _minConcurrency;
    private readonly int _maxConcurrency;
    private readonly PeriodicTimer _controlTimer;
    private readonly CancellationTokenSource _controlCts = new();
    private readonly Queue<TaskCompletionSource<bool>> _waiters = new();
    private readonly TimeSpan _interval;
    private readonly double _cpuHighThreshold;
    private readonly double _cpuLowThreshold;
    private readonly double _memoryHighThreshold;
    private readonly double _memoryLowThreshold;
    private readonly int _scaleUpStep;
    private readonly int _scaleDownStep;
    private readonly int _cooldownTicks;
    private readonly Task _controlLoopTask;
    private int _ticksSinceLastChange;
    private int _inUse;
    private int _queueDepth;
    private int _targetConcurrency;

    public EdiProcessingLimiter()
        : this(new RuntimeEdiSystemLoadMonitor(), Options.Create(new EdiAdaptiveLimiterOptions()))
    {
    }

    public EdiProcessingLimiter(
        IEdiSystemLoadMonitor loadMonitor,
        IOptions<EdiAdaptiveLimiterOptions> options)
    {
        var cfg = options.Value;
        _loadMonitor = loadMonitor;
        _minConcurrency = Math.Max(1, cfg.MinConcurrency);
        _maxConcurrency = Math.Max(_minConcurrency, cfg.MaxConcurrency);
        _targetConcurrency = Math.Clamp(cfg.InitialConcurrency, _minConcurrency, _maxConcurrency);
        _interval = cfg.ControlLoopIntervalMs <= 0 ? TimeSpan.FromSeconds(2) : TimeSpan.FromMilliseconds(cfg.ControlLoopIntervalMs);
        _controlTimer = new PeriodicTimer(_interval);
        _cpuHighThreshold = cfg.CpuHighThreshold;
        _cpuLowThreshold = cfg.CpuLowThreshold;
        _memoryHighThreshold = cfg.MemoryHighThreshold;
        _memoryLowThreshold = cfg.MemoryLowThreshold;
        _scaleUpStep = Math.Max(1, cfg.ScaleUpStep);
        _scaleDownStep = Math.Max(1, cfg.ScaleDownStep);
        _cooldownTicks = Math.Max(0, cfg.CooldownTicks);
        _controlLoopTask = RunControlLoopAsync();
    }

    public async Task<IAsyncDisposable> AcquireInboundSlotAsync(CancellationToken cancellationToken = default)
    {
        var waitStart = DateTime.UtcNow;
        Task waitTask;
        CancellationTokenRegistration cancellationRegistration = default;
        lock (_sync)
        {
            Interlocked.Increment(ref _queueDepth);
            EdiOperationalMetrics.QueueDepth.Add(1, new KeyValuePair<string, object?>("queue", "inbound"));
            if (_inUse < _targetConcurrency)
            {
                _inUse++;
                waitTask = Task.CompletedTask;
            }
            else
            {
                var waiter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _waiters.Enqueue(waiter);
                if (cancellationToken.CanBeCanceled)
                    cancellationRegistration = cancellationToken.Register(() => waiter.TrySetCanceled(cancellationToken));
                waitTask = waiter.Task;
            }
        }

        try
        {
            await waitTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            cancellationRegistration.Dispose();
        }

        Interlocked.Decrement(ref _queueDepth);
        EdiOperationalMetrics.QueueDepth.Add(-1, new KeyValuePair<string, object?>("queue", "inbound"));
        EdiOperationalMetrics.QueueWaitMs.Record(
            (DateTime.UtcNow - waitStart).TotalMilliseconds,
            new KeyValuePair<string, object?>("queue", "inbound"));
        EdiOperationalMetrics.ConcurrencyInUse.Record(
            Volatile.Read(ref _inUse),
            new KeyValuePair<string, object?>("limiter", "inbound"));
        return new Releaser(this);
    }

    public EdiProcessingLimiterSnapshot GetSnapshot()
        => new(Volatile.Read(ref _targetConcurrency), Volatile.Read(ref _inUse), Volatile.Read(ref _queueDepth));

    private async Task RunControlLoopAsync()
    {
        try
        {
            while (await _controlTimer.WaitForNextTickAsync(_controlCts.Token).ConfigureAwait(false))
            {
                AdjustTargetConcurrency();
                DrainWaiters();
            }
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
    }

    public async ValueTask DisposeAsync()
    {
        _controlCts.Cancel();
        _controlTimer.Dispose();
        try
        {
            await _controlLoopTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // expected during shutdown
        }
        _controlCts.Dispose();
    }

    private void AdjustTargetConcurrency()
    {
        var snapshot = _loadMonitor.Sample();
        var queue = Volatile.Read(ref _queueDepth);
        var target = Volatile.Read(ref _targetConcurrency);

        var shouldDecrease =
            snapshot.CpuUtilizationPercent >= _cpuHighThreshold ||
            snapshot.MemoryPressureRatio >= _memoryHighThreshold;

        var shouldIncrease =
            snapshot.CpuUtilizationPercent <= _cpuLowThreshold &&
            snapshot.MemoryPressureRatio <= _memoryLowThreshold &&
            (queue > 0 || Volatile.Read(ref _inUse) >= target);

        if (_ticksSinceLastChange < _cooldownTicks)
        {
            _ticksSinceLastChange++;
            return;
        }

        if (shouldDecrease)
            target = Math.Max(_minConcurrency, target - _scaleDownStep);
        else if (shouldIncrease)
            target = Math.Min(_maxConcurrency, target + _scaleUpStep);

        var prior = Interlocked.Exchange(ref _targetConcurrency, target);
        if (prior != target)
            _ticksSinceLastChange = 0;
        else
            _ticksSinceLastChange++;
    }

    private void DrainWaiters()
    {
        lock (_sync)
        {
            while (_waiters.Count > 0 && _inUse < _targetConcurrency)
            {
                var waiter = _waiters.Dequeue();
                if (waiter.Task.IsCanceled)
                    continue;
                _inUse++;
                waiter.TrySetResult(true);
            }
        }
    }

    private void Release()
    {
        TaskCompletionSource<bool>? waiter = null;
        lock (_sync)
        {
            if (_inUse > 0)
                _inUse--;
            while (_waiters.Count > 0 && _inUse < _targetConcurrency)
            {
                waiter = _waiters.Dequeue();
                if (waiter.Task.IsCanceled)
                    continue;
                _inUse++;
                break;
            }
        }

        waiter?.TrySetResult(true);
        EdiOperationalMetrics.ConcurrencyInUse.Record(
            Volatile.Read(ref _inUse),
            new KeyValuePair<string, object?>("limiter", "inbound"));
    }

    private sealed class Releaser : IAsyncDisposable
    {
        private readonly EdiProcessingLimiter _owner;
        private int _disposed;

        public Releaser(EdiProcessingLimiter owner)
        {
            _owner = owner;
        }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                _owner.Release();
            return ValueTask.CompletedTask;
        }
    }
}

