using System;

namespace Zebl.Infrastructure.Persistence.Entities;

/// <summary>
/// Entity for tracking HL7 DFT import operations
/// Stores import metadata and statistics
/// </summary>
public partial class Hl7_Import_Log
{
    public int ImportLogID { get; set; }

    public string FileName { get; set; } = null!;

    public DateTime ImportDateTime { get; set; }

    public int NewPatientsCount { get; set; }

    public int UpdatedPatientsCount { get; set; }

    public int NewClaimsCount { get; set; }

    public int NewServiceLinesCount { get; set; }

    public bool ImportSuccessful { get; set; }

    public string? ErrorMessage { get; set; }

    public string? ImportedBy { get; set; }
}
