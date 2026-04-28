namespace Zebl.Application.Options;

public sealed class EdiOperationalOptions
{
    public const string SectionName = "EdiOperational";

    public double ProcessingMetricSampleRate { get; set; } = 1.0d;
}

