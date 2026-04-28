using System.Diagnostics;

namespace Zebl.Infrastructure.Services;

public sealed class RuntimeEdiSystemLoadMonitor : IEdiSystemLoadMonitor
{
    private readonly Process _process = Process.GetCurrentProcess();
    private DateTime _lastTimeUtc = DateTime.UtcNow;
    private TimeSpan _lastCpu = Process.GetCurrentProcess().TotalProcessorTime;

    public EdiSystemLoadSnapshot Sample()
    {
        _process.Refresh();
        var now = DateTime.UtcNow;
        var cpuNow = _process.TotalProcessorTime;
        var elapsedMs = Math.Max(1d, (now - _lastTimeUtc).TotalMilliseconds);
        var cpuElapsedMs = Math.Max(0d, (cpuNow - _lastCpu).TotalMilliseconds);
        _lastTimeUtc = now;
        _lastCpu = cpuNow;

        var cpuPercent = (cpuElapsedMs / elapsedMs) / Math.Max(1, Environment.ProcessorCount) * 100d;

        var memory = GC.GetGCMemoryInfo();
        var memoryRatio = 0d;
        if (memory.HighMemoryLoadThresholdBytes > 0)
            memoryRatio = Math.Clamp(memory.MemoryLoadBytes / (double)memory.HighMemoryLoadThresholdBytes, 0d, 1.5d);

        return new EdiSystemLoadSnapshot(cpuPercent, memoryRatio);
    }
}

