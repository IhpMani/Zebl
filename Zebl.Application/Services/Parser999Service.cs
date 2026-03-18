using System.Text.RegularExpressions;

namespace Zebl.Application.Services;

public sealed class Parser999Service
{
    public IReadOnlyList<Parsed999Rejection> Parse(string content)
    {
        var results = new List<Parsed999Rejection>();
        if (string.IsNullOrWhiteSpace(content))
            return results;

        var segments = content.Split('~', StringSplitOptions.RemoveEmptyEntries);
        string? currentControlNumber = null;

        foreach (var rawSegment in segments)
        {
            var segment = rawSegment.Trim();
            if (segment.Length == 0) continue;

            var parts = segment.Split('*');
            if (parts.Length == 0) continue;

            var tag = parts[0];
            switch (tag)
            {
                case "AK2":
                    // AK2*837*<transactionSetControlNumber>
                    if (parts.Length > 2)
                        currentControlNumber = parts[2];
                    break;

                case "IK3":
                    // IK3*<segmentId>*<segmentPosition>*<loopId>*<errorCode>*...
                    if (parts.Length >= 5)
                    {
                        var segId = parts[1];
                        var elementPos = parts[2];
                        var errorCode = parts[4];
                        var description = $"Error {errorCode} in segment {segId}, element {elementPos}";

                        results.Add(new Parsed999Rejection
                        {
                            TransactionControlNumber = currentControlNumber ?? string.Empty,
                            ErrorCode = errorCode,
                            Description = description,
                            Segment = segId,
                            Element = elementPos
                        });
                    }
                    break;

                case "IK4":
                    // Optionally refine description with IK4 details; for now we ignore.
                    break;

                case "IK5":
                    // Overall status; not creating separate rejection records here.
                    break;
            }
        }

        return results;
    }
}

/// <summary>Parsed 999 rejection; TransactionControlNumber is from AK2 (same as 837 ST02).</summary>
public sealed class Parsed999Rejection
{
    public string TransactionControlNumber { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Segment { get; set; } = string.Empty;
    public string Element { get; set; } = string.Empty;
}

