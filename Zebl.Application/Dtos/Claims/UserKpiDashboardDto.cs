namespace Zebl.Application.Dtos.Claims;

public sealed class UserKpiDashboardDto
{
    public string UserName { get; set; } = string.Empty;
    public int TotalClaims { get; set; }
    public decimal TotalCharge { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalBalance { get; set; }
    public List<UserKpiStatusPointDto> ClaimsByStatus { get; set; } = new();
    public List<UserKpiTrendPointDto> ClaimsTrend { get; set; } = new();
    public List<UserKpiAgingBucketDto> AgingBuckets { get; set; } = new();
    public List<UserKpiPayerPointDto> TopPayers { get; set; } = new();
}

public sealed class UserKpiStatusPointDto
{
    public string Label { get; set; } = string.Empty;
    public int Value { get; set; }
}

public sealed class UserKpiTrendPointDto
{
    public string Label { get; set; } = string.Empty;
    public int Value { get; set; }
}

public sealed class UserKpiAgingBucketDto
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
}

public sealed class UserKpiPayerPointDto
{
    public string Label { get; set; } = string.Empty;
    public int Value { get; set; }
}
