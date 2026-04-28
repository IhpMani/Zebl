using System.Text;

namespace Zebl.Application.Edi.Generation;

/// <summary>
/// Serializes structured segments to X12 (element * separator, segment terminator).
/// </summary>
public static class EdiGenSerializer
{
    public static string Serialize(EdiTransaction transaction, char segmentTerminator = '~')
    {
        return Serialize(transaction.Flatten(), segmentTerminator);
    }

    public static string Serialize(IReadOnlyList<EdiGenSegment> segments, char segmentTerminator = '~')
    {
        if (segmentTerminator == '*')
            throw new InvalidOperationException("Segment terminator cannot equal element delimiter '*'.");

        var sb = new StringBuilder();
        foreach (var seg in segments)
        {
            if (string.IsNullOrWhiteSpace(seg.Id))
                throw new InvalidOperationException("Segment identifier cannot be empty.");

            sb.Append(seg.Id);
            foreach (var el in seg.Elements)
            {
                if (el.Contains(segmentTerminator, StringComparison.Ordinal))
                    throw new InvalidOperationException($"Element value in segment {seg.Id} contains segment terminator.");
                sb.Append('*');
                sb.Append(el);
            }

            sb.Append(segmentTerminator);
        }

        return sb.ToString();
    }

    public static string SerializeSv1Composite(Sv1Composite composite)
    {
        if (string.IsNullOrWhiteSpace(composite.ProductOrServiceIdQualifier))
            throw new InvalidOperationException("SV1 qualifier is required.");
        if (string.IsNullOrWhiteSpace(composite.ProcedureCode))
            throw new InvalidOperationException("SV1 procedure code is required.");

        var parts = new[]
        {
            composite.ProductOrServiceIdQualifier,
            composite.ProcedureCode,
            composite.Modifier1,
            composite.Modifier2,
            composite.Modifier3,
            composite.Modifier4
        };

        return string.Join(':', parts);
    }
}
