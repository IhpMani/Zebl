namespace Zebl.Application.Edi.Generation;

/// <summary>
/// Single implementation for X12 270 (005010X279A1) interchange construction.
/// </summary>
public static class Eligibility270Builder
{
    public static string BuildInterchange(Eligibility270Envelope env)
    {
        ArgumentNullException.ThrowIfNull(env);

        var now = DateTime.UtcNow;
        var genderCode = NormalizeGender(env.PatientSex);
        var patDob = env.PatientBirthDate;

        var segs = new List<EdiGenSegment>
        {
            EdiGenSegment.Create("ISA",
                EscapeIsa(env.AuthInfoQualifier), Pad(string.Empty, 10),
                EscapeIsa(env.SecurityInfoQualifier), Pad(string.Empty, 10),
                EscapeIsa(env.SenderQualifier), Pad(env.SenderId, 15),
                EscapeIsa(env.ReceiverQualifier), Pad(env.InterchangeReceiverId, 15),
                now.ToString("yyMMdd"), now.ToString("HHmm"), "^", "00501", env.InterchangeControlNumber, "0", Escape(env.TestProdIndicator), ":"),
            EdiGenSegment.Create("GS", "HS",
                Escape(env.GsSender), Escape(env.GsReceiver),
                now.ToString("yyyyMMdd"), now.ToString("HHmm"),
                env.GroupControlNumber, "X", "005010X279A1"),
            EdiGenSegment.Create("ST", "270", env.TransactionSetControlNumber, "005010X279A1"),
            EdiGenSegment.Create("BHT", "0022", "13", env.InterchangeControlNumber, now.ToString("yyyyMMdd"), now.ToString("HHmm")),
            EdiGenSegment.Create("HL", "1", "", "20", "1"),
            EdiGenSegment.Create("NM1", "PR", "2", Escape(env.ReceiverName), "", "", "", "", "PI", Escape(env.ReceiverId)),
            EdiGenSegment.Create("HL", "2", "1", "21", "1"),
            EdiGenSegment.Create("NM1", "1P", "1", Escape(env.ProviderName), "", "", "", "XX", Escape(env.ProviderNpi)),
            EdiGenSegment.Create("HL", "3", "2", "22", "0"),
            EdiGenSegment.Create("TRN", "1", env.InterchangeControlNumber, Escape(env.SubmitterId)),
            EdiGenSegment.Create("NM1", "IL", "1",
                Escape(env.SubscriberLastName), Escape(env.SubscriberFirstName), "", "", "", "MI", Escape(env.SubscriberMemberId)),
            EdiGenSegment.Create("DMG", "D8", patDob.ToString("yyyyMMdd"), genderCode),
            EdiGenSegment.Create("DTP", "291", "D8", now.ToString("yyyyMMdd")),
            EdiGenSegment.Create("EQ", "30", "", "", Escape(env.PayerEligibilityId))
        };

        var transaction = BuildLoopAwareTransaction(segs);
        var flattened = transaction.Flatten();
        var stIndex = flattened.ToList().FindIndex(s => s.Id == "ST");
        if (stIndex < 0)
            throw new InvalidOperationException("270 generation missing ST segment.");
        var seCount = (flattened.Count - stIndex) + 1;
        transaction.FooterSegments.Add(EdiGenSegment.Create("SE", seCount.ToString(), env.TransactionSetControlNumber));
        transaction.FooterSegments.Add(EdiGenSegment.Create("GE", "1", env.GroupControlNumber));
        transaction.FooterSegments.Add(EdiGenSegment.Create("IEA", "1", env.InterchangeControlNumber));

        return EdiGenSerializer.Serialize(transaction);
    }

    private static EdiTransaction BuildLoopAwareTransaction(IReadOnlyList<EdiGenSegment> segments)
    {
        var tx = new EdiTransaction();
        EdiLoop? loop2000A = null;
        EdiLoop? loop2300 = null;

        foreach (var seg in segments)
        {
            if (seg.Id == "HL" && seg.Elements.Count > 3 && seg.Elements[3] == "20")
            {
                loop2000A = new EdiLoop("2000A");
                tx.Loop2000A.Add(loop2000A);
            }
            else if (seg.Id == "EQ")
            {
                loop2300 = new EdiLoop("2300");
                tx.Loop2300.Add(loop2300);
            }

            if (loop2300 != null)
                loop2300.Segments.Add(seg);
            else if (loop2000A != null)
                loop2000A.Segments.Add(seg);
            else
                tx.HeaderSegments.Add(seg);
        }

        return tx;
    }

    private static string Pad(string value, int width)
    {
        var trimmed = value.Trim();
        return trimmed.Length > width ? trimmed[..width] : trimmed.PadRight(width);
    }

    private static string Escape(string value) =>
        value.Replace("*", string.Empty).Replace("~", string.Empty).Replace("^", string.Empty).Trim();

    private static string EscapeIsa(string value) =>
        value.Replace("*", string.Empty).Replace("~", string.Empty).Replace("^", string.Empty);

    private static string NormalizeGender(string? value)
    {
        if (string.Equals(value, "M", StringComparison.OrdinalIgnoreCase))
            return "M";
        if (string.Equals(value, "F", StringComparison.OrdinalIgnoreCase))
            return "F";
        return "U";
    }
}
