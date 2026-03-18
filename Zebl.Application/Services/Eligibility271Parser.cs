using System.Globalization;

namespace Zebl.Application.Services;

public sealed class Eligibility271Parser
{
    public Eligibility271Result Parse(string raw271)
    {
        if (string.IsNullOrWhiteSpace(raw271))
            return new Eligibility271Result();

        var result = new Eligibility271Result();
        var segments = raw271.Split('~', StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            var parts = segment.Split('*');
            if (parts.Length == 0)
                continue;

            switch (parts[0])
            {
                case "EB" when result.CoverageStatus == null:
                    // EB*<1>*<2>*<3>*<4>*<5>*<6>*<7>*<8>*...
                    if (parts.Length > 1)
                        result.CoverageStatus = parts[1];
                    if (parts.Length > 3)
                        result.PlanName = parts[3];
                    if (parts.Length > 5 && decimal.TryParse(parts[5], NumberStyles.Any, CultureInfo.InvariantCulture, out var copay))
                        result.CopayAmount = copay;
                    if (parts.Length > 6 && decimal.TryParse(parts[6], NumberStyles.Any, CultureInfo.InvariantCulture, out var deductible))
                        result.DeductibleAmount = deductible;
                    if (parts.Length > 7 && decimal.TryParse(parts[7], NumberStyles.Any, CultureInfo.InvariantCulture, out var coins))
                        result.CoinsurancePercent = coins;
                    break;

                case "DTP":
                    // DTP*291*D8*YYYYMMDD or DTP*292*RD8*YYYYMMDD-YYYYMMDD
                    if (parts.Length >= 3)
                    {
                        var qualifier = parts[1];
                        var format = parts[2];
                        if (parts.Length > 3)
                        {
                            var value = parts[3];
                            if (format == "D8")
                            {
                                if (DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                                {
                                    if (qualifier == "291")
                                        result.CoverageStartDate = dt;
                                    else if (qualifier == "292")
                                        result.CoverageEndDate = dt;
                                }
                            }
                            else if (format == "RD8" && value.Contains('-'))
                            {
                                var dates = value.Split('-', StringSplitOptions.RemoveEmptyEntries);
                                if (dates.Length == 2)
                                {
                                    if (DateTime.TryParseExact(dates[0], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
                                        result.CoverageStartDate = start;
                                    if (DateTime.TryParseExact(dates[1], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
                                        result.CoverageEndDate = end;
                                }
                            }
                        }
                    }
                    break;
            }
        }

        return result;
    }
}

public sealed class Eligibility271Result
{
    public string? CoverageStatus { get; set; }
    public string? PlanName { get; set; }
    public decimal? DeductibleAmount { get; set; }
    public decimal? CopayAmount { get; set; }
    public decimal? CoinsurancePercent { get; set; }
    public DateTime? CoverageStartDate { get; set; }
    public DateTime? CoverageEndDate { get; set; }
}

