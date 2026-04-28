namespace Zebl.Application.Options;

public sealed class EdiAdaptiveLimiterOptions
{
    public const string SectionName = "EdiAdaptiveLimiter";

    public int InitialConcurrency { get; set; } = 8;
    public int MinConcurrency { get; set; } = 2;
    public int MaxConcurrency { get; set; } = 16;
    public int ControlLoopIntervalMs { get; set; } = 2000;
    public int ScaleUpStep { get; set; } = 1;
    public int ScaleDownStep { get; set; } = 1;
    public int CooldownTicks { get; set; } = 2;
    public double CpuHighThreshold { get; set; } = 85d;
    public double CpuLowThreshold { get; set; } = 55d;
    public double MemoryHighThreshold { get; set; } = 0.85d;
    public double MemoryLowThreshold { get; set; } = 0.60d;
}

