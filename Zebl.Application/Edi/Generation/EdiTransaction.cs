namespace Zebl.Application.Edi.Generation;

/// <summary>
/// Minimal loop-aware transaction model for outbound generation.
/// </summary>
public sealed class EdiTransaction
{
    public List<EdiGenSegment> HeaderSegments { get; } = new();
    public List<EdiLoop> Loop2000A { get; } = new();
    public List<EdiLoop> Loop2300 { get; } = new();
    public List<EdiLoop> Loop2400 { get; } = new();
    public List<EdiGenSegment> FooterSegments { get; } = new();

    public IReadOnlyList<EdiGenSegment> Flatten()
    {
        var segments = new List<EdiGenSegment>(HeaderSegments);
        AppendLoops(segments, Loop2000A);
        AppendLoops(segments, Loop2300);
        AppendLoops(segments, Loop2400);
        segments.AddRange(FooterSegments);
        return segments;
    }

    private static void AppendLoops(List<EdiGenSegment> segments, IEnumerable<EdiLoop> loops)
    {
        foreach (var loop in loops)
        {
            segments.AddRange(loop.Segments);
            if (loop.Children.Count > 0)
                AppendLoops(segments, loop.Children);
        }
    }
}

