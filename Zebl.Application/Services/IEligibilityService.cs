using System;
using System.Threading;
using System.Threading.Tasks;

namespace Zebl.Application.Services;

/// <summary>
/// Runs eligibility (270/271) checks using clearinghouse credentials from Program Settings.
/// Credentials are only for clearinghouse communication, not application login.
/// </summary>
public interface IEligibilityService
{
    /// <summary>
    /// Check eligibility for a patient. Loads patientEligibility settings, builds ANSI 270,
    /// sends to clearinghouse, returns parsed 271. Never logs credentials.
    /// </summary>
    Task<EligibilityCheckResultDto> CheckEligibilityAsync(int patientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get eligibility history for a patient (requests and latest responses).
    /// </summary>
    Task<EligibilityHistoryItemDto[]> GetHistoryAsync(int patientId, CancellationToken cancellationToken = default);
}

public sealed class EligibilityCheckResultDto
{
    public bool Success { get; set; }
    public string? Raw271 { get; set; }
    public string? Message { get; set; }
    public string? PayerName { get; set; }
    public string? Status { get; set; }

    public decimal? DeductibleAmount { get; set; }
    public decimal? CopayAmount { get; set; }
    public decimal? CoinsurancePercent { get; set; }
    public DateTime? CoverageStartDate { get; set; }
    public DateTime? CoverageEndDate { get; set; }

    // Insured/Patient fields shown in the Eligibility Response popup.
    public string? PatientName { get; set; }
    public string? PatientAddress { get; set; }
    public string? Identification { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public DateOnly? EligibilityDate { get; set; }
    public DateOnly? InquiryDate { get; set; }
}

public sealed class EligibilityHistoryItemDto
{
    public int RequestId { get; set; }
    public DateTime CheckDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CoverageStatus { get; set; }
    public string? PlanName { get; set; }
    public decimal? DeductibleAmount { get; set; }
    public decimal? CopayAmount { get; set; }
    public decimal? CoinsurancePercent { get; set; }
    public DateTime? CoverageStartDate { get; set; }
    public DateTime? CoverageEndDate { get; set; }
}

