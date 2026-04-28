using System.Collections.Generic;
using System.Linq;

namespace Zebl.Application.Services;

public interface IEligibilityParser
{
    EligibilityParseResult Parse(string raw271);
}

public sealed class EligibilityParser : IEligibilityParser
{
    public EligibilityParseResult Parse(string raw271)
    {
        if (string.IsNullOrWhiteSpace(raw271))
        {
            return new EligibilityParseResult
            {
                EligibilityStatus = "Unknown",
                ErrorMessage = "Empty 271 content."
            };
        }

        var result = new EligibilityParseResult { EligibilityStatus = "Unknown" };
        var segments = raw271
            .Split('~', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var segment in segments)
        {
            var parts = segment.Split('*');
            if (parts.Length == 0)
                continue;

            if (string.Equals(parts[0], "NM1", StringComparison.Ordinal) &&
                parts.Length > 3 &&
                string.Equals(parts[1], "PR", StringComparison.Ordinal))
            {
                result.PayerName = parts[3].Trim();
                continue;
            }

            if (string.Equals(parts[0], "AAA", StringComparison.Ordinal))
            {
                result.ErrorMessage = segment;
                continue;
            }

            if (string.Equals(parts[0], "DTP", StringComparison.Ordinal) && parts.Length > 3)
            {
                if (parts[1] == "346" || parts[1] == "291")
                    result.EligibilityStartDate = parts[3].Trim();
                else if (parts[1] == "356")
                    result.EligibilityEndDate = parts[3].Trim();
                continue;
            }

            if (!string.Equals(parts[0], "EB", StringComparison.Ordinal) || parts.Length < 2)
                continue;

            result.EligibilityStatus = parts[1] switch
            {
                "1" => "Active",
                "6" => "Inactive",
                _ => "Unknown"
            };

            var benefit = new EligibilityBenefitResult
            {
                ServiceType = parts.Length > 3 ? parts[3].Trim() : null,
                Benefit = parts[1].Trim(),
                Amount = parts.Length > 7 ? parts[7].Trim() : null,
                Description = parts.Length > 5 ? parts[5].Trim() : null
            };
            result.Benefits.Add(benefit);

            if (string.IsNullOrWhiteSpace(result.PlanName) && parts.Length > 5 && !string.IsNullOrWhiteSpace(parts[5]))
                result.PlanName = parts[5].Trim();
        }

        if (result.Benefits.Count > 0)
        {
            result.PlanDetails = string.Join("; ", result.Benefits
                .Where(b => !string.IsNullOrWhiteSpace(b.Description))
                .Select(b => b.Description!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase));
        }

        return result;
    }
}

public sealed class EligibilityParseResult
{
    public string EligibilityStatus { get; set; } = "Unknown";
    public string? ErrorMessage { get; set; }
    public string? PayerName { get; set; }
    public string? PlanName { get; set; }
    public string? PlanDetails { get; set; }
    public string? EligibilityStartDate { get; set; }
    public string? EligibilityEndDate { get; set; }
    public List<EligibilityBenefitResult> Benefits { get; set; } = [];
}

public sealed class EligibilityBenefitResult
{
    public string? ServiceType { get; set; }
    public string? Benefit { get; set; }
    public string? Amount { get; set; }
    public string? Description { get; set; }
}
