using System.Linq;

namespace Zebl.Application.Edi.Parsing;

/// <summary>
/// Structured 835 parser: loop-aware CLP grouping with nested CAS adjustments.
/// </summary>
public static class Edi835Parser
{
    public static async Task<Edi835ParseResult> ParseAsync(
        Stream stream,
        Func<Edi835ClaimGroup, ValueTask>? onClaimGroup,
        CancellationToken cancellationToken = default)
    {
        var state = new ParseState();
        await foreach (var seg in X12Tokenizer.EnumerateAsync(stream, cancellationToken).ConfigureAwait(false))
            await ProcessSegmentAsync(seg, state, onClaimGroup).ConfigureAwait(false);
        if (state.Current != null)
            await CommitCurrentAsync(state, onClaimGroup).ConfigureAwait(false);
        return BuildResult(state);
    }

    public static async Task<Edi835ParseResult> ParseAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var state = new ParseState();
        await foreach (var seg in X12Tokenizer.EnumerateAsync(stream, cancellationToken).ConfigureAwait(false))
            await ProcessSegmentAsync(seg, state, null).ConfigureAwait(false);
        if (state.Current != null)
            await CommitCurrentAsync(state, null).ConfigureAwait(false);
        return BuildResult(state);
    }

    public static Edi835ParseResult Parse(string raw)
    {
        return Parse(X12Tokenizer.Enumerate(raw));
    }

    public static Edi835ParseResult Parse(IEnumerable<X12Segment> segments)
    {
        var state = new ParseState();
        foreach (var seg in segments)
            ProcessSegment(seg, state);
        if (state.Current != null)
            state.Groups.Add(state.Current.Build());
        return BuildResult(state);
    }

    private static async Task ProcessSegmentAsync(X12Segment seg, ParseState state, Func<Edi835ClaimGroup, ValueTask>? onClaimGroup)
    {
        var beforeCount = state.Groups.Count;
        ProcessSegment(seg, state);
        if (onClaimGroup != null && state.Groups.Count > beforeCount)
        {
            for (var i = beforeCount; i < state.Groups.Count; i++)
                await onClaimGroup(state.Groups[i]).ConfigureAwait(false);
        }
    }

    private static async Task CommitCurrentAsync(ParseState state, Func<Edi835ClaimGroup, ValueTask>? onClaimGroup)
    {
        if (state.Current == null)
            return;
        var committed = state.Current.Build();
        state.Groups.Add(committed);
        if (onClaimGroup != null)
            await onClaimGroup(committed).ConfigureAwait(false);
        state.Current = null;
    }

    private static void ProcessSegment(X12Segment seg, ParseState state)
    {
        switch (seg.Id)
        {
            case "BPR" when seg.Elements.Count > 15:
                // BPR16 (1-based) = production date / check date in many 835 implementations.
                state.BprPaymentAmount = seg.Elements.Count > 2 && decimal.TryParse(seg.Elements[2].Trim(), out var bprAmt) ? bprAmt : null;
                state.BprCheckDateRaw = seg.Elements[15]?.Trim();
                break;
            case "N1" when seg.Elements.Count > 3 && seg.Elements[1] == "PR":
                state.PayerName = seg.Elements[3].Trim();
                break;
            case "TRN" when seg.Elements.Count > 2:
                state.TraceTypeCode = seg.Elements.Count > 1 ? seg.Elements[1]?.Trim() : null;
                state.TraceNumber = seg.Elements.Count > 2 ? seg.Elements[2]?.Trim() : null;
                state.OriginatingCompanyIdentifier = seg.Elements.Count > 3 ? seg.Elements[3]?.Trim() : null;
                break;
            case "CLP" when seg.Elements.Count > 4:
                if (state.Current != null)
                    state.Groups.Add(state.Current.Build());
                state.Current = new Edi835ClaimGroupBuilder(seg);
                state.ClaimPayments.Add(new Edi835ClaimPayment
                {
                    ClaimId = seg.Elements.Count > 1 ? seg.Elements[1].Trim() : string.Empty,
                    StatusCode = seg.Elements.Count > 2 ? seg.Elements[2].Trim() : null,
                    TotalCharge = seg.Elements.Count > 3 && decimal.TryParse(seg.Elements[3].Trim(), out var tc) ? tc : null,
                    PaidAmount = seg.Elements.Count > 4 && decimal.TryParse(seg.Elements[4].Trim(), out var pa) ? pa : null,
                    PatientResponsibility = seg.Elements.Count > 5 && decimal.TryParse(seg.Elements[5].Trim(), out var pr) ? pr : null
                });
                if (state.PendingCasBeforeFirstClp.Count > 0)
                {
                    foreach (var pending in state.PendingCasBeforeFirstClp)
                        state.Current.AddAdjustment(pending);
                    state.PendingCasBeforeFirstClp.Clear();
                }
                break;
            case "CAS" when seg.Elements.Count > 3:
                var adj = new Edi835CasAdjustment
                {
                    GroupCode = seg.Elements.Count > 1 ? seg.Elements[1] : null,
                    ReasonCode = seg.Elements.Count > 2 ? seg.Elements[2] : null,
                    Amount = seg.Elements.Count > 3 && decimal.TryParse(seg.Elements[3].Trim(), out var a) ? a : null
                };
                state.FlatCas.Add(adj);
                if (state.Current != null)
                    state.Current.AddAdjustment(adj);
                else
                    state.PendingCasBeforeFirstClp.Add(adj);
                break;
            case "SVC" when seg.Elements.Count > 3:
                var svc = new Edi835ServiceLineDetail
                {
                    ProcedureComposite = seg.Elements.Count > 1 ? seg.Elements[1] : null,
                    ServiceDate = null,
                    LineChargeAmount = seg.Elements.Count > 2 && decimal.TryParse(seg.Elements[2].Trim(), out var lc) ? lc : null,
                    LinePaidAmount = seg.Elements.Count > 3 && decimal.TryParse(seg.Elements[3].Trim(), out var lp) ? lp : null,
                    RevenueCode = seg.Elements.Count > 4 ? seg.Elements[4] : null
                };
                state.SvcDetails.Add(svc);
                state.Current?.AddServiceLine(svc);
                break;
            case "DTM" when seg.Elements.Count > 2:
                // Service-line DOS commonly appears as DTM*472*yyyymmdd following SVC.
                if (seg.Elements[1]?.Trim() == "472" && state.Current != null)
                    state.Current.SetCurrentServiceDate(ParseDate(seg.Elements[2]));
                break;
            case "PLB" when seg.Elements.Count > 3:
                var pairIndex = 0;
                for (var i = 3; i < seg.Elements.Count; i += 2)
                {
                    pairIndex++;
                    var identifier = seg.Elements[i];
                    decimal? amount = seg.Elements.Count > i + 1 && decimal.TryParse(seg.Elements[i + 1].Trim(), out var plbAmount) ? plbAmount : null;
                    state.PlbAdjustments.Add(new Edi835PlbAdjustment
                    {
                        ProviderId = seg.Elements.Count > 1 ? seg.Elements[1] : null,
                        FiscalPeriodDate = ParseDate(seg.Elements.Count > 2 ? seg.Elements[2] : null),
                        AdjustmentIdentifier = identifier,
                        Amount = amount,
                        PairIndex = pairIndex
                    });
                }
                break;
        }
    }

    private static Edi835ParseResult BuildResult(ParseState state)
    {
        decimal? firstPayment = state.Groups.Count > 0 ? state.Groups[0].ClaimPaymentAmount : null;
        string? note = null;
        if (state.PayerName != null || firstPayment != null)
        {
            var parts = new List<string>();
            if (state.PayerName != null) parts.Add($"Payment from {state.PayerName}");
            if (firstPayment != null) parts.Add($"${firstPayment.Value:F2}");
            note = string.Join(" - ", parts);
        }

        return new Edi835ParseResult
        {
            PayerName = state.PayerName,
            TraceNumber = string.IsNullOrWhiteSpace(state.TraceNumber) ? "NoTrace" : state.TraceNumber!,
            TraceTypeCode = state.TraceTypeCode,
            OriginatingCompanyIdentifier = state.OriginatingCompanyIdentifier,
            ClaimGroups = state.Groups,
            ClaimPayments = state.ClaimPayments,
            ClaimPaymentAmount = firstPayment,
            CasAdjustments = state.FlatCas,
            ServiceLineDetails = state.SvcDetails,
            ProviderAdjustments = state.PlbAdjustments,
            SummaryNote = note,
            CheckDateUtc = ParseBprCheckDateUtc(state.BprCheckDateRaw)
            ,
            BprPaymentAmount = state.BprPaymentAmount
        };
    }

    private sealed class Edi835ClaimGroupBuilder
    {
        private readonly List<Edi835CasAdjustment> _adjustments = new();
        private readonly List<Edi835ServiceLineBuilder> _serviceLineBuilders = new();
        private Edi835ServiceLineBuilder? _currentServiceLine;
        private readonly string? _patientControl;
        private readonly string? _status;
        private readonly decimal? _charge;
        private readonly decimal? _paid;
        private readonly decimal? _patientResponsibility;

        public Edi835ClaimGroupBuilder(X12Segment clp)
        {
            _patientControl = clp.Elements.Count > 1 ? clp.Elements[1].Trim() : null;
            _status = clp.Elements.Count > 2 ? clp.Elements[2].Trim() : null;
            _charge = clp.Elements.Count > 3 && decimal.TryParse(clp.Elements[3].Trim(), out var c) ? c : null;
            _paid = clp.Elements.Count > 4 && decimal.TryParse(clp.Elements[4].Trim(), out var p) ? p : null;
            _patientResponsibility = clp.Elements.Count > 5 && decimal.TryParse(clp.Elements[5].Trim(), out var pr) ? pr : null;
        }

        public void AddAdjustment(Edi835CasAdjustment adj)
        {
            if (_currentServiceLine != null)
            {
                _currentServiceLine.Adjustments.Add(adj);
                return;
            }
            _adjustments.Add(adj);
        }

        public void AddServiceLine(Edi835ServiceLineDetail svc)
        {
            _currentServiceLine = new Edi835ServiceLineBuilder
            {
                ProcedureComposite = svc.ProcedureComposite,
                ServiceDate = svc.ServiceDate,
                LineChargeAmount = svc.LineChargeAmount,
                LinePaidAmount = svc.LinePaidAmount,
                RevenueCode = svc.RevenueCode
            };
            _serviceLineBuilders.Add(_currentServiceLine);
        }

        public void SetCurrentServiceDate(DateOnly? serviceDate)
        {
            if (_currentServiceLine != null)
                _currentServiceLine.ServiceDate = serviceDate;
        }

        public Edi835ClaimGroup Build()
        {
            return new Edi835ClaimGroup
            {
                ClaimId = _patientControl,
                ClaimStatusCode = _status,
                TotalClaimChargeAmount = _charge,
                ClaimPaymentAmount = _paid,
                PatientResponsibilityAmount = _patientResponsibility,
                Adjustments = _adjustments.ToArray(),
                ServiceLines = _serviceLineBuilders.Select(s => new Edi835ServiceLineDetail
                {
                    ProcedureComposite = s.ProcedureComposite,
                    ServiceDate = s.ServiceDate,
                    LineChargeAmount = s.LineChargeAmount,
                    LinePaidAmount = s.LinePaidAmount,
                    RevenueCode = s.RevenueCode,
                    Adjustments = s.Adjustments.ToArray()
                }).ToArray()
            };
        }
    }

    private sealed class Edi835ServiceLineBuilder
    {
        public string? ProcedureComposite { get; init; }
        public DateOnly? ServiceDate { get; set; }
        public decimal? LineChargeAmount { get; init; }
        public decimal? LinePaidAmount { get; init; }
        public string? RevenueCode { get; init; }
        public List<Edi835CasAdjustment> Adjustments { get; } = new();
    }

    private static DateOnly? ParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        return DateOnly.TryParseExact(raw.Trim(), "yyyyMMdd", out var d) ? d : null;
    }

    private static DateTime? ParseBprCheckDateUtc(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var t = raw.Trim();
        if (t.Length >= 8 && DateOnly.TryParseExact(t[..8], "yyyyMMdd", out var d))
            return d.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        return null;
    }

    private sealed class ParseState
    {
        public string? BprCheckDateRaw { get; set; }
        public decimal? BprPaymentAmount { get; set; }
        public string? PayerName { get; set; }
        public string? TraceTypeCode { get; set; }
        public string? TraceNumber { get; set; }
        public string? OriginatingCompanyIdentifier { get; set; }
        public List<Edi835ClaimGroup> Groups { get; } = new();
        public List<Edi835ClaimPayment> ClaimPayments { get; } = new();
        public List<Edi835CasAdjustment> FlatCas { get; } = new();
        public List<Edi835ServiceLineDetail> SvcDetails { get; } = new();
        public List<Edi835PlbAdjustment> PlbAdjustments { get; } = new();
        public List<Edi835CasAdjustment> PendingCasBeforeFirstClp { get; } = new();
        public Edi835ClaimGroupBuilder? Current { get; set; }
    }
}
