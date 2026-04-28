namespace Zebl.Application.Edi.Generation;

/// <summary>
/// Structured X12 segment (Id + data elements after the segment identifier).
/// </summary>
public sealed class EdiGenSegment
{
    public EdiGenSegment(string id, IReadOnlyList<string> elements)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Elements = elements ?? throw new ArgumentNullException(nameof(elements));
    }

    public string Id { get; }
    public IReadOnlyList<string> Elements { get; }

    public static EdiGenSegment Create(string id, params string[] elements) => new(id, elements);
}
