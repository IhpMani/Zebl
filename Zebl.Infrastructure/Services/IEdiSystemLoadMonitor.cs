namespace Zebl.Infrastructure.Services;

public interface IEdiSystemLoadMonitor
{
    EdiSystemLoadSnapshot Sample();
}

public readonly record struct EdiSystemLoadSnapshot(double CpuUtilizationPercent, double MemoryPressureRatio);

