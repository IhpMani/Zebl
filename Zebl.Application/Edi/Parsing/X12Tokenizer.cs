using System.Text;

namespace Zebl.Application.Edi.Parsing;

/// <summary>
/// Minimal X12 tokenizer: detects segment terminator from ISA (106th character) and splits segments.
/// </summary>
public static class X12Tokenizer
{
    public static char DetectSegmentTerminator(ReadOnlySpan<char> s)
    {
        var idx = s.IndexOf("ISA", StringComparison.Ordinal);
        if (idx < 0)
            return '~';
        var slice = s.Slice(idx);
        return slice.Length >= 106 ? slice[105] : '~';
    }

    public static IReadOnlyList<X12Segment> Tokenize(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return Array.Empty<X12Segment>();

        return Enumerate(raw).ToList();
    }

    public static async Task<IReadOnlyList<X12Segment>> TokenizeAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var result = new List<X12Segment>();
        await foreach (var seg in EnumerateAsync(stream, cancellationToken).ConfigureAwait(false))
            result.Add(seg);
        return result;
    }

    public static IEnumerable<X12Segment> Enumerate(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            yield break;

        var term = DetectSegmentTerminator(raw.AsSpan());
        var sb = new StringBuilder();
        foreach (var ch in raw)
        {
            if (ch == '\r' || ch == '\n')
                continue;
            if (ch == term)
            {
                if (sb.Length > 0)
                {
                    yield return X12Segment.Parse(sb.ToString());
                    sb.Clear();
                }
            }
            else
            {
                sb.Append(ch);
            }
        }

        if (sb.Length > 0)
            yield return X12Segment.Parse(sb.ToString());
    }

    public static async IAsyncEnumerable<X12Segment> EnumerateAsync(
        Stream stream,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024 * 64, leaveOpen: true);
        var segmentBuilder = new StringBuilder();
        var probeBuilder = new StringBuilder(capacity: 160);
        var term = '~';
        var resolvedTerminator = false;
        var buffer = new char[4096];

        while (true)
        {
            var read = await reader.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read <= 0)
                break;

            for (var i = 0; i < read; i++)
            {
                var ch = buffer[i];
                if (ch == '\r' || ch == '\n')
                    continue;

                if (!resolvedTerminator && probeBuilder.Length < 220)
                {
                    probeBuilder.Append(ch);
                    var probe = probeBuilder.ToString();
                    var isaIdx = probe.IndexOf("ISA", StringComparison.Ordinal);
                    if (isaIdx >= 0 && probe.Length >= isaIdx + 106)
                    {
                        term = probe[isaIdx + 105];
                        resolvedTerminator = true;
                    }
                }

                if (ch == term)
                {
                    if (segmentBuilder.Length > 0)
                    {
                        yield return X12Segment.Parse(segmentBuilder.ToString());
                        segmentBuilder.Clear();
                    }
                }
                else
                {
                    segmentBuilder.Append(ch);
                }
            }
        }

        if (segmentBuilder.Length > 0)
            yield return X12Segment.Parse(segmentBuilder.ToString());
    }
}

public sealed class X12Segment
{
    public string Id { get; private init; } = "";
    public IReadOnlyList<string> Elements { get; private init; } = Array.Empty<string>();

    public static X12Segment Parse(string line)
    {
        var parts = line.Split('*', StringSplitOptions.None);
        if (parts.Length == 0)
            return new X12Segment { Id = "", Elements = Array.Empty<string>() };
        return new X12Segment
        {
            Id = parts[0],
            Elements = parts
        };
    }
}
