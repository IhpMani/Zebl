using Zebl.Application.Edi.Parsing;
using Microsoft.Extensions.Logging;

namespace Zebl.Application.Services.Edi;

/// <summary>
/// Validates control numbers and envelope counts for generated outbound EDI.
/// </summary>
public sealed class EdiValidationService : IEdiValidationService
{
    private readonly ILogger<EdiValidationService> _logger;

    public EdiValidationService(ILogger<EdiValidationService> logger)
    {
        _logger = logger;
    }

    public void Validate(string ediContent, OutboundEdiKind expectedKind)
    {
        if (string.IsNullOrWhiteSpace(ediContent))
            ThrowValidation("EDI payload is empty.", "DOCUMENT_EMPTY", null);

        var segments = X12Tokenizer.Tokenize(ediContent);
        if (segments.Count == 0)
            ThrowValidation("EDI payload has no segments.", "NO_SEGMENTS", null);

        var isa = segments.FirstOrDefault(s => s.Id == "ISA")
            ?? ThrowValidation<X12Segment>("Missing ISA segment.", "MISSING_SEGMENT", "ISA");
        var gs = segments.FirstOrDefault(s => s.Id == "GS")
            ?? ThrowValidation<X12Segment>("Missing GS segment.", "MISSING_SEGMENT", "GS");
        var st = segments.FirstOrDefault(s => s.Id == "ST")
            ?? ThrowValidation<X12Segment>("Missing ST segment.", "MISSING_SEGMENT", "ST");
        var se = segments.LastOrDefault(s => s.Id == "SE")
            ?? ThrowValidation<X12Segment>("Missing SE segment.", "MISSING_SEGMENT", "SE");
        var ge = segments.LastOrDefault(s => s.Id == "GE")
            ?? ThrowValidation<X12Segment>("Missing GE segment.", "MISSING_SEGMENT", "GE");
        var iea = segments.LastOrDefault(s => s.Id == "IEA")
            ?? ThrowValidation<X12Segment>("Missing IEA segment.", "MISSING_SEGMENT", "IEA");

        RequireControl(isa, 13, "ISA13");
        RequireControl(gs, 6, "GS06");
        RequireControl(st, 2, "ST02");
        RequireControl(se, 2, "SE02");

        var st02 = st.Elements[2];
        var se02 = se.Elements[2];
        if (!string.Equals(st02, se02, StringComparison.Ordinal))
            ThrowValidation("SE02 must match ST02.", "CONTROL_MISMATCH", "SE02");

        var st01 = st.Elements.Count > 1 ? st.Elements[1] : null;
        var expectedSt = expectedKind == OutboundEdiKind.Claim837 ? "837" : "270";
        if (!string.Equals(st01, expectedSt, StringComparison.Ordinal))
            ThrowValidation($"ST01 expected {expectedSt}, got {st01 ?? "<null>"}.", "UNEXPECTED_ST01", "ST01");

        if (!int.TryParse(se.Elements[1], out var se01))
            ThrowValidation("SE01 segment count is invalid.", "INVALID_COUNT", "SE01");
        var stIndex = -1;
        var seIndex = -1;
        for (var i = 0; i < segments.Count; i++)
        {
            if (ReferenceEquals(segments[i], st) && stIndex < 0)
                stIndex = i;
            if (ReferenceEquals(segments[i], se))
                seIndex = i;
        }
        if (stIndex < 0 || seIndex < stIndex)
            ThrowValidation("Invalid ST/SE ordering.", "SEGMENT_ORDER", "ST/SE");
        var actualSetCount = (seIndex - stIndex) + 1;
        if (se01 != actualSetCount)
            ThrowValidation($"SE01 mismatch. Expected {actualSetCount}, got {se01}.", "COUNT_MISMATCH", "SE01");

        var stCount = segments.Count(s => s.Id == "ST");
        if (!int.TryParse(ge.Elements[1], out var ge01) || ge01 != stCount)
            ThrowValidation($"GE01 mismatch. Expected {stCount}, got {ge.Elements.ElementAtOrDefault(1) ?? "<null>"}.", "COUNT_MISMATCH", "GE01");

        var gsCount = segments.Count(s => s.Id == "GS");
        if (!int.TryParse(iea.Elements[1], out var iea01) || iea01 != gsCount)
            ThrowValidation($"IEA01 mismatch. Expected {gsCount}, got {iea.Elements.ElementAtOrDefault(1) ?? "<null>"}.", "COUNT_MISMATCH", "IEA01");
    }

    private void RequireControl(X12Segment segment, int index, string label)
    {
        if (segment.Elements.Count <= index || string.IsNullOrWhiteSpace(segment.Elements[index]))
            ThrowValidation($"Missing {label} control number.", "MISSING_CONTROL", label);
    }

    private void ThrowValidation(string message, string rule, string? segment)
    {
        EdiOperationalMetrics.ValidationFailureCount.Add(
            1,
            new KeyValuePair<string, object?>("rule", rule),
            new KeyValuePair<string, object?>("outcome", "failed"));
        _logger.LogError("EDI validation failure. Rule={Rule} Segment={Segment} Message={Message}", rule, segment ?? "<none>", message);
        throw new EdiValidationException(message);
    }

    private T ThrowValidation<T>(string message, string rule, string? segment)
    {
        ThrowValidation(message, rule, segment);
        throw new InvalidOperationException("Unreachable");
    }
}

