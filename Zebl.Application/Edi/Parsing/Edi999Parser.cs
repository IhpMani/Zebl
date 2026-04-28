using System.Linq;

namespace Zebl.Application.Edi.Parsing;

/// <summary>
/// Structured 999 functional acknowledgment parser (segment walk, ST01-aware context).
/// </summary>
public static class Edi999Parser
{
    public static async Task<Edi999ParseResult> ParseAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var state = new ParseState();
        await foreach (var seg in X12Tokenizer.EnumerateAsync(stream, cancellationToken).ConfigureAwait(false))
            ProcessSegment(seg, state);
        return BuildResult(state);
    }

    public static Edi999ParseResult Parse(string raw)
    {
        var state = new ParseState();
        foreach (var seg in X12Tokenizer.Enumerate(raw))
            ProcessSegment(seg, state);
        return BuildResult(state);
    }

    private static void ProcessSegment(X12Segment seg, ParseState state)
    {
        switch (seg.Id)
        {
            case "ST" when seg.Elements.Count > 1:
                state.St01 = seg.Elements[1].Trim();
                break;
            case "AK2" when seg.Elements.Count > 2:
                state.CurrentStControl = seg.Elements[2];
                break;
            case "IK3" when seg.Elements.Count >= 5:
                state.Rejections.Add(new Edi999RejectionLine
                {
                    TransactionControlNumber = state.CurrentStControl ?? string.Empty,
                    ErrorCode = seg.Elements[4],
                    Description = $"Error {seg.Elements[4]} in segment {seg.Elements[1]}, element {seg.Elements[2]}",
                    Segment = seg.Elements[1],
                    Element = seg.Elements[2]
                });
                break;
            case "IK5" when seg.Elements.Count > 1:
                state.Ik5Lines.Add(new Edi999Ik5Line
                {
                    TransactionSetAcknowledgmentCode = seg.Elements[1].Trim(),
                    ImplementationTransactionSetSyntaxErrorCode = seg.Elements.Count > 2 ? seg.Elements[2].Trim() : null,
                    ErrorDescription = seg.Elements.Count > 3 ? string.Join("*", seg.Elements.Skip(3)) : null
                });
                break;
            case "AK9" when seg.Elements.Count > 1:
                state.Ak901 = seg.Elements[1].Trim();
                state.Ak9Lines.Add(new Edi999Ak9Line
                {
                    FunctionalGroupAcknowledgeCode = seg.Elements[1].Trim(),
                    IncludedTransactionSets = seg.Elements.Count > 2 ? seg.Elements[2].Trim() : null,
                    ReceivedTransactionSets = seg.Elements.Count > 3 ? seg.Elements[3].Trim() : null,
                    AcceptedTransactionSets = seg.Elements.Count > 4 ? seg.Elements[4].Trim() : null
                });
                break;
        }
    }

    private static Edi999ParseResult BuildResult(ParseState state)
    {
        var ik501 = state.Ik5Lines.FirstOrDefault()?.TransactionSetAcknowledgmentCode;
        var note = BuildSummaryNote(state.Ak901, ik501);
        return new Edi999ParseResult
        {
            TransactionSetIdentifier = state.St01,
            FunctionalAckCode = state.Ak901,
            SummaryNote = note,
            Rejections = state.Rejections,
            Ik5Lines = state.Ik5Lines,
            Ak9Lines = state.Ak9Lines
        };
    }

    private static string? BuildSummaryNote(string? ak901, string? ik501)
    {
        if (ak901 == null && ik501 == null)
            return null;
        if (ik501 != null)
            return ik501 == "A" ? "Transaction set accepted (IK5)." : "Transaction set rejected (IK5).";
        return ak901 == "A" ? "Batch Accepted" : "Batch Rejected";
    }

    private sealed class ParseState
    {
        public string? St01 { get; set; }
        public string? Ak901 { get; set; }
        public string? CurrentStControl { get; set; }
        public List<Edi999RejectionLine> Rejections { get; } = new();
        public List<Edi999Ik5Line> Ik5Lines { get; } = new();
        public List<Edi999Ak9Line> Ak9Lines { get; } = new();
    }
}

public sealed class Edi999ParseResult
{
    public string? TransactionSetIdentifier { get; init; }
    public string? FunctionalAckCode { get; init; }
    public string? SummaryNote { get; init; }
    public IReadOnlyList<Edi999RejectionLine> Rejections { get; init; } = Array.Empty<Edi999RejectionLine>();
    public IReadOnlyList<Edi999Ik5Line> Ik5Lines { get; init; } = Array.Empty<Edi999Ik5Line>();
    public IReadOnlyList<Edi999Ak9Line> Ak9Lines { get; init; } = Array.Empty<Edi999Ak9Line>();
}

public sealed class Edi999RejectionLine
{
    public string TransactionControlNumber { get; init; } = "";
    public string ErrorCode { get; init; } = "";
    public string Description { get; init; } = "";
    public string Segment { get; init; } = "";
    public string Element { get; init; } = "";
}
