using System.Globalization;
using Zebl.Application.Domain;
using Zebl.Application.Dtos.Claims;

namespace Zebl.Application.Edi.Generation;

/// <summary>
/// Builds 837 claim interchange as structured segments; serialized via <see cref="EdiGenSerializer"/>.
/// </summary>
public static class Claim837Builder
{
    public static string BuildInterchange(
        Claim837EdiContext ctx,
        EdiSubmitterReceiverConfig submitterReceiver,
        EdiControlNumbers controlNumbers)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(submitterReceiver);

        static string RequireNonEmpty(string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"{fieldName} is required in submitter/receiver configuration for 837 export.");
            return value.Trim();
        }

        static string PadIsaField(string value, int width)
        {
            var t = value.Trim();
            return t.Length > width ? t[..width] : t.PadRight(width);
        }

        var data = ctx.Data;
        var payer = ctx.Payer;
        var claimFilingIndicator = ctx.ClaimFilingIndicator;
        var insuranceTypeCode = ctx.InsuranceTypeCode;

        var now = DateTime.Now;
        var senderQualifier = RequireNonEmpty(submitterReceiver.SenderQualifier, nameof(submitterReceiver.SenderQualifier));
        var senderIdRaw = RequireNonEmpty(submitterReceiver.SenderId, nameof(submitterReceiver.SenderId));
        var receiverQualifier = RequireNonEmpty(submitterReceiver.ReceiverQualifier, nameof(submitterReceiver.ReceiverQualifier));
        var interchangeReceiverIdRaw = RequireNonEmpty(submitterReceiver.InterchangeReceiverId, nameof(submitterReceiver.InterchangeReceiverId));
        var authInfoQualifier = RequireNonEmpty(submitterReceiver.AuthorizationInfoQualifier, nameof(submitterReceiver.AuthorizationInfoQualifier));
        var authInfo = PadIsaField(submitterReceiver.AuthorizationInfo ?? string.Empty, 10);
        var securityInfoQualifier = RequireNonEmpty(submitterReceiver.SecurityInfoQualifier, nameof(submitterReceiver.SecurityInfoQualifier));
        var securityInfo = PadIsaField(submitterReceiver.SecurityInfo ?? string.Empty, 10);
        var gsSender = RequireNonEmpty(submitterReceiver.SenderCode, nameof(submitterReceiver.SenderCode));
        var gsReceiver = RequireNonEmpty(submitterReceiver.ReceiverCode, nameof(submitterReceiver.ReceiverCode));
        var testProd = RequireNonEmpty(submitterReceiver.TestProdIndicator, nameof(submitterReceiver.TestProdIndicator));
        var submitterName = RequireNonEmpty(submitterReceiver.SubmitterName, nameof(submitterReceiver.SubmitterName));
        var submitterId = RequireNonEmpty(submitterReceiver.SubmitterId, nameof(submitterReceiver.SubmitterId));
        var receiverName = RequireNonEmpty(submitterReceiver.ReceiverName, nameof(submitterReceiver.ReceiverName));
        var receiverId = RequireNonEmpty(submitterReceiver.ReceiverId, nameof(submitterReceiver.ReceiverId));

        var senderId = PadIsaField(senderIdRaw, 15);
        var interchangeReceiverId = PadIsaField(interchangeReceiverIdRaw, 15);
        var interchangeControlNumber = controlNumbers.InterchangeControlNumber.PadLeft(9, '0');
        if (interchangeControlNumber.Length > 9)
            interchangeControlNumber = interchangeControlNumber[^9..];
        var groupControlNumber = controlNumbers.GroupControlNumber;
        var transactionControlNumber = controlNumbers.TransactionControlNumber;

        var segs = new List<EdiGenSegment>();

        segs.Add(EdiGenSegment.Create("ISA",
            EscapeIsa(authInfoQualifier), EscapeIsa(authInfo),
            EscapeIsa(securityInfoQualifier), EscapeIsa(securityInfo),
            EscapeIsa(senderQualifier), EscapeIsa(senderId),
            EscapeIsa(receiverQualifier), EscapeIsa(interchangeReceiverId),
            now.ToString("yyMMdd"), now.ToString("HHmm"), "^", "00501", Escape(interchangeControlNumber), "0", Escape(testProd), ":"));

        segs.Add(EdiGenSegment.Create("GS", "HC",
            Escape(gsSender), Escape(gsReceiver),
            now.ToString("yyyyMMdd"), now.ToString("HHmm"),
            Escape(groupControlNumber), "X", "005010X222A1"));

        segs.Add(EdiGenSegment.Create("ST", "837", transactionControlNumber));

        segs.Add(EdiGenSegment.Create("BHT", "0019", "00",
            now.ToString("yyyyMMddHHmm"),
            now.ToString("yyyyMMdd"),
            now.ToString("HHmm"),
            "CH"));

        segs.Add(EdiGenSegment.Create("NM1", "41", "2", Escape(submitterName), "", "", "", "", "46", Escape(submitterId)));
        segs.Add(EdiGenSegment.Create("NM1", "40", "2", Escape(receiverName), "", "", "", "", "46", Escape(receiverId)));

        if (data.BillingProvider != null)
        {
            segs.Add(EdiGenSegment.Create("HL", "1", "", "20", "1"));
            segs.Add(EdiGenSegment.Create("PRV", "BI", "PXC", Escape(data.BillingProvider.PhyNPI ?? "")));
            segs.Add(EdiGenSegment.Create("NM1", "85", "2",
                Escape(data.BillingProvider.PhyLastName ?? ""),
                Escape(data.BillingProvider.PhyFirstName ?? "")));
        }

        if (data.PrimaryInsured != null)
        {
            segs.Add(EdiGenSegment.Create("HL", "2", "1", "22", "0"));
            segs.Add(EdiGenSegment.Create("SBR", "P", "18",
                Escape(data.PrimaryInsured.ClaInsGroupNumber ?? ""),
                "", "", "", "",
                Escape(claimFilingIndicator ?? "")));

            segs.Add(EdiGenSegment.Create("NM1", "PR", "2", Escape(payer.PayName ?? "")));
            segs.Add(EdiGenSegment.Create("N3", Escape(payer.PayAddr1 ?? "")));
            segs.Add(EdiGenSegment.Create("N4",
                Escape(payer.PayCity ?? ""),
                Escape(payer.PayState ?? ""),
                Escape(payer.PayZip ?? "")));

            segs.Add(EdiGenSegment.Create("NM1", "IL", "1",
                Escape(data.PrimaryInsured.ClaInsLastName ?? ""),
                Escape(data.PrimaryInsured.ClaInsFirstName ?? "")));

            if (data.PrimaryInsured.ClaInsBirthDate.HasValue || !string.IsNullOrWhiteSpace(data.PrimaryInsured.ClaInsSex))
            {
                segs.Add(EdiGenSegment.Create("DMG", "D8",
                    FormatDate(data.PrimaryInsured.ClaInsBirthDate),
                    Escape(data.PrimaryInsured.ClaInsSex ?? "")));
            }

            if (payer.PayExportSSN && !string.IsNullOrWhiteSpace(data.PrimaryInsured.ClaInsSSN))
                segs.Add(EdiGenSegment.Create("REF", "SY", Escape(data.PrimaryInsured.ClaInsSSN)));
        }

        if (data.Patient != null)
        {
            segs.Add(EdiGenSegment.Create("HL", "3", "2", "23", "0"));
            segs.Add(EdiGenSegment.Create("NM1", "QC", "1",
                Escape(data.Patient.PatLastName ?? ""),
                Escape(data.Patient.PatFirstName ?? ""),
                Escape(data.Patient.PatMI ?? "")));
            segs.Add(EdiGenSegment.Create("DMG", "D8",
                FormatDate(data.Patient.PatBirthDate),
                Escape(data.Patient.PatSex ?? "")));
        }

        // CLM01 = Claim.ClaEdiClaimId (set at 837 prepare to internal ClaID string) for 835 CLP01 round-trip.
        var claimControlNumber = ResolveClm01ClaimIdentifier(data);
        segs.Add(EdiGenSegment.Create("CLM", Escape(claimControlNumber), "0"));
        if (!string.IsNullOrWhiteSpace(insuranceTypeCode))
            segs.Add(EdiGenSegment.Create("REF", "EL", Escape(insuranceTypeCode)));

        if (payer.PayExportAuthIn2400)
            segs.Add(EdiGenSegment.Create("REF", "G1", Escape(data.ClaReferralNumber ?? "")));
        else if (!string.IsNullOrWhiteSpace(data.ClaReferralNumber))
            segs.Add(EdiGenSegment.Create("REF", "G1", Escape(data.ClaReferralNumber)));

        if (payer.PayExportOriginalRefIn2330B && !string.IsNullOrWhiteSpace(data.ClaReferralNumber))
            segs.Add(EdiGenSegment.Create("REF", "1L", Escape(data.ClaReferralNumber)));

        if (!payer.PayIgnoreRenderingProvider && data.RenderingProvider != null)
        {
            segs.Add(EdiGenSegment.Create("NM1", "82", "1",
                Escape(data.RenderingProvider.PhyLastName ?? ""),
                Escape(data.RenderingProvider.PhyFirstName ?? ""),
                "", "", "", "", "XX", Escape(data.RenderingProvider.PhyNPI ?? "")));
        }

        if (payer.PayExportPatientAmtDueIn2430)
            segs.Add(EdiGenSegment.Create("AMT", "EAF", "0"));

        var lx = 1;
        foreach (var line in data.ServiceLines)
        {
            var proc = Escape(line.SrvProcedureCode ?? "");
            var m1 = Escape(line.SrvModifier1 ?? "");
            var m2 = Escape(line.SrvModifier2 ?? "");
            var m3 = Escape(line.SrvModifier3 ?? "");
            var m4 = Escape(line.SrvModifier4 ?? "");
            var units = line.SrvUnits.HasValue && line.SrvUnits.Value > 0 ? line.SrvUnits.Value.ToString("0.##") : "1";
            var charge = line.SrvCharges.ToString("0.00");
            var serviceDate = line.SrvFromDate ?? line.SrvToDate;
            var sv1Composite = new Sv1Composite
            {
                ProductOrServiceIdQualifier = "HC",
                ProcedureCode = proc,
                Modifier1 = m1,
                Modifier2 = m2,
                Modifier3 = m3,
                Modifier4 = m4
            };

            segs.Add(EdiGenSegment.Create("LX", lx.ToString()));
            segs.Add(EdiGenSegment.Create("SV1", EdiGenSerializer.SerializeSv1Composite(sv1Composite),
                charge, "UN", units, "", "", "1"));

            if (serviceDate.HasValue)
                segs.Add(EdiGenSegment.Create("DTP", "472", "D8", FormatDate(serviceDate)));

            if (!string.IsNullOrWhiteSpace(line.SrvDesc))
                segs.Add(EdiGenSegment.Create("NTE", "ADD", Escape(line.SrvDesc)));

            if (!string.IsNullOrWhiteSpace(line.SrvNationalDrugCode))
            {
                segs.Add(EdiGenSegment.Create("LIN", "", "N4", Escape(line.SrvNationalDrugCode)));
                if (line.SrvDrugUnitCount.HasValue || !string.IsNullOrWhiteSpace(line.SrvDrugUnitMeasurement))
                {
                    var count = line.SrvDrugUnitCount.HasValue ? line.SrvDrugUnitCount.Value.ToString("0.####") : "";
                    segs.Add(EdiGenSegment.Create("CTP", "", "", "", charge, Escape(count), Escape(line.SrvDrugUnitMeasurement ?? "")));
                }
            }

            lx++;
        }

        var transaction = BuildLoopAwareTransaction(segs);
        var flattened = transaction.Flatten();
        var stIndex = flattened.ToList().FindIndex(s => s.Id == "ST");
        if (stIndex < 0)
            throw new InvalidOperationException("837 generation missing ST segment.");
        var segmentCount = (flattened.Count - stIndex) + 1;
        transaction.FooterSegments.Add(EdiGenSegment.Create("SE", segmentCount.ToString(), transactionControlNumber));
        transaction.FooterSegments.Add(EdiGenSegment.Create("GE", "1", Escape(groupControlNumber)));
        transaction.FooterSegments.Add(EdiGenSegment.Create("IEA", "1", Escape(interchangeControlNumber)));

        return EdiGenSerializer.Serialize(transaction);
    }

    private static EdiTransaction BuildLoopAwareTransaction(IReadOnlyList<EdiGenSegment> segments)
    {
        var transaction = new EdiTransaction();
        EdiLoop? currentLoop = null;

        foreach (var seg in segments)
        {
            if (seg.Id == "HL" && seg.Elements.Count > 3 && seg.Elements[3] == "20")
            {
                currentLoop = new EdiLoop("2000A");
                transaction.Loop2000A.Add(currentLoop);
            }
            else if (seg.Id == "CLM")
            {
                currentLoop = new EdiLoop("2300");
                transaction.Loop2300.Add(currentLoop);
            }
            else if (seg.Id == "LX")
            {
                currentLoop = new EdiLoop("2400");
                transaction.Loop2400.Add(currentLoop);
            }

            if (currentLoop == null)
                transaction.HeaderSegments.Add(seg);
            else
                currentLoop.Segments.Add(seg);
        }

        return transaction;
    }

    private static string ResolveClm01ClaimIdentifier(Claim837ExportData data)
    {
        var value = data.ClaEdiClaimId;
        if (string.IsNullOrWhiteSpace(value))
            value = data.ClaimId.ToString(CultureInfo.InvariantCulture);

        value = value.Trim();
        if (value.Length > 20)
            value = value[..20];

        return value;
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Replace("*", "").Replace("~", "").Replace("^", "").Trim();
    }

    private static string EscapeIsa(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Replace("*", "").Replace("~", "").Replace("^", "");
    }

    private static string FormatDate(DateOnly? d) => d?.ToString("yyyyMMdd") ?? "";
}
