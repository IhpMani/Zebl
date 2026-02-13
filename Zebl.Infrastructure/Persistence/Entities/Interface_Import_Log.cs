namespace Zebl.Infrastructure.Persistence.Entities;

/// <summary>
/// Entity for file-level interface import history.
/// Separate from Claim_Audit - one record per imported file.
/// </summary>
public partial class Interface_Import_Log
{
    public int ImportID { get; set; }

    public string FileName { get; set; } = null!;

    public DateTime ImportDate { get; set; }

    public string? UserName { get; set; }

    public string? ComputerName { get; set; }

    public int NewPatientsCount { get; set; }

    public int UpdatedPatientsCount { get; set; }

    public int NewClaimsCount { get; set; }

    public int DuplicateClaimsCount { get; set; }

    public decimal TotalAmount { get; set; }

    public string? Notes { get; set; }
}
