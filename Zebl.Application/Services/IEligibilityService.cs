using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Zebl.Application.Services;

/// <summary>
/// Runs eligibility (270/271) checks using clearinghouse credentials from Program Settings.
/// Credentials are only for clearinghouse communication, not application login.
/// </summary>
public interface IEligibilityService
{
    Task<EligibilityRequestResultDto> RequestEligibilityAsync(EligibilityRequestCreateDto request, CancellationToken cancellationToken = default);
    Task<EligibilityRequestStatusDto?> GetEligibilityStatusAsync(int requestId, CancellationToken cancellationToken = default);
    Task<string> Generate270Async(EligibilityRequestCreateDto request, CancellationToken cancellationToken = default);
    Task<EligibilityPreflightResultDto> PreflightAsync(EligibilityPreflightRequestDto request, CancellationToken cancellationToken = default);
}

public sealed class EligibilityRequestCreateDto
{
    public int PatientId { get; set; }
}

public sealed class EligibilityRequestResultDto
{
    public int Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? BatchFileName { get; set; }
    public string ControlNumber { get; set; } = string.Empty;
    public string? ProviderNpi { get; set; }
    public string? ProviderMode { get; set; }
    public bool UsedPayerOverride { get; set; }
}

public sealed class EligibilityRequestStatusDto
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public int PayerId { get; set; }
    public string SubscriberId { get; set; } = string.Empty;
    public string ControlNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? BatchFileName { get; set; }
    public string? EligibilityStatus { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Raw271 { get; set; }
    public string? PayerName { get; set; }
    public string? PlanName { get; set; }
    public string? PlanDetails { get; set; }
    public string? EligibilityStartDate { get; set; }
    public string? EligibilityEndDate { get; set; }
    public List<EligibilityBenefitDto> Benefits { get; set; } = [];
    public string? ProviderNpi { get; set; }
    public string? ProviderMode { get; set; }
    public bool UsedPayerOverride { get; set; }
}

public sealed class EligibilityPreflightRequestDto
{
    /// <summary>Optional: when set, validates patient insurance and provider resolution.</summary>
    public int? PatientId { get; set; }
}

public sealed class EligibilityPreflightResultDto
{
    public bool Valid { get; set; }
    public List<string> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public bool? ServerReachable { get; set; }
}

public sealed class EligibilityBenefitDto
{
    public string? ServiceType { get; set; }
    public string? Benefit { get; set; }
    public string? Amount { get; set; }
    public string? Description { get; set; }
}

