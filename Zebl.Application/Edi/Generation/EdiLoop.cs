namespace Zebl.Application.Edi.Generation;

/// <summary>
/// Generic X12 loop container with nested child loops.
/// </summary>
public sealed class EdiLoop
{
    public EdiLoop(string name)
    {
        Name = name;
    }

    public string Name { get; }
    public List<EdiGenSegment> Segments { get; } = new();
    public List<EdiLoop> Children { get; } = new();
}

