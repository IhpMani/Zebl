namespace Zebl.Application.Domain;

/// <summary>
/// Tracks each 837 submission so 999 AK2 TransactionControlNumber can be resolved to ClaimId.
/// Supports multiple claims per batch via BatchId.
/// </summary>
public class ClaimSubmission
{
    public int Id { get; set; }

    public int ClaimId { get; set; }

    /// <summary>ST02 / AK2 value sent in 837 and echoed in 999.</summary>
    public string TransactionControlNumber { get; set; } = string.Empty;

    /// <summary>Patient/claim control identifier (e.g. CLM01 or invoice).</summary>
    public string? PatientControlNumber { get; set; }

    /// <summary>Optional batch identifier when submitting multiple claims together.</summary>
    public string? BatchId { get; set; }

    /// <summary>Optional ISA/GS-level file control number for future correlation.</summary>
    public string? FileControlNumber { get; set; }

    public DateTime SubmissionDate { get; set; }
}
